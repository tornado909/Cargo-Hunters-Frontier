using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using BepInEx.Unity.IL2CPP;
using Game.AttributeSystem;
using Game.Common;
using Game.UI.Views.ItemInformation;
using HarmonyLib;
using Il2CppSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace Frontier.ReferenceFix;

internal static class LocaleRuntime
{
	private sealed record Text(string Ru, string En, string Key);

	private static readonly Dictionary<long, Text> Known = new Dictionary<long, Text>();

	private static readonly KnownLocalizationKeys Keys = new KnownLocalizationKeys();

	private static readonly HashSet<int> CheckedAttributes = new HashSet<int>();

	[System.ThreadStatic]
	private static bool addingKey;

	private static Evidence report = null;

	internal static void Install(Harmony h, LocalizationRepairPlugin p)
	{
		//IL_0400: Unknown result type (might be due to invalid IL or missing references)
		//IL_040d: Expected O, but got Unknown
		report = new Evidence("Frontier-localization-report.json", ((BasePlugin)p).Log, "0.1.0", "known-key-native-labels-and-nested-notifications-v10.8");
		string directoryName = Path.GetDirectoryName(typeof(LocalizationRepairPlugin).Assembly.Location);
		using (JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(Path.GetFullPath(Path.Combine(directoryName, "..", "..", "compiled", "localization.json")))))
		{
			foreach (JsonProperty item in jsonDocument.RootElement.EnumerateObject())
			{
				if (long.TryParse(item.Name, out var result) && item.Value.ValueKind == JsonValueKind.Object)
				{
					JsonElement value;
					string text = (item.Value.TryGetProperty("ru", out value) ? (value.GetString() ?? "") : "");
					JsonElement value2;
					string text2 = (item.Value.TryGetProperty("en", out value2) ? (value2.GetString() ?? "") : "");
					if (LocalizationPolicy.Broken(text) || LocalizationPolicy.Broken(text2))
					{
						report.Add("invalidCatalogTexts", 1L);
						continue;
					}
					if (!LocalizationPolicy.Plain(text) || !LocalizationPolicy.Plain(text2))
					{
						report.Add("formattedEntriesLeftNative", 1L);
						continue;
					}
					JsonElement value3;
					string key = (item.Value.TryGetProperty("key", out value3) ? (value3.GetString() ?? item.Name) : item.Name);
					Known[result] = new Text(text, text2, key);
					Keys.Add(result, key);
				}
			}
		}
		foreach (KeyValuePair<int, (string, string)> attribute in LocalizationPolicy.Attributes)
		{
			long num = 3468000000000000000L + attribute.Key;
			string key2 = "attribute." + attribute.Key;
			Known[num] = new Text(attribute.Value.Item1, attribute.Value.Item2, key2);
			Keys.Add(num, key2);
		}
		Hook(R.Method(typeof(LocalizedStringDatabase), "ProcessUntranslatedText", 5), "Missing");
		Hook(R.Method(typeof(LocalizedStringDatabase), "GenerateLocalizedString", 6), "Generate");
		Hook(R.Method(typeof(LocalizedString), "GetSourceValue", 1), "Nested");
		MethodInfo[] methods = typeof(LocalizedString).GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		foreach (MethodInfo methodInfo in methods)
		{
			if (methodInfo.Name == "GetLocalizedString" && methodInfo.ReturnType == typeof(string) && !System.Array.Exists(methodInfo.GetParameters(), (ParameterInfo parameterInfo) => parameterInfo.ParameterType.IsArray))
			{
				Hook(methodInfo, "Direct");
			}
		}
		Hook(R.Method(typeof(LocalizedString), "InvokeChangeHandler", 1), "Changed");
		h.Patch((MethodBase)typeof(SharedTableData).GetMethod("GetEntry", new System.Type[1] { typeof(long) }), (HarmonyMethod)null, new HarmonyMethod(typeof(LocaleRuntime).GetMethod("SharedEntry", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		Hook(typeof(SharedTableData).GetMethod("GetKey", new System.Type[1] { typeof(long) }), "SharedKey");
		Hook(R.Method(typeof(ItemAttributeView), "CreateViewData", 3), "Attribute");
		report.Data["knownKeys"] = Known.Count;
		report.Data["keyForms"] = "numeric ID, exact shared-table name and Frontier-qualified name";
		report.Data["brokenItemNotificationsCovered"] = true;
		report.Data["graphicalNotificationsVerified"] = false;
		report.Data["attributeIds"] = new List<int>(LocalizationPolicy.Attributes.Keys);
		report.Data["unknownErrorsSuppressed"] = false;
		report.Data["status"] = "KNOWN_KEYS_READY";
		report.Save(force: true);
		((BasePlugin)p).Log.LogInfo((object)"Localization v10.8: item names/descriptions, nested notification variables and verified attribute IDs; unknown missing text is logged, not guessed.");
		void Hook(MethodInfo m, string fn)
		{
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Expected O, but got Unknown
			h.Patch((MethodBase)m, new HarmonyMethod(typeof(LocaleRuntime).GetMethod(fn, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
			{
				priority = 800
			}, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static bool Resolve(long id, UnityEngine.Localization.Locale? locale, out string value)
	{
		value = "";
		if (!Known.TryGetValue(id, out Text value2))
		{
			return false;
		}
		if (locale == null)
		{
			locale = LocalizationSettings.SelectedLocale;
		}
		value = (LocalizationPolicy.Russian(locale?.Identifier.Code) ? value2.Ru : value2.En);
		return true;
	}

	private static long EntryId(TableEntryReference entry)
	{
		return Keys.Resolve(entry.KeyId, entry.Key);
	}

	private static long Id(LocalizedString s)
	{
		return EntryId(s.TableEntryReference);
	}

	private static bool Direct(LocalizedString __instance, ref string __result)
	{
		try
		{
			if (Resolve(Id(__instance), __instance.LocaleOverride, out string value))
			{
				__result = value;
				report.Add("directResolutions", 1L);
				return false;
			}
		}
		catch (System.Exception e)
		{
			report.Error("direct", e);
		}
		return true;
	}

	private static bool Nested(LocalizedString __instance, ref Il2CppSystem.Object __result)
	{
		try
		{
			if (Resolve(Id(__instance), __instance.LocaleOverride, out string value))
			{
				__result = (Il2CppSystem.String)value;
				report.Add("nestedNotificationResolutions", 1L);
				return false;
			}
		}
		catch (System.Exception e)
		{
			report.Error("nested", e);
		}
		return true;
	}

	private static void Changed(LocalizedString __instance, ref string __0)
	{
		try
		{
			if (LocalizationPolicy.Broken(__0) && Resolve(Id(__instance), __instance.LocaleOverride, out string value))
			{
				__0 = value;
				report.Add("notificationCallbacksRepaired", 1L);
			}
		}
		catch (System.Exception e)
		{
			report.Error("callback", e);
		}
	}

	private static bool Missing(string __0, long __1, UnityEngine.Localization.Locale __4, ref string __result)
	{
		try
		{
			if (Resolve(Keys.Resolve(__1, __0), __4, out string value))
			{
				__result = value;
				report.Add("missingKeysResolved", 1L);
				return false;
			}
			report.Data["lastUnknownKey"] = new
			{
				key = __0,
				id = __1
			};
			report.Save();
		}
		catch (System.Exception e)
		{
			report.Error("missing", e);
		}
		return true;
	}

	private static bool Generate(TableEntryReference __3, UnityEngine.Localization.Locale __4, ref string __result)
	{
		try
		{
			if (Resolve(EntryId(__3), __4, out string value))
			{
				__result = value;
				report.Add("generatedResolutions", 1L);
				return false;
			}
		}
		catch (System.Exception e)
		{
			report.Error("generate", e);
		}
		return true;
	}

	private static SharedTableData.SharedTableEntry? EnsureShared(SharedTableData data, long id)
	{
		if (addingKey || !Known.TryGetValue(id, out Text value))
		{
			return null;
		}
		try
		{
			addingKey = true;
			SharedTableData.SharedTableEntry sharedTableEntry = data.GetEntry(id);
			if (sharedTableEntry == null)
			{
				sharedTableEntry = data.AddKey("Frontier." + value.Key, id);
				report.Add("sharedTableKeysAdded", 1L);
			}
			return sharedTableEntry;
		}
		finally
		{
			addingKey = false;
		}
	}

	private static void SharedEntry(SharedTableData __instance, long __0, ref SharedTableData.SharedTableEntry __result)
	{
		if (__result != null || addingKey)
		{
			return;
		}
		try
		{
			SharedTableData.SharedTableEntry sharedTableEntry = EnsureShared(__instance, __0);
			if (sharedTableEntry != null)
			{
				__result = sharedTableEntry;
			}
		}
		catch (System.Exception e)
		{
			report.Error("shared entry", e);
		}
	}

	private static bool SharedKey(SharedTableData __instance, long __0, ref string __result)
	{
		if (addingKey || !Known.TryGetValue(__0, out Text value))
		{
			return true;
		}
		try
		{
			EnsureShared(__instance, __0);
			__result = "Frontier." + value.Key;
			report.Add("sharedKeyResolutions", 1L);
			return false;
		}
		catch (System.Exception e)
		{
			report.Error("shared key", e);
			return true;
		}
	}

	private static void Attribute(AttributeData __0)
	{
		try
		{
			if (__0 != null && LocalizationPolicy.Attributes.ContainsKey(__0.Id) && CheckedAttributes.Add(__0.Id))
			{
				long existingKey = __0.LocalizedName?.TableEntryReference ?? 0;
				string existingText = "";
				int id = __0.Id;
				if (id != 79 && id != 92 && __0.LocalizedName != null)
				{
					Game.Common.LocalizedReference localizedName = __0.LocalizedName;
					existingText = new LocalizedString
					{
						TableReference = localizedName.TableReference,
						TableEntryReference = localizedName.TableEntryReference
					}.GetLocalizedString();
				}
				if (!LocalizationPolicy.ReplaceAttribute(__0.Id, existingKey, existingText))
				{
					report.Add("existingAttributeNamesPreserved", 1L);
					return;
				}
				(string, string) tuple = LocalizationPolicy.Name(__0.Id, __0.ConvertToSpeedValueFromTime());
				long num = 3468000000000000000L + __0.Id;
				Known[num] = new Text(tuple.Item1, tuple.Item2, "attribute." + __0.Id);
				__0.LocalizedName = new Game.Common.LocalizedReference
				{
					TableReference = "UI_Main",
					TableEntryReference = num
				};
				report.Add("attributeNamesBound", 1L);
				report.Save();
			}
		}
		catch (System.Exception e)
		{
			report.Error("attribute name", e);
		}
	}
}
