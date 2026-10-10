using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Frontier.Engine;

/// <summary>Pure managed, deterministic repository transformation. No game or Unity references.</summary>
public sealed class PatchEngine
{
    private readonly JsonObject compatibility;
    private readonly List<JsonObject> modules;
    private readonly JsonObject aliases;
    private readonly HashSet<string> enabled;
    private readonly Dictionary<string, byte[]> cache = new(StringComparer.Ordinal);
    private readonly object sync = new();
    public string GameVersion => Str(compatibility, "gameVersion");
    public IReadOnlyCollection<string> ModuleIds { get; }
    public IReadOnlyCollection<string> RepositoryNames { get; }
    public JsonObject FileHashes => (JsonObject)compatibility["files"]!;
    public bool IsEnabled(string id) => enabled.Contains(id);
    public PatchEngine(string packPath, IEnumerable<string> enabledModules)
    {
        compatibility = ReadObject(Path.Combine(packPath, "compatibility.json"));
        if ((int)compatibility["schema"]! != 1) throw new InvalidDataException("Unsupported compatibility schema");
        aliases = ReadObject(Path.Combine(packPath, "aliases.json"));
        modules = Directory.EnumerateFiles(packPath, "*.json")
            .Where(p => Path.GetFileName(p).Length > 3 && char.IsDigit(Path.GetFileName(p)[0]) && char.IsDigit(Path.GetFileName(p)[1]) && Path.GetFileName(p)[2] == '_')
            .Select(ReadObject).OrderBy(m => (int)m["order"]!).ToList();
        var ids = modules.Select(m => Str(m, "id")).ToArray();
        var expected = ((JsonArray)compatibility["moduleIds"]!).Select(n => n!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        if (ids.Length != expected.Count || !expected.SetEquals(ids))
            throw new InvalidDataException("Install all seven manifests. Disable sources in configuration; do not delete manifests.");
        foreach (var m in modules)
        {
            if ((int)m["schema"]! != 1) throw new InvalidDataException("Unsupported module schema");
            if (m["operations"] is not JsonArray) throw new InvalidDataException("Missing operations");
        }
        ModuleIds = ids;
        RepositoryNames = ((JsonObject)compatibility["repositories"]!).Select(k => k.Key).ToArray();
        enabled = enabledModules.ToHashSet(StringComparer.Ordinal);
        if (!enabled.IsSubsetOf(expected)) throw new InvalidDataException("Unknown EnabledModules value");
        var opIds = modules.SelectMany(m => (JsonArray)m["operations"]!).Select(o => Str(o!, "operationId")).ToArray();
        if (opIds.Distinct(StringComparer.Ordinal).Count() != opIds.Length) throw new InvalidDataException("Duplicate operation ID");
    }
    public string? ResolveRepository(string filePath)
    {
        string name = filePath.Replace('\\', '/').Split('/').Last();
        if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) name = name[..^5];
        return RepositoryNames.FirstOrDefault(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
    }
    public static string Hash(byte[] input) => Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
    public static string NormalizedText(byte[] input) => Encoding.UTF8.GetString(input).TrimStart('\uFEFF').Replace("\r\n", "\n");
    public static JsonObject ReadObject(string path) => JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8))?.AsObject() ?? throw new InvalidDataException(path);
    public static JsonNode? Copy(JsonNode? n) => n is null ? null : JsonNode.Parse(n.ToJsonString());
    public static string Str(JsonNode n, string key) => n[key]?.GetValue<string>() ?? throw new InvalidDataException($"Missing string {key}");
    public static string Scalar(JsonNode? n) => n is null ? throw new InvalidDataException("Null ID") : n is JsonValue v && v.TryGetValue<string>(out var s) ? s : n.ToJsonString();
    public void VerifyRepository(string name, byte[] original)
    {
        var pin = compatibility["repositories"]?[name] ?? throw new InvalidDataException($"Unknown repository: {name}");
        if (Hash(original) != Str(pin, "sha256") && Hash(Encoding.UTF8.GetBytes(NormalizedText(original))) != Str(pin, "normalizedSha256"))
            throw new InvalidDataException($"Repository {name} differs from the analyzed game. No automatic repinning is allowed.");
    }
    /// <remarks>Call only with the original bytes, not the output of another invocation. The hash rejects double application.</remarks>
    public byte[] Transform(string name, byte[] original)
    {
        VerifyRepository(name, original);
        lock (sync)
        {
            if (cache.TryGetValue(name, out var known)) return known.ToArray();
            var root = JsonNode.Parse(NormalizedText(original)) ?? throw new InvalidDataException("Empty JSON");
            // Definitions first across ALL modules, then enabled acquisition routes. Catalogs stay stable for saves.
            foreach (string phase in new[] { "catalog", "active" })
            foreach (var m in modules)
            foreach (var node in (JsonArray)m["operations"]!)
            {
                var op = node!.AsObject();
                if (Str(op, "repository") != name || Str(op, "activation") != phase) continue;
                if (phase == "active" && (!enabled.Contains(Str(m, "id")) || ((JsonArray)op["requires"]!).Any(r => !enabled.Contains(r!.GetValue<string>())))) continue;
                try { root = ApplyOperation(root, op); }
                catch (Exception e) { throw new InvalidDataException($"Operation {Str(op, "operationId")} in {name} failed", e); }
            }
            if (name == "item_templates") ExpandAliases(root, aliases);
            EnsureUniqueIds(root);
            var output = Encoding.UTF8.GetBytes(root.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
            cache.Add(name, output);
            return output.ToArray();
        }
    }
    public static JsonNode ApplyOperation(JsonNode root, JsonObject op)
    {
        var kind = Str(op, "op");
        if (kind == "root") return ApplyEdits(root, (JsonArray)op["edits"]!);
        if (root is not JsonArray list) throw new InvalidDataException("Expected repository array");
        if (kind == "append") { list.Add(Copy(op["value"])); return root; }
        string key = Str(op, "key"), source = Scalar(op["source"]);
        var hits = list.Select((row, i) => (row, i)).Where(x => Scalar(x.row?[key]) == source).ToArray();
        if (hits.Length != 1) throw new InvalidDataException($"Expected one {key}={source}, got {hits.Length}");
        if (kind == "clone")
        {
            string id = Scalar(op["id"]);
            if (list.Any(n => Scalar(n?[key]) == id)) throw new InvalidDataException($"Duplicate clone {id}");
            var copy = ApplyEdits(Copy(hits[0].row)!, (JsonArray)op["edits"]!);
            if (Scalar(copy[key]) != id) throw new InvalidDataException("Clone ID differs from declared ID");
            list.Add(copy);
        }
        else if (kind == "edit") list[hits[0].i] = ApplyEdits(Copy(hits[0].row)!, (JsonArray)op["edits"]!);
        else throw new InvalidDataException($"Unknown operation: {kind}");
        return root;
    }
    public static JsonNode ApplyEdits(JsonNode root, JsonArray edits)
    {
        foreach (var edit in edits)
        {
            string kind = Str(edit!, "op"), path = Str(edit!, "path");
            if (path.Length == 0)
            {
                if (kind == "set") { root = Copy(edit!["value"]) ?? throw new InvalidDataException("Null root"); continue; }
                if (kind == "append" && root is JsonArray r) { r.Add(Copy(edit!["value"])); continue; }
                throw new InvalidDataException("Invalid root operation");
            }
            if (!path.StartsWith('/')) throw new InvalidDataException("Invalid JSON pointer");
            string[] pieces = path[1..].Split('/').Select(s => s.Replace("~1", "/").Replace("~0", "~")).ToArray();
            JsonNode parent = root;
            for (int i = 0; i < pieces.Length - 1; i++) parent = Child(parent, pieces[i]) ?? throw new InvalidDataException($"Missing path: {path}");
            string leaf = pieces[^1];
            if (kind == "append")
            {
                if (Child(parent, leaf) is not JsonArray a) throw new InvalidDataException("Append target is not an array");
                a.Add(Copy(edit!["value"]));
            }
            else if (kind == "set")
            {
                if (parent is JsonObject o) o[leaf] = Copy(edit!["value"]);
                else if (parent is JsonArray a) a[Index(leaf, a.Count)] = Copy(edit!["value"]);
                else throw new InvalidDataException("Invalid set parent");
            }
            else if (kind == "remove")
            {
                if (parent is JsonObject o) { if (!o.Remove(leaf)) throw new InvalidDataException("Missing remove key"); }
                else if (parent is JsonArray a) a.RemoveAt(Index(leaf, a.Count));
                else throw new InvalidDataException("Invalid remove parent");
            }
            else throw new InvalidDataException("Unknown edit operation");
        }
        return root;
    }
    private static int Index(string s, int length)
    {
        if (!int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out int i) || i < 0 || i >= length) throw new InvalidDataException("Array index out of bounds");
        return i;
    }
    private static JsonNode? Child(JsonNode n, string key) => n is JsonObject o ? o[key] : n is JsonArray a ? a[Index(key, a.Count)] : throw new InvalidDataException("Not a container");
    public static void ExpandAliases(JsonNode node, JsonObject aliases)
    {
        if (node is JsonArray array) { foreach (var x in array) if (x is not null) ExpandAliases(x, aliases); return; }
        if (node is not JsonObject obj) return;
        // Match the actual native ItemTemplateFilter only; never rewrite arbitrary ID fields or quest repositories.
        if (obj["$t"] is JsonValue tag && tag.TryGetValue<int>(out var type) && type == 23661 && obj["_templateIds"] is JsonArray ids)
        {
            var have = ids.Select(Scalar).ToHashSet(StringComparer.Ordinal);
            foreach (string id in have.ToArray())
                if (aliases[id] is JsonArray children)
                    foreach (var child in children) if (have.Add(Scalar(child))) ids.Add(Copy(child));
        }
        foreach (var value in obj.Select(k => k.Value).ToArray()) if (value is not null) ExpandAliases(value, aliases);
    }
    public static void EnsureUniqueIds(JsonNode root)
    {
        if (root is not JsonArray a) return;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var n in a)
        {
            var id = n?["_id"] ?? n?["Id"];
            if (id is not null && !seen.Add(Scalar(id))) throw new InvalidDataException($"Duplicate repository ID {Scalar(id)}");
        }
    }
}
