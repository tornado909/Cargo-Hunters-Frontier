using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Game.Application.Client;
using Game.Application.Server;
using Game.GameModes;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Frontier.SurvivorExitGuard;

/// <summary>
/// Fixes one observed Survivor 0.2.0 crash boundary:
/// SurvivorRun.Tick() can fetch Translation from a world/entity that is
/// already being unloaded during evacuation (native AccessViolation).
/// </summary>
[BepInPlugin("frontier.cargohunters.survivor.exitguard", "Frontier Survivor Exit Guard", "0.1.0")]
[BepInDependency("frontier.cargohunters.survivor", BepInDependency.DependencyFlags.HardDependency)]
public sealed class Plugin : BasePlugin
{
    private static readonly ExitGate gate = new();
    private static ManualLogSource? logger;
    private static FieldInfo? authorityWorldField, clientWorldField, authorityField;
    private static long prefixedTicks, rejectedTicks, missingComponents, errors;
    private static float nextCheck;
    private const string SurvivorMap = "Frontier_Survivor";

    public override void Load()
    {
        logger = Log;
        try
        {
            var run = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(x => x.GetName().Name == "Frontier.Survivor")
                ?.GetType("Frontier.Survivor.SurvivorRun", false);
            if (run == null)
                throw new MissingMemberException("Installed Frontier.Survivor.SurvivorRun not found");
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
            authorityWorldField = run.GetField("authorityWorld", flags);
            clientWorldField = run.GetField("clientWorld", flags);
            authorityField = run.GetField("authority", flags);
            var tick = run.GetMethod("Tick", flags);
            if (tick == null || tick.ReturnType != typeof(void) || tick.GetParameters().Length != 0 ||
                authorityWorldField == null || clientWorldField == null || authorityField == null)
                throw new MissingMemberException("SurvivorRun 0.2.0 contract mismatch");
            var h = new Harmony("frontier.survivor.exitguard.0.1.0");
            h.Patch(tick, prefix: new HarmonyMethod(typeof(Plugin), nameof(BeforeTick)));
            Register(h, typeof(ClientBattleState), "EnterExitState", nameof(ClientExit));
            Register(h, typeof(ClientBattleState), "EnterResultsState", nameof(ClientExit));
            Register(h, typeof(ClientBattleState), "EnterConnectionLostState", nameof(ClientExit));
            Register(h, typeof(ClientBattleState), "LeaveBattleState", nameof(ClientExit));
            Register(h, typeof(ClientBattleState), "Leave", nameof(ClientExit));
            Register(h, typeof(ClientBattleState), "Shutdown", nameof(ClientExit));
            Register(h, typeof(ServerBattleState), "EnterExitState", nameof(ServerExit));
            Register(h, typeof(ServerBattleState), "LeaveBattleState", nameof(ServerExit));
            Register(h, typeof(ServerBattleState), "Leave", nameof(ServerExit));
            Register(h, typeof(ClientBattleState), "EnterBattleState", nameof(ClientEntered), postfix: true);
            Register(h, typeof(ServerBattleState), "EnterBattleState", nameof(ServerEntered), postfix: true);
            logger.LogInfo("Survivor Exit Guard v0.1.0 installed: lifecycle shutdown-before-unload and Translation component preflight.");
        }
        catch (Exception e)
        {
            // Never alter unrelated battle states if expected APIs are missing.
            logger.LogError("Survivor Exit Guard initialization rejected: " + e);
        }
    }

    private static void Register(Harmony harmony, Type type, string method, string callback, bool postfix = false)
    {
        MethodInfo? target = AccessTools.Method(type, method, Type.EmptyTypes);
        if (target == null || target.ReturnType != typeof(void))
            throw new MissingMemberException(type.FullName + "." + method + " (void, zero args)");
        var hm = new HarmonyMethod(typeof(Plugin), callback);
        if (postfix) harmony.Patch(target, postfix: hm);
        else harmony.Patch(target, prefix: hm);
    }

    private static bool SameMap(string? name) =>
        string.Equals(name, SurvivorMap, StringComparison.Ordinal);

    private static bool BoundSurvivorWorld()
    {
        // Managed reflection only; no read from a potentially invalid ECS EntityManager.
        try { return authorityWorldField?.GetValue(null) != null || clientWorldField?.GetValue(null) != null; }
        catch { return false; }
    }

    private static void ArmExit(string eventName)
    {
        if (!gate.Exit()) return;
        logger?.LogInfo("SURVIVOR_EXIT_GUARD_ARMED: " + eventName + "; no more SurvivorRun.Tick during teardown.");
    }

    private static void ClientExit(ClientBattleState __instance)
    {
        // Map comparison is stable string metadata, not an ECS read.
        if (__instance != null && SameMap(__instance._mapName)) ArmExit("ClientBattleState");
    }

    private static void ServerExit(ServerBattleState __instance)
    {
        // Some disconnect/evac sequences dispose the server world first.
        // Check only native map metadata before accessing an entity manager.
        if (__instance != null && SameMap(__instance._configuration?.SessionInfo?.MapName))
            ArmExit("ServerBattleState");
    }

    private static void ClientEntered(ClientBattleState __instance)
    {
        if (__instance == null || !SameMap(__instance._mapName)) return;
        gate.Enter();
        nextCheck = 0f;
        logger?.LogInfo("SURVIVOR_EXIT_GUARD_REARMED: ClientBattleState.EnterBattleState");
    }

    private static void ServerEntered(ServerBattleState __instance)
    {
        if (__instance == null || !SameMap(__instance._configuration?.SessionInfo?.MapName)) return;
        gate.Enter();
        nextCheck = 0f;
    }

    private static bool BeforeTick()
    {
        Interlocked.Increment(ref prefixedTicks);
        if (gate.Stopping)
        {
            long n = Interlocked.Increment(ref rejectedTicks);
            if (n == 1 || n % 500 == 0)
                logger?.LogInfo("SURVIVOR_TICK_SKIPPED_DURING_EXIT: " + n);
            return false;
        }
        try
        {
            // Missing world is normal while loading; the original Tick handles it.
            if (!BoundSurvivorWorld()) return true;
            var serverWorld = authorityWorldField?.GetValue(null) as World;
            var clientWorld = clientWorldField?.GetValue(null) as World;
            if ((serverWorld != null && (((Il2CppObjectBase)serverWorld).WasCollected || !serverWorld.IsCreated))
                || (clientWorld != null && (((Il2CppObjectBase)clientWorld).WasCollected || !clientWorld.IsCreated)))
            {
                ArmExit("world-unloaded");
                return false;
            }
            if (serverWorld == null) return true; // Remote client: no local authoritative ECS reads.
            // The original SurvivorRun.Tick updates every 0.2 s; limit preflight accordingly.
            if (Time.realtimeSinceStartup < nextCheck) return true;
            nextCheck = Time.realtimeSinceStartup + 0.09f;
            var session = authorityField?.GetValue(null) as BattleSession;
            if (session == null || !session.IsBattleSession) return true;
            var manager = serverWorld.EntityManager;
            var ids = new Il2CppSystem.Collections.Generic.List<int>();
            session.GetPlayerIds(ids);
            if (ids.Count > 4096)
            {
                ArmExit("invalid-player-count");
                return false;
            }
            for (int i = 0; i < ids.Count; i++)
            {
                int id = ids[i];
                if (session.IsBotPlayer(id) || !session.GetEntityHasBeenSpawned(id)
                    || session.IsPlayerIsDead(id)) continue;
                Entity entity = session.GetPlayerEntity(id);
                if (entity == Entity.Null || !manager.Exists(entity)) continue;
                // The original code checks Exists() but omits HasComponent.
                // A surviving entity may have already lost Translation on evacuation.
                if (!manager.HasComponent<Translation>(entity))
                {
                    long missing = Interlocked.Increment(ref missingComponents);
                    if (missing <= 4 || missing % 100 == 0)
                        logger?.LogWarning("SURVIVOR_TICK_SKIPPED_NO_TRANSLATION: count=" + missing);
                    return false;
                }
            }
            return true;
        }
        catch (Exception e)
        {
            // Do not let the original Tick run on a partially destroyed world.
            long n = Interlocked.Increment(ref errors);
            if (n <= 5 || n % 100 == 0)
                logger?.LogWarning("SURVIVOR_TICK_PREFLIGHT_FAILED: " + e.GetType().Name + ": " + e.Message);
            return false;
        }
    }
}
