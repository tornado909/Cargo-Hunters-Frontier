using System;
using System.Threading;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Core.Utils;
using Game.ItemsSystem;
using Game.ItemsSystem.ItemComponents;
using Game.ItemsSystem.ItemOperations;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;

namespace Frontier.UniversalRepairKit;

// Uses ONLY the game's authoritative native repair transactions.
// The red/permanently lost part is upgraded BEFORE the native operation
// serializes changed ItemDto values. The previous v0.1.1 wrote the maximum
// in ExecuteAuthoritative postfix, after DTOs had already been produced,
// which was invisible to the client and could be lost on persistence.
[BepInPlugin("frontier.cargohunters.universal-repair", "Frontier Universal Repair Kit", "0.1.2")]
[BepInDependency("frontier.cargohunters.core", BepInDependency.DependencyFlags.HardDependency)]
public sealed class Plugin : BasePlugin
{
    internal const string KitTemplateId = "df757459-6db2-581c-9038-917f741c0866";
    private const float Epsilon = 0.0001f;
    private static ManualLogSource? logger;
    private static long scopesStarted, repairsObserved, redRestored, endConfirmed, failed, unrelatedRepairs, noSpend;
    [ThreadStatic] private static System.Collections.Generic.Stack<RepairScope>? threadScopes;

    private sealed class RepairScope
    {
        internal UniqueId TargetId;
        internal UniqueId KitId;
        internal IntPtr NativeConditionPointer;
        internal float BeforeMax, PerfectMax, BeforeKitDurability;
        internal string Path = "";
        internal bool EarlyApplied;
        internal float AfterEarly;
        internal int RepairHooks;
        internal bool ServerSpentKit;
    }

    public override void Load()
    {
        logger=Log;
        try
        {
            var h=new Harmony("frontier.universal.repair.v0.1.2");
            var one=AccessTools.Method(typeof(RestoreConditionOperation),"ExecuteAuthoritative");
            var tick=AccessTools.Method(typeof(RestoreConditionContinuousOperation),"ExecuteOneTimeAuthoritative");
            var complete=AccessTools.Method(typeof(RestoreConditionContinuousOperation),"FinishAuthoritative");
            var repair=AccessTools.Method(typeof(ConditionComponent),"Repair",new[]{typeof(float)});
            if (one==null||tick==null||complete==null||repair==null ||
                one.ReturnType!=typeof(AbstractItemOperation) ||
                tick.ReturnType!=typeof(bool) ||
                complete.ReturnType!=typeof(AbstractItemOperation) ||
                repair.ReturnType!=typeof(void))
                throw new MissingMemberException("Native authoritative repair ABI changed; no patches committed");

            // Patch the innermost Repair first. Only matching, thread-local
            // authoritative transaction scopes are ever modified.
            h.Patch(repair, postfix:new HarmonyMethod(typeof(Plugin),nameof(AfterNativeRepair)));
            h.Patch(one,
                prefix:new HarmonyMethod(typeof(Plugin),nameof(BeginImmediate)),
                postfix:new HarmonyMethod(typeof(Plugin),nameof(EndImmediate)));
            h.Patch(tick,
                prefix:new HarmonyMethod(typeof(Plugin),nameof(BeginContinuous)),
                postfix:new HarmonyMethod(typeof(Plugin),nameof(EndContinuousTick)));
            h.Patch(complete,
                prefix:new HarmonyMethod(typeof(Plugin),nameof(BeginContinuous)),
                postfix:new HarmonyMethod(typeof(Plugin),nameof(EndContinuous)));

            logger.LogInfo("Universal Repair Kit v0.1.2 ACTIVE: target max restoration is scoped inside native ConditionComponent.Repair BEFORE ItemDto serialization. No free repair/no direct save writes.");
        }
        catch(Exception e)
        {
            logger.LogError("Universal Repair v0.1.2 DISABLED: native ABI/prefix initialization failure: "+e);
        }
    }

    private static void BeginImmediate(RestoreConditionOperation __instance,
        ItemsOperationContext __0, out RepairScope? __state)
        => Begin(__0,__instance?.TargetItemId??default,__instance?.RepairKitId??default,
            "immediate",out __state);

    private static void EndImmediate(RestoreConditionOperation __instance,
        ItemsOperationContext __0, AbstractItemOperation __result, RepairScope? __state)
        => End(__0,__result!=null,__state);

    private static void BeginContinuous(RestoreConditionContinuousOperation __instance,
        ItemsOperationContext __0, out RepairScope? __state)
        => Begin(__0,__instance?.TargetItemId??default,__instance?.RepairKitId??default,
            "continuous",out __state);

    private static void EndContinuousTick(RestoreConditionContinuousOperation __instance,
        ItemsOperationContext __0, bool __result, RepairScope? __state)
        => End(__0,__result,__state);

    private static void EndContinuous(RestoreConditionContinuousOperation __instance,
        ItemsOperationContext __0, AbstractItemOperation __result, RepairScope? __state)
        => End(__0,__result!=null,__state);

    private static void Begin(ItemsOperationContext? context,UniqueId targetId,
        UniqueId kitId,string path,out RepairScope? state)
    {
        state=null;
        if(context?.ItemsManager==null)return;
        try
        {
            // The active scope must describe precisely one real, existing kit.
            var kit=context.ItemsManager.Find(kitId);
            if(kit==null || !string.Equals(kit.TemplateId.ToString(),KitTemplateId,
                StringComparison.OrdinalIgnoreCase) ||
                kit.GetComponent<ConditionRestoreKitComponent>()==null)
                return;

            var target=context.ItemsManager.Find(targetId);
            var condition=target?.GetComponent<ConditionComponent>();
            var durability=kit.GetComponent<DurabilityComponent>();
            if(condition==null||durability==null)return;
            var before=condition.MaxValue;
            var perfect=condition.PerfectValue;
            var current=condition.Value;
            var charge=durability.Durability;
            if(!float.IsFinite(before)||!float.IsFinite(perfect)||
               !float.IsFinite(current)||!float.IsFinite(charge)||
               perfect<=0f||perfect>1000000f||
               before<=0f||before>perfect||current<0f||current>before||
               charge<=0f)
               return;
            if(threadScopes!=null&&threadScopes.Count>=8)
            {
                Warn("nested-scope-overflow",new InvalidOperationException("Repair depth >8"));
                return;
            }
            var scope=new RepairScope {
                TargetId=targetId,KitId=kitId,
                NativeConditionPointer=((Il2CppObjectBase)condition).Pointer,
                BeforeMax=before,PerfectMax=perfect,
                BeforeKitDurability=charge,Path=path
            };
            if(scope.NativeConditionPointer==IntPtr.Zero)return;
            (threadScopes??=new System.Collections.Generic.Stack<RepairScope>()).Push(scope);
            state=scope;
            var n=Interlocked.Increment(ref scopesStarted);
            if(n<=4 || n%30==0)
                logger?.LogInfo($"UNIVERSAL_REPAIR_TX_BEGIN n={n} path={path} current={current:0.###} max={before:0.###} perfect={perfect:0.###} charge={charge:0.###}");
        }
        catch(Exception e){Warn("begin",e);}
    }

    // Native ConditionComponent.Repair executes only after the game's own
    // conditions/owner/kit compatibility checks. Changing the maximum here
    // lets the same native transaction include our value in changed ItemDto.
    private static void AfterNativeRepair(ConditionComponent __instance,float __0)
    {
        var scope=(threadScopes!=null&&threadScopes.Count>0)?threadScopes.Peek():null;
        if(scope==null||__instance==null)return;
        try
        {
            var ptr=((Il2CppObjectBase)__instance).Pointer;
            if(ptr==IntPtr.Zero || ptr!=scope.NativeConditionPointer)
            {
                Interlocked.Increment(ref unrelatedRepairs);
                return;
            }
            scope.RepairHooks++;
            var n=Interlocked.Increment(ref repairsObserved);
            var max=__instance.MaxValue;
            if(!float.IsFinite(max)||max<=0f||max>scope.PerfectMax+Epsilon)return;
            if(scope.PerfectMax>max+Epsilon)
            {
                __instance.SetMaxValue(scope.PerfectMax);
                var observed=__instance.MaxValue;
                var remained=__instance.NotPerfectMaxValue;
                scope.AfterEarly=observed;
                scope.EarlyApplied=observed>=scope.PerfectMax-Epsilon;
                if(scope.EarlyApplied)
                {
                    var good=Interlocked.Increment(ref redRestored);
                    logger?.LogInfo($"UNIVERSAL_REPAIR_RED_PRE_DTO_UPDATED n={good} path={scope.Path} from={max:0.###} to={observed:0.###} perfect={scope.PerfectMax:0.###} redRemaining={remained:0.###}");
                }
                else
                    logger?.LogWarning($"UNIVERSAL_REPAIR_RED_PRE_DTO_REJECTED from={max:0.###} attempted={scope.PerfectMax:0.###} observed={observed:0.###}");
            }
            else if(n<=4)
                logger?.LogInfo($"UNIVERSAL_REPAIR_NATIVE_REPAIR_CALLED n={n} max={max:0.###} perfect={scope.PerfectMax:0.###} (no red deficit)");
        }
        catch(Exception e){Warn("native-repair",e);}
    }

    private static void End(ItemsOperationContext? context,bool nativeSucceeded,RepairScope? scope)
    {
        if(scope==null)return;
        try
        {
            if(threadScopes==null||threadScopes.Count==0||
                !ReferenceEquals(threadScopes.Peek(),scope))
            {
                Warn("scope-mismatch",new InvalidOperationException("Unbalanced native repair transaction"));
                threadScopes?.Clear();
                return;
            }
            // At this point the native method has already produced a response
            // and/or changed ItemDto. This stage MUST NOT write new max values.
            if(context?.ItemsManager==null||!nativeSucceeded)return;
            var kit=context.ItemsManager.Find(scope.KitId);
            var remaining=kit?.GetComponent<DurabilityComponent>()?.Durability;
            bool spent=kit==null||(remaining.HasValue&&float.IsFinite(remaining.Value)&&
                                   remaining.Value+Epsilon<scope.BeforeKitDurability);
            scope.ServerSpentKit=spent;
            if(!spent)
            {
                Interlocked.Increment(ref noSpend);
                // Abort tentative early mutation if native transaction did
                // not consume the kit; only server-authoritative repair may
                // improve maximum durability.
                if(scope.EarlyApplied)
                {
                    var target=context.ItemsManager.Find(scope.TargetId);
                    var condition=target?.GetComponent<ConditionComponent>();
                    if(condition!=null &&
                       ((Il2CppObjectBase)condition).Pointer==scope.NativeConditionPointer)
                        condition.SetMaxValue(scope.BeforeMax);
                }
                return;
            }
            var post=context.ItemsManager.Find(scope.TargetId)?.GetComponent<ConditionComponent>();
            if(post==null)return;
            float last=post.MaxValue;
            bool good=scope.EarlyApplied &&
                last>=scope.PerfectMax-Epsilon &&
                post.NotPerfectMaxValue<=Epsilon;
            long count=Interlocked.Increment(ref endConfirmed);
            logger?.LogInfo($"UNIVERSAL_REPAIR_TX_END n={count} kitSpent=yes path={scope.Path} repairHooks={scope.RepairHooks} earlyApplied={scope.EarlyApplied} savedMax={last:0.###} perfect={scope.PerfectMax:0.###} redRemaining={post.NotPerfectMaxValue:0.###} preDtoVerified={good}");
            if(!good && scope.PerfectMax>scope.BeforeMax+Epsilon)
                logger?.LogWarning("UNIVERSAL_REPAIR_RED_NOT_CONFIRMED: waiting for native state/DTO verification; do NOT report success.");
        }
        catch(Exception e){Warn("end",e);}
        finally
        {
            // If native validation returned early, the scope must still leave
            // this thread; other item operations must never inherit it.
            if(threadScopes!=null&&threadScopes.Count>0 &&
               ReferenceEquals(threadScopes.Peek(),scope))
                threadScopes.Pop();
        }
    }

    private static void Warn(string where,Exception e)
    {
        var n=Interlocked.Increment(ref failed);
        if(n<=6||n%100==0)
            logger?.LogWarning($"UNIVERSAL_REPAIR_V012_ERROR {where} n={n}: {e.GetType().Name}: {e.Message}");
    }
}
