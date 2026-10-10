using System;
using System.Collections.Generic;
using System.Reflection;
using Core.DataRepository;
using Frontier.Safety;
using Game.Application.Client;
using Game.Application.Server;
using Game.Data;
using Game.GlobalMap;
using Game.GlobalMap.UI;
using Game.NavigationSystem;
using Game.Network;
using Game.SceneManagement;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem;
using Il2CppSystem.Collections.Generic;
using Il2CppSystem.Threading.Tasks;
using SickscoreGames.HUDNavigationSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Frontier.Reserve;

internal static class Hooks
{
	private static Harmony harmony = null;

	private static bool validating;

	private static readonly System.Collections.Generic.List<string> targets = new System.Collections.Generic.List<string>();

	private static void Hook(System.Type t, string name, System.Type result, System.Type[] args, string? pre = null, string? post = null, bool stat = false)
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		MethodInfo methodInfo = NativeHookContract.Exact(t, name, stat, result, args);
		if (!validating)
		{
			harmony.Patch((MethodBase)methodInfo, (pre == null) ? ((HarmonyMethod)null) : new HarmonyMethod(typeof(Hooks), pre, (System.Type[])null), (post == null) ? ((HarmonyMethod)null) : new HarmonyMethod(typeof(Hooks), post, (System.Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
		ValidateBinding(methodInfo, pre);
		ValidateBinding(methodInfo, post);
		targets.Add(methodInfo.DeclaringType.FullName + "." + methodInfo.Name);
	}

	internal static string[] Preflight()
	{
		validating = true;
		try
		{
			Install(null);
			return targets.ToArray();
		}
		finally
		{
			validating = false;
		}
	}

	private static void ValidateBinding(MethodInfo target, string? callback)
	{
		if (callback == null)
		{
			return;
		}
		ParameterInfo[] parameters = (typeof(Hooks).GetMethod(callback, BindingFlags.Static | BindingFlags.NonPublic) ?? throw new System.MissingMethodException(callback)).GetParameters();
		foreach (ParameterInfo parameterInfo in parameters)
		{
			System.Type type = (parameterInfo.ParameterType.IsByRef ? parameterInfo.ParameterType.GetElementType() : parameterInfo.ParameterType);
			if (parameterInfo.Name == "__instance")
			{
				if (target.IsStatic || !type.IsAssignableFrom(target.DeclaringType))
				{
					throw new System.InvalidOperationException("Hook instance mismatch");
				}
				continue;
			}
			if (parameterInfo.Name == "__result")
			{
				if (type != target.ReturnType)
				{
					throw new System.InvalidOperationException("Hook result mismatch");
				}
				continue;
			}
			if (parameterInfo.Name.StartsWith("__") && int.TryParse(parameterInfo.Name.Substring(2), out var result))
			{
				if (result >= target.GetParameters().Length || type != target.GetParameters()[result].ParameterType)
				{
					throw new System.InvalidOperationException("Hook argument mismatch");
				}
				continue;
			}
			throw new System.InvalidOperationException("Unmapped hook argument");
		}
	}

	internal static void Install(Harmony h)
	{
		harmony = h;
		targets.Clear();
		Hook(typeof(GhostSpawnerAuthoring), "DeclareReferencedPrefabs", typeof(void), new System.Type[1] { typeof(Il2CppSystem.Collections.Generic.List<GameObject>) }, null, "NativePrefabsDeclared");
		Hook(typeof(DataRepositoryLoader), "ParseDataFromBytes", typeof(void), new System.Type[2]
		{
			typeof(ILoadableRepository),
			typeof(Il2CppStructArray<byte>)
		}, null, "Repository");
		Hook(typeof(Game.SceneManagement.SceneManager), "LoadScene", typeof(void), new System.Type[4]
		{
			typeof(string),
			typeof(bool),
			typeof(bool),
			typeof(Il2CppSystem.Action)
		}, "PrepareScene");
		Hook(typeof(Game.SceneManagement.SceneManager), "LoadBuildInSceneAsync", typeof(Task), new System.Type[3]
		{
			typeof(string),
			typeof(LoadSceneMode),
			typeof(bool)
		}, "LoadScene");
		Hook(typeof(GlobalMapManager), "InitializeLocations", typeof(void), System.Type.EmptyTypes, "RegisterMap", "Locations");
		Hook(typeof(GlobalMapFakeController), "InitializeFrom", typeof(void), new System.Type[1] { typeof(GlobalMapFakeController.IDataProvider) }, null, "MenuCapture");
		Hook(typeof(GlobalMapFakeController), "Update", typeof(void), System.Type.EmptyTypes, null, "MenuTick");
		Hook(typeof(GlobalMapFakeController), "Shutdown", typeof(void), System.Type.EmptyTypes, "MenuClose");
		Hook(typeof(ClientBattleState), "InitializeScope", typeof(void), System.Type.EmptyTypes, null, "ClientBind");
		Hook(typeof(ClientBattleState), "CreateBaseLoopSystems", typeof(void), System.Type.EmptyTypes, null, "ClientCapture");
		Hook(typeof(ClientBattleState), "EnterBattleState", typeof(void), System.Type.EmptyTypes, "ClientEnter");
		Hook(typeof(ClientBattleState), "Leave", typeof(void), System.Type.EmptyTypes, null, "ClientLeave");
		Hook(typeof(ClientBattleState), "Shutdown", typeof(void), System.Type.EmptyTypes, null, "ClientLeave");
		Hook(typeof(ServerBattleState), "ReadyForConnection", typeof(bool), System.Type.EmptyTypes, null, "ServerReady");
		Hook(typeof(ServerBattleState), "InitializeScope", typeof(void), System.Type.EmptyTypes, null, "ServerBind");
		Hook(typeof(ServerBattleState), "CreateBaseLoopSystem", typeof(void), System.Type.EmptyTypes, null, "ServerCapture");
		Hook(typeof(ServerBattleState), "EnterBattleState", typeof(void), System.Type.EmptyTypes, null, "ServerBind");
		Hook(typeof(ServerBattleState), "LeaveBattleState", typeof(void), System.Type.EmptyTypes, null, "ServerLeave");
		Hook(typeof(ServerBattleState), "Leave", typeof(void), System.Type.EmptyTypes, null, "ServerLeave");
		Hook(typeof(NavigationSystemSceneConfigurationLoader), "LoadResourcesAsync", typeof(Task), System.Type.EmptyTypes, "NavigationLoad");
		Hook(typeof(NavigationSystemSceneConfigurationLoader), "get_SceneConfiguration", typeof(HNSSceneConfiguration), System.Type.EmptyTypes, "NavigationGet");
		if (!validating)
		{
			Trace.Data["hookTargets"] = targets.ToArray();
		}
	}

	private static void NativePrefabsDeclared(GhostSpawnerAuthoring __instance)
	{
		try
		{
			NativePrefabSources.Capture(__instance);
		}
		catch (System.Exception e)
		{
			Trace.Error("native prefab source", e);
		}
	}

	private static void Repository(ILoadableRepository __0, Il2CppStructArray<byte> __1)
	{
		try
		{
			Repositories.Enqueue(__0);
		}
		catch (System.Exception e)
		{
			Trace.Error("repository", e);
		}
	}

	private static void RegisterMap(GlobalMapManager __instance)
	{
		try
		{
			Repositories.Drain();
			Repositories.Register(__instance);
		}
		catch (System.Exception e)
		{
			Trace.Error("map registration", e);
		}
	}

	private static void Locations(GlobalMapManager __instance)
	{
		try
		{
			Menu.Locations(__instance);
		}
		catch (System.Exception e)
		{
			Trace.Error("map node", e);
		}
	}

	private static void MenuCapture(GlobalMapFakeController __instance)
	{
		Menu.Capture(__instance);
	}

	private static void MenuTick(GlobalMapFakeController __instance)
	{
		Menu.Tick(__instance);
	}

	private static void MenuClose(GlobalMapFakeController __instance)
	{
		Menu.Close(__instance);
	}

	private static void PrepareScene(Game.SceneManagement.SceneManager __instance, string __0)
	{
		if (Identity.Own(__0))
		{
			Repositories.Drain();
			Repositories.RegisterScene(__instance);
		}
	}

	private static bool LoadScene(string __0, LoadSceneMode __1, bool __2, ref Task __result)
	{
		if (!SceneContract.OwnResource(__0))
		{
			return true;
		}
		try
		{
			__result = Loader.Begin(__1);
			return false;
		}
		catch (System.Exception e)
		{
			Trace.Error("scene start", e);
			__result = Loader.Fault(e);
			return false;
		}
	}

	private static void ServerReady(ServerBattleState __instance, ref bool __result)
	{
		if (Identity.Own(__instance?._configuration?.SessionInfo?.MapName))
		{
			WorldBridge.Bind(__instance);
			__result = __result && WorldBridge.Ready(__instance);
		}
	}

	private static void ClientCapture(ClientBattleState __instance)
	{
		if (Identity.Own(__instance?._mapName))
		{
			WorldBridge.Capture(__instance);
		}
	}

	private static void ClientBind(ClientBattleState __instance)
	{
		if (Identity.Own(__instance?._mapName))
		{
			WorldBridge.Bind(__instance);
		}
	}

	private static void ClientEnter(ClientBattleState __instance)
	{
		if (Identity.Own(__instance?._mapName))
		{
			WorldBridge.Bind(__instance);
		}
	}

	private static void ClientLeave(ClientBattleState __instance)
	{
		if (__instance != null)
		{
			WorldBridge.Release(__instance);
		}
	}

	private static void ServerCapture(ServerBattleState __instance)
	{
		if (Identity.Own(__instance?._configuration?.SessionInfo?.MapName))
		{
			WorldBridge.Capture(__instance);
		}
	}

	private static void ServerBind(ServerBattleState __instance)
	{
		if (Identity.Own(__instance?._configuration?.SessionInfo?.MapName))
		{
			WorldBridge.Bind(__instance);
		}
	}

	private static void ServerLeave(ServerBattleState __instance)
	{
		if (__instance != null)
		{
			WorldBridge.Release(__instance);
		}
	}

	private static bool NavigationLoad(NavigationSystemSceneConfigurationLoader __instance, ref Task __result)
	{
		if (!Identity.Own(__instance?._sceneManager?.CurrentScene?.Name))
		{
			return true;
		}
		__result = Loader.WaitForNavigation();
		return false;
	}

	private static bool NavigationGet(NavigationSystemSceneConfigurationLoader __instance, ref HNSSceneConfiguration __result)
	{
		if (!Identity.Own(__instance?._sceneManager?.CurrentScene?.Name))
		{
			return true;
		}
		__result = Loader.NavigationConfig;
		return false;
	}
}
