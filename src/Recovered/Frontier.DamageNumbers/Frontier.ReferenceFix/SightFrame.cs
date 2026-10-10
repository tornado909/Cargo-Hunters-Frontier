using System;
using Game.Physics;
using Game.Physics.Components;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;

namespace Frontier.ReferenceFix;

internal sealed class SightFrame
{
	internal readonly Camera Camera;

	internal readonly Vector3 Position;

	internal readonly float Heading;

	private readonly CollisionWorld collision;

	private readonly ComponentDataFromEntity<HitColliderOwner> owners;

	private readonly Entity local;

	private readonly Evidence report;

	private int rays;

	internal const int MaxRays = 64;

	private SightFrame(Camera cam, float heading, CollisionWorld cw, ComponentDataFromEntity<HitColliderOwner> own, Entity me, Evidence e)
	{
		Camera = cam;
		Position = cam.transform.position;
		Heading = heading;
		collision = cw;
		owners = own;
		local = me;
		report = e;
	}

	internal static Camera? ActiveCamera()
	{
		Camera main = UnityEngine.Camera.main;
		if (!(main != null) || !main.isActiveAndEnabled || !main.gameObject.activeInHierarchy)
		{
			return null;
		}
		return main;
	}

	internal static float HeadingOf(Camera cam, float previous)
	{
		Vector3 forward = cam.transform.forward;
		return SightPolicy.CameraYaw(forward.x, forward.z, previous);
	}

	internal static SightFrame? Create(object world, object localEntity, Camera cam, Evidence report)
	{
		try
		{
			object obj = ((world is Il2CppObjectBase) ? world : null);
			World world2 = ((obj != null) ? ((Il2CppObjectBase)obj).TryCast<World>() : null);
			if (world2 == null || !world2.IsCreated)
			{
				return null;
			}
			ComponentSystemBase existingSystem = world2.GetExistingSystem(Il2CppType.Of<BuildPhysicsWorld>());
			BuildPhysicsWorld buildPhysicsWorld = ((existingSystem != null) ? ((Il2CppObjectBase)existingSystem).TryCast<BuildPhysicsWorld>() : null);
			if (buildPhysicsWorld == null)
			{
				report.Data["sightStatus"] = "NO_CLIENT_PHYSICS_WORLD";
				return null;
			}
			JobHandle outputDependency = buildPhysicsWorld.GetOutputDependency();
			if (!outputDependency.IsCompleted)
			{
				report.Add("sightDeferredBusyPhysics", 1L);
				report.Data["sightStatus"] = "CLIENT_PHYSICS_BUSY_HIDE_ENEMIES";
				return null;
			}
			outputDependency.Complete();
			CollisionWorld collisionWorld = buildPhysicsWorld.PhysicsData.PhysicsWorld.CollisionWorld;
			if (collisionWorld == null || collisionWorld.NumBodies == 0 || collisionWorld.NumStaticBodies == 0)
			{
				report.Data["sightStatus"] = "COLLISION_SCENE_NOT_READY";
				return null;
			}
			ComponentDataFromEntity<HitColliderOwner> componentDataFromEntity = world2.EntityManager.GetComponentDataFromEntity<HitColliderOwner>(isReadOnly: true);
			report.Data["sightStatus"] = "CLIENT_COLLISION_WORLD_READY";
			report.Data["sightStaticBodies"] = collisionWorld.NumStaticBodies;
			return new SightFrame(cam, HeadingOf(cam, 0f), collisionWorld, componentDataFromEntity, (Entity)localEntity, report);
		}
		catch (Exception e)
		{
			report.Error("line of sight binding", e);
			return null;
		}
	}

	internal bool Visible(object targetEntity, float x, float y, float z)
	{
		Entity target = (Entity)targetEntity;
		if (One(target, new Vector3(x, y + 1.05f, z)))
		{
			return true;
		}
		return One(target, new Vector3(x, y + 1.65f, z));
	}

	private bool One(Entity target, Vector3 to)
	{
		Vector3 vector = Camera.WorldToViewportPoint(to);
		if (!SightPolicy.InViewport(vector.x, vector.y, vector.z))
		{
			report.Add("outsideCameraSamples", 1L);
			return false;
		}
		if (rays >= 64)
		{
			report.Add("sightBudgetHidden", 1L);
			return false;
		}
		rays++;
		report.Add("lineOfSightRays", 1L);
		HitColliderQuery.HitResult closestHit;
		bool flag = SightPolicy.ClearHit(HitColliderQuery.Query(collision, owners, local, new float3(Position.x, Position.y, Position.z), new float3(to.x, to.y, to.z), out closestHit), closestHit.Entity.Index, closestHit.Entity.Version, closestHit.HitColliderEntity.Index, closestHit.HitColliderEntity.Version, target.Index, target.Version);
		report.Add(flag ? "visibleSamples" : "occludedSamples", 1L);
		return flag;
	}
}
