#nullable enable
using System;using System.IO;using System.Reflection;using System.Linq;using BepInEx;using BepInEx.Unity.IL2CPP;using HarmonyLib;using Il2CppInterop.Runtime;using UnityEngine;using Game.Application.Client;using Game.Application.Server;using Game.Data;using Core.DataRepository;using Il2CppInterop.Runtime.InteropTypes.Arrays;using Game.GlobalMap;using Game.GlobalMap.UI;using GSceneManager=Game.SceneManagement.SceneManager;using NativeTask=Il2CppSystem.Threading.Tasks.Task;
namespace Frontier.Survivor;
[BepInPlugin("frontier.cargohunters.survivor","Frontier Survivor",Identity.Version)]
[BepInDependency("frontier.cargohunters.core","0.6.0")]
public sealed class ReservePlugin:BasePlugin {
 public override void Load(){Identity.Folder=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;if(!File.Exists(Path.Combine(Identity.Folder,"reserve.location.json"))){var fallback=Path.Combine(Paths.PluginPath,"FrontierPack","modules","survivor");if(File.Exists(Path.Combine(fallback,"reserve.location.json")))Identity.Folder=fallback;}Trace.Init(Log);var h=new Harmony("frontier.survivor.v1");try{NativePrefabSources.Initialize();Content.Load();Hooks.Install(h);SurvivorHooks.Install(h);AddComponent<ReserveLoop>();Log.LogInfo("Reserve " + Identity.Version + " / " + Identity.Revision + ": event-bound dependency cache and native container-group loot; runtime acceptance is recorded separately.");}catch(Exception e){h.UnpatchSelf();Trace.Data["status"]="DISABLED_INITIALIZATION_FAILED";Trace.Error("initialization",e);}}
}
public sealed class ReserveLoop:MonoBehaviour {public ReserveLoop(IntPtr pointer):base(pointer){} public void Update(){try{Loader.Tick();WorldBridge.Tick();SurvivorRun.Tick();SurvivorHud.Tick();Trace.Save();}catch(Exception e){Trace.Error("frame",e);Loader.Fail(e);}}}
internal static class Content {
 internal static LocationDefinition Map=null!;internal static void Load(){Map=LocationDefinition.Load(Path.Combine(Identity.Folder,"reserve.location.json"));Identity.Hash=Map.CollisionFingerprint();Trace.Data["geometryFingerprint"]=Identity.Hash;Trace.Data["contentCounts"]=new{rooms=Map.Rooms.Length,solids=Map.Boxes.Count(b=>b.Solid),decorations=Map.Boxes.Count(b=>!b.Solid),anchors=Map.Anchors.Length};var grid=new WalkGrid(Map);foreach(var a in Map.Anchors.Where(a=>a.Kind=="player")){var f=grid.Flood(grid.Index(a.Position[0],a.Position[2]));foreach(var dest in Map.Anchors){int i=grid.Index(dest.Position[0],dest.Position[2]);if(i<0||f[i]<0)throw new InvalidDataException("Unreachable placement: "+dest.Id);}}Trace.Data["offlineRoutesValidated"]=true;}
}
internal static class Hooks {
 static Harmony harmony=null!;static bool validating;static readonly System.Collections.Generic.List<string> targets=new();
 static void Hook(Type t,string name,Type result,Type[] args,string? pre=null,string? post=null,bool stat=false){var m=Frontier.Safety.NativeHookContract.Exact(t,name,stat,result,args);if(!validating)harmony.Patch(m,pre==null?null:new HarmonyMethod(typeof(Hooks),pre),post==null?null:new HarmonyMethod(typeof(Hooks),post));ValidateBinding(m,pre);ValidateBinding(m,post);targets.Add(m.DeclaringType!.FullName+"."+m.Name);}
 internal static string[] Preflight(){validating=true;try{Install(null!);return targets.ToArray();}finally{validating=false;}}
 static void ValidateBinding(MethodInfo target,string? callback){if(callback==null)return;var cb=typeof(Hooks).GetMethod(callback,BindingFlags.NonPublic|BindingFlags.Static)??throw new MissingMethodException(callback);foreach(var p in cb.GetParameters()){var t=p.ParameterType.IsByRef?p.ParameterType.GetElementType()!:p.ParameterType;if(p.Name=="__instance"){if(target.IsStatic||!t.IsAssignableFrom(target.DeclaringType!))throw new InvalidOperationException("Hook instance mismatch");}else if(p.Name=="__result"){if(t!=target.ReturnType)throw new InvalidOperationException("Hook result mismatch");}else if(p.Name!.StartsWith("__")&&int.TryParse(p.Name[2..],out int i)){if(i>=target.GetParameters().Length||t!=target.GetParameters()[i].ParameterType)throw new InvalidOperationException("Hook argument mismatch");}else throw new InvalidOperationException("Unmapped hook argument");}}
 internal static void Install(Harmony h){harmony=h;targets.Clear();
 Hook(typeof(Game.Network.GhostSpawnerAuthoring),"DeclareReferencedPrefabs",typeof(void),new[]{typeof(Il2CppSystem.Collections.Generic.List<GameObject>)},post:nameof(NativePrefabsDeclared));
 Hook(typeof(DataRepositoryLoader),"ParseDataFromBytes",typeof(void),new[]{typeof(ILoadableRepository),typeof(Il2CppStructArray<byte>)},post:nameof(Repository));
 Hook(typeof(GSceneManager),"LoadScene",typeof(void),new[]{typeof(string),typeof(bool),typeof(bool),typeof(Il2CppSystem.Action)},pre:nameof(PrepareScene));
 Hook(typeof(GSceneManager),"LoadBuildInSceneAsync",typeof(NativeTask),new[]{typeof(string),typeof(UnityEngine.SceneManagement.LoadSceneMode),typeof(bool)},pre:nameof(LoadScene));
 Hook(typeof(GlobalMapManager),"InitializeLocations",typeof(void),Type.EmptyTypes,pre:nameof(RegisterMap),post:nameof(Locations));
 Hook(typeof(GlobalMapFakeController),"InitializeFrom",typeof(void),new[]{typeof(GlobalMapFakeController.IDataProvider)},post:nameof(MenuCapture));
 Hook(typeof(GlobalMapFakeController),"Update",typeof(void),Type.EmptyTypes,post:nameof(MenuTick));
 Hook(typeof(GlobalMapFakeController),"Shutdown",typeof(void),Type.EmptyTypes,pre:nameof(MenuClose));
 Hook(typeof(ClientBattleState),"InitializeScope",typeof(void),Type.EmptyTypes,post:nameof(ClientBind));
 Hook(typeof(ClientBattleState),"CreateBaseLoopSystems",typeof(void),Type.EmptyTypes,post:nameof(ClientCapture));
 Hook(typeof(ClientBattleState),"EnterBattleState",typeof(void),Type.EmptyTypes,pre:nameof(ClientEnter));
 Hook(typeof(ClientBattleState),"Leave",typeof(void),Type.EmptyTypes,post:nameof(ClientLeave));
 Hook(typeof(ClientBattleState),"Shutdown",typeof(void),Type.EmptyTypes,post:nameof(ClientLeave));
 Hook(typeof(ServerBattleState),"ReadyForConnection",typeof(bool),Type.EmptyTypes,post:nameof(ServerReady));
 Hook(typeof(ServerBattleState),"InitializeScope",typeof(void),Type.EmptyTypes,post:nameof(ServerBind));
 Hook(typeof(ServerBattleState),"CreateBaseLoopSystem",typeof(void),Type.EmptyTypes,post:nameof(ServerCapture));
 Hook(typeof(ServerBattleState),"EnterBattleState",typeof(void),Type.EmptyTypes,post:nameof(ServerBind));
 Hook(typeof(ServerBattleState),"LeaveBattleState",typeof(void),Type.EmptyTypes,post:nameof(ServerLeave));
 Hook(typeof(ServerBattleState),"Leave",typeof(void),Type.EmptyTypes,post:nameof(ServerLeave));
 Hook(typeof(Game.NavigationSystem.NavigationSystemSceneConfigurationLoader),"LoadResourcesAsync",typeof(NativeTask),Type.EmptyTypes,pre:nameof(NavigationLoad));
 Hook(typeof(Game.NavigationSystem.NavigationSystemSceneConfigurationLoader),"get_SceneConfiguration",typeof(SickscoreGames.HUDNavigationSystem.HNSSceneConfiguration),Type.EmptyTypes,pre:nameof(NavigationGet));
 if(!validating)Trace.Data["hookTargets"]=targets.ToArray();}
 static void NativePrefabsDeclared(Game.Network.GhostSpawnerAuthoring __instance){try{NativePrefabSources.Capture(__instance);}catch(Exception e){Trace.Error("native prefab source",e);}}
 static void Repository(ILoadableRepository __0,Il2CppStructArray<byte> __1){try{Repositories.Enqueue(__0);}catch(Exception e){Trace.Error("repository",e);}}
 static void RegisterMap(GlobalMapManager __instance){try{Repositories.Drain();Repositories.Register(__instance);}catch(Exception e){Trace.Error("map registration",e);}}
 static void Locations(GlobalMapManager __instance){try{Menu.Locations(__instance);}catch(Exception e){Trace.Error("map node",e);}}
 static void MenuCapture(GlobalMapFakeController __instance)=>Menu.Capture(__instance);static void MenuTick(GlobalMapFakeController __instance)=>Menu.Tick(__instance);static void MenuClose(GlobalMapFakeController __instance)=>Menu.Close(__instance);
 static void PrepareScene(GSceneManager __instance,string __0){if(Identity.Own(__0)){Repositories.Drain();Repositories.RegisterScene(__instance);}}
 static bool LoadScene(string __0,UnityEngine.SceneManagement.LoadSceneMode __1,bool __2,ref NativeTask __result){if(!SceneContract.OwnResource(__0))return true;try{__result=Loader.Begin(__1);return false;}catch(Exception e){Trace.Error("scene start",e);__result=Loader.Fault(e);return false;}}
 static void ServerReady(ServerBattleState __instance,ref bool __result){if(Identity.Own(__instance?._configuration?.SessionInfo?.MapName)){WorldBridge.Bind(__instance!);__result=__result&&WorldBridge.Ready(__instance!);}}
 static void ClientCapture(ClientBattleState __instance){if(Identity.Own(__instance?._mapName))WorldBridge.Capture(__instance!);}
 static void ClientBind(ClientBattleState __instance){if(Identity.Own(__instance?._mapName))WorldBridge.Bind(__instance!);}
 static void ClientEnter(ClientBattleState __instance){if(Identity.Own(__instance?._mapName))WorldBridge.Bind(__instance!);}
 static void ClientLeave(ClientBattleState __instance){if(__instance!=null)WorldBridge.Release(__instance);}
 static void ServerCapture(ServerBattleState __instance){if(Identity.Own(__instance?._configuration?.SessionInfo?.MapName))WorldBridge.Capture(__instance!);}
 static void ServerBind(ServerBattleState __instance){if(Identity.Own(__instance?._configuration?.SessionInfo?.MapName))WorldBridge.Bind(__instance!);}
 static void ServerLeave(ServerBattleState __instance){if(__instance!=null)WorldBridge.Release(__instance);}
 static bool NavigationLoad(Game.NavigationSystem.NavigationSystemSceneConfigurationLoader __instance,ref NativeTask __result){if(!Identity.Own(__instance?._sceneManager?.CurrentScene?.Name))return true;__result=Loader.WaitForNavigation();return false;}
 static bool NavigationGet(Game.NavigationSystem.NavigationSystemSceneConfigurationLoader __instance,ref SickscoreGames.HUDNavigationSystem.HNSSceneConfiguration __result){if(!Identity.Own(__instance?._sceneManager?.CurrentScene?.Name))return true;__result=Loader.NavigationConfig!;return false;}
}
