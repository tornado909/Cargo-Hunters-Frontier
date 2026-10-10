using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Frontier.Modular;

public static class ModuleRuntime
{
	private sealed class Package
	{
		internal string Id = "";

		internal string Folder = "";

		internal JsonObject Manifest = new JsonObject();

		internal JsonObject Files = new JsonObject();

		internal List<JsonObject> Operations = new List<JsonObject>();
	}

	public const string Version = "0.6.0";

	private static HashSet<string> installed = new HashSet<string>(StringComparer.Ordinal);

	private static bool prepared;

	private static bool shops;

	private static string shopDigest = "initial";

	private static readonly string[] Buckets = new string[7] { "armory", "equipment", "affixes", "salvage", "elites", "workshop", "progression" };

	public static string Root { get; private set; } = "";

	public static bool Feature(string id)
	{
		if (prepared)
		{
			if (!(id == "shops"))
			{
				return installed.Contains(id);
			}
			return shops;
		}
		return false;
	}

	public static bool OverlayGate(object ignored, string name)
	{
		return Feature("elite-visuals");
	}

	public static bool ContentGate(object ignored, string legacy)
	{
		return Feature((legacy == "progression") ? "rigos" : legacy);
	}

	public static string ShopMigrationPath(string path)
	{
		return path + "." + shopDigest;
	}

	public static bool RollsGate(bool active)
	{
		if (active)
		{
			if (!Feature("weapon-rarity") && !Feature("attachment-rarity"))
			{
				return Feature("robot-rarity");
			}
			return true;
		}
		return false;
	}

	public static string RuntimeRoot(string ignored)
	{
		return Root;
	}

	public static void AssertRegistered(string id)
	{
		if (!prepared || !installed.Contains(id))
		{
			throw new InvalidOperationException("Frontier module not composed: " + id);
		}
	}

	public static void InstallLoot(object plugin)
	{
		if (!Feature("rarity-core"))
		{
			return;
		}
		string text = Path.Combine(Root, "modules", "rarity-core", "Frontier.Loot.dll");
		Assembly assembly = Assembly.LoadFrom(text);
		if (assembly.GetName().Version != new Version(0, 6, 0, 0) || string.IsNullOrEmpty(assembly.Location) || Hash(assembly.Location) != Hash(text))
		{
			throw new InvalidDataException("An obsolete Frontier.Loot assembly is already loaded. Move the old monolithic FrontierPack folder outside BepInEx/plugins and reinstall the selected 0.6.0 ZIPs. Do not continue with mixed runtime versions.");
		}
		Type type = assembly.GetType("Frontier.Loot.Runtime", throwOnError: true);
		try
		{
			type.GetMethod("Install", BindingFlags.Static | BindingFlags.Public).Invoke(null, new object[1] { plugin });
		}
		catch (TargetInvocationException ex)
		{
			throw new InvalidOperationException("Frontier Rarity API initialization failed", ex.InnerException ?? ex);
		}
	}

	public static void PrepareOrStop(string root, string configRoot, string bepinexRoot)
	{
		try
		{
			Compose(root, configRoot, bepinexRoot, enforceHistory: true);
		}
		catch (Exception ex)
		{
			try
			{
				Directory.CreateDirectory(bepinexRoot);
				File.WriteAllText(Path.Combine(bepinexRoot, "Frontier-modules-report.json"), JsonSerializer.Serialize(new
				{
					version = "0.6.0",
					status = "BLOCKED_BEFORE_CATALOG",
					error = ex.ToString()
				}, new JsonSerializerOptions
				{
					WriteIndented = true
				}));
			}
			catch
			{
			}
			Console.Error.WriteLine("Frontier modular startup blocked before catalogs: " + ex);
			Environment.Exit(78);
			throw;
		}
	}

	private static JsonObject Obj(string file)
	{
		return JsonNode.Parse(File.ReadAllText(file, Encoding.UTF8)).AsObject();
	}

	private static JsonArray Arr(string file)
	{
		return JsonNode.Parse(File.ReadAllText(file, Encoding.UTF8)).AsArray();
	}

	private static JsonNode? Copy(JsonNode? n)
	{
		if (n != null)
		{
			return JsonNode.Parse(n.ToJsonString());
		}
		return null;
	}

	private static string Str(JsonNode n, string key)
	{
		return n[key]?.GetValue<string>() ?? throw new InvalidDataException("Missing " + key);
	}

	private static string Scalar(JsonNode n)
	{
		if (!(n is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
		{
			return n.ToJsonString();
		}
		return value;
	}

	private static IEnumerable<string> Strings(JsonNode? n)
	{
		if (!(n is JsonArray source))
		{
			return Array.Empty<string>();
		}
		return source.Select((JsonNode x) => x.GetValue<string>());
	}

	private static string Hash(string path)
	{
		return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
	}

	private static bool SafeName(string v)
	{
		int length = v.Length;
		if (length > 0 && length <= 100)
		{
			return v.All(delegate(char c)
			{
				bool flag = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
				if (!flag)
				{
					bool flag2 = ((c == '-' || c == '.' || c == '_') ? true : false);
					flag = flag2;
				}
				return flag;
			});
		}
		return false;
	}

	private static string CheckedFile(string folder, string rel)
	{
		if (!SafeName(rel) || rel == ".." || rel == ".")
		{
			throw new InvalidDataException("Unsafe module path: " + rel);
		}
		return Path.Combine(folder, rel);
	}

	private static void AddEqual(JsonObject dest, string key, JsonNode? value)
	{
		if (dest.TryGetPropertyValue(key, out JsonNode jsonNode))
		{
			if (jsonNode?.ToJsonString() != value?.ToJsonString())
			{
				throw new InvalidDataException("Conflicting shared value: " + key);
			}
		}
		else
		{
			dest.Add(key, Copy(value));
		}
	}

	private static string? Defines(JsonObject e)
	{
		return e["defines"]?.GetValue<string>();
	}

	private static string OpId(JsonObject e)
	{
		return Str(e["operation"], "operationId");
	}

	private static string RegistryKey(JsonObject e)
	{
		return Str(e["operation"], "repository") + "/" + Defines(e);
	}

	public static string[] Compose(string root, string configRoot, string bepinexRoot, bool enforceHistory)
	{
		Root = Path.GetFullPath(root);
		prepared = false;
		installed = new HashSet<string>(StringComparer.Ordinal);
		shops = false;
		string path = Path.Combine(Root, "base");
		string path2 = Path.Combine(Root, "modules");
		List<Package> packages = new List<Package>();
		if (Directory.Exists(path2))
		{
			foreach (string item in Directory.EnumerateDirectories(path2).OrderBy<string, string>((string x) => x, StringComparer.Ordinal))
			{
				string text = Path.Combine(item, "module.json");
				if (!File.Exists(text))
				{
					throw new InvalidDataException("Incomplete module folder: " + Path.GetFileName(item));
				}
				JsonObject jsonObject = Obj(text);
				string text2 = Str(jsonObject, "id");
				if (!SafeName(text2) || text2 != Path.GetFileName(item))
				{
					throw new InvalidDataException("Module ID/folder mismatch: " + text2);
				}
				JsonNode? jsonNode = jsonObject["schema"];
				if (jsonNode == null || jsonNode.GetValue<int>() != 1 || Str(jsonObject, "framework") != "0.6.0" || Str(jsonObject, "version") != "0.6.0")
				{
					throw new InvalidDataException("Module/framework version mismatch: " + text2 + ". Use all 0.6.0 packages.");
				}
				if (!installed.Add(text2))
				{
					throw new InvalidDataException("Duplicate module: " + text2);
				}
				JsonObject jsonObject2 = jsonObject["files"].AsObject();
				List<string> list = Strings(jsonObject["dataFiles"]).Append(Str(jsonObject, "pluginAssembly")).ToList();
				if (jsonObject["runtimeAssembly"] is JsonValue jsonValue)
				{
					list.Add(jsonValue.GetValue<string>());
				}
				foreach (string item2 in list)
				{
					if (!jsonObject2.ContainsKey(item2))
					{
						throw new InvalidDataException("Unpinned module file: " + text2 + "/" + item2);
					}
				}
				foreach (KeyValuePair<string, JsonNode> item3 in jsonObject2)
				{
					string path3 = CheckedFile(item, item3.Key);
					if (!File.Exists(path3) || Hash(path3) != item3.Value.GetValue<string>())
					{
						throw new InvalidDataException("Missing/mismatched module file: " + text2 + "/" + item3.Key);
					}
				}
				Package package = new Package
				{
					Id = text2,
					Folder = item,
					Manifest = jsonObject,
					Files = jsonObject2
				};
				foreach (JsonNode item4 in Arr(Path.Combine(item, "operations.json")))
				{
					package.Operations.Add(item4.AsObject());
				}
				packages.Add(package);
			}
		}
		foreach (Package item5 in packages)
		{
			foreach (string item6 in Strings(item5.Manifest["requires"]))
			{
				if (!installed.Contains(item6))
				{
					throw new InvalidDataException(item5.Id + " requires " + item6 + " 0.6.0. Install its separate ZIP.");
				}
			}
		}
		HashSet<string> done = new HashSet<string>(StringComparer.Ordinal);
		HashSet<string> visiting = new HashSet<string>(StringComparer.Ordinal);
		foreach (Package item7 in packages)
		{
			Visit(item7.Id);
		}
		Dictionary<string, JsonObject> dictionary = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
		List<JsonObject> list2 = new List<JsonObject>();
		foreach (Package item8 in packages)
		{
			foreach (JsonObject operation in item8.Operations)
			{
				string text3 = OpId(operation);
				string text4 = Str(operation["operation"], "activation");
				if (text4 == "catalog")
				{
					if (dictionary.TryGetValue(text3, out var value))
					{
						if (value["operation"].ToJsonString() != operation["operation"].ToJsonString())
						{
							throw new InvalidDataException("Conflicting operation ID: " + text3);
						}
					}
					else
					{
						dictionary.Add(text3, operation);
					}
					continue;
				}
				if (text4 == "active")
				{
					if (item8.Id == "legacy-catalog")
					{
						throw new InvalidDataException("Legacy catalog must not enable acquisition");
					}
					if (Strings(operation["when"]).All(installed.Contains))
					{
						list2.Add(operation);
					}
					continue;
				}
				throw new InvalidDataException("Unknown activation phase: " + text4);
			}
		}
		List<JsonObject> list3 = dictionary.Values.Select(delegate(JsonObject original)
		{
			if (!(original["optionalEdits"] is JsonArray source))
			{
				return original;
			}
			JsonObject[] array3 = (from x in source
				where !Strings(x["when"]).All(installed.Contains)
				select x.AsObject()).ToArray();
			if (array3.Length == 0)
			{
				return original;
			}
			JsonObject jsonObject10 = Copy(original).AsObject();
			JsonArray jsonArray3 = jsonObject10["operation"]["edits"].AsArray();
			foreach (int item9 in from x in array3
				select x["index"].GetValue<int>() into x
				orderby x descending
				select x)
			{
				jsonArray3.RemoveAt(item9);
			}
			HashSet<string> ignored = array3.SelectMany((JsonObject x) => Strings(x["needs"])).ToHashSet<string>(StringComparer.Ordinal);
			jsonObject10["needs"] = new JsonArray((from x in Strings(original["needs"])
				where !ignored.Contains(x)
				select x).Select((Func<string, JsonNode>)((string x) => JsonValue.Create(x))).ToArray());
			return jsonObject10;
		}).ToList();
		HashSet<string> defs = new HashSet<string>(StringComparer.Ordinal);
		int count;
		do
		{
			defs = (from x in list3
				where Defines(x) != null
				select Defines(x)).ToHashSet<string>(StringComparer.Ordinal);
			count = list3.Count;
			list3 = list3.Where((JsonObject x) => Strings(x["needs"]).All(defs.Contains)).ToList();
		}
		while (count != list3.Count);
		defs = (from x in list3
			where Defines(x) != null
			select Defines(x)).ToHashSet<string>(StringComparer.Ordinal);
		list2 = list2.Where((JsonObject x) => Strings(x["needs"]).All(defs.Contains)).ToList();
		bool flag = list2.Any((JsonObject x) => Str(x["operation"], "repository") == "shop_templates");
		string text5 = Path.Combine(configRoot, "FrontierModular.catalog-lock.json");
		bool flag2 = File.Exists(text5) && (Obj(text5)["hadShopRoutes"]?.GetValue<bool>() ?? false);
		bool flag3 = File.Exists(Path.Combine(Root, "packs", "compatibility.json"));
		shops = flag || flag2 || flag3;
		string s = string.Join("\n", from x in list2
			where Str(x["operation"], "repository") == "shop_templates"
			orderby x["sort"].GetValue<int>()
			select x["operation"].ToJsonString());
		shopDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant().Substring(0, 16);
		foreach (JsonNode item10 in Arr(Path.Combine(path, "support-operations.json")))
		{
			JsonObject jsonObject3 = item10.AsObject();
			if (OpId(jsonObject3) == "runtime.shop.catalog-epoch")
			{
				if (flag)
				{
					list2.Add(jsonObject3);
				}
			}
			else if (Strings(jsonObject3["aliasChildren"]).Any(defs.Contains) && Strings(jsonObject3["needs"]).All(defs.Contains))
			{
				list3.Add(jsonObject3);
			}
		}
		string[] array = (from x in list3
			where Defines(x) != null
			select RegistryKey(x)).Distinct<string>(StringComparer.Ordinal).OrderBy<string, string>((string x) => x, StringComparer.Ordinal).ToArray();
		string text6 = Path.Combine(configRoot, "FrontierModular.catalog-lock.json");
		if (enforceHistory)
		{
			HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
			if (File.Exists(text6))
			{
				foreach (string item11 in Strings(Obj(text6)["definitionKeys"]))
				{
					hashSet.Add(item11);
				}
			}
			string text7 = Path.Combine(Root, "packs", "compatibility.json");
			if (File.Exists(text7) && Str(Obj(text7), "packVersion").StartsWith("0.5.", StringComparison.Ordinal))
			{
				foreach (KeyValuePair<string, JsonNode> item12 in Obj(Path.Combine(path, "known-definitions.json")))
				{
					hashSet.Add(item12.Value.GetValue<string>() + "/" + item12.Key);
				}
			}
			hashSet.ExceptWith(array);
			if (hashSet.Count > 0)
			{
				throw new InvalidDataException("Removing " + hashSet.Count + " registered definitions could damage a save. Install Frontier Legacy Save Catalog (or restore the previous modules). No save has been changed. Example: " + hashSet.OrderBy<string, string>((string x) => x, StringComparer.Ordinal).First());
			}
		}
		SortedDictionary<string, JsonNode> sortedDictionary = new SortedDictionary<string, JsonNode>(StringComparer.Ordinal);
		HashSet<string> hashSet2 = new HashSet<string>(StringComparer.Ordinal);
		foreach (Package item13 in packages)
		{
			foreach (string item14 in Strings(item13.Manifest["legacyEnable"]))
			{
				hashSet2.Add(item14);
			}
		}
		foreach (JsonObject item15 in list2)
		{
			hashSet2.Add(Str(item15, "bucket"));
		}
		if (hashSet2.Any((string x) => !Buckets.Contains(x)))
		{
			throw new InvalidDataException("Unknown runtime bucket");
		}
		for (int num = 0; num < Buckets.Length; num++)
		{
			string bucket = Buckets[num];
			JsonArray jsonArray = new JsonArray();
			foreach (JsonObject item16 in (from x in list3.Concat(list2)
				where Str(x, "bucket") == bucket
				orderby x["sort"].GetValue<int>()
				select x).ThenBy<JsonObject, string>(OpId, StringComparer.Ordinal))
			{
				JsonObject jsonObject4 = Copy(item16["operation"]).AsObject();
				jsonObject4["requires"] = new JsonArray();
				jsonArray.Add(jsonObject4);
			}
			sortedDictionary[$"{(num + 1) * 10}_{bucket}.json"] = new JsonObject
			{
				["schema"] = 1,
				["id"] = bucket,
				["order"] = (num + 1) * 10,
				["operations"] = jsonArray
			};
		}
		JsonObject jsonObject5 = new JsonObject();
		SortedDictionary<string, List<string>> sortedDictionary2 = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
		foreach (Package item17 in packages)
		{
			foreach (KeyValuePair<string, JsonNode> item18 in Obj(Path.Combine(item17.Folder, "localization.json")))
			{
				AddEqual(jsonObject5, item18.Key, item18.Value);
			}
			foreach (KeyValuePair<string, JsonNode> item19 in Obj(Path.Combine(item17.Folder, "aliases.json")))
			{
				if (!sortedDictionary2.TryGetValue(item19.Key, out var value2))
				{
					value2 = (sortedDictionary2[item19.Key] = new List<string>());
				}
				foreach (string item20 in Strings(item19.Value))
				{
					if (defs.Contains(item20) && !value2.Contains(item20))
					{
						value2.Add(item20);
					}
				}
			}
		}
		JsonObject jsonObject6 = new JsonObject();
		foreach (KeyValuePair<string, List<string>> item21 in sortedDictionary2)
		{
			if (item21.Value.Count > 0)
			{
				jsonObject6[item21.Key] = new JsonArray(((IEnumerable<string>)item21.Value.OrderBy<string, string>((string x) => x, StringComparer.Ordinal)).Select((Func<string, JsonNode>)((string x) => JsonValue.Create(x))).ToArray());
			}
		}
		sortedDictionary["aliases.json"] = jsonObject6;
		sortedDictionary["localization.json"] = jsonObject5;
		sortedDictionary["compatibility.json"] = Obj(Path.Combine(path, "compatibility.json"));
		JsonObject jsonObject7 = Obj(Path.Combine(path, "runtime-contracts.json"));
		KeyValuePair<string, JsonNode>[] array2 = jsonObject7["addedIds"].AsObject().ToArray();
		for (int num2 = 0; num2 < array2.Length; num2++)
		{
			KeyValuePair<string, JsonNode> p = array2[num2];
			jsonObject7["addedIds"][p.Key] = new JsonArray(((IEnumerable<string>)list3.Where((JsonObject e) => Defines(e) != null && Str(e["operation"], "repository") == p.Key).Select(Defines).Distinct()
				.OrderBy<string, string>((string x) => x, StringComparer.Ordinal)).Select((Func<string, JsonNode>)((string x) => JsonValue.Create(x))).ToArray());
		}
		if (installed.Contains("rigos") || installed.Contains("legacy-catalog"))
		{
			jsonObject7["skillIds"] = new JsonArray(new JsonNode[5] { 9001, 9002, 9003, 9004, 9005 });
		}
		else
		{
			jsonObject7["skillIds"] = new JsonArray();
		}
		sortedDictionary["runtime-contracts.json"] = jsonObject7;
		Package package2 = packages.FirstOrDefault((Package x) => x.Id == "elites");
		sortedDictionary["elites.json"] = ((package2 != null) ? Obj(Path.Combine(package2.Folder, "elites.json")) : Obj(Path.Combine(path, "elites.json")));
		Package package3 = packages.FirstOrDefault((Package x) => x.Id == "elite-visuals");
		sortedDictionary["elite-presentation.json"] = ((package3 != null) ? Obj(Path.Combine(package3.Folder, "elite-presentation.json")) : new JsonObject
		{
			["schema"] = 1,
			["ranks"] = new JsonObject()
		});
		Package package4 = packages.FirstOrDefault((Package x) => x.Id == "rigos");
		sortedDictionary["skill-localization.json"] = ((package4 != null) ? Obj(Path.Combine(package4.Folder, "skill-localization.json")) : new JsonObject());
		Package package5 = packages.FirstOrDefault((Package x) => x.Id == "rarity-core");
		if (package5 != null)
		{
			JsonObject jsonObject8 = Obj(Path.Combine(package5.Folder, "loot-rarity.json"));
			jsonObject8["settings"] = new JsonObject
			{
				["weapons"] = installed.Contains("weapon-rarity"),
				["modules"] = installed.Contains("attachment-rarity"),
				["bodyParts"] = installed.Contains("robot-rarity"),
				["combat"] = jsonObject8["variants"].AsObject().Any<KeyValuePair<string, JsonNode>>((KeyValuePair<string, JsonNode> x) => defs.Contains(x.Key))
			};
			sortedDictionary["loot-rarity.json"] = jsonObject8;
			sortedDictionary["weapon-summary.json"] = Obj(Path.Combine(package5.Folder, "weapon-summary.json"));
			sortedDictionary["rarity-runtime.json"] = new JsonObject
			{
				["schema"] = 1,
				["version"] = "0.6.0",
				["assembly"] = "Frontier.Loot.dll",
				["sha256"] = Hash(Path.Combine(package5.Folder, "Frontier.Loot.dll")),
				["adapter"] = "rarity-loot-v1.2-fieldmap-summary-modular"
			};
		}
		JsonObject jsonObject9 = Obj(Path.Combine(path, "empty-settings.json"));
		jsonObject9["EnabledModules"] = new JsonArray(((IEnumerable<string>)hashSet2.OrderBy<string, string>((string x) => x, StringComparer.Ordinal)).Select((Func<string, JsonNode>)((string x) => JsonValue.Create(x))).ToArray());
		sortedDictionary["pack-settings.example.json"] = jsonObject9;
		JsonArray jsonArray2 = new JsonArray();
		foreach (Package item22 in packages.OrderBy<Package, string>((Package x) => x.Id, StringComparer.Ordinal))
		{
			jsonArray2.Add(new JsonObject
			{
				["id"] = item22.Id,
				["version"] = "0.6.0",
				["files"] = Copy(item22.Files)
			});
		}
		JsonObject value3 = new JsonObject
		{
			["framework"] = "0.6.0",
			["modularRuntimeSha256"] = Hash(Path.Combine(Root, "Frontier.Modular.dll")),
			["packages"] = jsonArray2
		};
		sortedDictionary["module-selection.json"] = value3;
		Directory.CreateDirectory(configRoot);
		Directory.CreateDirectory(bepinexRoot);
		string text8 = Path.Combine(Root, "compiled.next-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text8);
		foreach (KeyValuePair<string, JsonNode> item23 in sortedDictionary)
		{
			File.WriteAllText(Path.Combine(text8, item23.Key), item23.Value.ToJsonString() + "\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		}
		string text9 = Path.Combine(Root, "compiled");
		string text10 = text9 + ".previous";
		if (Directory.Exists(text10))
		{
			Directory.Delete(text10, recursive: true);
		}
		if (Directory.Exists(text9))
		{
			Directory.Move(text9, text10);
		}
		try
		{
			Directory.Move(text8, text9);
		}
		catch
		{
			if (Directory.Exists(text10) && !Directory.Exists(text9))
			{
				Directory.Move(text10, text9);
			}
			throw;
		}
		if (Directory.Exists(text10))
		{
			Directory.Delete(text10, recursive: true);
		}
		AtomicJson(Path.Combine(configRoot, "FrontierModular.generated.json"), jsonObject9);
		if (enforceHistory)
		{
			AtomicJson(text6, new JsonObject
			{
				["schema"] = 1,
				["hadShopRoutes"] = flag,
				["definitionKeys"] = new JsonArray(((IEnumerable<string>)array).Select((Func<string, JsonNode>)((string x) => JsonValue.Create(x))).ToArray())
			});
		}
		prepared = true;
		JsonObject node = new JsonObject
		{
			["version"] = "0.6.0",
			["status"] = "COMPOSED_NOT_PLAYTESTED",
			["installedModules"] = Copy(jsonArray2),
			["registeredDefinitions"] = array.Length,
			["catalogOperations"] = list3.Count,
			["activeOperations"] = list2.Count,
			["missingOptionalCatalogOperations"] = dictionary.Count - list3.Count((JsonObject x) => !OpId(x).StartsWith("hotfix051/", StringComparison.Ordinal)),
			["legacyDefinitionsOnly"] = installed.Contains("legacy-catalog"),
			["runtimeTestMode"] = false,
			["nativePlaytestClaim"] = false,
			["sameModulesRequiredOnAllPeers"] = true
		};
		AtomicJson(Path.Combine(bepinexRoot, "Frontier-modules-report.json"), node);
		return installed.OrderBy<string, string>((string x) => x, StringComparer.Ordinal).ToArray();
		void Visit(string id)
		{
			if (!done.Contains(id))
			{
				if (!visiting.Add(id))
				{
					throw new InvalidDataException("Dependency cycle: " + id);
				}
				foreach (string item24 in Strings(packages.Single((Package x) => x.Id == id).Manifest["requires"]))
				{
					Visit(item24);
				}
				visiting.Remove(id);
				done.Add(id);
			}
		}
	}

	private static void AtomicJson(string path, JsonNode node)
	{
		string text = path + ".tmp";
		File.WriteAllText(text, node.ToJsonString(new JsonSerializerOptions
		{
			WriteIndented = true
		}) + "\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		File.Move(text, path, overwrite: true);
	}
}
