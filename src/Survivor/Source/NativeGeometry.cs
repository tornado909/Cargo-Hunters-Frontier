#nullable enable
using System;using System.Linq;using System.Threading;using System.Runtime.InteropServices;using System.Collections.Generic;
using Il2CppInterop.Runtime;using Il2CppInterop.Runtime.InteropTypes;using Unity.Entities;using Unity.Mathematics;using Unity.Transforms;using UnityEngine;
using PhysicsBox=Unity.Physics.BoxCollider;
namespace Frontier.Survivor;
// Own-scene builders only. The world installer calls them at a quiescent boundary;
// existing game entities and persistent inventory are never edited here.
public sealed class NativeBuildBarrier {
 readonly int thread; readonly Action assertQuiescent;
 public NativeBuildBarrier(Action assertQuiescent){this.assertQuiescent=assertQuiescent??throw new ArgumentNullException(nameof(assertQuiescent));thread=Thread.CurrentThread.ManagedThreadId;Check();}
 public void Check(){if(Thread.CurrentThread.ManagedThreadId!=thread)throw new InvalidOperationException("World geometry must be built/disposed on its owning main thread");assertQuiescent();}
}
internal sealed class NativeRoot:IDisposable {
 IntPtr handle;public NativeRoot(Il2CppObjectBase obj){if(obj is null||obj.WasCollected)throw new ArgumentException("Native object unavailable");handle=IL2CPP.il2cpp_gchandle_new(obj.Pointer,false);if(handle==IntPtr.Zero)throw new InvalidOperationException("Native root allocation failed");}
 public void Dispose(){if(handle!=IntPtr.Zero){IL2CPP.il2cpp_gchandle_free(handle);handle=IntPtr.Zero;}}
}
public sealed class NativeCollisionLayer:IDisposable {
 readonly World world;readonly NativeBuildBarrier barrier;readonly List<Entity> entities=new();
 readonly List<BlobAssetReference<Unity.Physics.Collider>> blobs=new();readonly List<NativeRoot> roots=new();bool disposed;
 public int EntityCount=>entities.Count;public string Fingerprint{get;}
 NativeCollisionLayer(World w,NativeBuildBarrier b,string hash){world=w;barrier=b;Fingerprint=hash;}
 public static NativeCollisionLayer Build(World w,NativeBuildBarrier gate,LocationDefinition definition,Vector3 origin,Unity.Physics.CollisionFilter filter,Unity.Physics.Material material,Unity.Physics.PhysicsWorldIndex worldFilter){
  gate.Check();definition.Validate();if(w==null||w.WasCollected||!w.IsCreated)throw new ArgumentException("Target world is not alive");if(filter.IsEmpty)throw new ArgumentException("A native, non-empty environment filter is required");
  if(worldFilter.Value!=0)throw new NotSupportedException("Only default native physics world index is supported in this bridge");
  int physicsWorldIndex=TypeManager.GetTypeIndex(Il2CppType.Of<Unity.Physics.PhysicsWorldIndex>());if(physicsWorldIndex==0)throw new InvalidOperationException("Physics world index component absent");
  var layer=new NativeCollisionLayer(w,gate,definition.CollisionFingerprint());var em=w.EntityManager;
  try {foreach(var b in definition.Boxes.Where(b=>b.Solid)){
   var geometry=new Unity.Physics.BoxGeometry{Center=new float3(0,0,0),Orientation=new quaternion(0,0,0,1),Size=new float3(b.Size[0],b.Size[1],b.Size[2]),BevelRadius=0};
   var blob=PhysicsBox.Create(geometry,filter,material);if(blob is null||!blob.IsCreated)throw new InvalidOperationException("Native box collider allocation failed: "+b.Id);
   layer.blobs.Add(blob);layer.roots.Add(new NativeRoot(blob));
   Entity e=em.CreateEntity();layer.entities.Add(e);
   // Shared component default is zero. No guessed generic shared-component marshalling.
   em.AddComponent(e,ComponentType.ReadWrite(physicsWorldIndex));
   var t=new Translation{Value=new float3(b.Center[0]+origin.x,b.Center[1]+origin.y,b.Center[2]+origin.z)};
   var r=new Rotation{Value=new quaternion(0,0,0,1)};
   var m=new LocalToWorld{Value=new float4x4(new float4(1,0,0,0),new float4(0,1,0,0),new float4(0,0,1,0),new float4(t.Value.x,t.Value.y,t.Value.z,1))};
   var pc=new Unity.Physics.PhysicsCollider{Value=blob};
   NativeEcsValues.Write<Translation>(em,e,t.BoxIl2CppObject(),12);
   NativeEcsValues.Write<Rotation>(em,e,r.BoxIl2CppObject(),16);
   NativeEcsValues.Write<LocalToWorld>(em,e,m.BoxIl2CppObject(),64);
   NativeEcsValues.Write<Unity.Physics.PhysicsCollider>(em,e,pc,IntPtr.Size);
   if(!em.GetComponentData<Translation>(e).Value.Equals(t.Value)||!em.GetComponentData<Rotation>(e).Value.Equals(r.Value)||!em.GetComponentData<LocalToWorld>(e).Value.Equals(m.Value)||em.GetComponentData<Unity.Physics.PhysicsCollider>(e).Value.m_data.m_Ptr!=blob.m_data.m_Ptr)throw new InvalidOperationException("Independent native component readback failed: "+b.Id);
  }Trace.Data["collisionBuild:"+w.Name]=new{entities=layer.EntityCount,independentReadbacks=layer.EntityCount,sourceHash=layer.Fingerprint};return layer;}catch {layer.Dispose();throw;}
 }
 public void Dispose(){if(disposed)return;barrier.Check();
  if(!world.WasCollected&&world.IsCreated){var em=world.EntityManager;foreach(var e in entities)if(em.Exists(e))em.DestroyEntity(e);}
  entities.Clear();foreach(var b in blobs)if(b.IsCreated)b.Dispose();blobs.Clear();foreach(var r in roots)r.Dispose();roots.Clear();disposed=true;
 }
}
