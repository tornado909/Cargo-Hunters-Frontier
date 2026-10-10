#nullable enable
using System;
using System.Linq;
using System.Collections.Generic;
using Core.Utils;
using Frontier.Loot;
using Game.AISystem;
using Game.Character;
using Game.GameModes;
using Game.ItemsSystem;
using Game.SpawnPointSystem.BotSpawnPoints.Data;
using Game.SpawnPointSystem.GameEvents.ScriptingEvents;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AI;
namespace Frontier.Survivor;

internal static class SurvivorRun
{
 static World? authorityWorld,clientWorld;
 static BattleSession? authority,client;
 static readonly LeaseBag sessions=new();
 static readonly Dictionary<string,(BotPresetData Base,int Rank)> recipes=new(StringComparer.Ordinal);
 static readonly Dictionary<int,(int Rank,string Base)> live=new();
 static readonly int[] spawnedRanks=new int[28];
 static Random random=new(1);
 static double nextUpdate,lastUpdate,nextSpawn,pendingSince,startedAt;
 static int pendingPlayer=-1,spawnSerial,peakAlive,seed,pendingRank;
 static string pendingEquipment="",pendingBase="";
 static Il2CppSystem.Collections.Generic.List<int>? playerIds;
 static bool requestPending,prepared,ended;
 static string? fault;
 internal static RunProgress Progress {get;private set;}=new(1);
 internal static string RunId {get;private set;}="";
 internal static double Seconds {get;private set;}
 internal static int Rank=>Math.Clamp(1+(int)(Seconds/90),1,28);
 internal static int Alive=>live.Count;
 internal static long AmmoModifierTrials;
 internal static bool Active=>!ended&&authority!=null&&client!=null&&fault==null;
 internal static string? Fault=>fault;
 internal static bool HasWorld=>authorityWorld!=null||clientWorld!=null;
 internal static void Bind(World world,bool server)
 {
  if(server)
  {
   if(authorityWorld?.Pointer==world.Pointer)return;
   var previousClient=clientWorld;Reset();clientWorld=previousClient;authorityWorld=world;seed=Guid.NewGuid().GetHashCode();random=new Random(seed);Progress=new RunProgress(seed);
   RunId=Guid.NewGuid().ToString("N");startedAt=Time.realtimeSinceStartupAsDouble;lastUpdate=startedAt;
  }
  else clientWorld=world;
 }
 internal static void Unbind(World world)
 {
  if(authorityWorld?.Pointer==world.Pointer){Snapshot("ENDED_WORLD_RELEASE");Reset();SurvivorHud.Clear();}
  else if(clientWorld?.Pointer==world.Pointer){clientWorld=null;client=null;SurvivorHud.Clear();}
 }
 static void Reset()
 {
  authorityWorld=null;clientWorld=null;authority=null;client=null;sessions.Dispose();recipes.Clear();live.Clear();Array.Clear(spawnedRanks,0,28);
  nextUpdate=lastUpdate=nextSpawn=pendingSince=startedAt=Seconds=0;pendingPlayer=-1;pendingRank=0;pendingEquipment=pendingBase="";playerIds=null;spawnSerial=peakAlive=0;requestPending=prepared=ended=false;fault=null;AmmoModifierTrials=0;RunId="";Progress=new RunProgress(1);
 }
 internal static bool OwnWorld(World? w)=>!ended&&fault==null&&w!=null&&((authorityWorld!=null&&w.Pointer==authorityWorld.Pointer)||(clientWorld!=null&&w.Pointer==clientWorld.Pointer));
 internal static bool OwnAuthority(BattleSession? s)=>!ended&&fault==null&&s!=null&&authority!=null&&s.Pointer==authority.Pointer&&s.IsAuthoritative&&s.IsBattleSession;
 internal static bool IsHuman(BattleSession? s,int id)
 {
  if(!Active||s==null||!s.IsBattleSession)return false;
  if(authority!=null&&s.Pointer==authority.Pointer)return !s.IsBotPlayer(id)&&s.GetPlayerAccountId(id).HasValue&&!s.IsPlayerIsDead(id);
  return client!=null&&s.Pointer==client.Pointer&&id==client.GetLocalPlayerId()&&!s.IsPlayerIsDead(id);
 }
 static BattleSession? Session(World? w)
 {
  if(w==null||w.WasCollected||!w.IsCreated)return null;
  var handler=w.GetExistingSystem<CharacterSpawnHandlerSystem>();
  return handler!=null&&handler._dependencyInitialized?handler._battleSession:null;
 }
 static void Prepare()
 {
  if(RankLadder.Data==null)throw new InvalidOperationException("Frontier Rank28 data missing");
  SurvivorRanks.RegisterPresentation();
  foreach(var id in Repositories.BotIds)
  {
   var original=Repositories.ResolveBot(id);
   var variants=SurvivorRanks.Variants(id.ToString());if(variants.Length!=28)throw new InvalidOperationException("Native enemy missing from existing rank ladder: "+id);
   for(int i=0;i<28;i++)
   {
    var ranked=Repositories.ResolveBot(UniqueId.Parse(variants[i]));
    if(ranked.AISettingId==UniqueId.Empty)throw new InvalidOperationException("Native ranked AI missing");
    recipes.Add(variants[i],(original,i+1));
   }
  }
  prepared=true;Trace.Data["survivorRecipePolicy"]=new{initial="WAREHOUSE/Rookie",meleeTemplate="225e6aa0-dc59-4a9c-8846-40434e438fe4",firstRangedAfterSeconds=360,meleeOnlyFirstSeconds=180,rankStepSeconds=90,existingNativeRankRecipes=recipes.Count};
 }
 internal static void Tick()
 {
  if(!HasWorld||ended||fault!=null)return;
  double now=Time.realtimeSinceStartupAsDouble;if(now<nextUpdate)return;nextUpdate=now+.2;
  try
  {
   if(authority==null){var s=Session(authorityWorld);if(s!=null&&s.IsAuthoritative&&s.IsBattleSession){authority=s;sessions.Keep(s);}}
   if(client==null){var s=Session(clientWorld);if(s!=null&&s.IsBattleSession&&!s.IsAuthoritative){client=s;sessions.Keep(s);}}
   if(authority==null||client==null){lastUpdate=now;if(now-startedAt>90)Fail("Native survivor session did not initialize");return;}
   int player=client.GetLocalPlayerId();
   if(!authority.GetEntityHasBeenSpawned(player)){lastUpdate=now;return;}
   if(authority.IsPlayerIsDead(player)){ended=true;Snapshot("ENDED_PLAYER_DEAD");SurvivorHud.Clear();return;}
   if(authority.GetPlayersCountConfiguration()!=1){Fail("This Survivor build is single-player; cooperative run-state replication is not enabled");return;}
   if(!prepared)Prepare();
   Seconds+=Math.Clamp(now-lastUpdate,0,.5);lastUpdate=now;
   var manager=authority.EntityManager;
   foreach(int id in live.Keys.ToArray())
   {
    if(requestPending&&id==pendingPlayer)continue;
    var e=authority.GetPlayerEntity(id);
    if(authority.IsPlayerIsDead(id)||e==Entity.Null||!manager.Exists(e))live.Remove(id);
   }
   if(requestPending)
   {
    if(pendingEquipment.Length>0)
    {
     playerIds??=sessions.Keep(new Il2CppSystem.Collections.Generic.List<int>());
     playerIds.Clear();authority.GetPlayerIds(playerIds);
     if(playerIds.Count>4096)throw new InvalidOperationException("Native session player count exceeds tracking budget");
     for(int i=0;i<playerIds.Count;i++)
     {
      int id=playerIds[i];if(!authority.IsBotPlayer(id))continue;
      var equipment=authority.FindEquipmentByPlayerId(id);
      if(equipment==null||equipment.Id.ToString()!=pendingEquipment)continue;
      var e=authority.GetPlayerEntity(id);
      if(!authority.GetEntityHasBeenSpawned(id)||e==Entity.Null||!manager.Exists(e))continue;
      live[id]=(pendingRank,pendingBase);spawnedRanks[pendingRank-1]++;pendingPlayer=id;requestPending=false;
      Trace.Add("survivorSpawnAcknowledged");Trace.Data["survivorLastAcknowledged"]=new{player=id,equipmentId=pendingEquipment,rank=RankPolicy.Name(pendingRank),entity=e.ToString()};
      pendingEquipment="";break;
     }
    }
    if(requestPending&&now-pendingSince>12){Fail("Native spawn confirmation timed out; no duplicate retry was issued");return;}
   }
   var entity=authority.GetPlayerEntity(player);if(entity==Entity.Null||!manager.Exists(entity))return;
   var position=manager.GetComponentData<Translation>(entity).Value;
   if(Seconds>=8&&!requestPending&&now>=nextSpawn)
   {
    var spawner=authorityWorld!.GetExistingSystem<AiSpawnPointUpdateSystem>();
    if(spawner!=null&&spawner._dependencyInitialized)
    {
     int target=Math.Min(28,5+(int)(Seconds/40));
     if(spawner._maxAliveBotsCount>0)target=Math.Min(target,spawner._maxAliveBotsCount);
     if(live.Count<target)Spawn(spawner,new Vector3(position.x,position.y,position.z),player,now);
    }
    nextSpawn=now+Math.Max(.65,2.8-Seconds/480);
   }
   peakAlive=Math.Max(peakAlive,live.Count);
   Snapshot("RUNNING");
  }
  catch(Exception e){Fail(e.GetBaseException().Message);Trace.Error("survivor run",e);}
 }
 static void Spawn(AiSpawnPointUpdateSystem system,Vector3 player,int playerId,double now)
 {
  var camera=Camera.main;var candidates=new List<Anchor>();
  foreach(var a in Content.Map.Anchors)
  {
   if(a.Kind!="enemy")continue;var p=new Vector3(a.Position[0],a.Position[1],a.Position[2]);float distance=Vector3.Distance(player,p);
   if(distance<24||distance>52)continue;
   if(camera!=null){var v=camera.WorldToViewportPoint(p+Vector3.up);if(v.z>0&&v.x>-.15f&&v.x<1.15f&&v.y>-.2f&&v.y<1.2f)continue;}
   if(!NavMesh.SamplePositionFilter(p,out var hit,1,0,1)||Vector3.Distance(hit.position,p)>1)continue;
   candidates.Add(a);
  }
  if(candidates.Count==0){Trace.Add("survivorNoSafeSpawnPoint");return;}
  var selectedAnchor=candidates[random.Next(candidates.Count)];int family=0;
  if(Seconds>=180&&random.NextDouble()<.25)family=1;
  if(Seconds>=360&&random.NextDouble()<Math.Min(.22,(Seconds-360)/3600+.08))family=2;
  int rank=Rank;string baseId=Repositories.BotIds[family].ToString();string ranked=SurvivorRanks.Variants(baseId)[rank-1];
  var request=new SpawnBotEventArgs{BotSpawnPointId=Identity.Id(selectedAnchor.Id),BotPresetId=UniqueId.Parse(ranked),PlayerId=playerId,
   Tags=new Il2CppSystem.Collections.Generic.List<int>(),OverrideAiSettingsId=UniqueId.Empty,OverrideIntroBehavior=UniqueId.Empty,
   OverrideSpawnIdleItemTemplateId=UniqueId.Empty,IgnoreGuardAreaFromSpawnPoint=true,RequireExclusiveRoute=false};
  using var root=new NativeRoot(request);requestPending=true;pendingPlayer=-1;pendingSince=now;
  system.OnSpawnBotEventHandler(request);spawnSerial++;
  Trace.Data["survivorLastSpawnRequest"]=new{serial=spawnSerial,anchor=selectedAnchor.Id,rank=RankPolicy.Name(rank),baseId,rankedId=ranked,distance=Vector3.Distance(player,new Vector3(selectedAnchor.Position[0],selectedAnchor.Position[1],selectedAnchor.Position[2]))};
 }
 internal static bool TryRecipe(UniqueId ranked,out BotPresetData original,out int rank)
 {
  if(recipes.TryGetValue(ranked.ToString(),out var value)){original=value.Base;rank=value.Rank;return true;}original=null!;rank=0;return false;
 }
 internal static void EquipmentRequested(int player,int rank,string baseId)
 {
  if(!requestPending)throw new InvalidOperationException("Survivor bot created without a director request");
  pendingPlayer=-1;pendingRank=rank;pendingBase=baseId;pendingEquipment="";
 }
 internal static void EquipmentCreated(int player,Equipment equipment)
 {
  if(!requestPending||pendingRank<1)return;pendingEquipment=equipment.Id.ToString();var bot=(Rank:pendingRank,Base:pendingBase);
  Trace.Data["survivorLastEquipment"]=new{recipeContextPlayer=player,rank=RankPolicy.Name(bot.Rank),baseId=bot.Base,equipmentId=equipment.Id.ToString(),pipeline="Original recipe + existing Frontier completed-assembly rank binding"};
 }
 internal static void Kill(int experience)
 {
  Progress.Award(experience);Trace.Add("survivorNativeKills");Snapshot("RUNNING");Trace.Save(true);
 }
 internal static void Choose(int index)
 {
  if(!Active||!Progress.Choose(index))return;Trace.Add("survivorUpgradesChosen");Snapshot("RUNNING");Trace.Save(true);
 }
 internal static void Fail(string reason){fault=reason;Snapshot("BLOCKED");Trace.Save(true);}
 static void Snapshot(string status)
 {
  // The report writer is throttled; only small managed records reach it.
  Trace.Data["survivor"]=new{status,runId=RunId,seed,elapsedSeconds=Math.Round(Seconds,2),rank=RankPolicy.Name(Rank),alive=live.Count,peakAlive,requests=spawnSerial,pending=requestPending,
   level=Progress.Level,xp=Progress.Experience,xpNeeded=Progress.Required,kills=Progress.Kills,pendingChoices=Progress.PendingChoices,upgrades=Progress.Upgrades.ToArray(),offered=Progress.Offer.ToArray(),
   ammoModifierTrials=AmmoModifierTrials,spawnedByRank=spawnedRanks.ToArray(),fault};
 }
}
