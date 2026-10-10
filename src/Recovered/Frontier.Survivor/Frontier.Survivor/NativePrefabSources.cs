using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Game.Network;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Unity.NetCode;
using UnityEngine;

namespace Frontier.Survivor;

internal static class NativePrefabSources
{
	private static Il2CppReferenceArray<GameObject>? prefabs;

	private static GameObject? holder;

	private static NativeRoot? holderRoot;

	private static int mainThread;

	internal static bool Ready
	{
		get
		{
			if (prefabs != null)
			{
				return holder != null;
			}
			return false;
		}
	}

	internal static void Initialize()
	{
		mainThread = Thread.CurrentThread.ManagedThreadId;
	}

	private static void CheckThread()
	{
		if (Thread.CurrentThread.ManagedThreadId != mainThread)
		{
			throw new InvalidOperationException("Native prefab cache requires the Unity main thread");
		}
	}

	internal static void Capture(GhostSpawnerAuthoring source)
	{
		CheckThread();
		if (source == null || ((Il2CppObjectBase)source).WasCollected)
		{
			return;
		}
		Il2CppReferenceArray<GameObject> input = source._ghostPrefabs;
		if (input == null || ((Il2CppArrayBase<GameObject>)(object)input).Length != NativePrefabCatalog.Entries.Length || (Ready && Enumerable.Range(0, ((Il2CppArrayBase<GameObject>)(object)input).Length).All((int i) => ((Il2CppArrayBase<GameObject>)(object)input)[i] != null && ((Il2CppArrayBase<GameObject>)(object)prefabs)[i] != null && ((Il2CppObjectBase)((Il2CppArrayBase<GameObject>)(object)input)[i]).Pointer == ((Il2CppObjectBase)((Il2CppArrayBase<GameObject>)(object)prefabs)[i]).Pointer)))
		{
			return;
		}
		List<object> list = new List<object>();
		for (int num = 0; num < ((Il2CppArrayBase<GameObject>)(object)input).Length; num++)
		{
			Validate(((Il2CppArrayBase<GameObject>)(object)input)[num], NativePrefabCatalog.Entries[num]);
			list.Add(new
			{
				index = num,
				name = ((Il2CppArrayBase<GameObject>)(object)input)[num].name,
				ghostId = ((Il2CppArrayBase<GameObject>)(object)input)[num].GetComponent<GhostAuthoringComponent>().prefabId,
				networkId = NativePrefabCatalog.Entries[num].NetworkId
			});
		}
		Il2CppReferenceArray<GameObject> val = new Il2CppReferenceArray<GameObject>((long)((Il2CppArrayBase<GameObject>)(object)input).Length);
		for (int num2 = 0; num2 < ((Il2CppArrayBase<GameObject>)(object)input).Length; num2++)
		{
			((Il2CppArrayBase<GameObject>)(object)val)[num2] = ((Il2CppArrayBase<GameObject>)(object)input)[num2];
		}
		GameObject gameObject = null;
		NativeRoot nativeRoot = null;
		try
		{
			gameObject = new GameObject("Frontier.Survivor.NativePrefabLease");
			gameObject.SetActive(value: false);
			gameObject.hideFlags = HideFlags.HideAndDontSave;
			UnityEngine.Object.DontDestroyOnLoad(gameObject);
			gameObject.AddComponent<GhostSpawnerAuthoring>()._ghostPrefabs = val;
			nativeRoot = new NativeRoot((Il2CppObjectBase)(object)gameObject);
			Clear();
			holder = gameObject;
			holderRoot = nativeRoot;
			prefabs = val;
		}
		catch
		{
			nativeRoot?.Dispose();
			if (gameObject != null)
			{
				UnityEngine.Object.Destroy(gameObject);
			}
			throw;
		}
		Trace.Add("nativePrefabSourceCaptures");
		Trace.Data["nativePrefabSources"] = new
		{
			ready = true,
			count = ((Il2CppArrayBase<GameObject>)(object)val).Length,
			sourceScene = source.gameObject.scene.name,
			observed = list,
			policy = "Native DeclareReferencedPrefabs; one bounded source cache",
			shippedAssetSha256 = "6ddfeb37285ad613b78f4502fc90c32b6afe680a66b30b481cd721acc434b2b8"
		};
		Trace.Save(force: true);
	}

	private static void Validate(GameObject value, NativePrefabCatalog.Entry expected)
	{
		if (value == null || ((Il2CppObjectBase)value).WasCollected || value.name != expected.Name)
		{
			throw new InvalidOperationException("Native prefab identity mismatch: " + expected.Name);
		}
		GhostAuthoringComponent component = value.GetComponent<GhostAuthoringComponent>();
		if (component == null || !string.Equals(component.prefabId, expected.GhostId, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("Native ghost GUID mismatch: " + expected.Name);
		}
		NetworkPrefabAuthoring component2 = value.GetComponent<NetworkPrefabAuthoring>();
		if ((expected.NetworkId.HasValue && (component2 == null || component2._id != expected.NetworkId.Value)) || (!expected.NetworkId.HasValue && component2 != null))
		{
			throw new InvalidOperationException("Native network prefab ID mismatch: " + expected.Name);
		}
		HashSet<string> types = new HashSet<string>(from c in (IEnumerable<Component>)value.GetComponents<Component>()
			where c != null
			select c.GetIl2CppType().FullName, StringComparer.Ordinal);
		if (expected.Components.Any((string name) => !types.Contains(name)))
		{
			throw new InvalidOperationException("Native prefab authoring is incomplete: " + expected.Name);
		}
	}

	internal static Il2CppReferenceArray<GameObject> Require()
	{
		CheckThread();
		if (!Ready || ((Il2CppObjectBase)prefabs).WasCollected || ((Il2CppArrayBase<GameObject>)(object)prefabs).Length != NativePrefabCatalog.Entries.Length)
		{
			throw new InvalidOperationException("Native network prefabs were not captured from a loaded game scene");
		}
		for (int i = 0; i < ((Il2CppArrayBase<GameObject>)(object)prefabs).Length; i++)
		{
			Validate(((Il2CppArrayBase<GameObject>)(object)prefabs)[i], NativePrefabCatalog.Entries[i]);
		}
		return prefabs;
	}

	internal static void Clear()
	{
		CheckThread();
		if (holder != null)
		{
			UnityEngine.Object.Destroy(holder);
		}
		holderRoot?.Dispose();
		holder = null;
		holderRoot = null;
		prefabs = null;
	}
}
