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
[BepInPlugin("frontier.cargohunters.survivor.exitguard", "Frontier Survivor Exit Guard", "0.1.2")]
[BepInDependency("frontier.cargohunters.survivor", BepInDependency.DependencyFlags.HardDependency)]
public sealed class Plugin : BasePlugin
{
    private static readonly ExitGate gate = new();
    private static ManualLogSource? logger;
    private static FieldInfo? authorityWorldField, clientWorldField, authorityField;
    private static MethodInfo? runIdGetter;
    private static string exitingRunId = "";
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
            runIdGetter = run.GetProperty("RunId", flags)?.GetGetMethod(true);
            var tick = run.GetMethod("Tick", flags);
            if (tick == null || tick.ReturnType != typeof(void) || tick.GetParameters().Length != 0 ||
                authorityWorldField == null || clientWorldField == null || authorityField == null || runIdGetter == null)
                throw new MissingMemberException("SurvivorRun 0.2.0 contract mismatch");
            var h = new Harmony("frontier.survivor.exitguard.0.1.2");
            h.Patch(tick, prefix: new HarmonyMethod(typeof(Plugin), nameof(BeforeTick)));
            Register(h, typeof(ClientBattleState), "EnterExitState", nameof(ClientExit));
            Register(h, typeof(ClientBattleState), "EnterResultsState", nameof(ClientExit));
            Register(h, typeof(ClientBattleState), "EnterConnectionLostState", nameof(ClientExit));
            Register(h, typeof(ClientBattleState), "LeaveBattleState", nameof(ClientExit));
            Register(h, typeof(ClientBattleState), "Leave", nameof(ClientExit));
            Register(h, typeof(ClientBattleState), "Shutdown", nameof(ClientExit));
            Register(h, typeof(ClientBattleState), "EnterBattleState", nameof(ClientEntered), postfix: true);
            logger.LogInfo("Survivor Exit Guard v0.1.2 installed: lifecycle shutdown-before-unload and Translation component preflight.");
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

    private static string ReadRunId()
    {
        try { return runIdGetter?.Invoke(null, null) as string ?? ""; }
        catch { return ""; }
    }

    private static bool BoundSurvivorWorld()
    {
        // Managed reflection only; no read from a potentially invalid ECS EntityManager.
        try { return authorityWorldField?.GetValue(null) != null || clientWorldField?.GetValue(null) != null; }
        catch { return false; }
    }

    private static void ArmExit(string eventName)
    {
        if (!gate.Exit()) return;
        exitingRunId = ReadRunId();
        logger?.LogInfo("SURVIVOR_EXIT_GUARD_ARMED: " + eventName + "; no more SurvivorRun.Tick during teardown.");
    }

    // Lifecycle hooks may also be invoked by IL2CPP class initialization,
    // *before* the native battle configuration has been constructed. Never
    // dereference __instance._configuration, _mapName or SessionInfo here.
    // The managed SurvivorRun world fields are populated only for our map.
    private static void ClientExit()
    {
        if (BoundSurvivorWorld()) ArmExit("ClientBattleState");
    }

    private static void ServerExit()
    {
        if (BoundSurvivorWorld()) ArmExit("ServerBattleState");
    }

    private static void ClientEntered()
    {
        if (!BoundSurvivorWorld()) return;
        gate.Enter();
        exitingRunId = "";
        nextCheck = 0f;
        logger?.LogInfo("SURVIVOR_EXIT_GUARD_REARMED: ClientBattleState.EnterBattleState");
    }

    private static void ServerEntered()
    {
        if (!BoundSurvivorWorld()) return;
        gate.Enter();
        exitingRunId = "";
        nextCheck = 0f;
    }

    private static bool BeforeTick()
    {
        Interlocked.Increment(ref prefixedTicks);
        if (gate.Stopping)
        {
            // New Survivor run IDs are generated by the original Bind().
            // Restore ticking for a subsequent raid even if a native enter
            // callback occurred before WorldBridge completed binding.
            string current = ReadRunId();
            if (!string.IsNullOrEmpty(current) && current != exitingRunId && BoundSurvivorWorld())
            {
                gate.Enter();
                exitingRunId = "";
                nextCheck = 0f;
                logger?.LogInfo("SURVIVOR_EXIT_GUARD_REARMED: new run ID");
            }
            else
            {
                long n = Interlocked.Increment(ref rejectedTicks);
                if (n == 1 || n % 500 == 0)
                    logger?.LogInfo("SURVIVOR_TICK_SKIPPED_DURING_EXIT: " + n);
                return false;
            }
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
