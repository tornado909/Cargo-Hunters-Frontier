using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Frontier.Engine;

public sealed class PatchEngine
{
	private readonly JsonObject compatibility;

	private readonly List<JsonObject> modules;

	private readonly JsonObject aliases;

	private readonly bool runtimeTestMode;

	private readonly HashSet<string> enabled;

	private readonly Dictionary<string, byte[]> cache = new Dictionary<string, byte[]>(StringComparer.Ordinal);

	private readonly object sync = new object();

	public JsonObject RuntimeContracts { get; }

	public string GameVersion => Str(compatibility, "gameVersion");

	public IReadOnlyCollection<string> ModuleIds { get; }

	public IReadOnlyCollection<string> RepositoryNames { get; }

	public JsonObject FileHashes => (JsonObject)compatibility["files"];

	public bool IsEnabled(string id)
	{
		return enabled.Contains(id);
	}

	public PatchEngine(string packPath, IEnumerable<string> enabledModules, bool runtimeTestMode = false)
	{
		this.runtimeTestMode = runtimeTestMode;
		RuntimeContracts = ReadObject(Path.Combine(packPath, "runtime-contracts.json"));
		compatibility = ReadObject(Path.Combine(packPath, "compatibility.json"));
		if ((int)compatibility["schema"] != 1)
		{
			throw new InvalidDataException("Unsupported compatibility schema");
		}
		aliases = ReadObject(Path.Combine(packPath, "aliases.json"));
		modules = (from m in (from p in Directory.EnumerateFiles(packPath, "*.json")
				where Path.GetFileName(p).Length > 3 && char.IsDigit(Path.GetFileName(p)[0]) && char.IsDigit(Path.GetFileName(p)[1]) && Path.GetFileName(p)[2] == '_'
				select p).Select(ReadObject)
			orderby (int)m["order"]
			select m).ToList();
		string[] array = modules.Select((JsonObject m) => Str(m, "id")).ToArray();
		HashSet<string> hashSet = ((JsonArray)compatibility["moduleIds"]).Select((JsonNode n) => n.GetValue<string>()).ToHashSet<string>(StringComparer.Ordinal);
		if (array.Length != hashSet.Count || !hashSet.SetEquals(array))
		{
			throw new InvalidDataException("Install all seven manifests. Disable sources in configuration; do not delete manifests.");
		}
		foreach (JsonObject module in modules)
		{
			if ((int)module["schema"] != 1)
			{
				throw new InvalidDataException("Unsupported module schema");
			}
			if (!(module["operations"] is JsonArray))
			{
				throw new InvalidDataException("Missing operations");
			}
		}
		ModuleIds = (IReadOnlyCollection<string>)(object)array;
		RepositoryNames = (IReadOnlyCollection<string>)(object)((JsonObject)compatibility["repositories"]).Select<KeyValuePair<string, JsonNode>, string>((KeyValuePair<string, JsonNode> k) => k.Key).ToArray();
		enabled = enabledModules.ToHashSet<string>(StringComparer.Ordinal);
		if (!enabled.IsSubsetOf(hashSet))
		{
			throw new InvalidDataException("Unknown EnabledModules value");
		}
		string[] array2 = (from o in modules.SelectMany((JsonObject m) => (JsonArray)m["operations"])
			select Str(o, "operationId")).ToArray();
		if (array2.Distinct<string>(StringComparer.Ordinal).Count() != array2.Length)
		{
			throw new InvalidDataException("Duplicate operation ID");
		}
	}

	public string? ResolveRepository(string filePath)
	{
		string name = filePath.Replace('\\', '/').Split('/').Last();
		if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
		{
			string text = name;
			name = text.Substring(0, text.Length - 5);
		}
		return RepositoryNames.FirstOrDefault((string k) => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
	}

	public static string Hash(byte[] input)
	{
		return Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
	}

	public static string NormalizedText(byte[] input)
	{
		return Encoding.UTF8.GetString(input).TrimStart('\ufeff').Replace("\r\n", "\n");
	}

	public static JsonObject ReadObject(string path)
	{
		return JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8))?.AsObject() ?? throw new InvalidDataException(path);
	}

	public static JsonNode? Copy(JsonNode? n)
	{
		if (n != null)
		{
			return JsonNode.Parse(n.ToJsonString());
		}
		return null;
	}

	public static string Str(JsonNode n, string key)
	{
		return n[key]?.GetValue<string>() ?? throw new InvalidDataException("Missing string " + key);
	}

	public static string Scalar(JsonNode? n)
	{
		if (n != null)
		{
			if (!(n is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
			{
				return n.ToJsonString();
			}
			return value;
		}
		throw new InvalidDataException("Null ID");
	}

	public void VerifyRepository(string name, byte[] original)
	{
		JsonNode n = compatibility["repositories"]?[name] ?? throw new InvalidDataException("Unknown repository: " + name);
		if (Hash(original) != Str(n, "sha256") && Hash(Encoding.UTF8.GetBytes(NormalizedText(original))) != Str(n, "normalizedSha256"))
		{
			throw new InvalidDataException("Repository " + name + " differs from the analyzed game. No automatic repinning is allowed.");
		}
	}

	public byte[] Transform(string name, byte[] original)
	{
		VerifyRepository(name, original);
		lock (sync)
		{
			if (cache.TryGetValue(name, out byte[] value))
			{
				return value.ToArray();
			}
			JsonNode jsonNode = JsonNode.Parse(NormalizedText(original)) ?? throw new InvalidDataException("Empty JSON");
			IndexedOperationBatch indexedOperationBatch = new IndexedOperationBatch(jsonNode);
			string[] array = new string[2] { "catalog", "active" };
			foreach (string text in array)
			{
				foreach (JsonObject module in modules)
				{
					foreach (JsonNode item in (JsonArray)module["operations"])
					{
						JsonObject jsonObject = item.AsObject();
						if (!(Str(jsonObject, "repository") != name) && !(Str(jsonObject, "activation") != text) && (!(text == "active") || (enabled.Contains(Str(module, "id")) && !((JsonArray)jsonObject["requires"]).Any((JsonNode r) => !enabled.Contains(r.GetValue<string>())))))
						{
							try
							{
								jsonNode = indexedOperationBatch.Apply(jsonObject);
							}
							catch (Exception innerException)
							{
								throw new InvalidDataException($"Operation {Str(jsonObject, "operationId")} in {name} failed", innerException);
							}
						}
					}
				}
			}
			if (runtimeTestMode)
			{
				RuntimeTestContent.Apply(name, jsonNode, RuntimeContracts, IsEnabled);
			}
			if (name == "item_templates")
			{
				ExpandAliases(jsonNode, aliases);
			}
			EnsureUniqueIds(jsonNode);
			byte[] bytes = Encoding.UTF8.GetBytes(jsonNode.ToJsonString(new JsonSerializerOptions
			{
				WriteIndented = false
			}));
			cache.Add(name, bytes);
			return bytes.ToArray();
		}
	}

	public JsonNode Expected(string name)
	{
		lock (sync)
		{
			if (!cache.TryGetValue(name, out byte[] value))
			{
				throw new InvalidOperationException("No transformed catalog " + name);
			}
			return JsonNode.Parse(value);
		}
	}

	public static JsonNode ApplyOperation(JsonNode root, JsonObject op)
	{
		string text = Str(op, "op");
		if (text == "root")
		{
			return ApplyEdits(root, (JsonArray)op["edits"]);
		}
		if (!(root is JsonArray jsonArray))
		{
			throw new InvalidDataException("Expected repository array");
		}
		if (text == "append")
		{
			jsonArray.Add(Copy(op["value"]));
			return root;
		}
		string key = Str(op, "key");
		string source = Scalar(op["source"]);
		(JsonNode, int)[] array = (from x in jsonArray.Select((JsonNode row, int i) => (row: row, i: i))
			where Scalar(x.row?[key]) == source
			select x).ToArray();
		if (array.Length != 1)
		{
			throw new InvalidDataException($"Expected one {key}={source}, got {array.Length}");
		}
		if (text == "clone")
		{
			string id = Scalar(op["id"]);
			if (jsonArray.Any((JsonNode n) => Scalar(n?[key]) == id))
			{
				throw new InvalidDataException("Duplicate clone " + id);
			}
			JsonNode jsonNode = ApplyEdits(Copy(array[0].Item1), (JsonArray)op["edits"]);
			if (Scalar(jsonNode[key]) != id)
			{
				throw new InvalidDataException("Clone ID differs from declared ID");
			}
			jsonArray.Add(jsonNode);
		}
		else
		{
			if (!(text == "edit"))
			{
				throw new InvalidDataException("Unknown operation: " + text);
			}
			jsonArray[array[0].Item2] = ApplyEdits(Copy(array[0].Item1), (JsonArray)op["edits"]);
		}
		return root;
	}

	public static JsonNode ApplyEdits(JsonNode root, JsonArray edits)
	{
		foreach (JsonNode edit in edits)
		{
			string text = Str(edit, "op");
			string text2 = Str(edit, "path");
			if (text2.Length == 0)
			{
				if (text == "set")
				{
					root = Copy(edit["value"]) ?? throw new InvalidDataException("Null root");
					continue;
				}
				if (text == "append" && root is JsonArray jsonArray)
				{
					jsonArray.Add(Copy(edit["value"]));
					continue;
				}
				throw new InvalidDataException("Invalid root operation");
			}
			if (!text2.StartsWith('/'))
			{
				throw new InvalidDataException("Invalid JSON pointer");
			}
			string[] array = (from s in text2.Substring(1).Split('/')
				select s.Replace("~1", "/").Replace("~0", "~")).ToArray();
			JsonNode jsonNode = root;
			for (int num = 0; num < array.Length - 1; num++)
			{
				jsonNode = Child(jsonNode, array[num]) ?? throw new InvalidDataException("Missing path: " + text2);
			}
			string text3 = array[^1];
			switch (text)
			{
			case "append":
				((Child(jsonNode, text3) as JsonArray) ?? throw new InvalidDataException("Append target is not an array")).Add(Copy(edit["value"]));
				break;
			case "set":
				if (jsonNode is JsonObject jsonObject2)
				{
					jsonObject2[text3] = Copy(edit["value"]);
					break;
				}
				if (jsonNode is JsonArray jsonArray3)
				{
					jsonArray3[Index(text3, jsonArray3.Count)] = Copy(edit["value"]);
					break;
				}
				throw new InvalidDataException("Invalid set parent");
			case "remove":
				if (jsonNode is JsonObject jsonObject)
				{
					if (!jsonObject.Remove(text3))
					{
						throw new InvalidDataException("Missing remove key");
					}
					break;
				}
				if (jsonNode is JsonArray jsonArray2)
				{
					jsonArray2.RemoveAt(Index(text3, jsonArray2.Count));
					break;
				}
				throw new InvalidDataException("Invalid remove parent");
			default:
				throw new InvalidDataException("Unknown edit operation");
			}
		}
		return root;
	}

	private static int Index(string s, int length)
	{
		if (!int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var result) || result < 0 || result >= length)
		{
			throw new InvalidDataException("Array index out of bounds");
		}
		return result;
	}

	private static JsonNode? Child(JsonNode n, string key)
	{
		if (!(n is JsonObject jsonObject))
		{
			if (!(n is JsonArray jsonArray))
			{
				throw new InvalidDataException("Not a container");
			}
			return jsonArray[Index(key, jsonArray.Count)];
		}
		return jsonObject[key];
	}

	public static void ExpandAliases(JsonNode node, JsonObject aliases)
	{
		if (node is JsonArray jsonArray)
		{
			{
				foreach (JsonNode item in jsonArray)
				{
					if (item != null)
					{
						ExpandAliases(item, aliases);
					}
				}
				return;
			}
		}
		if (!(node is JsonObject jsonObject))
		{
			return;
		}
		if (jsonObject["$t"] is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var value) && value == 23661 && jsonObject["_templateIds"] is JsonArray jsonArray2)
		{
			HashSet<string> hashSet = jsonArray2.Select(Scalar).ToHashSet<string>(StringComparer.Ordinal);
			string[] array = hashSet.ToArray();
			foreach (string propertyName in array)
			{
				if (!(aliases[propertyName] is JsonArray jsonArray3))
				{
					continue;
				}
				foreach (JsonNode item2 in jsonArray3)
				{
					if (hashSet.Add(Scalar(item2)))
					{
						jsonArray2.Add(Copy(item2));
					}
				}
			}
		}
		JsonNode[] array2 = jsonObject.Select<KeyValuePair<string, JsonNode>, JsonNode>((KeyValuePair<string, JsonNode> k) => k.Value).ToArray();
		foreach (JsonNode jsonNode in array2)
		{
			if (jsonNode != null)
			{
				ExpandAliases(jsonNode, aliases);
			}
		}
	}

	public static void EnsureUniqueIds(JsonNode root)
	{
		if (!(root is JsonArray jsonArray))
		{
			return;
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		foreach (JsonNode item in jsonArray)
		{
			JsonNode jsonNode = item?["_id"] ?? item?["Id"];
			if (jsonNode != null && !hashSet.Add(Scalar(jsonNode)))
			{
				throw new InvalidDataException("Duplicate repository ID " + Scalar(jsonNode));
			}
		}
	}
}
