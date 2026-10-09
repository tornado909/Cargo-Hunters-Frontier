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

[BepInPlugin("frontier.cargohunters.survivor.wearguard", "Frontier Survivor Wear Guard", "0.1.0")]
[BepInDependency("frontier.cargohunters.survivor", BepInDependency.DependencyFlags.HardDependency)]
public sealed class Plugin : BasePlugin
{
    private static ManualLogSource? logger;
    private static MethodInfo? activeGetter, humanMethod;
    private static FieldInfo? authorityField, clientField;
    private static long totalCalls, protectedCalls, failures;

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
            new Harmony("frontier.survivor.wearguard.0.1.0")
                .Patch(target, prefix: new HarmonyMethod(typeof(Plugin), nameof(ConditionPrefix)));
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
        if (activeGetter == null || __instance == null || __0 == null || __1 == null
            || !float.IsFinite(__2) || __2 <= 0f || !__0.IsBattleSession)
            return true;
        try
        {
            var active = (bool)(activeGetter.Invoke(null, null) ?? false);
            if (!active) return true;
            var session = authorityField?.GetValue(null) ?? clientField?.GetValue(null);
            if (session == null) return true;
            // The game decides ownership. Never infer it from character proximity.
            var owner = __0.FindOwnerPlayerId(__instance.ItemId);
            if (owner == null || !owner.HasValue) return true;
            var playerId = owner.Value;
            var human = (bool)(humanMethod?.Invoke(null, new[] { session, (object)playerId }) ?? false);
            if (!human) return true;
            var equipped = __0.FindEquipmentByPlayerId(playerId);
            var containing = __0.FindEquipmentContainingItem(__instance.ItemId);
            var ownsEquipment = equipped != null && containing != null &&
                string.Equals(equipped.Id.ToString(), containing.Id.ToString(), StringComparison.Ordinal);
            if (!ConditionPolicy.ShouldPreserve(active, __0.IsBattleSession, human, ownsEquipment, __2)) return true;
            __result = true; // Successful no-op: do not decrease item's condition.
            var count = Interlocked.Increment(ref protectedCalls);
            if (count == 1 || count % 250 == 0)
                logger?.LogInfo($"Survivor condition preserved for equipped human item. protected={count} checked={totalCalls}");
            return false;
        }
        catch (Exception e)
        {
            // Never break a native authoritative item transaction on an adapter error.
            var n = Interlocked.Increment(ref failures);
            if (n <= 3 || n % 100 == 0)
                logger?.LogWarning($"Wear Guard fail-open #{n}: {e.GetType().Name}: {e.Message}");
            return true;
        }
    }
}
