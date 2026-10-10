using System;
using System.Collections.Generic;
using System.Linq;
using Core.Utils;
using Game.AISystem;
using Game.ExitZoneSystem;
using Game.ItemsSystem.Components;
using Game.SceneObjectsSystem;
using Game.SpawnPointSystem;
using Game.TriggerZoneSystem;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppSystem.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Frontier.Reserve;

internal sealed class NativePlacements : IDisposable
{
	private readonly World world;

	private readonly NativeBuildBarrier gate;

	private readonly BlobAssetStore blobs;

	private readonly LeaseBag roots = new LeaseBag();

	private readonly System.Collections.Generic.List<Entity> owned = new System.Collections.Generic.List<Entity>();

	private readonly System.Collections.Generic.Dictionary<string, WayPointAuthoring> waypoints = new System.Collections.Generic.Dictionary<string, WayPointAuthoring>();

	private readonly GameObject root;

	private bool disposed;

	internal int Converted => owned.Count;

	internal NativePlacements(World world, NativeBuildBarrier gate, bool server)
	{
		this.world = world;
		this.gate = gate;
		gate.Check();
		blobs = new BlobAssetStore();
		roots.Keep(blobs);
		root = new GameObject("Reserve.Points." + (server ? "Server" : "Client"));
		root.SetActive(value: false);
		SceneManager.MoveGameObjectToScene(root, Loader.Scene);
		HashSet<Entity> hashSet = Entities(world.EntityManager);
		try
		{
			if (server)
			{
				foreach (Anchor item in Content.Map.Anchors.Where((Anchor anchor2) => anchor2.Kind == "patrol"))
				{
					WayPointAuthoring wayPointAuthoring = Create(item).AddComponent<WayPointAuthoring>();
					wayPointAuthoring._viewPoints = new Il2CppSystem.Collections.Generic.List<Transform>();
					wayPointAuthoring._isStopPoint = true;
					wayPointAuthoring._stopDuration = 2f;
					wayPointAuthoring._isSpawnPoint = false;
					wayPointAuthoring._useSpline = false;
					wayPointAuthoring._useAutoTangent = false;
					wayPointAuthoring._localTangent = Vector3.zero;
					waypoints[item.Id] = wayPointAuthoring;
				}
				RoomDef[] rooms = Content.Map.Rooms;
				foreach (RoomDef room in rooms)
				{
					Anchor[] array = Content.Map.Anchors.Where((Anchor anchor2) => anchor2.Kind == "patrol" && anchor2.Room == room.Id).ToArray();
					for (int num2 = 0; num2 < array.Length; num2++)
					{
						WayPointAuthoring wayPointAuthoring2 = waypoints[array[num2].Id];
						wayPointAuthoring2._previousPoint = ((num2 > 0) ? waypoints[array[num2 - 1].Id] : null);
						wayPointAuthoring2._nextPoint = ((num2 + 1 < array.Length) ? waypoints[array[num2 + 1].Id] : null);
					}
				}
			}
			foreach (Anchor a in Content.Map.Anchors.Where((Anchor anchor2) => anchor2.Kind != "patrol" && (server || anchor2.Kind == "exit")))
			{
				GameObject gameObject = Create(a);
				if (a.Kind == "player" || a.Kind == "enemy")
				{
					SpawnPointAuthoring spawnPointAuthoring = gameObject.AddComponent<SpawnPointAuthoring>();
					spawnPointAuthoring._areaName = "Reserve/" + a.Room;
					spawnPointAuthoring._textDescription = a.Id;
					spawnPointAuthoring._index = (uint)Identity.Ordinal(a, Content.Map);
					spawnPointAuthoring._spawnPointId = Identity.Ordinal(a, Content.Map);
					spawnPointAuthoring._objectType = ((!(a.Kind == "player")) ? SpawnPoint.Type.BotPlayer : SpawnPoint.Type.Player);
					spawnPointAuthoring._sceneObjectStringId = Identity.Id(a.Id).ToString();
					spawnPointAuthoring._sceneObjectId = Identity.Id(a.Id);
					spawnPointAuthoring._setMinLevel = false;
					spawnPointAuthoring._setMaxLevel = false;
					spawnPointAuthoring._forDemonstration = false;
					spawnPointAuthoring._forPerformanceTest = false;
					spawnPointAuthoring._allowPatrolWayPointsRandomly = false;
					spawnPointAuthoring._patrolTraversalCount = 1;
					SpawnPointAuthoring.AISpawnPointInfo aISpawnPointInfo = new SpawnPointAuthoring.AISpawnPointInfo
					{
						FirstSpawnDelayInSeconds = 0,
						SpawnPauseInSeconds = 3600,
						KeepBotAlive = false,
						ApplyIdleStateForFirstSpawnOnly = true,
						OverrideAiSettings = false,
						OverrideIntroBehavior = false,
						_spawnHandingItemTemplateId = UniqueId.Empty.ToString(),
						_introBehaviorId = UniqueId.Empty.ToString(),
						_aiSettingsId = UniqueId.Empty.ToString()
					};
					aISpawnPointInfo.OnAfterDeserialize();
					spawnPointAuthoring._spawnPointInfo = aISpawnPointInfo;
					spawnPointAuthoring.OnAfterDeserialize();
					if (spawnPointAuthoring.GetSceneObjectId() != Identity.Id(a.Id))
					{
						throw new InvalidOperationException("Spawn authoring identity mismatch: " + a.Id);
					}
					if (a.Kind == "enemy")
					{
						Anchor anchor = Content.Map.Anchors.FirstOrDefault((Anchor p) => p.Kind == "patrol" && p.Room == a.Room);
						if (anchor != null)
						{
							spawnPointAuthoring._wayPointSource = waypoints[anchor.Id];
						}
					}
				}
				else if (a.Kind == "loot")
				{
					Identify(gameObject, a);
					ItemSpawnPointAuthoring itemSpawnPointAuthoring = gameObject.AddComponent<ItemSpawnPointAuthoring>();
					itemSpawnPointAuthoring._isLegacy = false;
					itemSpawnPointAuthoring._areaName = "Reserve/" + a.Room;
					itemSpawnPointAuthoring._textDescription = a.Id;
					itemSpawnPointAuthoring._spawnPointType = ItemSpawnPointAuthoring.SpawnPointTypes.Container;
					itemSpawnPointAuthoring._visualSize = ItemSpawnPointAuthoring.VisualSizes.Medium;
					itemSpawnPointAuthoring._containerTemplateId = LootProfiles.For(a).Template.ToString();
					itemSpawnPointAuthoring._templateId = LootProfiles.For(a).Template.ToString();
					itemSpawnPointAuthoring._mainItemAutoRespawn = false;
					itemSpawnPointAuthoring.IsInsideGroup = true;
					itemSpawnPointAuthoring._overrideInteractionCollider = true;
					itemSpawnPointAuthoring._overrideInteractionColliderCenter = new Vector3(0f, 0.45f, 0f);
					itemSpawnPointAuthoring._overrideInteractionColliderSize = new Vector3(1.2f, 0.9f, 1.2f);
					if (itemSpawnPointAuthoring.GetSceneObjectId() != Identity.Id(a.Id))
					{
						throw new InvalidOperationException("Loot authoring identity mismatch: " + a.Id);
					}
				}
				else if (a.Kind == "exit")
				{
					Identify(gameObject, a);
					TriggerZoneAuthoring triggerZoneAuthoring = gameObject.AddComponent<TriggerZoneAuthoring>();
					triggerZoneAuthoring._id = Identity.Ordinal(a, Content.Map);
					triggerZoneAuthoring._generateEvents = true;
					triggerZoneAuthoring._boxCenter = new Vector3(0f, 1.4f, 0f);
					triggerZoneAuthoring._boxSize = new Vector3(5f, 2.8f, 5f);
					triggerZoneAuthoring._collidesWith = uint.MaxValue;
					triggerZoneAuthoring._checkStaticWorld = false;
					triggerZoneAuthoring._triggerWithinRadius = false;
					ExitZoneAuthoring exitZoneAuthoring = gameObject.AddComponent<ExitZoneAuthoring>();
					exitZoneAuthoring._templateId = Repositories.ExitTemplate.ToString();
					exitZoneAuthoring._mainExit = true;
					exitZoneAuthoring._activationType = ExitZoneAuthoring.ActivationType.InstantlyActive;
					exitZoneAuthoring._index = Identity.Ordinal(a, Content.Map);
					exitZoneAuthoring._setExitZoneId = true;
					exitZoneAuthoring._exitZoneId = Identity.Ordinal(a, Content.Map);
					exitZoneAuthoring._setMinLevel = false;
					exitZoneAuthoring._setMaxLevel = false;
					exitZoneAuthoring._carriedItemsFilter = null;
					exitZoneAuthoring._equippedItemsFilter = null;
					exitZoneAuthoring._forDemonstration = false;
				}
			}
			root.SetActive(value: true);
			GameObjectConversionSettings gameObjectConversionSettings = GameObjectConversionSettings.FromWorld(world, blobs);
			roots.Keep(gameObjectConversionSettings);
			gameObjectConversionSettings.DebugConversionName = "Frontier Reserve " + (server ? "server" : "client");
			GameObjectConversionUtility.ConvertGameObjectHierarchy(root, gameObjectConversionSettings);
			foreach (Entity item2 in Entities(world.EntityManager))
			{
				if (!hashSet.Contains(item2))
				{
					owned.Add(item2);
				}
			}
			Trace.Data[server ? "serverPlacementValidated" : "clientPlacementValidated"] = false;
			Trace.Data[server ? "serverPlacementConversion" : "clientPlacementConversion"] = new
			{
				entities = owned.Count,
				spawns = Count<SpawnPoint>(),
				enemies = Count<AIPlayerSpawnPoint>(),
				loot = Count<ItemSpawnPoint>(),
				exits = Count<ExitZone>(),
				triggers = Count<TriggerZone>(),
				sourceHash = Identity.Hash
			};
			Validate(server);
			ValidateIdentity(server);
			Trace.Data[server ? "serverPlacementValidated" : "clientPlacementValidated"] = true;
		}
		catch
		{
			foreach (Entity item3 in Entities(world.EntityManager))
			{
				if (!hashSet.Contains(item3) && !owned.Contains(item3))
				{
					owned.Add(item3);
				}
			}
			Dispose();
			throw;
		}
	}

	private GameObject Create(Anchor a)
	{
		GameObject gameObject = new GameObject(a.Id);
		gameObject.transform.SetParent(root.transform, worldPositionStays: false);
		gameObject.transform.localPosition = new Vector3(a.Position[0], a.Position[1], a.Position[2]);
		gameObject.transform.rotation = Quaternion.Euler(0f, a.Yaw, 0f);
		return gameObject;
	}

	private static void Identify(GameObject go, Anchor a)
	{
		SceneObjectAuthoring sceneObjectAuthoring = go.AddComponent<SceneObjectAuthoring>();
		sceneObjectAuthoring._isNetworkObject = true;
		sceneObjectAuthoring._stringId = Identity.Id(a.Id).ToString();
		sceneObjectAuthoring.OnAfterDeserialize();
		sceneObjectAuthoring.SetUniqueId(Identity.Id(a.Id));
		if (sceneObjectAuthoring.Id != Identity.Id(a.Id))
		{
			throw new InvalidOperationException("Scene placement identity mismatch");
		}
	}

	private static HashSet<Entity> Entities(EntityManager em)
	{
		HashSet<Entity> hashSet = new HashSet<Entity>();
		NativeArray<Entity> allEntities = em.GetAllEntities();
		try
		{
			if (allEntities.Length > 200000)
			{
				throw new InvalidOperationException("Unexpected world entity budget");
			}
			for (int i = 0; i < allEntities.Length; i++)
			{
				hashSet.Add(allEntities[i]);
			}
			return hashSet;
		}
		finally
		{
			allEntities.Dispose();
		}
	}

	internal int Count<T>()
	{
		ComponentType type = ComponentType.ReadOnly(TypeManager.GetTypeIndex(Il2CppType.Of<T>()));
		return owned.Count((Entity e) => world.EntityManager.Exists(e) && world.EntityManager.HasComponent(e, type));
	}

	private void Validate(bool server)
	{
		if (Count<ExitZone>() != 2 || Count<TriggerZone>() != 2)
		{
			throw new InvalidOperationException("Native exit/trigger conversion count mismatch");
		}
		if (server && (Count<SpawnPoint>() != 4 || Count<AIPlayerSpawnPoint>() != 18 || Count<ItemSpawnPoint>() != 24))
		{
			throw new InvalidOperationException("Native player/bot/loot conversion count mismatch: spawns=" + Count<SpawnPoint>() + " bots=" + Count<AIPlayerSpawnPoint>() + " loot=" + Count<ItemSpawnPoint>());
		}
		ComponentType disabled = ComponentType.ReadOnly(TypeManager.GetTypeIndex(Il2CppType.Of<Disabled>()));
		if (owned.Any((Entity e) => world.EntityManager.HasComponent(e, disabled)))
		{
			throw new InvalidOperationException("Converted placements are disabled");
		}
	}

	private void ValidateIdentity(bool server)
	{
		EntityManager entityManager = world.EntityManager;
		ComponentType type = ComponentType.ReadOnly(TypeManager.GetTypeIndex(Il2CppType.Of<SpawnPoint>()));
		ComponentType type2 = ComponentType.ReadOnly(TypeManager.GetTypeIndex(Il2CppType.Of<ItemSpawnPoint>()));
		ComponentType type3 = ComponentType.ReadOnly(TypeManager.GetTypeIndex(Il2CppType.Of<ExitZone>()));
		ComponentType type4 = ComponentType.ReadOnly(TypeManager.GetTypeIndex(Il2CppType.Of<AIPlayerSpawnPoint>()));
		HashSet<int> hashSet = new HashSet<int>(from a in Content.Map.Anchors
			where a.Kind == "player"
			select Identity.Ordinal(a, Content.Map));
		HashSet<UniqueId> hashSet2 = new HashSet<UniqueId>(from a in Content.Map.Anchors
			where a.Kind == "enemy"
			select Identity.Id(a.Id));
		HashSet<UniqueId> hashSet3 = new HashSet<UniqueId>(from a in Content.Map.Anchors
			where a.Kind == "loot"
			select Identity.Id(a.Id));
		HashSet<int> hashSet4 = new HashSet<int>(from a in Content.Map.Anchors
			where a.Kind == "exit"
			select Identity.Ordinal(a, Content.Map));
		foreach (Entity item in owned)
		{
			if (server && entityManager.HasComponent(item, type))
			{
				SpawnPoint componentData = entityManager.GetComponentData<SpawnPoint>(item);
				if (componentData.ObjectType != SpawnPoint.Type.Player || !hashSet.Remove(componentData.SpawnPointId))
				{
					throw new InvalidOperationException("Converted player point ID mismatch");
				}
			}
			if (server && entityManager.HasComponent(item, type4) && !hashSet2.Remove(entityManager.GetComponentData<AIPlayerSpawnPoint>(item).BotSpawnPointId))
			{
				throw new InvalidOperationException("Converted AI point ID mismatch");
			}
			if (server && entityManager.HasComponent(item, type2))
			{
				ItemSpawnPoint componentData2 = entityManager.GetComponentData<ItemSpawnPoint>(item);
				if (!hashSet3.Remove(componentData2.ItemSpawnPointId))
				{
					throw new InvalidOperationException("Converted loot point ID mismatch");
				}
			}
			if (entityManager.HasComponent(item, type3))
			{
				ExitZone componentData3 = entityManager.GetComponentData<ExitZone>(item);
				if (!hashSet4.Remove(componentData3.ExitZoneId) || componentData3.TemplateId != Repositories.ExitTemplate)
				{
					throw new InvalidOperationException("Converted exit identity mismatch");
				}
			}
		}
		if (hashSet4.Count > 0 || (server && (hashSet.Count > 0 || hashSet2.Count > 0 || hashSet3.Count > 0)))
		{
			throw new InvalidOperationException("Incomplete native point ID bindings");
		}
	}

	public void Dispose()
	{
		if (disposed)
		{
			return;
		}
		gate.Check();
		if (!((Il2CppObjectBase)world).WasCollected && world.IsCreated)
		{
			EntityManager entityManager = world.EntityManager;
			foreach (Entity item in owned)
			{
				if (entityManager.Exists(item))
				{
					entityManager.DestroyEntity(item);
				}
			}
		}
		owned.Clear();
		if (root != null)
		{
			UnityEngine.Object.Destroy(root);
		}
		blobs.Dispose();
		roots.Dispose();
		disposed = true;
	}
}
