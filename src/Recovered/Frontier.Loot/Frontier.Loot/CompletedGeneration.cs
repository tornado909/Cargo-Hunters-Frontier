using System;
using System.Linq;
using System.Reflection;
using Frontier.Safety;
using Game.ItemsSystem;
using HarmonyLib;
using Il2CppSystem;
using Il2CppSystem.Collections.Generic;

namespace Frontier.Loot;

internal static class CompletedGeneration
{
	internal sealed class Scope
	{
		internal int Depth;

		internal int Start;

		internal Runtime.CreationContext? Previous;

		internal Runtime.CreationContext? Context;

		internal bool Own;

		internal bool Eligible;
	}

	[System.ThreadStatic]
	private static int depth;

	internal static MethodInfo[] Targets()
	{
		Assembly assembly = typeof(ItemPresetUtils).Assembly;
		string[] implemented = new string[8] { "ItemByAssemblyPreset", "ItemByContainer", "ItemByCriteria", "ItemByItemPresetId", "ItemByRandomPreset", "ItemByTemplateId", "ItemsByLeveledPreset", "ItemsByList" };
		return (from m in (from t in assembly.GetTypes()
				where t.Namespace == "Game.DroppedItemSpawnPointSystem.Data.ItemAtSpawnPoints" && !t.IsNested && implemented.Contains(t.Name) && t.BaseType == typeof(Il2CppSystem.Object)
				select t).SelectMany((System.Type t) => t.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public))
			where m.Name == "Generate" && m.ReturnType == typeof(void) && m.GetParameters().Length == 9 && m.GetParameters()[3].ParameterType == typeof(IItemsManager) && m.GetParameters()[5].ParameterType == typeof(List<IItem>) && m.GetParameters()[7].ParameterType == typeof(bool)
			select m).ToArray();
	}

	internal static void Install(Harmony h)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Expected O, but got Unknown
		//IL_007f: Expected O, but got Unknown
		//IL_007f: Expected O, but got Unknown
		MethodInfo[] array = Targets();
		if (array.Length == 0)
		{
			throw new System.InvalidOperationException("No native item-generation contract");
		}
		MethodInfo[] array2 = array;
		foreach (MethodInfo methodInfo in array2)
		{
			NativeHookContract.RequireSafe(methodInfo);
			h.Patch((MethodBase)methodInfo, new HarmonyMethod(typeof(CompletedGeneration).GetMethod("Before", BindingFlags.Static | BindingFlags.NonPublic)), new HarmonyMethod(typeof(CompletedGeneration).GetMethod("After", BindingFlags.Static | BindingFlags.NonPublic)), (HarmonyMethod)null, new HarmonyMethod(typeof(CompletedGeneration).GetMethod("Finish", BindingFlags.Static | BindingFlags.NonPublic)), (HarmonyMethod)null);
		}
		Runtime.Inc("completedGenerationHooks", array.Length);
	}

	private static void Before(IItemsManager __3, List<IItem> __5, bool __7, out Scope __state)
	{
		__state = new Scope
		{
			Depth = depth,
			Start = (__5?.Count ?? 0),
			Previous = Runtime.CurrentCreation
		};
		depth++;
		if (!(depth != 1 || !Runtime.Enabled || !RankLadder.Active || __7) && __3 != null && __3.IsAuthoritative && __5 != null && !Runtime.InNativeBotAssembly)
		{
			__state.Eligible = true;
			if (Runtime.CurrentCreation == null)
			{
				Runtime.CurrentCreation = new Runtime.CreationContext("normal", "", dissect: false);
				__state.Own = true;
			}
			__state.Context = Runtime.CurrentCreation;
		}
	}

	private static void After(IItemsManager __3, List<IItem> __5, Scope __state)
	{
		if (__state == null || !__state.Eligible || __state.Context == null || __5 == null)
		{
			return;
		}
		try
		{
			List<IItem> list = new List<IItem>();
			int count = __5.Count;
			if (count < __state.Start || count - __state.Start > 4096)
			{
				return;
			}
			for (int i = __state.Start; i < count; i++)
			{
				if (__5[i] != null)
				{
					list.Add(__5[i]);
				}
			}
			LateRankBinding.Apply(__3, list, __state.Context.Created, __state.Context.Rank, __state.Context.Dissection, "world");
			Runtime.Inc("completedWorldGenerations", 1L);
		}
		catch (System.Exception e)
		{
			Runtime.Error("completed-world-rank", e);
		}
	}

	private static System.Exception? Finish(System.Exception? __exception, Scope __state)
	{
		if (__state != null)
		{
			depth = __state.Depth;
			if (__state.Own)
			{
				Runtime.CurrentCreation = __state.Previous;
			}
		}
		return __exception;
	}
}
