using System;
using System.Collections;
using System.Reflection;
using Core.Utils;
using Frontier.Loot;
using Game.AISystem;
using Game.GameModes;
using Game.ItemsSystem;
using Game.ProgressSystem;
using Game.SpawnPointSystem.BotSpawnPoints.Data;
using Game.Weapons;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;

namespace Frontier.Survivor;

internal static class SurvivorHooks
{
	internal sealed class SpawnLease : IDisposable
	{
		internal long Pointer;

		internal NativeRoot? Root;

		public void Dispose()
		{
			if (Pointer != 0L)
			{
				((IDictionary)SelectionsField.GetValue(null)).Remove(Pointer);
				Pointer = 0L;
			}
			Root?.Dispose();
			Root = null;
		}
	}

	[ThreadStatic]
	private static bool weaponScope;

	private static readonly Type CombatType = typeof(Runtime).Assembly.GetType("Frontier.Loot.Combat", throwOnError: true);

	private static readonly MethodInfo CopyRecipe = AccessTools.Method(typeof(RankLadder), "CopySpawnRecipe", (Type[])null, (Type[])null);

	private static readonly Type SelectionType = typeof(RankLadder).GetNestedType("SpawnSelection", BindingFlags.NonPublic);

	private static readonly FieldInfo SelectionsField = AccessTools.Field(typeof(RankLadder), "SelectedSpawns");

	private static readonly string[] SelectionNames = new string[8] { "Preset", "Rank", "RankedId", "BaseId", "EquipmentEntries", "EquipmentId", "PlatformId", "LootId" };

	private static readonly FieldInfo[] SelectionFields = Array.ConvertAll(SelectionNames, (string n) => AccessTools.Field(SelectionType, n));

	internal static void Install(Harmony h)
	{
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Expected O, but got Unknown
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Expected O, but got Unknown
		//IL_00e3: Expected O, but got Unknown
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Expected O, but got Unknown
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		//IL_018b: Expected O, but got Unknown
		//IL_018b: Expected O, but got Unknown
		//IL_018b: Expected O, but got Unknown
		//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c1: Expected O, but got Unknown
		Preflight();
		SurvivorCoopTransport.Preflight();
		if (CopyRecipe == null || SelectionType == null || SelectionsField == null || Array.Exists(SelectionFields, (FieldInfo f) => f == null))
		{
			throw new MissingMemberException("Existing Frontier rank recipe adapter is unavailable");
		}
		h.Patch((MethodBase)AccessTools.Method(typeof(Runtime), "Aggregate", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(SurvivorHooks), "Aggregate", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		h.Patch((MethodBase)AccessTools.Method(CombatType, "BeforeWeapons", (Type[])null, (Type[])null), new HarmonyMethod(typeof(SurvivorHooks), "BeginWeapons", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, new HarmonyMethod(typeof(SurvivorHooks), "EndWeapons", (Type[])null), (HarmonyMethod)null);
		h.Patch((MethodBase)AccessTools.Method(typeof(LootMath), "Conserve", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(SurvivorHooks), "Conserve", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		MethodInfo methodInfo = AccessTools.Method(typeof(AiCharacterSpawnSystem), "GetPlayerEquipment", (Type[])null, (Type[])null);
		h.Patch((MethodBase)methodInfo, new HarmonyMethod(typeof(SurvivorHooks), "BeforeEquipment", (Type[])null)
		{
			priority = 800
		}, new HarmonyMethod(typeof(SurvivorHooks), "AfterEquipment", (Type[])null)
		{
			priority = 0
		}, (HarmonyMethod)null, new HarmonyMethod(typeof(SurvivorHooks), "EquipmentFinalizer", (Type[])null), (HarmonyMethod)null);
		h.Patch((MethodBase)AccessTools.Method(typeof(ServerExperienceManager), "HandleAIPlayerKilled", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(SurvivorHooks), "Killed", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		Trace.Data["survivorCombatAdapter"] = new
		{
			ammo = "Frontier.Loot.Runtime.Aggregate.SaveAmmo = 1; existing Combat.BeforeWeapons leases",
			chance = "100% only inside Survivor weapon update; original 95% cap elsewhere",
			persistentItemsChanged = false
		};
	}

	internal static string[] Preflight()
	{
		MethodInfo methodInfo = AccessTools.Method(typeof(ServerExperienceManager), "HandleAIPlayerKilled", (Type[])null, (Type[])null);
		if (methodInfo == null || methodInfo.ReturnType != typeof(void) || methodInfo.GetParameters().Length != 3 || methodInfo.GetParameters()[0].ParameterType != typeof(int) || methodInfo.GetParameters()[1].ParameterType != typeof(int) || methodInfo.GetParameters()[2].ParameterType != typeof(int).MakeByRefType() || !methodInfo.GetParameters()[2].IsOut)
		{
			throw new MissingMemberException("Native killer/experience contract changed");
		}
		MethodInfo methodInfo2 = AccessTools.Method(typeof(Runtime), "Aggregate", (Type[])null, (Type[])null);
		if (methodInfo2 == null || methodInfo2.ReturnType != typeof(Bonuses) || methodInfo2.GetParameters().Length != 3 || methodInfo2.GetParameters()[0].ParameterType != typeof(BattleSession) || methodInfo2.GetParameters()[1].ParameterType != typeof(int) || methodInfo2.GetParameters()[2].ParameterType != typeof(UniqueId))
		{
			throw new MissingMemberException("Personal combat aggregate contract changed");
		}
		if (CopyRecipe == null || SelectionsField == null || Array.Exists(SelectionFields, (FieldInfo f) => f == null))
		{
			throw new MissingMemberException("Native rank recipe adapter changed");
		}
		return new string[3]
		{
			methodInfo.ToString(),
			methodInfo2.ToString(),
			CopyRecipe.ToString()
		};
	}

	private static void BeginWeapons(WeaponUpdateSystem __0, out bool __state)
	{
		__state = weaponScope;
		weaponScope = SurvivorRun.OwnWorld(__0?.World);
	}

	private static Exception? EndWeapons(Exception? __exception, bool __state)
	{
		weaponScope = __state;
		return __exception;
	}

	private static void Conserve(double __0, double __1, int __2, ref bool __result)
	{
		if (weaponScope && __0 == 1.0 && double.IsFinite(__1))
		{
			__result = __2 > 0;
			if (__result)
			{
				SurvivorRun.AmmoModifierTrials++;
			}
		}
	}

	private static void Aggregate(BattleSession __0, int __1, UniqueId __2, ref Bonuses __result)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Expected O, but got Unknown
		if (__result != null && SurvivorRun.IsHuman(__0, __1))
		{
			RunProgress runProgress = SurvivorRun.Personal(__0, __1);
			Bonuses val = __result;
			__result = new Bonuses
			{
				Damage = val.Damage + runProgress.Damage,
				Crit = val.Crit + runProgress.Crit,
				CritPower = val.CritPower + runProgress.CritPower,
				SaveAmmo = 1f,
				FireRate = val.FireRate + runProgress.FireRate,
				Penetration = val.Penetration + runProgress.Penetration,
				Executor = val.Executor,
				Count = Math.Max(1, val.Count) + runProgress.Level,
				HasLadder = val.HasLadder
			};
		}
	}

	private static void BeforeEquipment(AiCharacterSpawnSystem __instance, int __0, ref BotPresetData __1, out SpawnLease? __state)
	{
		__state = null;
		if (!SurvivorRun.OwnAuthority(__instance?._battleSession) || __1 == null || !SurvivorRun.TryRecipe(__1.Id, out BotPresetData original, out int rank))
		{
			return;
		}
		BotPresetData botPresetData = (BotPresetData)CopyRecipe.Invoke(null, new object[2] { original, __1 });
		SpawnLease spawnLease = new SpawnLease
		{
			Pointer = ((Il2CppObjectBase)botPresetData).Pointer.ToInt64(),
			Root = new NativeRoot((Il2CppObjectBase)(object)botPresetData)
		};
		try
		{
			object obj = Activator.CreateInstance(SelectionType, nonPublic: true);
			object[] array = new object[8]
			{
				botPresetData,
				rank,
				__1.Id.ToString(),
				original.Id.ToString(),
				original.Equipments?.Count ?? 0,
				original.EquipmentPresetId.ToString(),
				original.PlatformPresetId.ToString(),
				original.LootPresetId.ToString()
			};
			for (int i = 0; i < array.Length; i++)
			{
				SelectionFields[i].SetValue(obj, array[i]);
			}
			((IDictionary)SelectionsField.GetValue(null)).Add(spawnLease.Pointer, obj);
			__1 = botPresetData;
			__state = spawnLease;
			SurvivorRun.EquipmentRequested(__0, rank, original.Id.ToString());
		}
		catch
		{
			spawnLease.Dispose();
			throw;
		}
	}

	private static void AfterEquipment(AiCharacterSpawnSystem __instance, int __0, Equipment __result)
	{
		if (__result != null && SurvivorRun.OwnAuthority(__instance?._battleSession))
		{
			SurvivorRun.EquipmentCreated(__0, __result);
		}
	}

	private static Exception? EquipmentFinalizer(Exception? __exception, SpawnLease? __state)
	{
		__state?.Dispose();
		if (__exception != null && __state != null)
		{
			SurvivorRun.Fail("native equipment: " + __exception.GetBaseException().Message);
		}
		return __exception;
	}

	private static void Killed(ServerExperienceManager __instance, int __0, int __1)
	{
		if (SurvivorRun.OwnAuthority(__instance?._battleSession) && SurvivorRun.IsHuman(__instance._battleSession, __0))
		{
			SurvivorRun.Kill(__0, __1);
		}
	}
}
