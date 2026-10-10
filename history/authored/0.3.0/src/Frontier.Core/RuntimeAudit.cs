using System.Globalization;
using System.Text.Json.Nodes;
using Frontier.Engine;
namespace Frontier.Core;

/// <summary>Read-back of actual native repositories. A void deserializer return is NOT a success criterion.</summary>
internal sealed class RuntimeAudit
{
    private readonly Plugin plugin;
    private readonly object sync = new();
    private readonly Dictionary<string, object> repositories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, object>> rows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, object> checks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonNode> expected = new(StringComparer.Ordinal);
    internal bool CatalogReady { get; private set; }
    internal RuntimeAudit(Plugin plugin) => this.plugin = plugin;
    internal object Snapshot { get { lock (sync) return new { catalogReady = CatalogReady, checks = new Dictionary<string, object>(checks) }; } }
    internal object? Get(string repository, string id)
    {
        lock (sync) return rows.TryGetValue(repository, out var values) && values.TryGetValue(id, out var value) ? value : null;
    }
    internal void Invalidate(string name)
    {
        lock (sync) { CatalogReady = false; repositories.Remove(name); rows.Remove(name); expected.Remove(name); checks[name] = new { status = "PENDING" }; }
    }
    internal void Verify(string name, object nativeRepository, JsonNode expectedRoot, bool applied)
    {
        lock (sync)
        {
            Type concrete = Reflect.FindType(PatchEngine.Str(plugin.Engine.RuntimeContracts["repositoryTypes"]!, name));
            object repository = Reflect.AsConcrete(nativeRepository, concrete);
            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (expectedRoot is JsonArray array)
            {
                object list = Reflect.Call(repository, "GetAllData") ?? throw new InvalidDataException(name + ": GetAllData returned null");
                int count = Reflect.Count(list);
                if (count != array.Count) throw new InvalidDataException($"{name}: native count {count}, expected {array.Count}. The byte replacement/load did not produce the expected catalog.");
                var get = concrete.GetMethods(Reflect.Flags).Single(m => m.Name == "GetData" && m.GetParameters().Length == 1 && !m.ContainsGenericParameters);
                Type idType = get.GetParameters()[0].ParameterType;
                foreach (var row in array)
                {
                    string id = PatchEngine.Scalar(row!["Id"] ?? row["_id"]);
                    object value = get.Invoke(repository, new[] { Reflect.ParseId(idType, id) })
                        ?? throw new InvalidDataException($"{name}: GetData({id}) returned null");
                    if (!string.Equals(Reflect.Id(value), id, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException(name + ": wrong object ID " + id);
                    values.Add(id, value);
                    // These scalar fields are confirmed in metadata. Check modifications as well as presence.
                    string[] fields = name switch {
                        "skills" => new[] { "IsDisabled", "MaxVersion", "Order", "IconName" },
                        "bot_presets" => new[] { "AISettingId", "EquipmentPresetId", "PlatformPresetId" },
                        "ai_settings" => new[] { "ExperienceForKill" },
                        _ => Array.Empty<string>()
                    };
                    foreach (string field in fields)
                        if (row[field] is JsonValue v) Match(name + "." + id + "." + field, Reflect.Read(value, field), v);
                }
                checks[name] = new { status = applied ? "OBJECTS_VERIFIED" : "BASELINE_OBJECTS_VERIFIED", nativeCount = count, expectedCount = array.Count, lookedUpIds = values.Count };
            }
            else
            {
                object value = Reflect.Call(repository, "GetData") ?? throw new InvalidDataException(name + ": empty native singleton");
                Match(name + ".MaxLevel", Reflect.Read(value, "MaxLevel"), expectedRoot["MaxLevel"]!.AsValue());
                checks[name] = new { status = applied ? "OBJECTS_VERIFIED" : "BASELINE_OBJECTS_VERIFIED", maxLevel = Convert.ToInt32(Reflect.Read(value, "MaxLevel")) };
            }
            repositories[name] = repository; rows[name] = values; expected[name] = expectedRoot;
            if (applied && plugin.Engine.RepositoryNames.All(repositories.ContainsKey))
            {
                CheckCrossReferences();
                CatalogReady = true;
            }
        }
    }
    private static void Match(string where, object? native, JsonValue value)
    {
        string expected = value.TryGetValue<string>(out var text) ? text : value.ToJsonString();
        string actual = native is bool b ? (b ? "true" : "false") : Convert.ToString(native, CultureInfo.InvariantCulture) ?? "null";
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"{where}: native {actual}, expected {expected}");
    }
    private void CheckCrossReferences()
    {
        int linked = 0;
        foreach (var id in plugin.Engine.RuntimeContracts["addedIds"]!["bot_presets"]!.AsArray())
        {
            object bot = rows["bot_presets"][PatchEngine.Scalar(id)];
            foreach (var pair in new[] { ("AISettingId", "ai_settings"), ("EquipmentPresetId", "bot_equipment_presets"), ("PlatformPresetId", "bot_equipment_presets") })
            {
                string reference = Reflect.Read(bot, pair.Item1)?.ToString() ?? "";
                if (!rows[pair.Item2].ContainsKey(reference)) throw new InvalidDataException("Broken elite reference: " + pair.Item1 + " -> " + reference);
                linked++;
            }
        }
        checks["eliteReferences"] = new { status = "OBJECTS_VERIFIED", references = linked };
        if (plugin.Engine.IsEnabled("progression"))
        {
            foreach (var id in plugin.Engine.RuntimeContracts["skillIds"]!.AsArray())
            {
                object skill = rows["skills"][PatchEngine.Scalar(id)];
                if (Reflect.Read(skill, "IsDisabled") is not false || Reflect.Count(Reflect.Read(skill, "PerkList")!) < 1)
                    throw new InvalidDataException("Skill disabled or no native perks: " + id);
            }
        }
        // Check actual commodity objects, not only edited JSON. A later GetPricelist hook verifies creation.
        string shopId = PatchEngine.Str(plugin.Engine.RuntimeContracts, "testShopId");
        var actualOffers = Reflect.Values(Reflect.Read(Reflect.Read(rows["shop_templates"][shopId], "PricelistTemplate"), "Commodities")!).ToDictionary(Reflect.Id, x => x, StringComparer.Ordinal);
        var jsonShop = expected["shop_templates"].AsArray().Single(x => PatchEngine.Scalar(x!["Id"]) == shopId)!;
        var wanted = jsonShop["PricelistTemplate"]!["Commodities"]!.AsArray().Where(x => x!["Id"]!.GetValue<string>().StartsWith("Frontier_", StringComparison.Ordinal)).ToArray();
        foreach (var offer in wanted)
        {
            string id = offer!["Id"]!.GetValue<string>();
            if (!actualOffers.TryGetValue(id, out var value) || Reflect.Read(value, "Item") is null) throw new InvalidDataException("Shop route not loaded: " + id);
        }
        checks["shopRoutes"] = new { status = "OBJECTS_VERIFIED", offers = wanted.Length, liveInventory = "NOT_OBSERVED" };
    }
    internal void Broken(string stage, Exception error)
    {
        lock (sync) { CatalogReady = false; checks[stage] = new { status = "BROKEN", error = error.GetBaseException().Message }; }
    }
}
