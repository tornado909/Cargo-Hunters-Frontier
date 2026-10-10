using System.Reflection;
using Frontier.Engine;
using HarmonyLib;

namespace Frontier.Core;

/// <summary>Only exact contracts observed in metadata 31 / game 0.26.38.59; no name-guessing fallback.</summary>
internal static class CoopHooks
{
    private static Plugin P => Plugin.Current;
    private static CoopController C => P.Coop;
    internal static readonly List<string> Resolved = new();
    private static HarmonyMethod Handler(string name) => new(typeof(CoopHooks).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!);
    public static void Install(Harmony harmony)
    {
        Type coop = Reflect.FindType("Game.CoopSystem.CoopService");
        Type steam = Reflect.FindType("Game.Application.Integrations.Steamworks.SteamworksService");
        Type battle = Reflect.FindType("Game.GameModes.BattleSession");
        Type connection = Reflect.FindType("Game.Application.Connections.BattleServerConnection");
        Type admission = Reflect.FindType("Game.Application.Server.SessionConnectionManager");
        Type itemManager = Reflect.FindType("Game.ItemsSystem.IItemsManager");
        if (Reflect.UniqueMethod(itemManager, "get_IsAuthoritative", 0).ReturnType != typeof(bool))
            throw new MissingMethodException("IItemsManager.IsAuthoritative bool contract missing");
        void Patch(Type type, string name, string[] parameters, Type returns, string? prefix = null, string? postfix = null, string? finalizer = null)
        {
            var method = type.GetMethods(Reflect.Flags).Single(m => m.Name == name &&
                m.GetParameters().Select(p => p.ParameterType.FullName).SequenceEqual(parameters));
            if (method.ReturnType != returns) throw new MissingMethodException(type.FullName, name);
            harmony.Patch(method, prefix: prefix is null ? null : Handler(prefix), postfix: postfix is null ? null : Handler(postfix),
                finalizer: finalizer is null ? null : Handler(finalizer));
            Resolved.Add(type.FullName + "." + name + "(" + string.Join(",", parameters) + ")");
        }
        // Constructors are selected by their complete known contract where available. Reflection
        // patching of generated constructors still needs native verification in BepInEx.
        ConstructorInfo coopCtor = coop.GetConstructors(Reflect.Flags).Single(c => c.GetParameters().Select(p => p.ParameterType.FullName).SequenceEqual(new[] {
            "Game.Application.Integrations.Steamworks.ISteamworksService", "Game.EventSystem.IEventPublisher",
            "Game.CoopSystem.ICoopAccountRequirementsChecker", "Game.CoopSystem.ICoopHostSessionStatus" }));
        harmony.Patch(coopCtor, postfix: Handler(nameof(CapturePostfix))); Resolved.Add(coop.FullName + ".ctor(4)");
        MethodInfo create = Reflect.UniqueMethod(coop, "CreateLobby", 5);
        if (create.ReturnType != typeof(void) || create.GetParameters()[0].ParameterType.FullName != "Game.CoopSystem.Data.LobbyVisibility" ||
            create.GetParameters()[1].ParameterType != typeof(string) || create.GetParameters()[2].ParameterType != typeof(string) ||
            create.GetParameters()[3].ParameterType != typeof(bool)) throw new MissingMethodException("CreateLobby contract changed");
        harmony.Patch(create, prefix: Handler(nameof(CapturePostfix))); Resolved.Add(coop.FullName + ".CreateLobby(5)");
        Patch(coop, "SetCurrentLobby", new[] { "Game.CoopSystem.Data.LobbyId", "Game.CoopSystem.CoopRole" }, typeof(void), postfix: nameof(LobbyPostfix));
        Patch(coop, "Dispose", Array.Empty<string>(), typeof(void), postfix: nameof(DisposePostfix));
        Patch(steam, "Tick", Array.Empty<string>(), typeof(void), postfix: nameof(TickPostfix));
        foreach (string t in new[] { "Game.CoopSystem.Data.LobbyInfo", "Game.CoopSystem.Data.LobbyId" })
            Patch(coop, "IsVersionCompatible", new[] { t }, typeof(bool), postfix: nameof(CompatiblePostfix));
        foreach (string name in new[] { "CanStartPrepareForSession", "CanStartSession" })
            Patch(coop, name, Array.Empty<string>(), typeof(bool), postfix: nameof(CanPreparePostfix));
        Patch(coop, "StartSession", new[] { "System.String" }, typeof(void), nameof(StartPrefix), nameof(StartPostfix), nameof(StartFinalizer));
        Patch(coop, "SessionIsReady", Array.Empty<string>(), typeof(void), prefix: nameof(SessionReadyPrefix));
        Patch(coop, "ClearSession", Array.Empty<string>(), typeof(void), postfix: nameof(ClearPostfix));
        MethodInfo join = Reflect.UniqueMethod(coop, "JoinLobby", 2);
        if (join.ReturnType != typeof(void) || join.GetParameters()[0].ParameterType.FullName != "Game.CoopSystem.Data.LobbyId" ||
            join.GetParameters()[1].ParameterType.GetMethods(Reflect.Flags).Count(m => m.Name == "Invoke" && m.GetParameters().Length == 1 &&
                m.GetParameters()[0].ParameterType.FullName == "Game.CoopSystem.Data.LobbyJoinResult") != 1)
            throw new MissingMethodException("JoinLobby/LobbyJoinResult callback contract missing");
        harmony.Patch(join, prefix: Handler(nameof(JoinPrefix))); Resolved.Add(coop.FullName + ".JoinLobby(LobbyId,Action<LobbyJoinResult>)");
        Patch(connection, "Connect", new[] { "Game.Application.Connections.ServerAddress" }, typeof(void), prefix: nameof(ConnectPrefix));
        Patch(connection, "Reconnect", Array.Empty<string>(), typeof(void), prefix: nameof(ReconnectPrefix));
        Patch(connection, "Disconnect", Array.Empty<string>(), typeof(void), prefix: nameof(DisconnectPrefix));
        Patch(admission, "TryPlayerConnection", new[] { "Game.Application.Server.PlayerConnectionInfo", "Unity.Entities.EntityCommandBuffer",
            "Game.GameModes.IServerGameMode", "Unity.Entities.World" }, typeof(bool), prefix: nameof(AdmissionPrefix));
        // Six-argument BattleSession constructor contains a nullable interface parameter after stripping;
        // validate the other five parameters exactly and require only one candidate.
        var battleCtor = battle.GetConstructors(Reflect.Flags).Single(c => c.GetParameters().Length == 6 &&
            c.GetParameters().Take(5).Select(p => p.ParameterType.FullName).SequenceEqual(new[] { "System.Boolean", "Unity.Entities.EntityManager",
                "Game.ItemsSystem.IItemTemplatesRepository", "Core.Network.IOperationSender", "Game.EventSystem.EventManager" }));
        harmony.Patch(battleCtor, postfix: Handler(nameof(BattlePostfix))); Resolved.Add(battle.FullName + ".ctor(bool,EntityManager,...,6)");
        C.HooksResolved = true;
    }
    private static void CapturePostfix(object __instance) => C.Capture(__instance);
    private static void LobbyPostfix(object __instance) { C.Capture(__instance); C.Tick(true); }
    private static void DisposePostfix(object __instance) => C.Release(__instance);
    private static void TickPostfix() => C.Tick();
    private static void CompatiblePostfix(object[] __args, ref bool __result)
    {
        if (!P.Active || !__result) return; // Never turn a native version/requirements failure into success.
        try { __result = C.IsCompatible(__args[0]); }
        catch (Exception e) { __result = false; C.Error(e); }
    }
    private static void CanPreparePostfix(object __instance, ref bool __result)
    {
        C.Capture(__instance);
        if (!P.Active || !__result) return;
        try { __result = C.CanPrepare(); }
        catch (Exception e) { __result = false; C.Error(e); }
    }
    private static bool JoinPrefix(object __instance, object[] __args)
    {
        C.Capture(__instance);
        if (!P.Active) return true;
        try
        {
            if (C.IsCompatible(__args[0])) return true;
            C.RejectJoin(__args[1], "Identical active Frontier pack required. Wait for lobby data, refresh and retry. " + C.LastError);
        }
        catch (Exception e)
        {
            C.Error(e);
            try { C.RejectJoin(__args[1], e.GetBaseException().Message); } catch (Exception callbackError) { C.Error(callbackError); }
        }
        return false;
    }
    private static bool StartPrefix(object __instance, object[] __args, out bool __state)
    {
        C.Capture(__instance); __state = false;
        if (!P.Active) return true;
        try
        {
            if (C.BeginDeployment(out __state)) { C.ClearDeferredStart(); return true; }
            C.DeferClientStart(__instance, (string)__args[0]); return false;
        }
        catch (Exception e) { C.Error(e); return false; }
    }
    private static void StartPostfix(bool __state)
    {
        if (!P.Active || !__state) return;
        try { C.NativeStartCompleted(__state); } catch (Exception e) { C.Error(e); }
    }
    private static Exception? StartFinalizer(Exception? __exception, bool __state)
    {
        if (__exception is not null && __state)
        {
            try { C.RollbackDeployment(); } catch (Exception e) { C.Error(e); }
            C.Error(__exception);
        }
        return __exception; // Do not swallow the game's own exceptions.
    }
    private static bool SessionReadyPrefix(object __instance)
    {
        C.Capture(__instance);
        if (!P.Active) return true;
        try { return C.CanPublishSessionReady(); } catch (Exception e) { C.Error(e); return false; }
    }
    private static void ClearPostfix(object __instance)
    {
        C.Capture(__instance);
        if (!P.Active) return;
        try { C.SessionCleared(); } catch (Exception e) { C.Error(e); }
    }
    private static bool ConnectPrefix(object __instance, object[] __args)
    {
        if (!P.Active) return true;
        try
        {
            if (C.CanConnect(__args[0])) { C.ClearDeferredConnection(__instance); return true; }
            C.DeferClientConnection(__instance, __args[0], false); return false;
        }
        catch (Exception e) { C.Error(e); return false; }
    }
    private static bool ReconnectPrefix(object __instance)
    {
        if (!P.Active) return true;
        try
        {
            object address = Reflect.Read(__instance, "_serverAddress") ?? throw new InvalidDataException("Missing reconnect address");
            if (C.CanConnect(address)) { C.ClearDeferredConnection(__instance); return true; }
            C.DeferClientConnection(__instance, address, true); return false;
        }
        catch (Exception e) { C.Error(e); return false; }
    }
    private static void DisconnectPrefix(object __instance) => C.ClearDeferredConnection(__instance);
    private static bool AdmissionPrefix(object __instance, ref bool __result)
    {
        if (!P.Active) return true;
        try { if (C.CanAdmitPlayer(__instance)) return true; }
        catch (Exception e) { C.Error(e); }
        __result = false; return false;
    }
    private static void BattlePostfix(object[] __args)
    {
        if (!P.Active || __args[0] is not true) return;
        P.ResetEnemySession();
    }
}
