using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace Frontier.Survivor;

public sealed class NativeCollisionLayer : IDisposable
{
	private readonly World world;

	private readonly NativeBuildBarrier barrier;

	private readonly List<Entity> entities = new List<Entity>();

	private readonly List<BlobAssetReference<Unity.Physics.Collider>> blobs = new List<BlobAssetReference<Unity.Physics.Collider>>();

	private readonly List<NativeRoot> roots = new List<NativeRoot>();

	private bool disposed;

	public int EntityCount => entities.Count;

	public string Fingerprint { get; }

	private NativeCollisionLayer(World w, NativeBuildBarrier b, string hash)
	{
		world = w;
		barrier = b;
		Fingerprint = hash;
	}

	public static NativeCollisionLayer Build(World w, NativeBuildBarrier gate, LocationDefinition definition, Vector3 origin, CollisionFilter filter, Unity.Physics.Material material, PhysicsWorldIndex worldFilter)
	{
		gate.Check();
		definition.Validate();
		if (w == null || ((Il2CppObjectBase)w).WasCollected || !w.IsCreated)
		{
			throw new ArgumentException("Target world is not alive");
		}
		if (filter.IsEmpty)
		{
			throw new ArgumentException("A native, non-empty environment filter is required");
		}
		if (worldFilter.Value != 0)
		{
			throw new NotSupportedException("Only default native physics world index is supported in this bridge");
		}
		int typeIndex = TypeManager.GetTypeIndex(Il2CppType.Of<PhysicsWorldIndex>());
		if (typeIndex == 0)
		{
			throw new InvalidOperationException("Physics world index component absent");
		}
		NativeCollisionLayer nativeCollisionLayer = new NativeCollisionLayer(w, gate, definition.CollisionFingerprint());
		EntityManager entityManager = w.EntityManager;
		try
		{
			foreach (BoxDef item in definition.Boxes.Where((BoxDef b) => b.Solid))
			{
				BlobAssetReference<Unity.Physics.Collider> blobAssetReference = Unity.Physics.BoxCollider.Create(new BoxGeometry
				{
					Center = new float3(0f, 0f, 0f),
					Orientation = new quaternion(0f, 0f, 0f, 1f),
					Size = new float3(item.Size[0], item.Size[1], item.Size[2]),
					BevelRadius = 0f
				}, filter, material);
				if ((object)blobAssetReference == null || !blobAssetReference.IsCreated)
				{
					throw new InvalidOperationException("Native box collider allocation failed: " + item.Id);
				}
				nativeCollisionLayer.blobs.Add(blobAssetReference);
				nativeCollisionLayer.roots.Add(new NativeRoot((Il2CppObjectBase)(object)blobAssetReference));
				Entity entity = entityManager.CreateEntity();
				nativeCollisionLayer.entities.Add(entity);
				entityManager.AddComponent(entity, ComponentType.ReadWrite(typeIndex));
				Translation translation = new Translation
				{
					Value = new float3(item.Center[0] + origin.x, item.Center[1] + origin.y, item.Center[2] + origin.z)
				};
				Rotation rotation = new Rotation
				{
					Value = new quaternion(0f, 0f, 0f, 1f)
				};
				LocalToWorld localToWorld = new LocalToWorld
				{
					Value = new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(translation.Value.x, translation.Value.y, translation.Value.z, 1f))
				};
				PhysicsCollider boxed = new PhysicsCollider
				{
					Value = blobAssetReference
				};
				NativeEcsValues.Write<Translation>(entityManager, entity, translation.BoxIl2CppObject(), 12);
				NativeEcsValues.Write<Rotation>(entityManager, entity, rotation.BoxIl2CppObject(), 16);
				NativeEcsValues.Write<LocalToWorld>(entityManager, entity, localToWorld.BoxIl2CppObject(), 64);
				NativeEcsValues.Write<PhysicsCollider>(entityManager, entity, boxed, IntPtr.Size);
				if (!entityManager.GetComponentData<Translation>(entity).Value.Equals(translation.Value) || !entityManager.GetComponentData<Rotation>(entity).Value.Equals(rotation.Value) || !entityManager.GetComponentData<LocalToWorld>(entity).Value.Equals(localToWorld.Value) || entityManager.GetComponentData<PhysicsCollider>(entity).Value.m_data.m_Ptr != blobAssetReference.m_data.m_Ptr)
				{
					throw new InvalidOperationException("Independent native component readback failed: " + item.Id);
				}
			}
			Trace.Data["collisionBuild:" + w.Name] = new
			{
				entities = nativeCollisionLayer.EntityCount,
				independentReadbacks = nativeCollisionLayer.EntityCount,
				sourceHash = nativeCollisionLayer.Fingerprint
			};
			return nativeCollisionLayer;
		}
		catch
		{
			nativeCollisionLayer.Dispose();
			throw;
		}
	}

	public void Dispose()
	{
		if (disposed)
		{
			return;
		}
		barrier.Check();
		if (!((Il2CppObjectBase)world).WasCollected && world.IsCreated)
		{
			EntityManager entityManager = world.EntityManager;
			foreach (Entity entity in entities)
			{
				if (entityManager.Exists(entity))
				{
					entityManager.DestroyEntity(entity);
				}
			}
		}
		entities.Clear();
		foreach (BlobAssetReference<Unity.Physics.Collider> blob in blobs)
		{
			if (blob.IsCreated)
			{
				blob.Dispose();
			}
		}
		blobs.Clear();
		foreach (NativeRoot root in roots)
		{
			root.Dispose();
		}
		roots.Clear();
		disposed = true;
	}
}
