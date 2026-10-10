using System;
using System.Collections.Generic;
using System.Linq;
using Core.Utils;
using Frontier.Loot;
using Game.AISystem;
using Game.Character;
using Game.GameModes;
using Game.ItemsSystem;
using Game.SpawnPointSystem.BotSpawnPoints.Data;
using Game.SpawnPointSystem.GameEvents.ScriptingEvents;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppSystem.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AI;

namespace Frontier.Survivor;

internal static class SurvivorRun
{
	private static World? authorityWorld;

	private static World? clientWorld;

	private static BattleSession? authority;

	private static BattleSession? client;

	private static readonly LeaseBag sessions = new LeaseBag();

	private static readonly System.Collections.Generic.Dictionary<string, (BotPresetData Base, int Rank)> recipes = new System.Collections.Generic.Dictionary<string, (BotPresetData, int)>(StringComparer.Ordinal);

	private static readonly System.Collections.Generic.Dictionary<int, (int Rank, string Base)> live = new System.Collections.Generic.Dictionary<int, (int, string)>();

	private static readonly int[] spawnedRanks = new int[28];

	private static System.Random random = new System.Random(1);

	private static double nextUpdate;

	private static double lastUpdate;

	private static double nextSpawn;

	private static double pendingSince;

	private static double startedAt;

	private static int pendingPlayer = -1;

	private static int spawnSerial;

	private static int peakAlive;

	private static int seed;

	private static int pendingRank;

	private static string pendingEquipment = "";

	private static string pendingBase = "";

	private static Il2CppSystem.Collections.Generic.List<int>? playerIds;

	private static bool requestPending;

	private static bool prepared;

	private static bool ended;

	private static string? fault;

	private static PersonalRuns progress = new PersonalRuns(1);

	private static RunProgress remoteProgress = new RunProgress(1);

	private static int remoteAlive;

	private static bool hadLivingPlayer;

	internal static long AmmoModifierTrials;

	internal static int LocalPlayerId => client?.GetLocalPlayerId() ?? (-1);

	internal static RunProgress Progress
	{
		get
		{
			if (authority == null || LocalPlayerId < 0)
			{
				return remoteProgress;
			}
			return progress.Get(LocalPlayerId);
		}
	}

	internal static bool Ended => ended;

	internal static bool ServerWorldBound => authorityWorld != null;

	internal static World? AuthorityWorld => authorityWorld;

	internal static string RunId { get; private set; } = "";

	internal static double Seconds { get; private set; }

	internal static int Rank => Math.Clamp(1 + (int)(Seconds / 90.0), 1, 28);

	internal static int Alive
	{
		get
		{
			if (authority == null)
			{
				return remoteAlive;
			}
			return live.Count;
		}
	}

	internal static bool Active
	{
		get
		{
			if (!ended && fault == null)
			{
				if (authority == null)
				{
					if (client != null)
					{
						return SurvivorCoopTransport.Fresh;
					}
					return false;
				}
				return true;
			}
			return false;
		}
	}

	internal static string? Fault => fault;

	internal static bool HasWorld
	{
		get
		{
			if (authorityWorld == null)
			{
				return clientWorld != null;
			}
			return true;
		}
	}

	internal static RunProgress ForPlayer(int id)
	{
		return progress.Get(id);
	}

	internal static RunProgress Personal(BattleSession s, int id)
	{
		if (authority == null)
		{
			return remoteProgress;
		}
		return progress.Get(id);
	}

	internal static void Receive(PersonalSnapshot s)
	{
		RunId = s.Header.Run;
		Seconds = s.Seconds;
		remoteAlive = s.Alive;
		ended = s.Ended;
		remoteProgress = RunProgress.Import(s.Progress);
		Snapshot(ended ? "ENDED_HOST_CONFIRMED" : "RUNNING_CLIENT_CONFIRMED");
	}

	internal static void Bind(World world, bool server)
	{
		if (server)
		{
			World? world2 = authorityWorld;
			if (!(((world2 != null) ? new IntPtr?(((Il2CppObjectBase)world2).Pointer) : ((IntPtr?)null)) == ((Il2CppObjectBase)world).Pointer))
			{
				World? world3 = clientWorld;
				Reset();
				clientWorld = world3;
				authorityWorld = world;
				seed = Guid.NewGuid().GetHashCode();
				random = new System.Random(seed);
				progress = new PersonalRuns(seed);
				RunId = Guid.NewGuid().ToString("N");
				startedAt = Time.realtimeSinceStartupAsDouble;
				lastUpdate = startedAt;
			}
		}
		else
		{
			clientWorld = world;
		}
	}

	internal static void Unbind(World world)
	{
		World? world2 = authorityWorld;
		if (((world2 != null) ? new IntPtr?(((Il2CppObjectBase)world2).Pointer) : ((IntPtr?)null)) == ((Il2CppObjectBase)world).Pointer)
		{
			Snapshot("ENDED_WORLD_RELEASE");
			Reset();
			SurvivorHud.Clear();
			return;
		}
		World? world3 = clientWorld;
		if (((world3 != null) ? new IntPtr?(((Il2CppObjectBase)world3).Pointer) : ((IntPtr?)null)) == ((Il2CppObjectBase)world).Pointer)
		{
			if (authorityWorld == null)
			{
				Reset();
			}
			else
			{
				clientWorld = null;
				client = null;
			}
			SurvivorHud.Clear();
		}
	}

	private static void Reset()
	{
		authorityWorld = null;
		clientWorld = null;
		authority = null;
		client = null;
		sessions.Dispose();
		recipes.Clear();
		live.Clear();
		Array.Clear(spawnedRanks, 0, 28);
		nextUpdate = (lastUpdate = (nextSpawn = (pendingSince = (startedAt = (Seconds = 0.0)))));
		pendingPlayer = -1;
		pendingRank = 0;
		pendingEquipment = (pendingBase = "");
		playerIds = null;
		spawnSerial = (peakAlive = 0);
		requestPending = (prepared = (ended = false));
		fault = null;
		AmmoModifierTrials = 0L;
		RunId = "";
		progress = new PersonalRuns(1);
		remoteProgress = new RunProgress(1);
		remoteAlive = 0;
		hadLivingPlayer = false;
		SurvivorCoopTransport.Clear();
	}

	internal static bool OwnWorld(World? w)
	{
		if (!ended && fault == null && w != null)
		{
			if (authorityWorld == null || !(((Il2CppObjectBase)w).Pointer == ((Il2CppObjectBase)authorityWorld).Pointer))
			{
				if (clientWorld != null)
				{
					return ((Il2CppObjectBase)w).Pointer == ((Il2CppObjectBase)clientWorld).Pointer;
				}
				return false;
			}
			return true;
		}
		return false;
	}

	internal static bool OwnAuthority(BattleSession? s)
	{
		if (!ended && fault == null && s != null && authority != null && ((Il2CppObjectBase)s).Pointer == ((Il2CppObjectBase)authority).Pointer && s.IsAuthoritative)
		{
			return s.IsBattleSession;
		}
		return false;
	}

	internal static bool IsHuman(BattleSession? s, int id)
	{
		if (!HasWorld || ended || fault != null || s == null || !s.IsBattleSession)
		{
			return false;
		}
		if (authority != null && ((Il2CppObjectBase)s).Pointer == ((Il2CppObjectBase)authority).Pointer)
		{
			if (!s.IsBotPlayer(id))
			{
				return s.GetPlayerAccountId(id).HasValue;
			}
			return false;
		}
		if (client != null && ((Il2CppObjectBase)s).Pointer == ((Il2CppObjectBase)client).Pointer)
		{
			return id == client.GetLocalPlayerId();
		}
		return false;
	}

	private static BattleSession? Session(World? w)
	{
		if (w == null || ((Il2CppObjectBase)w).WasCollected || !w.IsCreated)
		{
			return null;
		}
		CharacterSpawnHandlerSystem existingSystem = w.GetExistingSystem<CharacterSpawnHandlerSystem>();
		if (existingSystem == null || !existingSystem._dependencyInitialized)
		{
			return null;
		}
		return existingSystem._battleSession;
	}

	private static void Prepare()
	{
		if (RankLadder.Data == null)
		{
			throw new InvalidOperationException("Frontier Rank28 data missing");
		}
		SurvivorRanks.RegisterPresentation();
		UniqueId[] botIds = Repositories.BotIds;
		for (int i = 0; i < botIds.Length; i++)
		{
			UniqueId uniqueId = botIds[i];
			BotPresetData item = Repositories.ResolveBot(uniqueId);
			string[] array = SurvivorRanks.Variants(uniqueId.ToString());
			if (array.Length != 28)
			{
				UniqueId uniqueId2 = uniqueId;
				throw new InvalidOperationException("Native enemy missing from existing rank ladder: " + uniqueId2.ToString());
			}
			for (int j = 0; j < 28; j++)
			{
				if (Repositories.ResolveBot(UniqueId.Parse(array[j])).AISettingId == UniqueId.Empty)
				{
					throw new InvalidOperationException("Native ranked AI missing");
				}
				recipes.Add(array[j], (item, j + 1));
			}
		}
		prepared = true;
		Trace.Data["survivorRecipePolicy"] = new
		{
			initial = "WAREHOUSE/Rookie",
			meleeTemplate = "225e6aa0-dc59-4a9c-8846-40434e438fe4",
			firstRangedAfterSeconds = 360,
			meleeOnlyFirstSeconds = 180,
			rankStepSeconds = 90,
			existingNativeRankRecipes = recipes.Count
		};
	}

	internal static void Tick()
	{
		if (!HasWorld || fault != null)
		{
			return;
		}
		double realtimeSinceStartupAsDouble = Time.realtimeSinceStartupAsDouble;
		if (realtimeSinceStartupAsDouble < nextUpdate)
		{
			return;
		}
		nextUpdate = realtimeSinceStartupAsDouble + 0.2;
		try
		{
			if (authority == null)
			{
				BattleSession battleSession = Session(authorityWorld);
				if (battleSession != null && battleSession.IsAuthoritative && battleSession.IsBattleSession)
				{
					authority = battleSession;
					sessions.Keep(battleSession);
				}
			}
			if (client == null)
			{
				BattleSession battleSession2 = Session(clientWorld);
				if (battleSession2 != null && battleSession2.IsBattleSession && !battleSession2.IsAuthoritative)
				{
					client = battleSession2;
					sessions.Keep(battleSession2);
				}
			}
			if (client != null && RankLadder.Active)
			{
				SurvivorRanks.RegisterPresentation();
			}
			SurvivorCoopTransport.Tick(authority, client);
			if (ended || fault != null)
			{
				return;
			}
			if (authority == null)
			{
				lastUpdate = realtimeSinceStartupAsDouble;
				return;
			}
			if (playerIds == null)
			{
				playerIds = sessions.Keep(new Il2CppSystem.Collections.Generic.List<int>());
			}
			playerIds.Clear();
			authority.GetPlayerIds(playerIds);
			if (playerIds.Count > 4096)
			{
				throw new InvalidOperationException("Native player tracking budget exceeded");
			}
			System.Collections.Generic.List<int> list = new System.Collections.Generic.List<int>();
			System.Collections.Generic.List<int> list2 = new System.Collections.Generic.List<int>();
			for (int i = 0; i < playerIds.Count; i++)
			{
				int num = playerIds[i];
				if (IsHuman(authority, num))
				{
					list.Add(num);
					progress.Get(num);
					Entity playerEntity = authority.GetPlayerEntity(num);
					if (authority.GetEntityHasBeenSpawned(num) && !authority.IsPlayerIsDead(num) && playerEntity != Entity.Null && authority.EntityManager.Exists(playerEntity) && authority.GetPlayer(num).Status == BattleSession.Player.Statuses.Normal)
					{
						list2.Add(num);
					}
				}
			}
			int awaitingSpawn = list.Count((int id) => !authority.GetEntityHasBeenSpawned(id) && authority.GetPlayer(id).Status == BattleSession.Player.Statuses.Normal && !authority.IsPlayerIsDead(id));
			if (list2.Count == 0)
			{
				lastUpdate = realtimeSinceStartupAsDouble;
				if (SurvivorPartyPolicy.ShouldEnd(hadLivingPlayer, list2.Count, awaitingSpawn))
				{
					ended = true;
					Snapshot("ENDED_ALL_PLAYERS_UNAVAILABLE");
					SurvivorHud.Clear();
				}
				return;
			}
			hadLivingPlayer = true;
			int playerId = list2[spawnSerial % list2.Count];
			if (!prepared)
			{
				Prepare();
			}
			Seconds += Math.Clamp(realtimeSinceStartupAsDouble - lastUpdate, 0.0, 0.5);
			lastUpdate = realtimeSinceStartupAsDouble;
			EntityManager entityManager = authority.EntityManager;
			int[] array = live.Keys.ToArray();
			foreach (int num3 in array)
			{
				if (!requestPending || num3 != pendingPlayer)
				{
					Entity playerEntity2 = authority.GetPlayerEntity(num3);
					if (authority.IsPlayerIsDead(num3) || playerEntity2 == Entity.Null || !entityManager.Exists(playerEntity2))
					{
						live.Remove(num3);
					}
				}
			}
			if (requestPending)
			{
				if (pendingEquipment.Length > 0)
				{
					if (playerIds == null)
					{
						playerIds = sessions.Keep(new Il2CppSystem.Collections.Generic.List<int>());
					}
					playerIds.Clear();
					authority.GetPlayerIds(playerIds);
					if (playerIds.Count > 4096)
					{
						throw new InvalidOperationException("Native session player count exceeds tracking budget");
					}
					for (int num4 = 0; num4 < playerIds.Count; num4++)
					{
						int num5 = playerIds[num4];
						if (!authority.IsBotPlayer(num5))
						{
							continue;
						}
						Equipment equipment = authority.FindEquipmentByPlayerId(num5);
						if (equipment != null && !(equipment.Id.ToString() != pendingEquipment))
						{
							Entity playerEntity3 = authority.GetPlayerEntity(num5);
							if (authority.GetEntityHasBeenSpawned(num5) && !(playerEntity3 == Entity.Null) && entityManager.Exists(playerEntity3))
							{
								live[num5] = (pendingRank, pendingBase);
								spawnedRanks[pendingRank - 1]++;
								pendingPlayer = num5;
								requestPending = false;
								Trace.Add("survivorSpawnAcknowledged");
								Trace.Data["survivorLastAcknowledged"] = new
								{
									player = num5,
									equipmentId = pendingEquipment,
									rank = RankPolicy.Name(pendingRank),
									entity = playerEntity3.ToString()
								};
								pendingEquipment = "";
								break;
							}
						}
					}
				}
				if (requestPending && realtimeSinceStartupAsDouble - pendingSince > 12.0)
				{
					Fail("Native spawn confirmation timed out; no duplicate retry was issued");
					return;
				}
			}
			Entity playerEntity4 = authority.GetPlayerEntity(playerId);
			if (playerEntity4 == Entity.Null || !entityManager.Exists(playerEntity4))
			{
				return;
			}
			float3 value = entityManager.GetComponentData<Translation>(playerEntity4).Value;
			if (Seconds >= 8.0 && !requestPending && realtimeSinceStartupAsDouble >= nextSpawn)
			{
				AiSpawnPointUpdateSystem existingSystem = authorityWorld.GetExistingSystem<AiSpawnPointUpdateSystem>();
				if (existingSystem != null && existingSystem._dependencyInitialized)
				{
					int num6 = SurvivorPartyPolicy.EnemyBudget(list2.Count, Seconds, existingSystem._maxAliveBotsCount);
					if (live.Count < num6)
					{
						Spawn(existingSystem, new Vector3(value.x, value.y, value.z), playerId, list2, realtimeSinceStartupAsDouble);
					}
				}
				nextSpawn = realtimeSinceStartupAsDouble + Math.Max(0.65, 2.8 - Seconds / 480.0);
			}
			peakAlive = Math.Max(peakAlive, live.Count);
			Snapshot("RUNNING");
		}
		catch (Exception ex)
		{
			Fail(ex.GetBaseException().Message);
			Trace.Error("survivor run", ex);
		}
	}

	private static void Spawn(AiSpawnPointUpdateSystem system, Vector3 player, int playerId, System.Collections.Generic.List<int> living, double now)
	{
		Camera main = Camera.main;
		System.Collections.Generic.List<Anchor> list = new System.Collections.Generic.List<Anchor>();
		Anchor[] anchors = Content.Map.Anchors;
		foreach (Anchor anchor in anchors)
		{
			if (anchor.Kind != "enemy")
			{
				continue;
			}
			Vector3 vector = new Vector3(anchor.Position[0], anchor.Position[1], anchor.Position[2]);
			float num = Vector3.Distance(player, vector);
			if (num < 24f || num > 52f)
			{
				continue;
			}
			bool flag = false;
			foreach (int item in living)
			{
				Entity playerEntity = authority.GetPlayerEntity(item);
				float3 value = authority.EntityManager.GetComponentData<Translation>(playerEntity).Value;
				if (Vector3.Distance(new Vector3(value.x, value.y, value.z), vector) < 24f)
				{
					flag = true;
					break;
				}
			}
			if (flag)
			{
				continue;
			}
			if (main != null)
			{
				Vector3 vector2 = main.WorldToViewportPoint(vector + Vector3.up);
				if (vector2.z > 0f && vector2.x > -0.15f && vector2.x < 1.15f && vector2.y > -0.2f && vector2.y < 1.2f)
				{
					continue;
				}
			}
			if (NavMesh.SamplePositionFilter(vector, out var hit, 1f, 0, 1) && !(Vector3.Distance(hit.position, vector) > 1f))
			{
				list.Add(anchor);
			}
		}
		if (list.Count == 0)
		{
			Trace.Add("survivorNoSafeSpawnPoint");
			return;
		}
		Anchor anchor2 = list[random.Next(list.Count)];
		int num2 = 0;
		if (Seconds >= 180.0 && random.NextDouble() < 0.25)
		{
			num2 = 1;
		}
		if (Seconds >= 360.0 && random.NextDouble() < Math.Min(0.22, (Seconds - 360.0) / 3600.0 + 0.08))
		{
			num2 = 2;
		}
		int rank = Rank;
		string baseId = Repositories.BotIds[num2].ToString();
		string text = SurvivorRanks.Variants(baseId)[rank - 1];
		SpawnBotEventArgs e = new SpawnBotEventArgs
		{
			BotSpawnPointId = Identity.Id(anchor2.Id),
			BotPresetId = UniqueId.Parse(text),
			PlayerId = playerId,
			Tags = new Il2CppSystem.Collections.Generic.List<int>(),
			OverrideAiSettingsId = UniqueId.Empty,
			OverrideIntroBehavior = UniqueId.Empty,
			OverrideSpawnIdleItemTemplateId = UniqueId.Empty,
			IgnoreGuardAreaFromSpawnPoint = true,
			RequireExclusiveRoute = false
		};
		using (new NativeRoot((Il2CppObjectBase)(object)e))
		{
			requestPending = true;
			pendingPlayer = -1;
			pendingSince = now;
			system.OnSpawnBotEventHandler(e);
			spawnSerial++;
			Trace.Data["survivorLastSpawnRequest"] = new
			{
				serial = spawnSerial,
				anchor = anchor2.Id,
				rank = RankPolicy.Name(rank),
				baseId = baseId,
				rankedId = text,
				distance = Vector3.Distance(player, new Vector3(anchor2.Position[0], anchor2.Position[1], anchor2.Position[2]))
			};
		}
	}

	internal static bool TryRecipe(UniqueId ranked, out BotPresetData original, out int rank)
	{
		if (recipes.TryGetValue(ranked.ToString(), out (BotPresetData, int) value))
		{
			(original, rank) = value;
			return true;
		}
		original = null;
		rank = 0;
		return false;
	}

	internal static void EquipmentRequested(int player, int rank, string baseId)
	{
		if (!requestPending)
		{
			throw new InvalidOperationException("Survivor bot created without a director request");
		}
		pendingPlayer = -1;
		pendingRank = rank;
		pendingBase = baseId;
		pendingEquipment = "";
	}

	internal static void EquipmentCreated(int player, Equipment equipment)
	{
		if (requestPending && pendingRank >= 1)
		{
			pendingEquipment = equipment.Id.ToString();
			(int, string) tuple = (pendingRank, pendingBase);
			Trace.Data["survivorLastEquipment"] = new
			{
				recipeContextPlayer = player,
				rank = RankPolicy.Name(tuple.Item1),
				baseId = tuple.Item2,
				equipmentId = equipment.Id.ToString(),
				pipeline = "Original recipe + existing Frontier completed-assembly rank binding"
			};
		}
	}

	internal static void Kill(int killerPlayerId, int experience)
	{
		if (authority != null && IsHuman(authority, killerPlayerId))
		{
			progress.Get(killerPlayerId).Award(experience);
			Trace.Add("survivorNativeKills");
			Snapshot("RUNNING");
			Trace.Save(force: true);
		}
	}

	internal static void Choose(int index)
	{
		if (Active && client != null && !client.IsPlayerIsDead(LocalPlayerId))
		{
			if (authority == null)
			{
				SurvivorCoopTransport.Choose(index, Progress);
			}
			else if (Progress.Choose(index))
			{
				Trace.Add("survivorUpgradesChosen");
				Snapshot("RUNNING");
				Trace.Save(force: true);
			}
		}
	}

	internal static void Fail(string reason)
	{
		fault = reason;
		Snapshot("BLOCKED");
		Trace.Save(force: true);
	}

	private static void Snapshot(string status)
	{
		Trace.Data["survivor"] = new
		{
			status = status,
			runId = RunId,
			seed = seed,
			elapsedSeconds = Math.Round(Seconds, 2),
			rank = RankPolicy.Name(Rank),
			alive = Alive,
			peakAlive = peakAlive,
			requests = spawnSerial,
			pending = requestPending,
			players = Enumerable.Select(progress.Players, (System.Collections.Generic.KeyValuePair<int, RunProgress> p) => new
			{
				playerId = p.Key,
				state = p.Value.Export()
			}).ToArray(),
			localPlayerId = LocalPlayerId,
			level = Progress.Level,
			xp = Progress.Experience,
			xpNeeded = Progress.Required,
			kills = Progress.Kills,
			pendingChoices = Progress.PendingChoices,
			upgrades = Progress.Upgrades.ToArray(),
			offered = Progress.Offer.ToArray(),
			ammoModifierTrials = AmmoModifierTrials,
			spawnedByRank = spawnedRanks.ToArray(),
			fault = fault
		};
	}
}
