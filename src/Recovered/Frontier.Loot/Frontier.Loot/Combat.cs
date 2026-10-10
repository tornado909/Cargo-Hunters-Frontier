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
		__state = new List<Lease>();
		if (!Available("weapons"))
		{
			return;
		}
		try
		{
			BattleSession battleSession = Runtime.Session((SystemBase)(object)__instance);
			if (battleSession == null || !battleSession.IsBattleSession)
			{
				return;
			}
			((SystemBase)(object)__instance).CompleteDependency();
			EntityManager entityManager = ((ComponentSystemBase)(object)__instance).EntityManager;
			NativeArray<Entity> nativeArray = __instance._weaponsQuery.ToEntityArray(Allocator.Temp);
			try
			{
				for (int i = 0; i < nativeArray.Length; i++)
				{
					Entity entity = nativeArray[i];
					if (!entityManager.Exists(entity) || !EcsAccess.Has<UsableItem>(entityManager, entity) || !EcsAccess.Has<WeaponPredictedState>(entityManager, entity) || !EcsAccess.Has<WeaponCharacteristicsOverride>(entityManager, entity))
					{
						continue;
					}
					UsableItem usableItem = EcsAccess.Read<UsableItem>(entityManager, entity);
					if (!usableItem.IsActive || (!Runtime.Human(battleSession, usableItem.OwnerPlayerId) && !RankLadder.EligibleBot(battleSession, usableItem.OwnerPlayerId)))
					{
						continue;
					}
					WeaponPredictedState weaponPredictedState = EcsAccess.Read<WeaponPredictedState>(entityManager, entity);
					if (weaponPredictedState.AmmoInClip <= 0)
					{
						continue;
					}
					Bonuses bonuses = Runtime.Aggregate(battleSession, usableItem.OwnerPlayerId, usableItem.ItemId);
					if (bonuses.Count != 0)
					{
						WeaponCharacteristicsOverride weaponCharacteristicsOverride = EcsAccess.Read<WeaponCharacteristicsOverride>(entityManager, entity);
						Lease lease = new Lease
						{
							Entity = entity,
							Original = weaponCharacteristicsOverride,
							Random = weaponPredictedState.Random.state,
							Ammo = weaponPredictedState.AmmoInClip,
							Weapon = usableItem.ItemId,
							Bonus = bonuses
						};
						if (EcsAccess.Has<WeaponInterpolatedState>(entityManager, entity))
						{
							lease.FireTick = EcsAccess.Read<WeaponInterpolatedState>(entityManager, entity).FireTick;
						}
						string key = Runtime.Id(usableItem.ItemId);
						lease.Save = LootMath.Conserve(bonuses.SaveAmmo, LootMath.Sample(key, weaponPredictedState.Random.state ^ lease.FireTick, "ammo"), weaponPredictedState.AmmoInClip);
						WeaponCharacteristicsOverride value = weaponCharacteristicsOverride;
						value.IsInfiniteMagazineEnabled = weaponCharacteristicsOverride.IsInfiniteMagazineEnabled || lease.Save;
						if (bonuses.FireRate > 0f && EcsAccess.Has<WeaponCharacteristics>(entityManager, entity))
						{
							lease.Characteristics = EcsAccess.Read<WeaponCharacteristics>(entityManager, entity);
							lease.HasCharacteristics = true;
						}
						__state.Add(lease);
						EcsAccess.WriteFields(entityManager, entity, value, "IsInfiniteMagazineEnabled");
						if (lease.HasCharacteristics)
						{
							WeaponCharacteristics characteristics = lease.Characteristics;
							characteristics.ShotDuration = Math.Max(0.01f, characteristics.ShotDuration / (1f + bonuses.FireRate));
							EcsAccess.WriteFields(entityManager, entity, characteristics, "ShotDuration");
						}
						Runtime.Inc("weaponOverridePreparations", 1L);
					}
				}
			}
			finally
			{
				nativeArray.Dispose();
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
		if (leases == null || leases.Count == 0)
		{
			return;
		}
		try
		{
			((SystemBase)(object)system).CompleteDependency();
			EntityManager entityManager = ((ComponentSystemBase)(object)system).EntityManager;
			foreach (Lease lease in leases)
			{
				if (!entityManager.Exists(lease.Entity))
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
						WeaponPredictedState weaponPredictedState = EcsAccess.Read<WeaponPredictedState>(entityManager, lease.Entity);
						if (EcsAccess.Read<WeaponInterpolatedState>(entityManager, lease.Entity).FireTick == lease.FireTick && weaponPredictedState.Random.state == lease.Random)
						{
							continue;
						}
						Runtime.Inc("shotsObserved", 1L);
						BattleSession battleSession = Runtime.Session((SystemBase)(object)system);
						string text = ((battleSession != null && battleSession.IsAuthoritative) ? "authority" : "prediction");
						Runtime.Inc("shotsObserved." + text, 1L);
						if (lease.Bonus.SaveAmmo > 0f)
						{
							Runtime.Inc("ammoTrials." + text, 1L);
							if (lease.Save)
							{
								Runtime.Inc("ammoRollsSucceeded." + text, 1L);
							}
						}
						if (lease.Save && weaponPredictedState.AmmoInClip == lease.Ammo)
						{
							Runtime.Inc("conservedShots." + text, 1L);
						}
						if (lease.Save && weaponPredictedState.AmmoInClip == lease.Ammo)
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
							EcsAccess.WriteFields(entityManager, lease.Entity, lease.Original, "IsInfiniteMagazineEnabled");
						}
						if (lease.HasCharacteristics && EcsAccess.Has<WeaponCharacteristics>(entityManager, lease.Entity))
						{
							EcsAccess.WriteFields(entityManager, lease.Entity, lease.Characteristics, "ShotDuration");
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
		if (!Available("projectiles"))
		{
			return;
		}
		try
		{
			BattleSession battleSession = Runtime.Session((SystemBase)(object)__instance);
			if (battleSession == null || !battleSession.IsBattleSession)
			{
				return;
			}
			((SystemBase)(object)__instance).CompleteDependency();
			EntityManager entityManager = ((ComponentSystemBase)(object)__instance).EntityManager;
			NativeArray<Entity> nativeArray = __instance.ProjectileSpawnSystem_OnUpdate_Query.ToEntityArray(Allocator.Temp);
			try
			{
				for (int i = 0; i < nativeArray.Length; i++)
				{
					Entity entity = nativeArray[i];
					if (!entityManager.Exists(entity) || !EcsAccess.Has<ProjectileSpawnRequest>(entityManager, entity))
					{
						continue;
					}
					ProjectileSpawnRequest value = EcsAccess.Read<ProjectileSpawnRequest>(entityManager, entity);
					if ((!Runtime.Human(battleSession, value.OwnerId) && !RankLadder.EligibleBot(battleSession, value.OwnerId)) || value.DamageData.IsMeleeDamage || value.DamageData.IsSpreadDamage || value.DamageData.InstantKill)
					{
						continue;
					}
					RequestKey item = new RequestKey(((Il2CppObjectBase)((ComponentSystemBase)(object)__instance).World).Pointer, entity.Index, entity.Version, value.Tick);
					if (Prepared.Contains(item))
					{
						continue;
					}
					Bonuses bonuses = Runtime.Aggregate(battleSession, value.OwnerId, value.SpawnerId);
					if (bonuses.Count == 0)
					{
						continue;
					}
					string text = Runtime.Id(value.SpawnerId);
					double num = LootMath.CriticalFactor(bonuses.Crit, 1.5 + (double)bonuses.CritPower, LootMath.Sample(text, value.Tick, "critical-projectile"));
					float num2 = (float)((double)(1f + Math.Max(0f, bonuses.Damage)) * num);
					DamageData damageData = value.DamageData;
					damageData.Damage *= num2;
					damageData.MinDamage *= num2;
					damageData.ArmorDamage *= num2;
					damageData.MinArmorDamage *= num2;
					damageData.ArmorPenetratedDamage *= num2;
					damageData.MinArmorPenetratedDamage *= num2;
					damageData.ArmorPiercingClass = Math.Clamp(damageData.ArmorPiercingClass + bonuses.Penetration, 0, 100);
					value.DamageData = damageData;
					EcsAccess.WriteFields(entityManager, entity, value, "DamageData.Damage", "DamageData.MinDamage", "DamageData.ArmorDamage", "DamageData.MinArmorDamage", "DamageData.ArmorPenetratedDamage", "DamageData.MinArmorPenetratedDamage", "DamageData.ArmorPiercingClass");
					Prepared.Add(item);
					PreparedOrder.Enqueue(item);
					while (PreparedOrder.Count > 8192)
					{
						Prepared.Remove(PreparedOrder.Dequeue());
					}
					ShotKey shotKey = new ShotKey(((Il2CppObjectBase)((ComponentSystemBase)(object)__instance).World).Pointer, text, value.Tick);
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
				nativeArray.Dispose();
			}
		}
		catch (Exception e)
		{
			Fault("projectiles", e);
		}
	}

	private static void BeforeDamage(DamageEventSystem __instance)
	{
		if (!Available("damage"))
		{
			return;
		}
		try
		{
			BattleSession battleSession = Runtime.Session((SystemBase)(object)__instance);
			if (battleSession == null || !battleSession.IsBattleSession)
			{
				return;
			}
			((SystemBase)(object)__instance).CompleteDependency();
			EntityManager entityManager = ((ComponentSystemBase)(object)__instance).EntityManager;
			NativeArray<Entity> nativeArray = __instance.DamageEventSystem_OnUpdate_Query.ToEntityArray(Allocator.Temp);
			try
			{
				for (int i = 0; i < nativeArray.Length; i++)
				{
					Entity entity = nativeArray[i];
					if (!entityManager.Exists(entity) || !EcsAccess.Has<DamageEvent>(entityManager, entity))
					{
						continue;
					}
					DamageEvent value = EcsAccess.Read<DamageEvent>(entityManager, entity);
					if (value.DamageData.IsMeleeDamage || value.DamageData.IsSpreadDamage || value.DamageData.InstantKill || !entityManager.Exists(value.Assaulter) || !entityManager.Exists(value.Target) || !EcsAccess.Has<PlayerId>(entityManager, value.Assaulter) || !EcsAccess.Has<PlayerId>(entityManager, value.Target))
					{
						continue;
					}
					int value2 = EcsAccess.Read<PlayerId>(entityManager, value.Assaulter).Value;
					int value3 = EcsAccess.Read<PlayerId>(entityManager, value.Target).Value;
					if (!Runtime.Human(battleSession, value2) || battleSession.IsPlayerIsDead(value3) || (battleSession.IsAuthoritative && !battleSession.IsBotPlayer(value3)) || (!battleSession.IsAuthoritative && battleSession.GetPlayerAccountId(value3).HasValue))
					{
						continue;
					}
					ShotKey key = new ShotKey(((Il2CppObjectBase)((ComponentSystemBase)(object)__instance).World).Pointer, Runtime.Id(value.AssaultingItemId), (uint)value.ProjectileId);
					bool flag = false;
					if (Shots.TryGetValue(key, out (bool, long) value4) && Environment.TickCount64 - value4.Item2 < 30000)
					{
						(flag, _) = value4;
					}
					if (!flag)
					{
						Runtime.Inc("damageWithoutExecutorSnapshot", 1L);
					}
					else
					{
						if (!EcsAccess.Has<BodyPartsList>(entityManager, value.Target))
						{
							continue;
						}
						DynamicBuffer<BodyPartsList> buffer = entityManager.GetBuffer<BodyPartsList>(value.Target, isReadOnly: true);
						for (int j = 0; j < buffer.Length; j++)
						{
							BodyPartsList bodyPartsList = buffer[j];
							if (entityManager.Exists(bodyPartsList.Part) && EcsAccess.Has<BodyPart>(entityManager, bodyPartsList.Part) && EcsAccess.Read<BodyPart>(entityManager, bodyPartsList.Part).Type == BodyPartType.Head)
							{
								value.BodyPartId = bodyPartsList.Id;
								value.ArmorHit = false;
								value.ArmorIndex = -1;
								value.IsResistedByArmor = false;
								EcsAccess.WriteFields(entityManager, entity, value, "BodyPartId", "ArmorHit", "ArmorIndex", "IsResistedByArmor");
								Runtime.Inc("executorHeadEvents", 1L);
								break;
							}
						}
					}
				}
			}
			finally
			{
				nativeArray.Dispose();
			}
		}
		catch (Exception e)
		{
			Fault("damage", e);
		}
	}
}
