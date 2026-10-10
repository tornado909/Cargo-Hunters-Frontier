using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace Frontier.Core;

internal static class CoopHooks
{
	internal static readonly List<string> Resolved = new List<string>();

	private static Plugin P => Plugin.Current;

	private static CoopController C => P.Coop;

	private static HarmonyMethod Handler(string name)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Expected O, but got Unknown
		return new HarmonyMethod(typeof(CoopHooks).GetMethod(name, (BindingFlags)40));
	}

	public static void Install(Harmony harmony)
	{
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_0398: Unknown result type (might be due to invalid IL or missing references)
		System.Type type = Reflect.FindType("Game.CoopSystem.CoopService");
		System.Type type2 = Reflect.FindType("Game.Application.Integrations.Steamworks.SteamworksService");
		System.Type type3 = Reflect.FindType("Game.Application.Connections.BattleServerConnection");
		System.Type type4 = Reflect.FindType("Game.Application.Server.SessionConnectionManager");
		if (Reflect.UniqueMethod(Reflect.FindType("Game.ItemsSystem.IItemsManager"), "get_IsAuthoritative", 0).ReturnType != typeof(bool))
		{
			throw new MissingMethodException("IItemsManager.IsAuthoritative bool contract missing");
		}
		MethodInfo val = Reflect.UniqueMethod(type, "CreateLobby", 5);
		if (val.ReturnType != typeof(void) || ((MethodBase)val).GetParameters()[0].ParameterType.FullName != "Game.CoopSystem.Data.LobbyVisibility" || ((MethodBase)val).GetParameters()[1].ParameterType != typeof(string) || ((MethodBase)val).GetParameters()[2].ParameterType != typeof(string) || ((MethodBase)val).GetParameters()[3].ParameterType != typeof(bool))
		{
			throw new MissingMethodException("CreateLobby contract changed");
		}
		harmony.Patch((MethodBase)(object)val, Handler("CapturePostfix"), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		Resolved.Add(type.FullName + ".CreateLobby(5)");
		Patch(type, "SetCurrentLobby", new string[2] { "Game.CoopSystem.Data.LobbyId", "Game.CoopSystem.CoopRole" }, typeof(void), null, "LobbyPostfix");
		Patch(type, "Dispose", System.Array.Empty<string>(), typeof(void), "DisposePostfix");
		Patch(type2, "Tick", System.Array.Empty<string>(), typeof(void), null, "TickPostfix");
		Patch(type, "LeaveLobby", System.Array.Empty<string>(), typeof(void), "LeaveLobbyPrefix");
		string[] array = new string[2] { "Game.CoopSystem.Data.LobbyInfo", "Game.CoopSystem.Data.LobbyId" };
		foreach (string text in array)
		{
			Patch(type, "IsVersionCompatible", new string[1] { text }, typeof(bool), null, "CompatiblePostfix");
		}
		array = new string[2] { "CanStartPrepareForSession", "CanStartSession" };
		foreach (string name in array)
		{
			Patch(type, name, System.Array.Empty<string>(), typeof(bool), null, "CanPreparePostfix");
		}
		Patch(type, "StartSession", new string[1] { "System.String" }, typeof(void), "StartPrefix", "StartPostfix", "StartFinalizer");
		Patch(type, "SessionIsReady", System.Array.Empty<string>(), typeof(void), "SessionReadyPrefix");
		Patch(type, "ClearSession", System.Array.Empty<string>(), typeof(void), null, "ClearPostfix");
		MethodInfo val2 = Reflect.UniqueMethod(type, "JoinLobby", 2);
		if (val2.ReturnType != typeof(void) || ((MethodBase)val2).GetParameters()[0].ParameterType.FullName != "Game.CoopSystem.Data.LobbyId" || Enumerable.Count<MethodInfo>((System.Collections.Generic.IEnumerable<MethodInfo>)((MethodBase)val2).GetParameters()[1].ParameterType.GetMethods((BindingFlags)60), (Func<MethodInfo, bool>)((MethodInfo m) => ((MemberInfo)m).Name == "Invoke" && ((MethodBase)m).GetParameters().Length == 1 && ((MethodBase)m).GetParameters()[0].ParameterType.FullName == "Game.CoopSystem.Data.LobbyJoinResult")) != 1)
		{
			throw new MissingMethodException("JoinLobby/LobbyJoinResult callback contract missing");
		}
		harmony.Patch((MethodBase)(object)val2, Handler("JoinPrefix"), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		Resolved.Add(type.FullName + ".JoinLobby(LobbyId,Action<LobbyJoinResult>)");
		Patch(type3, "Connect", new string[1] { "Game.Application.Connections.ServerAddress" }, typeof(void), "ConnectPrefix");
		Patch(type3, "Reconnect", System.Array.Empty<string>(), typeof(void), "ReconnectPrefix");
		Patch(type3, "Disconnect", System.Array.Empty<string>(), typeof(void), "DisconnectPrefix");
		Patch(type4, "TryPlayerConnection", new string[4] { "Game.Application.Server.PlayerConnectionInfo", "Unity.Entities.EntityCommandBuffer", "Game.GameModes.IServerGameMode", "Unity.Entities.World" }, typeof(bool), "AdmissionPrefix");
		Patch(Reflect.FindType("Game.Application.Client.ClientShelterState"), "Enter", System.Array.Empty<string>(), typeof(void), "ShelterEnterPrefix");
		Patch(Reflect.FindType("Game.Application.Client.ClientShelterState"), "ConnectToShelter", System.Array.Empty<string>(), typeof(void), "ShelterConnectionPrefix", null, "ShelterConnectionFinalizer");
		Patch(Reflect.FindType("Game.Application.Client.ClientShelterState"), "Leave", System.Array.Empty<string>(), typeof(void), "ShelterLeavePrefix");
		Patch(Reflect.FindType("Game.Application.Client.ClientLobbyState"), "Enter", System.Array.Empty<string>(), typeof(void), "MenuEnterPrefix");
		Patch(Reflect.FindType("Game.Application.Client.ClientConnectToBattleServerState"), "Enter", System.Array.Empty<string>(), typeof(void), "RaidConnectPrefix");
		System.Type type5 = Reflect.FindType("Game.Application.Client.ClientBattleState");
		Patch(type5, "EnterBattleState", System.Array.Empty<string>(), typeof(void), null, "RaidEnterPostfix");
		array = new string[6] { "LeaveBattleState", "Leave", "Shutdown", "EnterResultsState", "EnterConnectionLostState", "EnterExitState" };
		foreach (string name2 in array)
		{
			Patch(type5, name2, System.Array.Empty<string>(), typeof(void), "RaidExitPrefix");
		}
		C.HooksResolved = true;
		((BasePlugin)P).Log.LogInfo((object)"Co-op lifecycle v10.6: client+host reset; owner-bound lobby header; lobby-only native handover; no live raid migration");
		[CompilerGenerated]
		void Patch(System.Type type6, string text2, string[] parameters, System.Type returns, string? prefix = null, string? postfix = null, string? finalizer = null)
		{
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			MethodInfo val3 = Enumerable.Single<MethodInfo>((System.Collections.Generic.IEnumerable<MethodInfo>)type6.GetMethods((BindingFlags)60), (Func<MethodInfo, bool>)((MethodInfo m) => ((MemberInfo)m).Name == text2 && Enumerable.SequenceEqual<string>(Enumerable.Select<ParameterInfo, string>((System.Collections.Generic.IEnumerable<ParameterInfo>)((MethodBase)m).GetParameters(), (Func<ParameterInfo, string>)((ParameterInfo p) => p.ParameterType.FullName)), (System.Collections.Generic.IEnumerable<string>)parameters)));
			if (val3.ReturnType != returns)
			{
				throw new MissingMethodException(type6.FullName, text2);
			}
			harmony.Patch((MethodBase)(object)val3, (prefix == null) ? null : Handler(prefix), (postfix == null) ? null : Handler(postfix), (HarmonyMethod)null, (finalizer == null) ? null : Handler(finalizer), (HarmonyMethod)null);
			Resolved.Add(string.Concat(new string[6]
			{
				type6.FullName,
				".",
				text2,
				"(",
				string.Join(",", parameters),
				")"
			}));
		}
	}

	private static void MenuEnterPrefix()
	{
		if (P.Active)
		{
			C.EnterMenu();
		}
	}

	private static void RaidConnectPrefix()
	{
		if (P.Active)
		{
			C.BeginRaidConnection();
		}
	}

	private static void RaidEnterPostfix()
	{
		if (P.Active)
		{
			C.EnterRaid();
		}
	}

	private static void RaidExitPrefix()
	{
		if (P.Active)
		{
			C.ExitRaid();
		}
	}

	private static void LeaveLobbyPrefix(object __instance)
	{
		if (P.Active)
		{
			C.LeaveLobbyIntent(__instance);
		}
	}

	private static void ShelterEnterPrefix()
	{
		if (P.Active)
		{
			C.EnterShelter();
		}
	}

	private static void ShelterLeavePrefix()
	{
		if (P.Active)
		{
			C.LeaveShelter();
		}
	}

	private static void ShelterConnectionPrefix(object __instance)
	{
		if (!P.Active)
		{
			return;
		}
		try
		{
			C.CaptureShelterConnection(__instance);
		}
		catch (System.Exception error)
		{
			C.LeaveShelter();
			C.Error(error);
		}
	}

	private static System.Exception? ShelterConnectionFinalizer(object __instance, System.Exception? __exception)
	{
		if (__exception != null && P.Active)
		{
			try
			{
				C.ShelterNativeCallFailed(__instance, __exception);
			}
			catch (System.Exception error)
			{
				C.LeaveShelter();
				C.Error(error);
			}
		}
		return __exception;
	}

	private static void CapturePostfix(object __instance)
	{
		C.Capture(__instance);
	}

	private static void LobbyPostfix(object __instance)
	{
		C.Capture(__instance);
		C.LobbyChanged(__instance);
	}

	private static void DisposePostfix(object __instance)
	{
		C.Release(__instance);
	}

	private static void TickPostfix()
	{
		C.Tick();
	}

	private static void CompatiblePostfix(object[] __args, ref bool __result)
	{
		if (!P.Active || !__result)
		{
			return;
		}
		try
		{
			__result = C.IsCompatible(__args[0]);
		}
		catch (System.Exception error)
		{
			__result = false;
			C.Error(error);
		}
	}

	private static void CanPreparePostfix(object __instance, ref bool __result)
	{
		C.Capture(__instance);
		if (!P.Active || !__result)
		{
			return;
		}
		try
		{
			__result = C.CanPrepare();
		}
		catch (System.Exception error)
		{
			__result = false;
			C.Error(error);
		}
	}

	private static bool JoinPrefix(object __instance, object[] __args)
	{
		C.Capture(__instance);
		if (!P.Active)
		{
			return true;
		}
		try
		{
			if (C.IsCompatible(__args[0]))
			{
				return true;
			}
			C.RejectJoin(__args[1], "Identical active Frontier pack required. Wait for lobby data, refresh and retry. " + C.LastError);
		}
		catch (System.Exception ex)
		{
			C.Error(ex);
			try
			{
				C.RejectJoin(__args[1], ex.GetBaseException().Message);
			}
			catch (System.Exception error)
			{
				C.Error(error);
			}
		}
		return false;
	}

	private static bool StartPrefix(object __instance, object[] __args, out bool __state)
	{
		C.Capture(__instance);
		__state = false;
		if (!P.Active)
		{
			return true;
		}
		try
		{
			if (C.BeginDeployment(out __state))
			{
				C.ClearDeferredStart();
				return true;
			}
			C.DeferClientStart(__instance, (string)__args[0]);
			return false;
		}
		catch (System.Exception error)
		{
			C.Error(error);
			return false;
		}
	}

	private static void StartPostfix(bool __state)
	{
		if (!P.Active || !__state)
		{
			return;
		}
		try
		{
			C.NativeStartCompleted(__state);
		}
		catch (System.Exception error)
		{
			C.Error(error);
		}
	}

	private static System.Exception? StartFinalizer(System.Exception? __exception, bool __state)
	{
		if (__exception != null && __state)
		{
			try
			{
				C.RollbackDeployment();
			}
			catch (System.Exception error)
			{
				C.Error(error);
			}
			C.Error(__exception);
		}
		return __exception;
	}

	private static bool SessionReadyPrefix(object __instance)
	{
		C.Capture(__instance);
		if (!P.Active)
		{
			return true;
		}
		try
		{
			return C.CanPublishSessionReady();
		}
		catch (System.Exception error)
		{
			C.Error(error);
			return false;
		}
	}

	private static void ClearPostfix(object __instance)
	{
		C.Capture(__instance);
		if (!P.Active)
		{
			return;
		}
		try
		{
			C.SessionCleared();
		}
		catch (System.Exception error)
		{
			C.Error(error);
		}
	}

	private static bool ConnectPrefix(object __instance, object[] __args)
	{
		if (!P.Active)
		{
			return true;
		}
		try
		{
			if (C.CanConnect(__instance, __args[0]))
			{
				C.ClearDeferredConnection(__instance);
				return true;
			}
			C.DeferClientConnection(__instance, __args[0], reconnect: false);
			return false;
		}
		catch (System.Exception error)
		{
			C.Error(error);
			return false;
		}
	}

	private static bool ReconnectPrefix(object __instance)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		if (!P.Active)
		{
			return true;
		}
		try
		{
			object address = Reflect.Read(__instance, "_serverAddress") ?? throw new InvalidDataException("Missing reconnect address");
			if (C.CanConnect(__instance, address))
			{
				C.ClearDeferredConnection(__instance);
				return true;
			}
			C.DeferClientConnection(__instance, address, reconnect: true);
			return false;
		}
		catch (System.Exception error)
		{
			C.Error(error);
			return false;
		}
	}

	private static void DisconnectPrefix(object __instance)
	{
		C.ConnectionDisconnected(__instance);
	}

	private static bool AdmissionPrefix(object __instance, ref bool __result)
	{
		if (!P.Active)
		{
			return true;
		}
		try
		{
			if (C.CanAdmitPlayer(__instance))
			{
				return true;
			}
		}
		catch (System.Exception error)
		{
			C.Error(error);
		}
		__result = false;
		return false;
	}
}
