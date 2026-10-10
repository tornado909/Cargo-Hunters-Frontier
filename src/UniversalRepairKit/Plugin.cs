using System;
using System.Reflection;
using System.Threading;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Core.Utils;
using Game.ItemsSystem;
using Game.ItemsSystem.ItemComponents;
using Game.ItemsSystem.ItemOperations;
using HarmonyLib;

namespace Frontier.UniversalRepairKit;

[BepInPlugin("frontier.cargohunters.universal-repair", "Frontier Universal Repair Kit", "0.1.1")]
[BepInDependency("frontier.cargohunters.core", BepInDependency.DependencyFlags.HardDependency)]
public sealed class Plugin : BasePlugin
{
    internal const string KitTemplateId = "df757459-6db2-581c-9038-917f741c0866";
    private static ManualLogSource? log;
    private static long eligible, restored, skipped, errors;
    
    private sealed class Snapshot
    {
        internal UniqueId TargetId;
        internal UniqueId KitId;
        internal float BeforeCurrent;
        internal float BeforeMaximum;
        internal float OriginalMaximum;
        internal float BeforeKitDurability;
        internal string Path="";
    }

    public override void Load()
    {
        log=Log;
        try
        {
            var h=new Harmony("frontier.universal.repair.v0.1.1");
            var simple=AccessTools.Method(typeof(RestoreConditionOperation), "ExecuteAuthoritative");
            var continuous=AccessTools.Method(typeof(RestoreConditionContinuousOperation), "ExecuteOneTimeAuthoritative");
            var finish=AccessTools.Method(typeof(RestoreConditionContinuousOperation), "FinishAuthoritative");
            if (simple==null||continuous==null||finish==null||
                simple.ReturnType!=typeof(AbstractItemOperation)||
                continuous.ReturnType!=typeof(bool)||
                finish.ReturnType!=typeof(AbstractItemOperation))
                throw new MissingMemberException("Native repair operation signature changed");
            h.Patch(simple,
                prefix:new HarmonyMethod(typeof(Plugin),nameof(StartImmediate)),
                postfix:new HarmonyMethod(typeof(Plugin),nameof(CompleteImmediate)));
            h.Patch(continuous,
                prefix:new HarmonyMethod(typeof(Plugin),nameof(StartContinuous)),
                postfix:new HarmonyMethod(typeof(Plugin),nameof(CompleteContinuousTick)));
            h.Patch(finish,
                prefix:new HarmonyMethod(typeof(Plugin),nameof(StartContinuous)),
                postfix:new HarmonyMethod(typeof(Plugin),nameof(CompleteContinuous)));
            Log.LogInfo("Native universal-repair hooks registered on authoritative repair operations; source item must be purchased/crafted normally.");
        }
        catch(Exception e)
        {
            Log.LogError("Universal repair effect DISABLED (fail closed): "+e);
        }
    }

    private static void StartImmediate(RestoreConditionOperation __instance,
        ItemsOperationContext __0,out Snapshot? __state)
        => __state=Capture(__0,__instance?.TargetItemId??default,
             __instance?.RepairKitId??default,"immediate");

    private static void CompleteImmediate(RestoreConditionOperation __instance,
        ItemsOperationContext __0, AbstractItemOperation __result,Snapshot? __state)
        => Commit(__0,__result!=null,__state);

    private static void StartContinuous(RestoreConditionContinuousOperation __instance,
        ItemsOperationContext __0,out Snapshot? __state)
        => __state=Capture(__0,__instance?.TargetItemId??default,
             __instance?.RepairKitId??default,"continuous");

    private static void CompleteContinuousTick(RestoreConditionContinuousOperation __instance,
        ItemsOperationContext __0, bool __result,Snapshot? __state)
        => Commit(__0,__result,__state);

    private static void CompleteContinuous(RestoreConditionContinuousOperation __instance,
        ItemsOperationContext __0, AbstractItemOperation __result,Snapshot? __state)
        => Commit(__0,__result!=null,__state);

    private static Snapshot? Capture(ItemsOperationContext? context,UniqueId targetId,
        UniqueId kitId,string path)
    {
        if(context?.ItemsManager==null)return null;
        try
        {
            var kit=context.ItemsManager.Find(kitId);
            if(kit==null || !string.Equals(kit.TemplateId.ToString(),
                KitTemplateId,StringComparison.OrdinalIgnoreCase))return null;
            if(kit.GetComponent<ConditionRestoreKitComponent>()==null)return null;
            var target=context.ItemsManager.Find(targetId);
            var condition=target?.GetComponent<ConditionComponent>();
            if(condition==null)return null;
            float current=condition.Value,maximum=condition.MaxValue,original=condition.PerfectValue;
            if(!float.IsFinite(current)||!float.IsFinite(maximum)||
                !float.IsFinite(original)||original<=0f||maximum<=0f||
                current<0f||current>maximum||maximum>original||original>1000000f)
                return null;
            float beforeDurability=kit.GetComponent<DurabilityComponent>()?.Durability??float.NaN;
            if(!float.IsFinite(beforeDurability) || beforeDurability<=0f)return null;
            Interlocked.Increment(ref eligible);
            return new Snapshot { TargetId=targetId,KitId=kitId,
                 BeforeCurrent=current,BeforeMaximum=maximum,OriginalMaximum=original,
                 BeforeKitDurability=beforeDurability,Path=path };
        }
        catch(Exception e){Warn("capture",e);return null;}
    }

    private static void Commit(ItemsOperationContext? context,
       bool nativeSucceeded, Snapshot? state)
    {
        if(context?.ItemsManager==null || !nativeSucceeded || state==null)return;
        try
        {
            var kitAfter=context.ItemsManager.Find(state.KitId);
            var remaining=kitAfter?.GetComponent<DurabilityComponent>()?.Durability;
            // An actual native authoritative repair transaction must have spent
            // a kit charge or removed the one-use kit. Never give free repairs.
            bool spent=kitAfter==null ||
                (remaining.HasValue && float.IsFinite(remaining.Value) &&
                 remaining.Value+0.00001f < state.BeforeKitDurability);
            if(!spent)
            {
                Interlocked.Increment(ref skipped);
                return;
            }
            var target=context.ItemsManager.Find(state.TargetId);
            var condition=target?.GetComponent<ConditionComponent>();
            if(condition==null)return;
            float oldMaximum=condition.MaxValue;
            float targetMax=state.OriginalMaximum;
            if(targetMax<=oldMaximum+0.00001f)return;
            if(!float.IsFinite(condition.Value)||
               condition.Value<0f || condition.Value>targetMax+0.0001f)return;
            // Native kit validation, price/payment, consumption and current
            // condition repair have already run. Only restore lost maximum.
            // Preserve actual current repair progress; don't create extra heals.
            condition.SetMaxValue(targetMax);
            if(condition.Value>targetMax)condition.SetValue(targetMax);
            long count=Interlocked.Increment(ref restored);
            log?.LogInfo($"UNIVERSAL_REPAIR_MAX_RESTORED path={state.Path} n={count} from={oldMaximum:0.###} to={targetMax:0.###} kitSpent=yes");
        }
        catch(Exception e){Warn("commit",e);}
    }

    private static void Warn(string where,Exception e)
    {
        long n=Interlocked.Increment(ref errors);
        if(n<=5||n%100==0)
            log?.LogWarning($"UNIVERSAL_REPAIR_ERROR {where} #{n}: {e.GetType().Name}: {e.Message}");
    }
}
