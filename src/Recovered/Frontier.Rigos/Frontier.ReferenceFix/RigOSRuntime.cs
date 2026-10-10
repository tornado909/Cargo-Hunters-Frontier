using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

namespace Frontier.ReferenceFix;

internal static class RigOSRuntime
{
	private const string AccountType = "Game.AccountSystem.Account";

	private const string SkillsType = "Game.SkillSystem.SkillsDto";

	private static readonly int[] AddedIds = new int[5] { 9001, 9002, 9003, 9004, 9005 };

	private static Type skillUtils = null;

	private static MethodInfo initialize = null;

	private static MethodInfo applyLabel = null;

	private static Evidence report = null;

	[ThreadStatic]
	private static bool migrating;

	private static readonly HashSet<long> KnownKeyArrays = new HashSet<long>();

	internal static readonly (string Assembly, string Type, string Method, int Arity, string? Prefix, string? Postfix)[] Targets = new(string, string, string, int, string, string)[7]
	{
		("Game", "Game.AccountSystem.AccountManager", "InitializeFrom", 2, "AccountInputBefore", null),
		("Game", "Game.AccountSystem.SkillHubManager", "InitializeFrom", 1, "AccountInputBefore", null),
		("Game.Shared", "Game.AccountSystem.Account", "UpdateSkills", 1, "SkillsInputBefore", null),
		("Game.Shared", "Game.AccountSystem.Account", "MetaForceUpdateSkills", 1, "SkillsInputBefore", null),
		("Game.Shared", "Game.AccountSystem.AccountManagerUtils", "SkillLevelIncrease", 3, "BeforeIncrease", "AfterIncrease"),
		("Game", "Game.UI.Controllers.Skills.SkillHubController", "UpdateSkillView", 0, "ViewListBefore", null),
		("Game", "Game.UI.Views.SkillView", "InitializeFrom", 5, null, "ViewAfter")
	};

	internal static void Install(Harmony h, RigOSPlugin plugin)
	{
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_019a: Expected O, but got Unknown
		report = new Evidence("Frontier-rigos-report.json", ((BasePlugin)plugin).Log, "0.6.2", "validated-lifecycle-no-account-getter-v10.1");
		report.Data["accountGetSkillsPatched"] = false;
		report.Data["privateAccountDataTraversal"] = false;
		report.Data["nativeTypeValidation"] = true;
		skillUtils = R.Type("Game.Shared", "Game.SkillSystem.SkillUtils");
		MethodInfo methodInfo = R.Method(skillUtils, "GetSkillIds", 0);
		initialize = R.Method(skillUtils, "InitializeSkillsLevel", 2);
		Type type = R.Type("Frontier.Core", "Frontier.Core.RigOSLocalizationFix");
		applyLabel = R.Method(type, "Apply", 2);
		((IDictionary)(R.Get(type, "Names") ?? throw new MissingFieldException("RigOS Names")))[9005] = ValueTuple.Create("Наноремонт", "Nanorepair");
		((IDictionary)(R.Get(type, "SkillKeys") ?? throw new MissingFieldException("RigOS SkillKeys")))[3465000000000009005L] = 9005;
		report.Data["existingLocalizationExtended"] = true;
		MethodInfo methodInfo2 = typeof(RigOSRuntime).GetMethod("KeysAfter", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).MakeGenericMethod(methodInfo.ReturnType);
		h.Patch((MethodBase)methodInfo, (HarmonyMethod)null, new HarmonyMethod(methodInfo2), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		(string, string, string, int, string, string)[] targets = Targets;
		for (int i = 0; i < targets.Length; i++)
		{
			(string, string, string, int, string, string) tuple = targets[i];
			R.Hook(h, R.Method(R.Type(tuple.Item1, tuple.Item2), tuple.Item3, tuple.Item4), typeof(RigOSRuntime), tuple.Item5, tuple.Item6);
		}
		object obj = methodInfo.Invoke(null, null);
		if (obj == null || !R.Values(obj, 256).Select(R.Int).Contains(9005))
		{
			throw new InvalidOperationException("Native GetSkillIds did not include 9005");
		}
		report.Data["nativeSkillIds"] = R.Values(obj, 256).Select(R.Int).ToArray();
		report.Data["hookTargets"] = Targets.Select(((string Assembly, string Type, string Method, int Arity, string Prefix, string Postfix) t) => t.Type + "." + t.Method).Prepend("Game.SkillSystem.SkillUtils.GetSkillIds").ToArray();
		report.Data["skillPointsWritten"] = false;
		report.Data["purchaseResultOverridden"] = false;
		report.Data["status"] = "REGISTERED_WAITING_FOR_ACCOUNT";
		report.Save(force: true);
		((BasePlugin)plugin).Log.LogInfo((object)"RigOS v10.1: Account.GetSkills is NOT patched; no Account._data traversal; validated lifecycle and native purchase paths only.");
	}

	private static void KeysAfter<T>(ref T __result)
	{
		try
		{
			if (__result == null)
			{
				return;
			}
			object obj = __result;
			long item = R.Pointer(obj);
			if (KnownKeyArrays.Contains(item))
			{
				return;
			}
			List<object> list = R.Values(obj, 256).ToList();
			HashSet<int> present = list.Select(R.Int).ToHashSet();
			int[] array = AddedIds.Where((int x) => !present.Contains(x)).ToArray();
			if (array.Length == 0)
			{
				KnownKeyArrays.Add(item);
				return;
			}
			object obj2 = Activator.CreateInstance(obj.GetType(), (long)(list.Count + array.Length));
			for (int num = 0; num < list.Count; num++)
			{
				R.Put(obj2, num, list[num]);
			}
			for (int num2 = 0; num2 < array.Length; num2++)
			{
				R.Put(obj2, list.Count + num2, array[num2]);
			}
			R.Set(skillUtils, "_skillIds", obj2);
			__result = (T)obj2;
			KnownKeyArrays.Add(R.Pointer(obj2));
			report.Add("nativeIdArraysExtended", 1L);
		}
		catch (Exception e)
		{
			report.Error("native skill IDs", e);
		}
	}

	private static bool Matches(object? obj, string expected)
	{
		string assembly = ((expected == "Game.AccountSystem.SkillHubManager" || expected.StartsWith("Game.UI.", StringComparison.Ordinal)) ? "Game" : "Game.Shared");
		return Matches(obj, R.Type(assembly, expected));
	}

	private static bool Matches(object? obj, Type expected)
	{
		Il2CppObjectBase val = (Il2CppObjectBase)((obj is Il2CppObjectBase) ? obj : null);
		if (val == null || val.WasCollected)
		{
			return false;
		}
		IntPtr pointer = val.Pointer;
		if (pointer == IntPtr.Zero)
		{
			return false;
		}
		IntPtr nativeClassPointer = Il2CppClassPointerStore.GetNativeClassPointer(expected);
		IntPtr intPtr = IL2CPP.il2cpp_object_get_class(pointer);
		bool assignable = nativeClassPointer != IntPtr.Zero && intPtr != IntPtr.Zero && IL2CPP.il2cpp_class_is_assignable_from(nativeClassPointer, intPtr);
		if (SkillPolicy.AcceptNative(pointer.ToInt64(), nativeClassPointer.ToInt64(), intPtr.ToInt64(), assignable))
		{
			return true;
		}
		report.Add("nativeTypeMismatches", 1L);
		report.Data["lastRejectedExpectedType"] = expected.FullName;
		report.Save();
		return false;
	}

	private static void AccountInputBefore(object __0)
	{
		EnsureAccount(__0, "account lifecycle");
	}

	private static void SkillsInputBefore(object __0)
	{
		Ensure(__0, "native skill update");
	}

	private static void EnsureAccount(object? account, string origin)
	{
		if (migrating || account == null)
		{
			return;
		}
		try
		{
			if (Matches(account, "Game.AccountSystem.Account"))
			{
				object obj = R.Call(account, "GetSkills");
				if (obj != null)
				{
					Ensure(obj, origin);
				}
			}
		}
		catch (Exception e)
		{
			report.Error(origin, e);
		}
	}

	private static int Id(object row)
	{
		return R.Int(R.Get(row, "Id"));
	}

	private static void Ensure(object? skills, string origin)
	{
		if (migrating || skills == null)
		{
			return;
		}
		try
		{
			if (!Matches(skills, "Game.SkillSystem.SkillsDto"))
			{
				return;
			}
			object obj = R.Get(skills, "Skills");
			if (obj == null)
			{
				report.Add("uninitializedListsDeferred", 1L);
			}
			else
			{
				if (!Matches(obj, initialize.GetParameters()[0].ParameterType))
				{
					return;
				}
				List<object> list = R.Values(obj, 512).ToList();
				if (list.Any((object x) => Id(x) == 9005))
				{
					report.Add("existing9005Preserved", 1L);
					return;
				}
				migrating = true;
				int num = R.Int(R.Get(skills, "SkillPointsCount"));
				object obj2 = Activator.CreateInstance(obj.GetType());
				foreach (object item in list)
				{
					R.Call(obj2, "Add", item);
				}
				object[] parameters = new object[2] { obj2, false };
				object obj3 = initialize.Invoke(null, parameters);
				report.Add("nativeInitializeCalls", 1L);
				if (obj3 == null)
				{
					throw new InvalidOperationException("Native initializer returned null");
				}
				List<object> list2 = SkillPolicy.Merge(list, R.Values(obj3, 512).ToList(), Id, delegate(object row)
				{
					R.Set(row, "Level", (short)0);
					return row;
				});
				object obj4 = Activator.CreateInstance(obj.GetType());
				foreach (object item2 in list2)
				{
					R.Call(obj4, "Add", item2);
				}
				List<object> list3 = R.Values(obj4, 512).ToList();
				if (list3.Count != list2.Count || !list3.Any((object x) => Id(x) == 9005))
				{
					throw new InvalidOperationException("Prepared skill list failed read-back");
				}
				for (int num2 = 0; num2 < list.Count; num2++)
				{
					string[] array = new string[4] { "Id", "Level", "Experience", "NextLevelExperienceGoal" };
					foreach (string text in array)
					{
						if (R.Int(R.Get(list[num2], text)) != R.Int(R.Get(list3[num2], text)))
						{
							throw new InvalidOperationException("Existing progress changed: " + text);
						}
					}
				}
				if (R.Int(R.Get(skills, "SkillPointsCount")) != num)
				{
					throw new InvalidOperationException("Skill points changed during initialization");
				}
				R.Set(skills, "Skills", obj4);
				report.Add("accountListsMigrated", 1L);
				report.Data["lastMigrationOrigin"] = origin;
				report.Data["preservedRecordCount"] = list.Count;
				report.Data["nativeRegisteredLevel"] = 0;
				report.Data["status"] = "SKILL_9005_REGISTERED_LEVEL_0";
				report.Save(force: true);
			}
		}
		catch (Exception e)
		{
			report.Error(origin + " initialization", e);
		}
		finally
		{
			migrating = false;
		}
	}

	private static bool IsNanoRequest(object changes)
	{
		return R.Values(changes, 128).Any((object x) => R.Int(R.Get(x, "SkillId")) == 9005);
	}

	private static void BeforeIncrease(object __0, object __1)
	{
		try
		{
			if (IsNanoRequest(__0) && Matches(__1, "Game.AccountSystem.AccountContext"))
			{
				EnsureAccount(R.Get(__1, "Account"), "before native Nanorepair purchase");
			}
		}
		catch (Exception e)
		{
			report.Error("before native purchase", e);
		}
	}

	private static void AfterIncrease(object __0, object? __result)
	{
		try
		{
			if (!IsNanoRequest(__0))
			{
				return;
			}
			report.Add("purchaseCallsObserved", 1L);
			if (__result != null && Matches(__result, "Game.SkillSystem.SkillsDto"))
			{
				object obj = R.Values(R.Get(__result, "Skills"), 512).FirstOrDefault((object x) => Id(x) == 9005);
				if (obj != null)
				{
					report.Data["nativeReturnedLevel"] = R.Int(R.Get(obj, "Level"));
				}
				report.Add("nativePurchaseResultsReturned", 1L);
			}
			else
			{
				report.Add("nativePurchaseFailures", 1L);
			}
			report.Save(force: true);
		}
		catch (Exception e)
		{
			report.Error("purchase observation", e);
		}
	}

	private static void ViewListBefore(object __instance)
	{
		try
		{
			if (Matches(__instance, "Game.UI.Controllers.Skills.SkillHubController"))
			{
				object obj = R.Get(__instance, "_skillHubManager");
				if (obj != null && Matches(obj, "Game.AccountSystem.SkillHubManager"))
				{
					EnsureAccount(R.Call(obj, "GetAccount"), "skill screen refresh");
				}
			}
		}
		catch (Exception e)
		{
			report.Error("skill screen", e);
		}
	}

	private static void ViewAfter(object __instance, object __0)
	{
		try
		{
			if (R.Int(R.Get(__0, "Id")) == 9005)
			{
				applyLabel.Invoke(null, new object[2] { __instance, 9005 });
				report.Add("skill9005Views", 1L);
				report.Data["label"] = "Наноремонт";
				report.Save(force: true);
			}
		}
		catch (Exception e)
		{
			report.Error("existing RigOS label path", e);
		}
	}
}
