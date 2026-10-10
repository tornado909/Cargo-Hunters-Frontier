using System;
using System.Collections.Generic;
using System.Linq;
using Game.Application.Client;
using Game.Application.Server;
using Game.SceneObjectsSystem.Systems;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Unity.Entities;
using Unity.Physics;
using UnityEngine;

namespace Frontier.Reserve;

internal static class WorldBridge
{
	private sealed class Binding
	{
		internal IntPtr Owner;

		internal bool Server;

		internal World World;

		internal NativeRoot Root;

		internal NativeCollisionLayer? Collision;

		internal NativePlacements? Placements;

		internal NativePrefabLayer? Prefabs;

		internal bool Ready;

		internal double Started;

		internal bool Failed;

		internal double NextCheck;

		internal string Waiting = "";
	}

	private static readonly Dictionary<IntPtr, Binding> bindings = new Dictionary<IntPtr, Binding>();

	private static readonly HashSet<IntPtr> rejected = new HashSet<IntPtr>();

	internal static int ActiveCount => bindings.Count;

	internal static void Capture(ClientBattleState state)
	{
		Capture(((Il2CppObjectBase)state).Pointer, state._world, server: false);
	}

	internal static void Capture(ServerBattleState state)
	{
		Capture(((Il2CppObjectBase)state).Pointer, state._world, server: true);
	}

	private static void Capture(IntPtr owner, World? world, bool server)
	{
		if (owner == IntPtr.Zero || world == null || ((Il2CppObjectBase)world).WasCollected || !world.IsCreated)
		{
			return;
		}
		if (bindings.TryGetValue(owner, out Binding value))
		{
			if (((Il2CppObjectBase)value.World).Pointer == ((Il2CppObjectBase)world).Pointer)
			{
				return;
			}
			Release(owner);
		}
		bindings.Add(owner, new Binding
		{
			Owner = owner,
			World = world,
			Root = new NativeRoot((Il2CppObjectBase)(object)world),
			Server = server,
			Started = Time.realtimeSinceStartupAsDouble
		});
		Trace.Add(server ? "serverWorldCaptures" : "clientWorldCaptures");
	}

	internal static bool Ready(ClientBattleState s)
	{
		if (bindings.TryGetValue(((Il2CppObjectBase)s).Pointer, out Binding value))
		{
			return value.Ready;
		}
		return false;
	}

	internal static bool Ready(ServerBattleState s)
	{
		if (bindings.TryGetValue(((Il2CppObjectBase)s).Pointer, out Binding value))
		{
			return value.Ready;
		}
		return false;
	}

	internal static void Bind(ClientBattleState state)
	{
		Capture(state);
		Install(((Il2CppObjectBase)state).Pointer);
	}

	internal static void Bind(ServerBattleState state)
	{
		Capture(state);
		Install(((Il2CppObjectBase)state).Pointer);
	}

	private static void Install(IntPtr owner)
	{
		if (!bindings.TryGetValue(owner, out Binding b) || b.Ready || b.Failed || !Loader.Ready || !Repositories.Ready || Time.realtimeSinceStartupAsDouble < b.NextCheck)
		{
			return;
		}
		b.NextCheck = Time.realtimeSinceStartupAsDouble + 0.1;
		try
		{
			b.World.EntityManager.CompleteAllJobs();
			NativeBuildBarrier gate = new NativeBuildBarrier(delegate
			{
				if (!((Il2CppObjectBase)b.World).WasCollected && b.World.IsCreated)
				{
					b.World.EntityManager.CompleteAllJobs();
				}
			});
			Binding binding = b;
			if (binding.Prefabs == null)
			{
				binding.Prefabs = NativePrefabLayer.Build(b.World, gate, b.Server);
			}
			if (!b.Server || NativeServerPrefabsReady(b))
			{
				CollisionFilter filter = CollisionFilter.Default;
				Unity.Physics.Material material = Unity.Physics.Material.Default;
				b.Collision = NativeCollisionLayer.Build(b.World, gate, Content.Map, Vector3.zero, filter, material, new PhysicsWorldIndex
				{
					Value = 0u
				});
				b.Placements = new NativePlacements(b.World, gate, b.Server);
				if (b.Collision.Fingerprint != Identity.Hash)
				{
					throw new InvalidOperationException("World collision fingerprint mismatch");
				}
				b.Ready = true;
				Trace.Data[b.Server ? "serverWorldReady" : "clientWorldReady"] = new
				{
					name = b.World.Name,
					collisionCount = b.Collision.EntityCount,
					placementCount = b.Placements.Converted,
					geometryHash = Identity.Hash
				};
				Trace.Data["status"] = "NATIVE_WORLD_CONTENT_INSTALLED";
				Trace.Save(force: true);
			}
		}
		catch (Exception e)
		{
			b.Failed = true;
			rejected.Add(owner);
			try
			{
				b.Placements?.Dispose();
				b.Placements = null;
				b.Collision?.Dispose();
				b.Collision = null;
				b.Prefabs?.Dispose();
				b.Prefabs = null;
			}
			catch (Exception e2)
			{
				Trace.Error("world cleanup", e2);
			}
			Trace.Error(b.Server ? "server content" : "client content", e);
			Loader.Fail(e);
		}
	}

	private static bool NativeServerPrefabsReady(Binding binding)
	{
		ComponentSystemBase existingSystem = binding.World.GetExistingSystem(Il2CppType.Of<SceneObjectInitializationServerSystem>());
		SceneObjectInitializationServerSystem sceneObjectInitializationServerSystem = ((existingSystem != null) ? ((Il2CppObjectBase)existingSystem).TryCast<SceneObjectInitializationServerSystem>() : null);
		int num = -1;
		string text;
		if (sceneObjectInitializationServerSystem == null || ((Il2CppObjectBase)sceneObjectInitializationServerSystem).WasCollected)
		{
			text = "WAITING_FOR_SCENE_OBJECT_INITIALIZER";
		}
		else if (sceneObjectInitializationServerSystem._ghostPrefabCollectionQuery.__impl == IntPtr.Zero)
		{
			text = "WAITING_FOR_NATIVE_PREFAB_QUERY";
		}
		else
		{
			num = sceneObjectInitializationServerSystem._ghostPrefabCollectionQuery.CalculateEntityCount();
			if (num > 1)
			{
				throw new InvalidOperationException("Native network prefab collection is not a singleton: " + num);
			}
			if (num == 0)
			{
				text = "WAITING_FOR_NATIVE_NETWORK_PREFABS";
			}
			else
			{
				Entity ghostSceneObjectPrefab = sceneObjectInitializationServerSystem.GetGhostSceneObjectPrefab();
				if (!(ghostSceneObjectPrefab == Entity.Null) && binding.World.EntityManager.Exists(ghostSceneObjectPrefab))
				{
					Trace.Data["serverNativePrefabDependency"] = new
					{
						world = binding.World.Name,
						collectionCount = num,
						prefab = ghostSceneObjectPrefab.ToString(),
						ready = true,
						elapsedSeconds = Time.realtimeSinceStartupAsDouble - binding.Started
					};
					binding.Waiting = "";
					return true;
				}
				text = "WAITING_FOR_NATIVE_SCENE_OBJECT_PREFAB";
			}
		}
		if (binding.Waiting != text)
		{
			binding.Waiting = text;
			Trace.Data["serverNativePrefabDependency"] = new
			{
				world = binding.World.Name,
				collectionCount = num,
				ready = false,
				reason = text
			};
			Trace.Data["status"] = text;
			Trace.Save(force: true);
		}
		return false;
	}

	internal static void Tick()
	{
		if (bindings.Count == 0)
		{
			return;
		}
		Binding[] array = bindings.Values.ToArray();
		foreach (Binding binding in array)
		{
			if (((Il2CppObjectBase)binding.World).WasCollected || !binding.World.IsCreated)
			{
				Release(binding.Owner);
			}
			else if (!binding.Ready && !binding.Failed)
			{
				Install(binding.Owner);
				if (Time.realtimeSinceStartupAsDouble - binding.Started > 90.0)
				{
					binding.Failed = true;
					Loader.Fail(new TimeoutException("Reserve world did not become ready"));
				}
			}
		}
	}

	internal static void Release(ClientBattleState s)
	{
		Release(((Il2CppObjectBase)s).Pointer);
	}

	internal static void Release(ServerBattleState s)
	{
		Release(((Il2CppObjectBase)s).Pointer);
	}

	private static void Release(IntPtr owner)
	{
		rejected.Remove(owner);
		if (!bindings.Remove(owner, out Binding value))
		{
			return;
		}
		try
		{
			if (!((Il2CppObjectBase)value.World).WasCollected && value.World.IsCreated)
			{
				value.World.EntityManager.CompleteAllJobs();
			}
			value.Placements?.Dispose();
			value.Collision?.Dispose();
			value.Prefabs?.Dispose();
		}
		catch (Exception e)
		{
			Trace.Error("world teardown", e);
		}
		finally
		{
			value.Root.Dispose();
			Trace.Add("worldsReleased");
		}
		if (bindings.Count == 0)
		{
			Loader.ScheduleRelease();
		}
	}
}
