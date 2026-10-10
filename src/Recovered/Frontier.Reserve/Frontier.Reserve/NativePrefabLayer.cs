using System;
using System.Collections.Generic;
using Game.Network;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Frontier.Reserve;

internal sealed class NativePrefabLayer : IDisposable
{
	private readonly World world;

	private readonly NativeBuildBarrier gate;

	private readonly LeaseBag roots = new LeaseBag();

	private readonly List<Entity> owned = new List<Entity>();

	private BlobAssetStore? blobs;

	private GameObject? root;

	private bool disposed;

	private NativePrefabLayer(World world, NativeBuildBarrier gate)
	{
		this.world = world;
		this.gate = gate;
	}

	internal static NativePrefabLayer Build(World world, NativeBuildBarrier gate, bool server)
	{
		gate.Check();
		NativePrefabLayer nativePrefabLayer = new NativePrefabLayer(world, gate);
		try
		{
			nativePrefabLayer.Install(server);
			return nativePrefabLayer;
		}
		catch
		{
			nativePrefabLayer.Dispose();
			throw;
		}
	}

	private void Install(bool server)
	{
		EntityManager entityManager = world.EntityManager;
		Entity entity;
		bool flag = FindCollection(entityManager, out entity);
		if (!flag)
		{
			Il2CppReferenceArray<GameObject> ghostPrefabs = NativePrefabSources.Require();
			HashSet<Entity> hashSet = Entities(entityManager);
			blobs = new BlobAssetStore();
			roots.Keep(blobs);
			root = new GameObject("Frontier.Reserve.NativeGhostCollection." + (server ? "Server" : "Client"));
			root.SetActive(value: false);
			SceneManager.MoveGameObjectToScene(root, Loader.Scene);
			root.AddComponent<GhostSpawnerAuthoring>()._ghostPrefabs = ghostPrefabs;
			GameObjectConversionSettings gameObjectConversionSettings = GameObjectConversionSettings.FromWorld(world, blobs);
			roots.Keep(gameObjectConversionSettings);
			gameObjectConversionSettings.DebugConversionName = "Frontier Reserve native network prefabs";
			try
			{
				root.SetActive(value: true);
				GameObjectConversionUtility.ConvertGameObjectHierarchy(root, gameObjectConversionSettings);
			}
			finally
			{
				foreach (Entity item in Entities(entityManager))
				{
					if (!hashSet.Contains(item))
					{
						owned.Add(item);
					}
				}
			}
			if (!FindCollection(entityManager, out entity))
			{
				throw new InvalidOperationException("Native ghost authoring did not produce its collection");
			}
		}
		int prefabCount = Validate(entityManager, entity);
		Trace.Data[server ? "serverNativePrefabBootstrap" : "clientNativePrefabBootstrap"] = new
		{
			world = world.Name,
			reusedNativeCollection = flag,
			ownedEntities = owned.Count,
			prefabCount = prefabCount,
			collection = entity.ToString(),
			method = "Native GhostSpawnerAuthoring + ConvertGameObjectHierarchy",
			gameplayAccepted = false
		};
	}

	private static bool FindCollection(EntityManager em, out Entity entity)
	{
		entity = Entity.Null;
		ComponentType componentType = ComponentType.ReadOnly(TypeManager.GetTypeIndex(Il2CppType.Of<GhostSpawner>()));
		EntityQuery entityQuery = em.CreateEntityQuery(new Il2CppStructArray<ComponentType>(new ComponentType[1] { componentType }));
		try
		{
			int num = entityQuery.CalculateEntityCount();
			switch (num)
			{
			case 0:
				return false;
			default:
				throw new InvalidOperationException("Native ghost collection is not unique: " + num);
			case 1:
				entity = entityQuery.GetSingletonEntity();
				return true;
			}
		}
		finally
		{
			entityQuery.Dispose();
		}
	}

	private static int Validate(EntityManager em, Entity collection)
	{
		DynamicBuffer<GhostSpawnerBuffer> buffer = em.GetBuffer<GhostSpawnerBuffer>(collection);
		if (buffer.Length != NativePrefabCatalog.Entries.Length)
		{
			throw new InvalidOperationException("Native ghost prefab count mismatch: " + buffer.Length);
		}
		ComponentType type = ComponentType.ReadOnly(TypeManager.GetTypeIndex(Il2CppType.Of<Prefab>()));
		ComponentType type2 = ComponentType.ReadOnly(TypeManager.GetTypeIndex(Il2CppType.Of<NetworkPrefab>()));
		for (int i = 0; i < buffer.Length; i++)
		{
			Entity value = buffer[i].Value;
			NativePrefabCatalog.Entry entry = NativePrefabCatalog.Entries[i];
			if (value == Entity.Null || !em.Exists(value) || !em.HasComponent(value, type))
			{
				throw new InvalidOperationException("Missing native ghost prefab entity: " + entry.Name);
			}
			bool flag = em.HasComponent(value, type2);
			if (entry.NetworkId.HasValue && (!flag || em.GetComponentData<NetworkPrefab>(value).Id != entry.NetworkId.Value))
			{
				throw new InvalidOperationException("Converted native prefab order/ID mismatch: " + entry.Name);
			}
		}
		return buffer.Length;
	}

	private static HashSet<Entity> Entities(EntityManager em)
	{
		HashSet<Entity> hashSet = new HashSet<Entity>();
		NativeArray<Entity> allEntities = em.GetAllEntities();
		try
		{
			if (allEntities.Length > 200000)
			{
				throw new InvalidOperationException("Native entity budget exceeded");
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
		root = null;
		blobs?.Dispose();
		blobs = null;
		roots.Dispose();
		disposed = true;
	}
}
