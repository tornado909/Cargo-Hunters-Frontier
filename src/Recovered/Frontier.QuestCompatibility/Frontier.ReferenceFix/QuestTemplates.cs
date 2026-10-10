using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace Frontier.ReferenceFix;

internal static class QuestTemplates
{
	private static readonly Dictionary<Guid, Guid> Variants = new Dictionary<Guid, Guid>();

	private static readonly object Gate = new object();

	private static Type filterType = null;

	private static Type itemType = null;

	private static Evidence report = null;

	internal static void Install(Harmony h, QuestCompatibilityPlugin plugin)
	{
		report = new Evidence("Frontier-quest-compat-report.json", ((BasePlugin)plugin).Log, "0.1.0", "native-template-filter-canonical-families-v10.2");
		string path = R.Key(R.Get(R.Type("Frontier.Modular", "Frontier.Modular.ModuleRuntime"), "Root"));
		string path2 = Path.Combine(path, "compiled", "loot-rarity.json");
		if (!File.Exists(path2))
		{
			path2 = Path.Combine(path, "modules", "rarity-core", "loot-rarity.json");
		}
		byte[] array = File.ReadAllBytes(path2);
		if (array.Length > 33554432)
		{
			throw new InvalidDataException("Unexpected rarity map size");
		}
		using (JsonDocument jsonDocument = JsonDocument.Parse((ReadOnlyMemory<byte>)array, default(JsonDocumentOptions)))
		{
			JsonElement property = jsonDocument.RootElement.GetProperty("families");
			foreach (JsonProperty item in jsonDocument.RootElement.GetProperty("variants").EnumerateObject())
			{
				if (Guid.TryParse(item.Name, out var result))
				{
					JsonElement value = item.Value.GetProperty("base");
					if (Guid.TryParse(value.GetString(), out var result2) && !(result == result2) && property.TryGetProperty(result2.ToString(), out value))
					{
						Variants.Add(result, result2);
						continue;
					}
				}
				throw new InvalidDataException("Unverified variant family: " + item.Name);
			}
		}
		if (Variants.Count == 0 || Variants.Count > 20000 || Variants.Values.Any((Guid x) => Variants.ContainsKey(x)))
		{
			throw new InvalidDataException("Empty/cyclic rarity map");
		}
		filterType = R.Type("Game.Shared", "Game.ItemsSystem.ItemFilters.ItemTemplateIdFilter");
		itemType = R.Type("Game.Shared", "Game.ItemsSystem.IItem");
		MethodInfo methodInfo = filterType.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Single((MethodInfo m) => m.Name == "IsCompatible" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == itemType && m.ReturnType == typeof(bool));
		R.Hook(h, methodInfo, typeof(QuestTemplates), null, "AfterTemplateCheck");
		report.Data["mappedVariants"] = Variants.Count;
		report.Data["mapSha256"] = Convert.ToHexString(SHA256.HashData(array)).ToLowerInvariant();
		report.Data["hookTarget"] = methodInfo.ToString();
		report.Data["itemTemplateWrites"] = 0;
		report.Data["quantityWrites"] = 0;
		report.Data["questProgressWrites"] = 0;
		report.Data["specificVariantRequirementsRemainExact"] = true;
		report.Data["scope"] = "Template identity leaf only; composite conditions and item consumption remain native";
		report.Data["status"] = "BOUND_WAITING_FOR_TEMPLATE_COMPARISON";
		report.Save(force: true);
		((BasePlugin)plugin).Log.LogInfo((object)("Quest template compatibility v10.2: " + Variants.Count + " documented variants; base model identity only, no quest completion or item mutation."));
	}

	private static void AfterTemplateCheck(object __instance, object __0, ref bool __result)
	{
		lock (Gate)
		{
			try
			{
				report.Add("templateChecks", 1L);
				object obj = NativeChecked.As(__instance, filterType);
				object obj2 = NativeChecked.As(__0, itemType);
				if (obj == null || obj2 == null || !Guid.TryParse(R.Key(R.Get(obj2, "TemplateId")), out var result) || !Variants.TryGetValue(result, out var value))
				{
					return;
				}
				report.Add("variantChecks", 1L);
				List<Guid> list = new List<Guid>();
				foreach (object item in R.Values(R.Get(obj, "TemplateIds"), 2048))
				{
					if (!Guid.TryParse(R.Key(item), out var result2))
					{
						return;
					}
					list.Add(result2);
				}
				int policy = R.Int(R.Get(obj, "_filterPolicy"));
				bool? flag = TemplatePolicy.Evaluate(result, list, policy, Variants);
				if (flag.HasValue)
				{
					bool flag2 = __result;
					__result = flag.Value;
					report.Add("canonicalComparisons", 1L);
					if (flag2 != __result)
					{
						report.Add("identityResultsChanged", 1L);
					}
					report.Data["lastComparison"] = new
					{
						actualTemplate = result,
						baseTemplate = value,
						requiredTemplates = list,
						policy = policy,
						original = flag2,
						result = __result
					};
					report.Data["status"] = "CANONICAL_TEMPLATE_CHECK_OBSERVED";
				}
			}
			catch (Exception e)
			{
				report.Error("template identity", e);
			}
			finally
			{
				report.Save();
			}
		}
	}
}
