#nullable enable
using System;
using System.Collections.Generic;
using Game.Network;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
namespace Frontier.Reserve;

// Uses the game's real GhostSpawnerAuthoring and native conversion pipeline.
// Never substitutes an empty singleton or creates hand-written network templates.
internal sealed class NativePrefabLayer : IDisposable
{
    readonly World world;
    readonly NativeBuildBarrier gate;
    readonly LeaseBag roots = new();
    readonly List<Entity> owned = new();
    BlobAssetStore? blobs;
    GameObject? root;
    bool disposed;
    NativePrefabLayer(World world, NativeBuildBarrier gate) { this.world = world; this.gate = gate; }
    internal static NativePrefabLayer Build(World world, NativeBuildBarrier gate, bool server)
    {
        gate.Check();
        var layer = new NativePrefabLayer(world, gate);
        try { layer.Install(server); return layer; }
        catch { layer.Dispose(); throw; }
    }
    void Install(bool server)
    {
        var em = world.EntityManager;
        bool existing = FindCollection(em, out var collection);
        if (!existing)
        {
            var sources = NativePrefabSources.Require();
            var before = Entities(em);
            blobs = new BlobAssetStore(); roots.Keep(blobs);
            root = new GameObject("Frontier.Reserve.NativeGhostCollection." + (server ? "Server" : "Client"));
            root.SetActive(false);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, Loader.Scene);
            root.AddComponent<GhostSpawnerAuthoring>()._ghostPrefabs = sources;
            var settings = GameObjectConversionSettings.FromWorld(world, blobs);
            roots.Keep(settings);
            settings.DebugConversionName = "Frontier Reserve native network prefabs";
            try
            {
                root.SetActive(true);
                GameObjectConversionUtility.ConvertGameObjectHierarchy(root, settings);
            }
            finally
            {
                foreach (var entity in Entities(em)) if (!before.Contains(entity)) owned.Add(entity);
            }
            if (!FindCollection(em, out collection))
                throw new InvalidOperationException("Native ghost authoring did not produce its collection");
        }
        int count = Validate(em, collection);
        Trace.Data[server ? "serverNativePrefabBootstrap" : "clientNativePrefabBootstrap"] = new {
            world = world.Name, reusedNativeCollection = existing, ownedEntities = owned.Count,
            prefabCount = count, collection = collection.ToString(),
            method = "Native GhostSpawnerAuthoring + ConvertGameObjectHierarchy", gameplayAccepted = false };
    }
    static bool FindCollection(EntityManager em, out Entity entity)
    {
        entity = Entity.Null;
        var type = ComponentType.ReadOnly(TypeManager.GetTypeIndex(Il2CppType.Of<GhostSpawner>()));
        var query = em.CreateEntityQuery(new Il2CppStructArray<ComponentType>(new[] { type }));
        try
        {
            int count = query.CalculateEntityCount();
            if (count == 0) return false;
            if (count != 1) throw new InvalidOperationException("Native ghost collection is not unique: " + count);
            entity = query.GetSingletonEntity(); return true;
        }
        finally { query.Dispose(); }
    }
    static int Validate(EntityManager em, Entity collection)
    {
        var buffer = em.GetBuffer<GhostSpawnerBuffer>(collection);
        if (buffer.Length != NativePrefabCatalog.Entries.Length)
            throw new InvalidOperationException("Native ghost prefab count mismatch: " + buffer.Length);
        var prefabTag = ComponentType.ReadOnly(TypeManager.GetTypeIndex(Il2CppType.Of<Prefab>()));
        var networkType = ComponentType.ReadOnly(TypeManager.GetTypeIndex(Il2CppType.Of<NetworkPrefab>()));
        for (int i = 0; i < buffer.Length; i++)
        {
            var entity = buffer[i].Value;
            var expected = NativePrefabCatalog.Entries[i];
            if (entity == Entity.Null || !em.Exists(entity) || !em.HasComponent(entity, prefabTag))
                throw new InvalidOperationException("Missing native ghost prefab entity: " + expected.Name);
            bool hasNetworkId = em.HasComponent(entity, networkType);
            // Validate known explicit IDs. Other native authoring may add components too;
            // absence of NetworkPrefabAuthoring is not proof of an absent ECS component.
            if (expected.NetworkId.HasValue && (!hasNetworkId ||
                em.GetComponentData<NetworkPrefab>(entity).Id != expected.NetworkId.Value))
                throw new InvalidOperationException("Converted native prefab order/ID mismatch: " + expected.Name);
        }
        return buffer.Length;
    }
    static HashSet<Entity> Entities(EntityManager em)
    {
        var found = new HashSet<Entity>();
        var values = em.GetAllEntities(Allocator.Temp);
        try
        {
            if (values.Length > 200000) throw new InvalidOperationException("Native entity budget exceeded");
            for (int i = 0; i < values.Length; i++) found.Add(values[i]);
            return found;
        }
        finally { values.Dispose(); }
    }
    public void Dispose()
    {
        if (disposed) return;
        gate.Check();
        if (!world.WasCollected && world.IsCreated)
        {
            var em = world.EntityManager;
            foreach (var entity in owned) if (em.Exists(entity)) em.DestroyEntity(entity);
        }
        owned.Clear();
        if (root != null) UnityEngine.Object.Destroy(root);
        root = null;
        blobs?.Dispose(); blobs = null;
        roots.Dispose(); disposed = true;
    }
}
