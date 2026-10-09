#nullable enable
using System;using System.Linq;using System.Collections.Generic;using UnityEngine;using Unity.Entities;using Game.Application.Client;using Game.Application.Server;
namespace Frontier.Reserve;
internal static class WorldBridge {
 sealed class Binding {internal IntPtr Owner;internal bool Server;internal World World=null!;internal NativeRoot Root=null!;internal NativeCollisionLayer? Collision;internal NativePlacements? Placements;internal NativePrefabLayer? Prefabs;internal bool Ready;internal double Started;internal bool Failed;internal double NextCheck;internal string Waiting="";}
 static readonly Dictionary<IntPtr,Binding> bindings=new();static readonly HashSet<IntPtr> rejected=new();internal static int ActiveCount=>bindings.Count;
 internal static void Capture(ClientBattleState state)=>Capture(state.Pointer,state._world,false);internal static void Capture(ServerBattleState state)=>Capture(state.Pointer,state._world,true);
 static void Capture(IntPtr owner,World? world,bool server){if(owner==IntPtr.Zero||world==null||world.WasCollected||!world.IsCreated)return;if(bindings.TryGetValue(owner,out var previous)){if(previous.World.Pointer==world.Pointer)return;Release(owner);}bindings.Add(owner,new Binding{Owner=owner,World=world,Root=new NativeRoot(world),Server=server,Started=Time.realtimeSinceStartupAsDouble});Trace.Add(server?"serverWorldCaptures":"clientWorldCaptures");}
 internal static bool Ready(ClientBattleState s)=>bindings.TryGetValue(s.Pointer,out var b)&&b.Ready;internal static bool Ready(ServerBattleState s)=>bindings.TryGetValue(s.Pointer,out var b)&&b.Ready;
 internal static void Bind(ClientBattleState state){Capture(state);Install(state.Pointer);}internal static void Bind(ServerBattleState state){Capture(state);Install(state.Pointer);}
 static void Install(IntPtr owner){if(!bindings.TryGetValue(owner,out var b)||b.Ready||b.Failed||!Loader.Ready||!Repositories.Ready)return;if(Time.realtimeSinceStartupAsDouble<b.NextCheck)return;b.NextCheck=Time.realtimeSinceStartupAsDouble+.1;try{
  b.World.EntityManager.CompleteAllJobs();var gate=new NativeBuildBarrier(()=>{if(b.World.WasCollected||!b.World.IsCreated)return;b.World.EntityManager.CompleteAllJobs();});b.Prefabs??=NativePrefabLayer.Build(b.World,gate,b.Server);if(b.Server&&!NativeServerPrefabsReady(b))return;var filter=Unity.Physics.CollisionFilter.Default;var material=Unity.Physics.Material.Default;
  b.Collision=NativeCollisionLayer.Build(b.World,gate,Content.Map,Vector3.zero,filter,material,new Unity.Physics.PhysicsWorldIndex{Value=0});b.Placements=new NativePlacements(b.World,gate,b.Server);if(b.Collision.Fingerprint!=Identity.Hash)throw new InvalidOperationException("World collision fingerprint mismatch");b.Ready=true;Trace.Data[b.Server?"serverWorldReady":"clientWorldReady"]=new{name=b.World.Name,collisionCount=b.Collision.EntityCount,placementCount=b.Placements.Converted,geometryHash=Identity.Hash};Trace.Data["status"]="NATIVE_WORLD_CONTENT_INSTALLED";Trace.Save(true);
 }catch(Exception e){b.Failed=true;rejected.Add(owner);try{b.Placements?.Dispose();b.Placements=null;b.Collision?.Dispose();b.Collision=null;b.Prefabs?.Dispose();b.Prefabs=null;}catch(Exception cleanup){Trace.Error("world cleanup",cleanup);}Trace.Error(b.Server?"server content":"client content",e);Loader.Fail(e);}}

 // Never expose network SceneObject entities before their real native prefab collection exists.
 // The game's initializer calls GetSingletonEntity without an empty-query guard.
 static bool NativeServerPrefabsReady(Binding binding)
 {
  var system=binding.World.GetExistingSystem(Il2CppInterop.Runtime.Il2CppType.Of<Game.SceneObjectsSystem.Systems.SceneObjectInitializationServerSystem>())?.TryCast<Game.SceneObjectsSystem.Systems.SceneObjectInitializationServerSystem>();
  string reason;int count=-1;
  if(system==null||system.WasCollected)reason="WAITING_FOR_SCENE_OBJECT_INITIALIZER";
  else if(system._ghostPrefabCollectionQuery.__impl==IntPtr.Zero)reason="WAITING_FOR_NATIVE_PREFAB_QUERY";
  else {
   count=system._ghostPrefabCollectionQuery.CalculateEntityCount();
   if(count>1)throw new InvalidOperationException("Native network prefab collection is not a singleton: "+count);
   if(count==0)reason="WAITING_FOR_NATIVE_NETWORK_PREFABS";
   else {
    var prefab=system.GetGhostSceneObjectPrefab();
    if(prefab==Entity.Null||!binding.World.EntityManager.Exists(prefab))reason="WAITING_FOR_NATIVE_SCENE_OBJECT_PREFAB";
    else {
     Trace.Data["serverNativePrefabDependency"]=new{world=binding.World.Name,collectionCount=count,prefab=prefab.ToString(),ready=true,elapsedSeconds=Time.realtimeSinceStartupAsDouble-binding.Started};
     binding.Waiting="";return true;
    }
   }
  }
  if(binding.Waiting!=reason){binding.Waiting=reason;Trace.Data["serverNativePrefabDependency"]=new{world=binding.World.Name,collectionCount=count,ready=false,reason};Trace.Data["status"]=reason;Trace.Save(true);}
  return false;
 }

 internal static void Tick(){if(bindings.Count==0)return;foreach(var b in bindings.Values.ToArray()){if(b.World.WasCollected||!b.World.IsCreated){Release(b.Owner);continue;}if(!b.Ready&&!b.Failed){Install(b.Owner);if(Time.realtimeSinceStartupAsDouble-b.Started>90){b.Failed=true;Loader.Fail(new TimeoutException("Reserve world did not become ready"));}}}}
 internal static void Release(ClientBattleState s)=>Release(s.Pointer);internal static void Release(ServerBattleState s)=>Release(s.Pointer);
 static void Release(IntPtr owner){rejected.Remove(owner);if(!bindings.Remove(owner,out var b))return;try{if(!b.World.WasCollected&&b.World.IsCreated)b.World.EntityManager.CompleteAllJobs();b.Placements?.Dispose();b.Collision?.Dispose();b.Prefabs?.Dispose();}catch(Exception e){Trace.Error("world teardown",e);}finally{b.Root.Dispose();Trace.Add("worldsReleased");}if(bindings.Count==0)Loader.ScheduleRelease();}
}
