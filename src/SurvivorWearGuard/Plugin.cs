using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Core.Utils;
using Game.ItemsSystem;
using Game.ItemsSystem.ItemOperations;
using HarmonyLib;

namespace Frontier.SurvivorWearGuard;

[BepInPlugin("frontier.cargohunters.survivor.wearguard", "Frontier Survivor Wear Guard", "0.1.3")]
[BepInDependency("frontier.cargohunters.survivor", BepInDependency.DependencyFlags.HardDependency)]
public sealed class Plugin : BasePlugin
{
    private static ManualLogSource? logger;
    private static MethodInfo? activeGetter, humanMethod;
    private static FieldInfo? authorityField, clientField;
    private static long totalCalls, protectedCalls;

    public override void Load()
    {
        logger = Log;
        try
        {
            var t = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(x => x.GetName().Name == "Frontier.Survivor")
                ?.GetType("Frontier.Survivor.SurvivorRun", false);
            if (t == null) throw new MissingMemberException("Installed SurvivorRun not found");
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
            activeGetter = t.GetProperty("Active", flags)?.GetGetMethod(true);
            humanMethod = t.GetMethod("IsHuman", flags);
            authorityField = t.GetField("authority", flags);
            clientField = t.GetField("client", flags);
            if (activeGetter == null || humanMethod == null || authorityField == null || clientField == null)
                throw new MissingMemberException("Survivor 0.2.0 session/owner adapter changed");
            var target = AccessTools.Method(typeof(DecreaseConditionItemOperation), "DecreaseItemCondition");
            if (target == null || target.ReturnType != typeof(bool) || target.GetParameters().Length != 3
                || target.GetParameters()[0].ParameterType != typeof(ItemsOperationContext)
                || target.GetParameters()[1].ParameterType != typeof(IItem)
                || target.GetParameters()[2].ParameterType != typeof(float))
                throw new MissingMemberException("Native DecreaseItemCondition ABI changed; patch rejected");
            var harmony = new Harmony("frontier.survivor.wearguard.0.1.3");
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(Plugin), nameof(ConditionPrefix)));
            WearDiagnostics.Install(harmony, t, Log);
            Log.LogInfo("Survivor Wear Guard installed: native condition-decrease hook, fail-open outside Survivor.");
        }
        catch (Exception e)
        {
            activeGetter = null;
            Log.LogError("Wear Guard disabled without altering game state: " + e);
        }
    }

    // Return true unless the item belongs to an authenticated human participant
    // in this exact Survivor session. This patch is not a generic god mode.
    private static bool ConditionPrefix(
        DecreaseConditionItemOperation __instance,
        ItemsOperationContext __0,
        IItem __1,
        float __2,
        ref bool __result)
    {
        Interlocked.Increment(ref totalCalls);
        if (__instance==null || __0==null || __1==null) return true;
        if (WearDiagnostics.ProtectLegacy(__0,__instance.ItemId,__1,__2))
        {
            __result=true;
            Interlocked.Increment(ref protectedCalls);
            return false;
        }
        return true;
    }
}
