#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Game.Network;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Unity.NetCode;
using UnityEngine;
namespace Frontier.Survivor;
// One bounded dependency cache; capture only at a real native authoring boundary.
// No global scene/resource scans, periodic discovery, or native prefab edits.
internal static class NativePrefabSources
{
    static Il2CppReferenceArray<GameObject>? prefabs;
    static GameObject? holder;
    static NativeRoot? holderRoot;
    static int mainThread;
    internal static bool Ready => prefabs != null && holder != null;
    internal static void Initialize() => mainThread = Thread.CurrentThread.ManagedThreadId;
    static void CheckThread()
    {
        if (Thread.CurrentThread.ManagedThreadId != mainThread)
            throw new InvalidOperationException("Native prefab cache requires the Unity main thread");
    }
    internal static void Capture(GhostSpawnerAuthoring source)
    {
        CheckThread();
        if (source == null || source.WasCollected) return;
        var input = source._ghostPrefabs;
        if (input == null || input.Length != NativePrefabCatalog.Entries.Length) return;
        if (Ready && Enumerable.Range(0, input.Length).All(i => input[i] != null &&
            prefabs![i] != null && input[i].Pointer == prefabs[i].Pointer)) return;
        var observed = new List<object>();
        for (int i = 0; i < input.Length; i++)
        {
            Validate(input[i], NativePrefabCatalog.Entries[i]);
            observed.Add(new { index = i, name = input[i].name,
                ghostId = input[i].GetComponent<GhostAuthoringComponent>().prefabId,
                networkId = NativePrefabCatalog.Entries[i].NetworkId });
        }
        var copy = new Il2CppReferenceArray<GameObject>(input.Length);
        for (int i = 0; i < input.Length; i++) copy[i] = input[i];
        GameObject? next = null;
        NativeRoot? nextRoot = null;
        try
        {
            next = new GameObject("Frontier.Survivor.NativePrefabLease");
            next.SetActive(false);
            next.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(next);
            next.AddComponent<GhostSpawnerAuthoring>()._ghostPrefabs = copy;
            nextRoot = new NativeRoot(next);
            Clear();
            holder = next; holderRoot = nextRoot; prefabs = copy;
        }
        catch
        {
            nextRoot?.Dispose();
            if (next != null) UnityEngine.Object.Destroy(next);
            throw;
        }
        Trace.Add("nativePrefabSourceCaptures");
        Trace.Data["nativePrefabSources"] = new { ready = true, count = copy.Length,
            sourceScene = source.gameObject.scene.name, observed,
            policy = "Native DeclareReferencedPrefabs; one bounded source cache",
            shippedAssetSha256 = NativePrefabCatalog.AssetSha256 };
        Trace.Save(true);
    }
    static void Validate(GameObject value, NativePrefabCatalog.Entry expected)
    {
        if (value == null || value.WasCollected || value.name != expected.Name)
            throw new InvalidOperationException("Native prefab identity mismatch: " + expected.Name);
        var ghost = value.GetComponent<GhostAuthoringComponent>();
        if (ghost == null || !string.Equals(ghost.prefabId, expected.GhostId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Native ghost GUID mismatch: " + expected.Name);
        var network = value.GetComponent<NetworkPrefabAuthoring>();
        if ((expected.NetworkId.HasValue && (network == null || network._id != expected.NetworkId.Value)) ||
            (!expected.NetworkId.HasValue && network != null))
            throw new InvalidOperationException("Native network prefab ID mismatch: " + expected.Name);
        var types = new HashSet<string>(value.GetComponents<Component>()
            .Where(c => c != null).Select(c => c.GetIl2CppType().FullName), StringComparer.Ordinal);
        if (expected.Components.Any(name => !types.Contains(name)))
            throw new InvalidOperationException("Native prefab authoring is incomplete: " + expected.Name);
    }
    internal static Il2CppReferenceArray<GameObject> Require()
    {
        CheckThread();
        if (!Ready || prefabs!.WasCollected || prefabs.Length != NativePrefabCatalog.Entries.Length)
            throw new InvalidOperationException("Native network prefabs were not captured from a loaded game scene");
        for (int i = 0; i < prefabs.Length; i++) Validate(prefabs[i], NativePrefabCatalog.Entries[i]);
        return prefabs;
    }
    internal static void Clear()
    {
        CheckThread();
        if (holder != null) UnityEngine.Object.Destroy(holder);
        holderRoot?.Dispose();
        holder = null; holderRoot = null; prefabs = null;
    }
}
