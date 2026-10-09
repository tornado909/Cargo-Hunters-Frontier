using System;
using System.Collections.Generic;
using System.Linq;
using Core.Utils;
using Game.DamageSystem;
using Game.DamageSystem.Data;
using Game.GameModes;
using Game.Network;
using Game.ProjectilesSystem;
using Game.UsableItemSystem.Components;
using Game.Weapons;
using Game.Weapons.Components;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using Unity.Collections;
using Unity.Entities;

namespace Frontier.Loot;

internal static class Combat
{
	internal sealed class Lease
	{
		internal Entity Entity;

		internal WeaponCharacteristicsOverride Original;

		internal WeaponCharacteristics Characteristics;

		internal uint Random;

		internal uint FireTick;

		internal int Ammo;

		internal bool Save;

		internal bool HasCharacteristics;

		internal UniqueId Weapon;

		internal Bonuses Bonus;
	}

	private readonly record struct ShotKey(IntPtr World, string Weapon, uint Tick);

	private readonly record struct RequestKey(IntPtr World, int Index, int Version, uint Tick);

	private static readonly HashSet<string> Faulted = new HashSet<string>();

	private static readonly Dictionary<ShotKey, (bool Executor, long Created)> Shots = new Dictionary<ShotKey, (bool, long)>();

	private static readonly Queue<ShotKey> Order = new Queue<ShotKey>();

	private static readonly HashSet<RequestKey> Prepared = new HashSet<RequestKey>();

	private static readonly Queue<RequestKey> PreparedOrder = new Queue<RequestKey>();

	internal static string[] OpenCircuits()
	{
		return Faulted.ToArray();
	}

	internal static bool Available(string area)
	{
		if (Runtime.CombatEnabled)
		{
			return !Faulted.Contains(area);
		}
		return false;
	}

	private static void Fault(string area, Exception e)
	{
		if (Faulted.Add(area))
		{
			Runtime.Error(area + ".circuit-open", e);
			Runtime.Inc("circuitBreakersOpened", 1L);
			Runtime.WriteReport();
		}
	}

	internal static void Install(Harmony h)
	{
		Runtime.Patch(h, typeof(WeaponUpdateSystem), "OnUpdate", 0, "BeforeWeapons", "AfterWeapons", "WeaponFinalizer", typeof(Combat));
		Runtime.Patch(h, typeof(ProjectileSpawnSystem), "OnUpdate", 0, "BeforeProjectiles", null, null, typeof(Combat));
		Runtime.Patch(h, typeof(DamageEventSystem), "GatherIncomingDamageEvents", 0, "BeforeDamage", null, null, typeof(Combat));
	}

	private static void BeforeWeapons(WeaponUpdateSystem __instance, out List<Lease> __state)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Expected O, but got Unknown
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_0155: Unknown result type (might be due to invalid IL or missing references)
		//IL_0164: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_0184: Unknown result type (might be due to invalid IL or missing references)
		//IL_019c: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_0171: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_0221: Unknown result type (might be due to invalid IL or missing references)
		//IL_0222: Unknown result type (might be due to invalid IL or missing references)
		//IL_0224: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0244: Unknown result type (might be due to invalid IL or missing references)
		//IL_0249: Unknown result type (might be due to invalid IL or missing references)
		//IL_0252: Unknown result type (might be due to invalid IL or missing references)
		//IL_0271: Unknown result type (might be due to invalid IL or missing references)
		//IL_0272: Unknown result type (might be due to invalid IL or missing references)
		//IL_0274: Unknown result type (might be due to invalid IL or missing references)
		//IL_0203: Unknown result type (might be due to invalid IL or missing references)
		//IL_0204: Unknown result type (might be due to invalid IL or missing references)
		//IL_0206: Unknown result type (might be due to invalid IL or missing references)
		//IL_020b: Unknown result type (might be due to invalid IL or missing references)
		__state = new List<Lease>();
		if (!Available("weapons"))
		{
			return;
		}
		try
		{
			BattleSession val = Runtime.Session((SystemBase)__instance);
			if (val == null || !val.IsBattleSession)
			{
				return;
			}
			((SystemBase)__instance).CompleteDependency();
			EntityManager entityManager = ((ComponentSystemBase)__instance).EntityManager;
			EntityQuery weaponsQuery = __instance._weaponsQuery;
			NativeArray<Entity> val2 = ((EntityQuery)(ref weaponsQuery)).ToEntityArray((Allocator)2);
			try
			{
				for (int i = 0; i < val2.Length; i++)
				{
					Entity val3 = val2[i];
					if (!((EntityManager)(ref entityManager)).Exists(val3) || !EcsAccess.Has<UsableItem>(entityManager, val3) || !EcsAccess.Has<WeaponPredictedState>(entityManager, val3) || !EcsAccess.Has<WeaponCharacteristicsOverride>(entityManager, val3))
					{
						continue;
					}
					UsableItem val4 = EcsAccess.Read<UsableItem>(entityManager, val3);
					if (!val4.IsActive || (!Runtime.Human(val, val4.OwnerPlayerId) && !RankLadder.EligibleBot(val, val4.OwnerPlayerId)))
					{
						continue;
					}
					WeaponPredictedState val5 = EcsAccess.Read<WeaponPredictedState>(entityManager, val3);
					if (val5.AmmoInClip <= 0)
					{
						continue;
					}
					Bonuses bonuses = Runtime.Aggregate(val, val4.OwnerPlayerId, val4.ItemId);
					if (bonuses.Count != 0)
					{
						WeaponCharacteristicsOverride val6 = EcsAccess.Read<WeaponCharacteristicsOverride>(entityManager, val3);
						Lease lease = new Lease
						{
							Entity = val3,
							Original = val6,
							Random = val5.Random.state,
							Ammo = val5.AmmoInClip,
							Weapon = val4.ItemId,
							Bonus = bonuses
						};
						if (EcsAccess.Has<WeaponInterpolatedState>(entityManager, val3))
						{
							lease.FireTick = EcsAccess.Read<WeaponInterpolatedState>(entityManager, val3).FireTick;
						}
						string key = Runtime.Id(val4.ItemId);
						lease.Save = LootMath.Conserve(bonuses.SaveAmmo, LootMath.Sample(key, val5.Random.state ^ lease.FireTick, "ammo"), val5.AmmoInClip);
						WeaponCharacteristicsOverride value = val6;
						value.IsInfiniteMagazineEnabled = val6.IsInfiniteMagazineEnabled || lease.Save;
						if (bonuses.FireRate > 0f && EcsAccess.Has<WeaponCharacteristics>(entityManager, val3))
						{
							lease.Characteristics = EcsAccess.Read<WeaponCharacteristics>(entityManager, val3);
							lease.HasCharacteristics = true;
						}
						__state.Add(lease);
						EcsAccess.WriteFields<WeaponCharacteristicsOverride>(entityManager, val3, value, new string[1] { "IsInfiniteMagazineEnabled" });
						if (lease.HasCharacteristics)
						{
							WeaponCharacteristics characteristics = lease.Characteristics;
							characteristics.ShotDuration = Math.Max(0.01f, characteristics.ShotDuration / (1f + bonuses.FireRate));
							EcsAccess.WriteFields<WeaponCharacteristics>(entityManager, val3, characteristics, new string[1] { "ShotDuration" });
						}
						Runtime.Inc("weaponOverridePreparations", 1L);
					}
				}
			}
			finally
			{
				val2.Dispose();
			}
		}
		catch (Exception e)
		{
			Fault("weapons", e);
			Restore(__instance, __state, observe: false);
		}
	}

	private static void AfterWeapons(WeaponUpdateSystem __instance, List<Lease> __state)
	{
		Restore(__instance, __state, observe: true);
		Runtime.ReportTick();
	}

	private static Exception? WeaponFinalizer(WeaponUpdateSystem __instance, Exception? __exception, List<Lease> __state)
	{
		if (__state != null && __state.Count > 0)
		{
			Restore(__instance, __state, observe: false);
		}
		return __exception;
	}

	private static void Restore(WeaponUpdateSystem system, List<Lease> leases, bool observe)
	{
		//IL_0193: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Expected O, but got Unknown
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Unknown result type (might be due to invalid IL or missing references)
		if (leases == null || leases.Count == 0)
		{
			return;
		}
		try
		{
			((SystemBase)system).CompleteDependency();
			EntityManager entityManager = ((ComponentSystemBase)system).EntityManager;
			foreach (Lease lease in leases)
			{
				if (!((EntityManager)(ref entityManager)).Exists(lease.Entity))
				{
					continue;
				}
				try
				{
					try
					{
						if (!observe || !EcsAccess.Has<WeaponInterpolatedState>(entityManager, lease.Entity) || !EcsAccess.Has<WeaponPredictedState>(entityManager, lease.Entity))
						{
							continue;
						}
						WeaponPredictedState val = EcsAccess.Read<WeaponPredictedState>(entityManager, lease.Entity);
						if (EcsAccess.Read<WeaponInterpolatedState>(entityManager, lease.Entity).FireTick == lease.FireTick && val.Random.state == lease.Random)
						{
							continue;
						}
						Runtime.Inc("shotsObserved", 1L);
						BattleSession val2 = Runtime.Session((SystemBase)system);
						string text = ((val2 != null && val2.IsAuthoritative) ? "authority" : "prediction");
						Runtime.Inc("shotsObserved." + text, 1L);
						if (lease.Bonus.SaveAmmo > 0f)
						{
							Runtime.Inc("ammoTrials." + text, 1L);
							if (lease.Save)
							{
								Runtime.Inc("ammoRollsSucceeded." + text, 1L);
							}
						}
						if (lease.Save && val.AmmoInClip == lease.Ammo)
						{
							Runtime.Inc("conservedShots." + text, 1L);
						}
						if (lease.Save && val.AmmoInClip == lease.Ammo)
						{
							Runtime.Inc("conservedShots", 1L);
						}
					}
					catch (Exception e)
					{
						Runtime.Error("weapons.observe", e);
					}
					finally
					{
						if (EcsAccess.Has<WeaponCharacteristicsOverride>(entityManager, lease.Entity))
						{
							EcsAccess.WriteFields<WeaponCharacteristicsOverride>(entityManager, lease.Entity, lease.Original, new string[1] { "IsInfiniteMagazineEnabled" });
						}
						if (lease.HasCharacteristics && EcsAccess.Has<WeaponCharacteristics>(entityManager, lease.Entity))
						{
							EcsAccess.WriteFields<WeaponCharacteristics>(entityManager, lease.Entity, lease.Characteristics, new string[1] { "ShotDuration" });
						}
					}
				}
				catch (Exception e2)
				{
					Fault("weapons", e2);
					Runtime.Inc("restoreFailures", 1L);
				}
			}
		}
		catch (Exception e3)
		{
			Fault("weapons", e3);
		}
		finally
		{
			leases.Clear();
		}
	}

	private static void BeforeProjectiles(ProjectileSpawnSystem __instance)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Expected O, but got Unknown
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0212: Unknown result type (might be due to invalid IL or missing references)
		//IL_0214: Unknown result type (might be due to invalid IL or missing references)
		//IL_0219: Unknown result type (might be due to invalid IL or missing references)
		//IL_021a: Unknown result type (might be due to invalid IL or missing references)
		//IL_021c: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b6: Unknown result type (might be due to invalid IL or missing references)
		if (!Available("projectiles"))
		{
			return;
		}
		try
		{
			BattleSession val = Runtime.Session((SystemBase)__instance);
			if (val == null || !val.IsBattleSession)
			{
				return;
			}
			((SystemBase)__instance).CompleteDependency();
			EntityManager entityManager = ((ComponentSystemBase)__instance).EntityManager;
			EntityQuery projectileSpawnSystem_OnUpdate_Query = __instance.ProjectileSpawnSystem_OnUpdate_Query;
			NativeArray<Entity> val2 = ((EntityQuery)(ref projectileSpawnSystem_OnUpdate_Query)).ToEntityArray((Allocator)2);
			try
			{
				for (int i = 0; i < val2.Length; i++)
				{
					Entity val3 = val2[i];
					if (!((EntityManager)(ref entityManager)).Exists(val3) || !EcsAccess.Has<ProjectileSpawnRequest>(entityManager, val3))
					{
						continue;
					}
					ProjectileSpawnRequest val4 = EcsAccess.Read<ProjectileSpawnRequest>(entityManager, val3);
					if ((!Runtime.Human(val, val4.OwnerId) && !RankLadder.EligibleBot(val, val4.OwnerId)) || val4.DamageData.IsMeleeDamage || val4.DamageData.IsSpreadDamage || val4.DamageData.InstantKill)
					{
						continue;
					}
					RequestKey item = new RequestKey(((Il2CppObjectBase)((ComponentSystemBase)__instance).World).Pointer, val3.Index, val3.Version, val4.Tick);
					if (Prepared.Contains(item))
					{
						continue;
					}
					Bonuses bonuses = Runtime.Aggregate(val, val4.OwnerId, val4.SpawnerId);
					if (bonuses.Count == 0)
					{
						continue;
					}
					string text = Runtime.Id(val4.SpawnerId);
					double num = LootMath.CriticalFactor(bonuses.Crit, 1.5 + (double)bonuses.CritPower, LootMath.Sample(text, val4.Tick, "critical-projectile"));
					float num2 = (float)((double)(1f + Math.Max(0f, bonuses.Damage)) * num);
					DamageData damageData = val4.DamageData;
					damageData.Damage *= num2;
					damageData.MinDamage *= num2;
					damageData.ArmorDamage *= num2;
					damageData.MinArmorDamage *= num2;
					damageData.ArmorPenetratedDamage *= num2;
					damageData.MinArmorPenetratedDamage *= num2;
					damageData.ArmorPiercingClass = Math.Clamp(damageData.ArmorPiercingClass + bonuses.Penetration, 0, 100);
					val4.DamageData = damageData;
					EcsAccess.WriteFields<ProjectileSpawnRequest>(entityManager, val3, val4, new string[7] { "DamageData.Damage", "DamageData.MinDamage", "DamageData.ArmorDamage", "DamageData.MinArmorDamage", "DamageData.ArmorPenetratedDamage", "DamageData.MinArmorPenetratedDamage", "DamageData.ArmorPiercingClass" });
					Prepared.Add(item);
					PreparedOrder.Enqueue(item);
					while (PreparedOrder.Count > 8192)
					{
						Prepared.Remove(PreparedOrder.Dequeue());
					}
					ShotKey shotKey = new ShotKey(((Il2CppObjectBase)((ComponentSystemBase)__instance).World).Pointer, text, val4.Tick);
					if (!Shots.ContainsKey(shotKey))
					{
						Order.Enqueue(shotKey);
						Runtime.Inc("projectileShotsModified", 1L);
						if (num > 1.0)
						{
							Runtime.Inc("criticalShots", 1L);
						}
						if (bonuses.Penetration > 0)
						{
							Runtime.Inc("penetratingShots", 1L);
						}
					}
					Shots[shotKey] = (bonuses.Executor, Environment.TickCount64);
					while (Order.Count > 8192)
					{
						Shots.Remove(Order.Dequeue());
					}
					Runtime.Inc("projectileRequestsModified", 1L);
				}
			}
			finally
			{
				val2.Dispose();
			}
		}
		catch (Exception e)
		{
			Fault("projectiles", e);
		}
	}

	private static void BeforeDamage(DamageEventSystem __instance)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Expected O, but got Unknown
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0179: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0194: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0216: Unknown result type (might be due to invalid IL or missing references)
		//IL_021b: Unknown result type (might be due to invalid IL or missing references)
		//IL_021f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0221: Unknown result type (might be due to invalid IL or missing references)
		//IL_0230: Unknown result type (might be due to invalid IL or missing references)
		//IL_0231: Unknown result type (might be due to invalid IL or missing references)
		//IL_0233: Unknown result type (might be due to invalid IL or missing references)
		//IL_023f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0240: Unknown result type (might be due to invalid IL or missing references)
		//IL_0242: Unknown result type (might be due to invalid IL or missing references)
		//IL_0247: Unknown result type (might be due to invalid IL or missing references)
		//IL_024c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0252: Invalid comparison between Unknown and I4
		//IL_0256: Unknown result type (might be due to invalid IL or missing references)
		//IL_027a: Unknown result type (might be due to invalid IL or missing references)
		//IL_027b: Unknown result type (might be due to invalid IL or missing references)
		//IL_027d: Unknown result type (might be due to invalid IL or missing references)
		if (!Available("damage"))
		{
			return;
		}
		try
		{
			BattleSession val = Runtime.Session((SystemBase)__instance);
			if (val == null || !val.IsBattleSession)
			{
				return;
			}
			((SystemBase)__instance).CompleteDependency();
			EntityManager entityManager = ((ComponentSystemBase)__instance).EntityManager;
			EntityQuery damageEventSystem_OnUpdate_Query = __instance.DamageEventSystem_OnUpdate_Query;
			NativeArray<Entity> val2 = ((EntityQuery)(ref damageEventSystem_OnUpdate_Query)).ToEntityArray((Allocator)2);
			try
			{
				for (int i = 0; i < val2.Length; i++)
				{
					Entity val3 = val2[i];
					if (!((EntityManager)(ref entityManager)).Exists(val3) || !EcsAccess.Has<DamageEvent>(entityManager, val3))
					{
						continue;
					}
					DamageEvent val4 = EcsAccess.Read<DamageEvent>(entityManager, val3);
					if (val4.DamageData.IsMeleeDamage || val4.DamageData.IsSpreadDamage || val4.DamageData.InstantKill || !((EntityManager)(ref entityManager)).Exists(val4.Assaulter) || !((EntityManager)(ref entityManager)).Exists(val4.Target) || !EcsAccess.Has<PlayerId>(entityManager, val4.Assaulter) || !EcsAccess.Has<PlayerId>(entityManager, val4.Target))
					{
						continue;
					}
					int value = EcsAccess.Read<PlayerId>(entityManager, val4.Assaulter).Value;
					int value2 = EcsAccess.Read<PlayerId>(entityManager, val4.Target).Value;
					if (!Runtime.Human(val, value) || val.IsPlayerIsDead(value2) || (val.IsAuthoritative && !val.IsBotPlayer(value2)) || (!val.IsAuthoritative && val.GetPlayerAccountId(value2).HasValue))
					{
						continue;
					}
					ShotKey key = new ShotKey(((Il2CppObjectBase)((ComponentSystemBase)__instance).World).Pointer, Runtime.Id(val4.AssaultingItemId), (uint)val4.ProjectileId);
					bool flag = false;
					if (Shots.TryGetValue(key, out (bool, long) value3) && Environment.TickCount64 - value3.Item2 < 30000)
					{
						(flag, _) = value3;
					}
					if (!flag)
					{
						Runtime.Inc("damageWithoutExecutorSnapshot", 1L);
					}
					else
					{
						if (!EcsAccess.Has<BodyPartsList>(entityManager, val4.Target))
						{
							continue;
						}
						DynamicBuffer<BodyPartsList> buffer = ((EntityManager)(ref entityManager)).GetBuffer<BodyPartsList>(val4.Target, true);
						for (int j = 0; j < buffer.Length; j++)
						{
							BodyPartsList val5 = buffer[j];
							if (((EntityManager)(ref entityManager)).Exists(val5.Part) && EcsAccess.Has<BodyPart>(entityManager, val5.Part) && (int)EcsAccess.Read<BodyPart>(entityManager, val5.Part).Type == 4)
							{
								val4.BodyPartId = val5.Id;
								val4.ArmorHit = false;
								val4.ArmorIndex = -1;
								val4.IsResistedByArmor = false;
								EcsAccess.WriteFields<DamageEvent>(entityManager, val3, val4, new string[4] { "BodyPartId", "ArmorHit", "ArmorIndex", "IsResistedByArmor" });
								Runtime.Inc("executorHeadEvents", 1L);
								break;
							}
						}
					}
				}
			}
			finally
			{
				val2.Dispose();
			}
		}
		catch (Exception e)
		{
			Fault("damage", e);
		}
	}
}
