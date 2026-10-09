using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using BepInEx.Unity.IL2CPP;
using Frontier.Engine;
using Frontier.Modular;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppSystem;

namespace Frontier.Core;

internal static class RigOSLocalizationFix
{
	private static readonly Dictionary<int, ValueTuple<string, string>> Names = new Dictionary<int, ValueTuple<string, string>>
	{
		[9001] = new ValueTuple<string, string>("Оператор", "Operator"),
		[9002] = new ValueTuple<string, string>("Марш-бросок", "Forced March"),
		[9003] = new ValueTuple<string, string>("Техноархеолог", "Technoarchaeologist"),
		[9004] = new ValueTuple<string, string>("Изоляция", "Insulation")
	};

	private static readonly Dictionary<long, int> SkillKeys = new Dictionary<long, int>();

	[field: CompilerGenerated]
	internal static int Repaired
	{
		[CompilerGenerated]
		get;
		[CompilerGenerated]
		private set;
	}

	internal static void Install(Harmony harmony)
	{
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Expected O, but got Unknown
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Expected O, but got Unknown
		if (!ModuleRuntime.Feature("rigos"))
		{
			return;
		}
		System.Collections.Generic.IEnumerator<KeyValuePair<string, JsonNode>> enumerator = PatchEngine.ReadObject(Path.Combine(Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location), "compiled"), "skill-localization.json")).GetEnumerator();
		try
		{
			while (((System.Collections.IEnumerator)enumerator).MoveNext())
			{
				KeyValuePair<string, JsonNode> current = enumerator.Current;
				SkillKeys[long.Parse(((object)current.Value).ToString(), (IFormatProvider)(object)CultureInfo.InvariantCulture)] = int.Parse(current.Key);
			}
		}
		finally
		{
			((System.IDisposable)enumerator)?.Dispose();
		}
		System.Type type = Reflect.FindType("UnityEngine.Localization.LocalizedString");
		MethodInfo val = Reflect.UniqueMethod(type, "GetSourceValue", 1);
		if (val.ReturnType != typeof(Object))
		{
			throw new MissingMethodException("LocalizedString source contract");
		}
		harmony.Patch((MethodBase)(object)val, new HarmonyMethod(typeof(RigOSLocalizationFix).GetMethod("NestedPrefix", (BindingFlags)60)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		System.Collections.Generic.IEnumerator<MethodInfo> enumerator2 = Enumerable.Where<MethodInfo>((System.Collections.Generic.IEnumerable<MethodInfo>)type.GetMethods((BindingFlags)60), (Func<MethodInfo, bool>)((MethodInfo m) => ((MemberInfo)m).Name == "GetLocalizedString" && m.ReturnType == typeof(string))).GetEnumerator();
		try
		{
			while (((System.Collections.IEnumerator)enumerator2).MoveNext())
			{
				MethodInfo current2 = enumerator2.Current;
				harmony.Patch((MethodBase)(object)current2, new HarmonyMethod(typeof(RigOSLocalizationFix).GetMethod("TextPrefix", (BindingFlags)60)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			}
		}
		finally
		{
			((System.IDisposable)enumerator2)?.Dispose();
		}
		((BasePlugin)Plugin.Current).Log.LogInfo((object)"RigOS localization adapter: nested-skill-keys-v3");
	}

	private static bool Russian()
	{
		try
		{
			PropertyInfo property = Reflect.FindType("UnityEngine.Localization.Settings.LocalizationSettings").GetProperty("SelectedLocale", (BindingFlags)60);
			return (Reflect.Read(Reflect.Read((property != null) ? property.GetValue((object)null) : null, "Identifier"), "Code") as string)?.StartsWith("ru", (StringComparison)5) ?? true;
		}
		catch
		{
			return true;
		}
	}

	private static bool Lookup(object value, out string text)
	{
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		text = "";
		if (!Plugin.Current.Active)
		{
			return false;
		}
		try
		{
			long num = Convert.ToInt64(Reflect.Read(Reflect.Read(value, "TableEntryReference"), "KeyId"));
			int num2 = default(int);
			if (!SkillKeys.TryGetValue(num, ref num2))
			{
				return false;
			}
			text = (Russian() ? Names[num2].Item1 : Names[num2].Item2);
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static bool TextPrefix(object __instance, ref string __result)
	{
		if (!Lookup(__instance, out string text))
		{
			return true;
		}
		__result = text;
		Repaired++;
		return false;
	}

	private static bool NestedPrefix(object __instance, ref Object __result)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Expected O, but got Unknown
		if (!Lookup(__instance, out string text))
		{
			return true;
		}
		__result = (Object)new String(IL2CPP.ManagedStringToIl2Cpp(text));
		Repaired++;
		return false;
	}

	internal static void Apply(object view, int id)
	{
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		ValueTuple<string, string> val = default(ValueTuple<string, string>);
		if (!Names.TryGetValue(id, ref val))
		{
			return;
		}
		try
		{
			object obj = Reflect.Read(view, "_nameLocalizeEvent");
			if (obj != null)
			{
				object[] array = new object[1];
				string text = ((!Russian()) ? val.Item2 : val.Item1);
				array[0] = text;
				Reflect.Call(obj, "UpdateString", array);
				Repaired++;
			}
		}
		catch (System.Exception ex)
		{
			((BasePlugin)Plugin.Current).Log.LogWarning((object)("RigOS label: " + ex.GetBaseException().Message));
		}
	}
}
