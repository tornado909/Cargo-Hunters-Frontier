using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Unity.IL2CPP;
using Core.Utils;
using Game.AccountSystem;
using Game.Application.Client;
using Game.Application.Server;
using Game.Character;
using Game.DamageSystem;
using Game.GameModes;
using Game.ItemsSystem;
using Game.ItemsSystem.GameEvents;
using Game.ItemsSystem.ItemComponents;
using Game.SkillSystem;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppSystem.Collections.Generic;
using Unity.Entities;

namespace Frontier.ReferenceFix;

internal static class NanoRuntime
{
	private sealed class Clock
	{
		internal long Session;

		internal long World;

		internal double Last = R.Now;

		internal double Next;

		internal double Seen = R.Now;

		internal readonly System.Collections.Generic.Dictionary<int, Owner> Owners = new System.Collections.Generic.Dictionary<int, Owner>();
	}

	private sealed class Owner
	{
		internal int Index;

		internal int Version;

		internal int Level;

		internal double SkillAt;

		internal readonly System.Collections.Generic.Dictionary<string, float> Previous = new System.Collections.Generic.Dictionary<string, float>();
	}

	private static readonly System.Collections.Generic.Dictionary<long, Clock> Clocks = new System.Collections.Generic.Dictionary<long, Clock>();

	private static Evidence report = null;

	private static bool inside;

	private static double nextClient;

	private static double nextPrune;

	internal static void Install(Harmony h, NanorepairPlugin p)
	{
		report = new Evidence("Frontier-nanorepair-report.json", ((BasePlugin)p).Log, "0.3.3", "server-battle-loop-safe-lifecycle-v10.8.1");
		report.Data["skillId"] = 9005;
		report.Data["repairPerSecond"] = 0.01;
		report.Data["healthAdded"] = 0.0;
		report.Data["rawEcsWrites"] = false;
		report.Data["completeAllJobsUsed"] = false;
		report.Data["accountGetterPatched"] = false;
		report.Data["shelterHealingByThisPlugin"] = false;
		report.Data["tickOwner"] = "ServerBattleState.UpdateBattleState; independent of BodyPartsSynchronizationSystem invocation";
		report.Data["healthBinding"] = "Native BodyPartStateExtensions.ApplyRepair -> current CharacterHealthDataProvider.SetBodyPartState -> installed BodyPartComponent.Set; native status rules";
		R.Hook(h, NanoHookPlan.ServerTick(), typeof(NanoRuntime), null, "ServerTick");
		MethodInfo[] array = NanoHookPlan.Cleanup();
		foreach (MethodInfo native in array)
		{
			R.Hook(h, native, typeof(NanoRuntime), "Remove");
		}
		R.Hook(h, NanoHookPlan.ClientTick(), typeof(NanoRuntime), null, "ClientObserve");
		report.Data["startupRevision"] = "v10.8.1";
		report.Data["enterExitStatePatched"] = false;
		report.Data["cleanupPolicy"] = "non-shared LeaveBattleState/Leave only; null-safe, existing-clock-only, idempotent";
		report.Data["status"] = "WAITING_FOR_SERVER_BATTLE_LOOP";
		report.Save(force: true);
		((BasePlugin)p).Log.LogInfo((object)"Nanorepair v10.8.1: shared EnterExitState hook removed; server battle-loop cadence; native ApplyRepair status rules; authoritative entity AND installed-item confirmation. No client-side healing.");
	}

	private static void Remove(ServerBattleState? __instance)
	{
		if (__instance == null || ((Il2CppObjectBase)__instance).WasCollected)
		{
			return;
		}
		try
		{
			RemoveKey(((Il2CppObjectBase)__instance).Pointer.ToInt64());
		}
		catch (Exception e)
		{
			if (report != null)
			{
				report.Error("lifecycle cleanup", e);
			}
		}
	}

	private static bool RemoveKey(long key)
	{
		if (key == 0L || !Clocks.Remove(key))
		{
			return false;
		}
		if (report != null)
		{
			report.Add("lifecycleCleanups", 1L);
			report.Data["status"] = "SERVER_RAID_LEFT";
			report.Save();
		}
		return true;
	}

	private static Clock BindClock(long key, long session, long world, double now)
	{
		if (!Clocks.TryGetValue(key, out Clock value) || value.Session != session || value.World != world)
		{
			System.Collections.Generic.Dictionary<long, Clock> clocks = Clocks;
			Clock obj = new Clock
			{
				Session = session,
				World = world,
				Last = now
			};
			value = obj;
			clocks[key] = obj;
			if (report != null)
			{
				report.Add("lifecycleClockStarts", 1L);
			}
		}
		value.Seen = now;
		return value;
	}

	private static void Prune(double now, long current)
	{
		if (now < nextPrune)
		{
			return;
		}
		nextPrune = now + 5.0;
		System.Collections.Generic.List<long> list = new System.Collections.Generic.List<long>();
		foreach (System.Collections.Generic.KeyValuePair<long, Clock> clock in Clocks)
		{
			if (clock.Key != current && now - clock.Value.Seen > 10.0)
			{
				list.Add(clock.Key);
			}
		}
		foreach (long item in list)
		{
			Clocks.Remove(item);
		}
	}

	private static int Skill(BattleSession.Player p)
	{
		Account account = p.Account;
		if (account == null)
		{
			return 0;
		}
		Il2CppSystem.Collections.Generic.List<SkillExperienceDto> list = account.GetSkills()?.Skills;
		if (list == null || list.Count > 512)
		{
			return 0;
		}
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i].Id == 9005)
			{
				return list[i].Level;
			}
		}
		return 0;
	}

	private static void ServerTick(ServerBattleState __instance)
	{
		if (inside || __instance == null || ((Il2CppObjectBase)__instance).WasCollected || report == null)
		{
			return;
		}
		inside = true;
		double now = R.Now;
		try
		{
			report.Add("serverLoopCalls", 1L);
			BattleSession battleSession = __instance._battleSession;
			World world = __instance._world;
			if (battleSession == null || world == null || !world.IsCreated || !battleSession.IsAuthoritative)
			{
				report.Data["status"] = "WAITING_FOR_SERVER_WORLD";
				return;
			}
			if (!battleSession.IsBattleSession || world.Name.Contains("Shelter", StringComparison.OrdinalIgnoreCase))
			{
				Remove(__instance);
				report.Add("nonRaidWorldsSkipped", 1L);
				return;
			}
			long num = ((Il2CppObjectBase)__instance).Pointer.ToInt64();
			long session = ((Il2CppObjectBase)battleSession).Pointer.ToInt64();
			long world2 = ((Il2CppObjectBase)world).Pointer.ToInt64();
			Clock clock = BindClock(num, session, world2, now);
			Prune(now, num);
			if (now < clock.Next)
			{
				return;
			}
			double num2 = Math.Clamp(now - clock.Last, 0.0, 1.0);
			clock.Last = now;
			clock.Next = now + 0.5;
			if (num2 <= 0.0)
			{
				return;
			}
			Il2CppSystem.Collections.Generic.List<int> list = new Il2CppSystem.Collections.Generic.List<int>();
			battleSession.GetPlayerIds(list);
			if (list.Count > 2048)
			{
				throw new InvalidOperationException("Player count bound");
			}
			int num3 = 0;
			for (int i = 0; i < list.Count; i++)
			{
				BattleSession.Player player = battleSession.GetPlayer(list[i]);
				if (player == null || player.IsBot || player.IsDead || !player.EntityHasBeenSpawned || !player.AccountId.HasValue)
				{
					continue;
				}
				Entity entity = player.Entity;
				EntityManager entityManager = world.EntityManager;
				if (entityManager.Exists(entity))
				{
					if (!clock.Owners.TryGetValue(player.Id, out Owner value) || value.Index != entity.Index || value.Version != entity.Version)
					{
						System.Collections.Generic.Dictionary<int, Owner> owners = clock.Owners;
						int id = player.Id;
						Owner obj = new Owner
						{
							Index = entity.Index,
							Version = entity.Version
						};
						value = obj;
						owners[id] = obj;
					}
					if (now >= value.SkillAt)
					{
						value.Level = Skill(player);
						value.SkillAt = now + 2.0;
					}
					if (value.Level < 1)
					{
						report.Add("unlearnedSkipped", 1L);
						continue;
					}
					num3++;
					CharacterHealthDataProvider hp = new CharacterHealthDataProvider(battleSession.ItemsManager, entityManager, entity);
					Repair(battleSession, player, hp, value, num2);
				}
			}
			report.Add("repairPasses", 1L);
			report.Data["lastEligibleOwners"] = num3;
			report.Data["world"] = world.Name;
			report.Data["status"] = ((num3 > 0) ? "AUTHORITATIVE_REPAIR_PASS" : "NO_LEARNED_OWNER");
		}
		catch (Exception e)
		{
			report.Error("server repair", e);
			report.Data["status"] = "REPAIR_ERROR";
		}
		finally
		{
			inside = false;
			report.Timing("repairPassMs", (R.Now - now) * 1000.0);
			report.Save();
		}
	}

	private static void ClientObserve(ClientBattleState __instance)
	{
		if (__instance == null || ((Il2CppObjectBase)__instance).WasCollected || report == null || R.Now < nextClient)
		{
			return;
		}
		nextClient = R.Now + 2.0;
		try
		{
			BattleSession battleSession = __instance._inGameUiController?._battleSession;
			if (battleSession == null || battleSession.IsAuthoritative || !battleSession.IsBattleSession)
			{
				return;
			}
			BattleSession.Player player = battleSession.GetPlayer(battleSession.GetLocalPlayerId());
			IHealthDataProvider healthDataProvider = player?.CharacterHealth;
			if (player == null || healthDataProvider == null || player.IsDead || !player.EntityHasBeenSpawned)
			{
				return;
			}
			Il2CppSystem.Collections.Generic.List<int> bodyPartIdsDontUseForEach = healthDataProvider.GetBodyPartIdsDontUseForEach();
			Il2CppSystem.Collections.Generic.Dictionary<int, UniqueId> bodyPartsItemsIds = healthDataProvider.GetBodyPartsItemsIds();
			if (bodyPartIdsDontUseForEach == null || bodyPartsItemsIds == null || bodyPartIdsDontUseForEach.Count > 32)
			{
				return;
			}
			System.Collections.Generic.List<object> list = new System.Collections.Generic.List<object>();
			for (int i = 0; i < bodyPartIdsDontUseForEach.Count; i++)
			{
				int num = bodyPartIdsDontUseForEach[i];
				if (healthDataProvider.TryGetBodyPartState(num, out var state) && bodyPartsItemsIds.ContainsKey(num))
				{
					BodyPartComponent bodyPartComponent = battleSession.ItemsManager.Find(bodyPartsItemsIds[num])?.GetComponent<BodyPartComponent>();
					if (bodyPartComponent != null)
					{
						BodyPartState bodyPartState = bodyPartComponent.BodyPartState;
						list.Add(new
						{
							id = num,
							health = state.Health,
							maximum = state.MaxHealth,
							status = (int)state.Status,
							itemHealth = bodyPartState.Health,
							itemMaximum = bodyPartState.MaxHealth,
							itemStatus = (int)bodyPartState.Status
						});
					}
				}
			}
			report.Add("clientObservations", 1L);
			report.Data["lastClientObservation"] = new
			{
				player = player.Id,
				health = healthDataProvider.GetHealth(),
				maximum = healthDataProvider.GetMaxHealth(),
				parts = list,
				utc = DateTime.UtcNow.ToString("O"),
				uiPixelsVerified = false
			};
			report.Save();
		}
		catch (Exception e)
		{
			report.Error("client observation", e);
		}
	}

	private static void Repair(BattleSession s, BattleSession.Player p, CharacterHealthDataProvider hp, Owner o, double dt)
	{
		Il2CppSystem.Collections.Generic.List<int> bodyPartIdsDontUseForEach = hp.GetBodyPartIdsDontUseForEach();
		Il2CppSystem.Collections.Generic.Dictionary<int, UniqueId> bodyPartsItemsIds = hp.GetBodyPartsItemsIds();
		if (bodyPartIdsDontUseForEach == null || bodyPartsItemsIds == null || bodyPartIdsDontUseForEach.Count > 32)
		{
			report.Add("partsNotReady", 1L);
			return;
		}
		System.Collections.Generic.List<object> list = new System.Collections.Generic.List<object>();
		for (int i = 0; i < bodyPartIdsDontUseForEach.Count; i++)
		{
			int num = bodyPartIdsDontUseForEach[i];
			if (!bodyPartsItemsIds.ContainsKey(num) || !hp.TryGetBodyPartState(num, out var state))
			{
				continue;
			}
			IItem item = s.ItemsManager.Find(bodyPartsItemsIds[num]);
			BodyPartComponent bodyPartComponent = item?.GetComponent<BodyPartComponent>();
			if (item == null || bodyPartComponent == null || bodyPartComponent.IsFramePart || bodyPartComponent.BodyPartId != num)
			{
				report.Add("unmatchedParts", 1L);
				continue;
			}
			BodyPartState bodyPartState = bodyPartComponent.BodyPartState;
			string text = item.Id.ToString();
			if (o.Previous.Remove(text, out var value))
			{
				report.Add((state.Health + 0.025f >= value) ? "increasesPersistedNextPass" : "damagedOrChangedNextPass", 1L);
			}
			report.Add("bodyPartsInspected", 1L);
			float num2 = HealthPolicy.Target(state.Health, state.MaxHealth, (int)state.Status, state.IsDismembered, o.Level, bot: false, dead: false, dt);
			if (num2 <= state.Health)
			{
				continue;
			}
			BodyPartState state2 = state;
			state2.ApplyRepair(num2 - state.Health);
			if (!float.IsFinite(state2.Health) || state2.MaxHealth != state.MaxHealth || state2.IsDismembered != state.IsDismembered || state2.Health <= state.Health || state2.Health > num2 + 0.03f)
			{
				report.Add("nativeRepairPolicyRejected", 1L);
				continue;
			}
			hp.SetBodyPartState(num, state2);
			report.Add("nativePartSetCalls", 1L);
			if (!hp.TryGetBodyPartState(num, out var state3) || state3.Health <= state.Health || state3.Health > num2 + 0.03f)
			{
				report.Add("nativeReadbackRejected", 1L);
				continue;
			}
			bodyPartComponent.Set(state3);
			s.ItemsOperationContext.EventPublisher.Publish(new ItemChangedEventArgs
			{
				ItemId = item.Id
			});
			BodyPartState bodyPartState2 = bodyPartComponent.BodyPartState;
			bool flag = Math.Abs(bodyPartState2.Health - state3.Health) < 0.03f;
			float num3 = Math.Max(0f, state3.Health - state.Health);
			if (flag)
			{
				report.Add("confirmedServerAndItemIncreases", 1L);
				report.Data["healthAdded"] = Convert.ToDouble(report.Data["healthAdded"]) + (double)num3;
				o.Previous[text] = state3.Health;
			}
			list.Add(new
			{
				part = num,
				item = text,
				before = state.Health,
				requested = num2,
				after = state3.Health,
				itemBefore = bodyPartState.Health,
				itemAfter = bodyPartState2.Health,
				beforeStatus = (int)state.Status,
				afterStatus = (int)state3.Status,
				matched = flag
			});
		}
		report.Data["lastServerObservation"] = new
		{
			player = p.Id,
			entity = p.Entity.ToString(),
			health = hp.GetHealth(),
			maxHealth = hp.GetMaxHealth(),
			parts = list,
			utc = DateTime.UtcNow.ToString("O"),
			clientPixelsVerified = false
		};
	}
}
