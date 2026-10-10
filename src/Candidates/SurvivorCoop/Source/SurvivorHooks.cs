#nullable enable
using System;
using System.Reflection;
using System.Collections;
using HarmonyLib;
using Core.Utils;
using Frontier.Loot;
using Game.AISystem;
using Game.GameModes;
using Game.ItemsSystem;
using Game.SpawnPointSystem.BotSpawnPoints.Data;
using Game.Weapons;
using Game.ProgressSystem;
namespace Frontier.Survivor;

// Adapters to the EXISTING Frontier combat and native bot-assembly paths.
// No ammo stacks are created/refilled and no permanent traits are added to items.
internal static class SurvivorHooks
{
 [ThreadStatic] static bool weaponScope;
 static readonly Type CombatType=typeof(Frontier.Loot.Runtime).Assembly.GetType("Frontier.Loot.Combat",true)!;
 static readonly MethodInfo CopyRecipe=AccessTools.Method(typeof(RankLadder),"CopySpawnRecipe");
 static readonly Type SelectionType=typeof(RankLadder).GetNestedType("SpawnSelection",BindingFlags.NonPublic)!;
 static readonly FieldInfo SelectionsField=AccessTools.Field(typeof(RankLadder),"SelectedSpawns");
 static readonly string[] SelectionNames={"Preset","Rank","RankedId","BaseId","EquipmentEntries","EquipmentId","PlatformId","LootId"};
 static readonly FieldInfo[] SelectionFields=Array.ConvertAll(SelectionNames,n=>AccessTools.Field(SelectionType,n));
 internal sealed class SpawnLease:IDisposable
 {
  internal long Pointer;
  internal NativeRoot? Root;
  public void Dispose(){if(Pointer!=0){((IDictionary)SelectionsField.GetValue(null)!).Remove(Pointer);Pointer=0;}Root?.Dispose();Root=null;}
 }
 internal static void Install(Harmony h)
 {
  if(CopyRecipe==null||SelectionType==null||SelectionsField==null||Array.Exists(SelectionFields,f=>f==null))throw new MissingMemberException("Existing Frontier rank recipe adapter is unavailable");
  h.Patch(AccessTools.Method(typeof(Frontier.Loot.Runtime),"Aggregate"),postfix:new HarmonyMethod(typeof(SurvivorHooks),nameof(Aggregate)));
  h.Patch(AccessTools.Method(CombatType,"BeforeWeapons"),prefix:new HarmonyMethod(typeof(SurvivorHooks),nameof(BeginWeapons)),finalizer:new HarmonyMethod(typeof(SurvivorHooks),nameof(EndWeapons)));
  h.Patch(AccessTools.Method(typeof(LootMath),nameof(LootMath.Conserve)),postfix:new HarmonyMethod(typeof(SurvivorHooks),nameof(Conserve)));
  var spawn=AccessTools.Method(typeof(AiCharacterSpawnSystem),"GetPlayerEquipment");
  h.Patch(spawn,prefix:new HarmonyMethod(typeof(SurvivorHooks),nameof(BeforeEquipment)){priority=Priority.First},postfix:new HarmonyMethod(typeof(SurvivorHooks),nameof(AfterEquipment)){priority=Priority.Last},finalizer:new HarmonyMethod(typeof(SurvivorHooks),nameof(EquipmentFinalizer)));
  h.Patch(AccessTools.Method(typeof(ServerExperienceManager),"HandleAIPlayerKilled"),postfix:new HarmonyMethod(typeof(SurvivorHooks),nameof(Killed)));
  Trace.Data["survivorCombatAdapter"]=new{ammo="Frontier.Loot.Runtime.Aggregate.SaveAmmo = 1; existing Combat.BeforeWeapons leases",chance="100% only inside Survivor weapon update; original 95% cap elsewhere",persistentItemsChanged=false};
 }
 static void BeginWeapons(WeaponUpdateSystem __0,out bool __state)
 {__state=weaponScope;weaponScope=SurvivorRun.OwnWorld(__0?.World);}
 static Exception? EndWeapons(Exception? __exception,bool __state){weaponScope=__state;return __exception;}
 static void Conserve(double __0,double __1,int __2,ref bool __result)
 {
  if(weaponScope&&__0==1.0&&double.IsFinite(__1)){
   __result=__2>0;if(__result)SurvivorRun.AmmoModifierTrials++;
  }
 }
 static void Aggregate(BattleSession __0,int __1,UniqueId __2,ref Bonuses __result)
 {
  if(__result==null||!SurvivorRun.IsHuman(__0,__1))return;
  var p=SurvivorRun.Progress;var b=__result;
  // Never mutate the regular aggregate or any cached bonus object.
  __result=new Bonuses{Damage=b.Damage+p.Damage,Crit=b.Crit+p.Crit,CritPower=b.CritPower+p.CritPower,
   SaveAmmo=1f,FireRate=b.FireRate+p.FireRate,Penetration=b.Penetration+p.Penetration,
   Executor=b.Executor,Count=Math.Max(1,b.Count)+p.Level,HasLadder=b.HasLadder};
 }
 static void BeforeEquipment(AiCharacterSpawnSystem __instance,int __0,ref BotPresetData __1,out SpawnLease? __state)
 {
  __state=null;
  if(!SurvivorRun.OwnAuthority(__instance?._battleSession)||__1==null)return;
  if(!SurvivorRun.TryRecipe(__1.Id,out var original,out int rank))return;
  var copy=(BotPresetData)CopyRecipe.Invoke(null,new object[]{original,__1})!;
  var lease=new SpawnLease{Pointer=copy.Pointer.ToInt64(),Root=new NativeRoot(copy)};
  try
  {
   var selection=Activator.CreateInstance(SelectionType,true)!;
   object[] values={copy,rank,__1.Id.ToString(),original.Id.ToString(),original.Equipments?.Count??0,original.EquipmentPresetId.ToString(),original.PlatformPresetId.ToString(),original.LootPresetId.ToString()};
   for(int i=0;i<values.Length;i++)SelectionFields[i].SetValue(selection,values[i]);
   ((IDictionary)SelectionsField.GetValue(null)!).Add(lease.Pointer,selection);
   __1=copy;__state=lease;SurvivorRun.EquipmentRequested(__0,rank,original.Id.ToString());
  }
  catch{lease.Dispose();throw;}
 }
 static void AfterEquipment(AiCharacterSpawnSystem __instance,int __0,Equipment __result)
 {if(__result!=null&&SurvivorRun.OwnAuthority(__instance?._battleSession))SurvivorRun.EquipmentCreated(__0,__result);}
 static Exception? EquipmentFinalizer(Exception? __exception,SpawnLease? __state)
 {__state?.Dispose();if(__exception!=null&&__state!=null)SurvivorRun.Fail("native equipment: "+__exception.GetBaseException().Message);return __exception;}
 static void Killed(ServerExperienceManager __instance,int __0,int __1)
 {
  if(SurvivorRun.OwnAuthority(__instance?._battleSession)&&SurvivorRun.IsHuman(__instance!._battleSession,__0))SurvivorRun.Kill(__1);
 }
}
