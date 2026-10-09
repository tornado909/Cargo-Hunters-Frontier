#nullable enable
using System;using System.Linq;using System.Threading;using System.Runtime.InteropServices;using System.Collections.Generic;
using Il2CppInterop.Runtime;using Il2CppInterop.Runtime.InteropTypes;using Unity.Entities;using Unity.Mathematics;using Unity.Transforms;using UnityEngine;
using PhysicsBox=Unity.Physics.BoxCollider;
namespace Frontier.Reserve;
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
public sealed class NativeVisualLayer:IDisposable {
 readonly List<UnityEngine.Material> materials=new();readonly List<Mesh> meshes=new();readonly NativeBuildBarrier barrier;bool disposed;
 public GameObject Root{get;} public string CollisionFingerprint{get;}public int BoxCount{get;}
 public NativeVisualLayer(LocationDefinition def,NativeBuildBarrier gate,Vector3 origin,Shader verifiedShader){
  gate.Check();def.Validate();barrier=gate;if(verifiedShader==null)throw new ArgumentException("A shader supported by the current game render pipeline is required");
  Root=new GameObject("Frontier.Reserve.Geometry");Root.SetActive(false);Root.transform.position=origin;CollisionFingerprint=def.CollisionFingerprint();
  try {var mats=new Dictionary<string,UnityEngine.Material>();foreach(var pair in def.Materials){var m=new UnityEngine.Material(verifiedShader);m.name="Reserve."+pair.Key;
   var c=pair.Value.Color;var color=new Color(Convert.ToInt32(c.Substring(0,2),16)/255f,Convert.ToInt32(c.Substring(2,2),16)/255f,Convert.ToInt32(c.Substring(4,2),16)/255f,1);
   if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);if(m.HasProperty("_Color"))m.SetColor("_Color",color);
   if(m.HasProperty("_Glossiness"))m.SetFloat("_Glossiness",1-pair.Value.Roughness);
   if(pair.Value.Emission!=null&&m.HasProperty("_EmissionColor")){m.SetColor("_EmissionColor",color*.8f);m.EnableKeyword("_EMISSION");}mats[pair.Key]=m;materials.Add(m);}
   foreach(var group in def.Boxes.GroupBy(b=>(b.Material,b.Kind))){
    var go=new GameObject(group.Key.Kind+"."+group.Key.Material);go.transform.SetParent(Root.transform,false);
    var mesh=MakeMesh(group.ToArray());meshes.Add(mesh);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mats[group.Key.Material];
   }
   foreach(var room in def.Rooms){var go=new GameObject("ReserveLight."+room.Id);go.transform.SetParent(Root.transform,false);go.transform.localPosition=new Vector3(room.X,4.6f,room.Z);var light=go.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.88f,.65f);light.intensity=1.2f;light.range=Math.Max(room.Width,room.Depth)*.72f;light.shadows=LightShadows.None;}
   BoxCount=def.Boxes.Length;
  }catch {Dispose();throw;}
 }
 public void Activate(){barrier.Check();if(disposed)throw new ObjectDisposedException(nameof(NativeVisualLayer));Root.SetActive(true);}
 static Mesh MakeMesh(BoxDef[] boxes){
  var vs=new List<Vector3>();var ns=new List<Vector3>();var ts=new List<int>();
  int[][] corners={new[]{1,-1,-1,1,1,-1,1,1,1,1,-1,1},new[]{-1,-1,1,-1,1,1,-1,1,-1,-1,-1,-1},new[]{-1,1,-1,-1,1,1,1,1,1,1,1,-1},new[]{-1,-1,1,-1,-1,-1,1,-1,-1,1,-1,1},new[]{-1,-1,1,1,-1,1,1,1,1,-1,1,1},new[]{1,-1,-1,-1,-1,-1,-1,1,-1,1,1,-1}};
  Vector3[] normals={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
  foreach(var b in boxes)for(int f=0;f<6;f++){int start=vs.Count;for(int j=0;j<4;j++){vs.Add(new Vector3(b.Center[0]+corners[f][j*3]*b.Size[0]/2,b.Center[1]+corners[f][j*3+1]*b.Size[1]/2,b.Center[2]+corners[f][j*3+2]*b.Size[2]/2));ns.Add(normals[f]);}ts.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});}
  if(vs.Count>65535)throw new InvalidOperationException("Mesh vertex budget exceeded");var m=new Mesh();m.name="Reserve geometry batch";m.vertices=new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(vs.ToArray());m.normals=new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(ns.ToArray());m.triangles=new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int>(ts.ToArray());m.RecalculateBounds();return m;
 }
 public void Dispose(){if(disposed)return;barrier.Check();if(Root!=null)UnityEngine.Object.Destroy(Root);foreach(var m in materials)if(m!=null)UnityEngine.Object.Destroy(m);materials.Clear();foreach(var m in meshes)if(m!=null)UnityEngine.Object.Destroy(m);meshes.Clear();disposed=true;}
}
