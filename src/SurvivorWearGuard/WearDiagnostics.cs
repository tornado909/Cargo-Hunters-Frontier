using System;
using System.Reflection;
using System.Threading;
using BepInEx.Logging;
using Core.Utils;
using Game.GameModes;
using Game.ItemsSystem;
using Game.ItemsSystem.ItemComponents;
using Game.ItemsSystem.ItemOperations;
using Game.WeaponSystem.ItemComponents;
using HarmonyLib;

namespace Frontier.SurvivorWearGuard;

// Two independent native condition-decrease entry points are used by Cargo
// Hunters. A previous item-container lookup was empty for the actual gun.
// This guard verifies the account-bound owner and the item's real component.
internal static class WearDiagnostics
{
    private static ManualLogSource? log;
    private static MethodInfo? activeMethod, humanMethod;
    private static FieldInfo? authorityField, clientField;
    private static long callsTyped, callsPlain, callsComponent, callsLegacy;
    private static long protectedTyped, protectedPlain, protectedLegacy, failures;

    internal static void Install(Harmony harmony, Type survivorType, ManualLogSource logger)
    {
        log=logger;
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static;
        activeMethod=survivorType.GetProperty("Active",flags)?.GetGetMethod(true);
        humanMethod=survivorType.GetMethod("IsHuman",flags);
        authorityField=survivorType.GetField("authority",flags);
        clientField=survivorType.GetField("client",flags);
        if(activeMethod==null || humanMethod==null || authorityField==null || clientField==null ||
           humanMethod.GetParameters().Length!=2)
            throw new MissingMemberException("Survivor 0.2.0 active/human ownership contract missing");
        var full=AccessTools.Method(typeof(SessionItemsConditionManager),
          "Game_ItemsSystem_IItemsConditionManager_DecreaseCondition",
          new[]{typeof(UniqueId),typeof(ConditionDecreaseType),typeof(float),typeof(ConditionDecreaseDamageSideType)});
        var plain=AccessTools.Method(typeof(SessionItemsConditionManager),
          "Game_ItemsSystem_IItemsConditionManager_DecreaseCondition",
          new[]{typeof(UniqueId),typeof(float)});
        var component=AccessTools.Method(typeof(ConditionComponent),"Decrease",new[]{typeof(float)});
        if(full==null||plain==null||component==null)
            throw new MissingMemberException("Native condition decrease ABI mismatch");
        harmony.Patch(full,prefix:new HarmonyMethod(typeof(WearDiagnostics),nameof(TypedGuard)));
        harmony.Patch(plain,prefix:new HarmonyMethod(typeof(WearDiagnostics),nameof(PlainGuard)));
        harmony.Patch(component,prefix:new HarmonyMethod(typeof(WearDiagnostics),nameof(ComponentProbe)));
        log.LogInfo("WearGuard v0.1.1: patched typed+plain SessionItemsConditionManager and observed ConditionComponent.Decrease.");
    }

    private static bool Active() {
        try{return activeMethod?.Invoke(null,null) is true;}catch{return false;}
    }

    private static bool CombatGear(IItem item)
    {
        return item.GetComponent<FirearmComponent>() != null ||
               item.GetComponent<MeleeWeaponComponent>() != null ||
               item.GetComponent<WeaponModuleComponent>() != null ||
               item.GetComponent<MagazineComponent>() != null ||
               item.GetComponent<ArmorComponent>() != null ||
               item.GetComponent<BodyPartComponent>() != null;
    }

    internal static bool Protect(BattleSession? battle, UniqueId itemId, IItem? item,float quantity,out string reason)
    {
        reason="not-active";
        var active=Active();
        if(!active || battle==null || !battle.IsBattleSession) return false;
        reason="invalid-amount";
        if(!float.IsFinite(quantity) || quantity<0f) return false;
        reason="unowned";
        var owner=battle.FindOwnerPlayerId(itemId);
        if(owner==null||!owner.HasValue) return false;
        reason="not-human";
        if(humanMethod?.Invoke(null,new object[]{battle,owner.Value}) is not true)
            return false;
        reason="not-gear";
        if(item==null || !CombatGear(item))return false;
        reason="protected-human-combat-equipment";
        return ConditionPolicy.ShouldPreserve(active,true,true,true,quantity);
    }

    internal static bool ProtectLegacy(ItemsOperationContext context,UniqueId id,IItem item,float quantity)
    {
        try
        {
            // Session-local context and its native owner mapping are authoritative;
            // use only current Survivor server or local client session.
            var battle=authorityField?.GetValue(null) as BattleSession
                   ??clientField?.GetValue(null) as BattleSession;
            if(!context.IsBattleSession || battle==null)return false;
            var ok=Protect(battle,id,item,quantity,out var reason);
            var n=Interlocked.Increment(ref callsLegacy);
            if(ok)
            {
                var saved=Interlocked.Increment(ref protectedLegacy);
                if(saved<=5 || saved%100==0)log?.LogInfo($"WearGuard protected old operation #{saved} owner verified.");
            }
            else if(n<=5)
                log?.LogInfo($"WearGuard old operation n={n} ignored reason={reason}");
            return ok;
        }
        catch(Exception e){Failure("legacy",e);return false;}
    }

    private static bool TypedGuard(SessionItemsConditionManager __instance,UniqueId __0,
        ConditionDecreaseType __1,float __2)
        => ManagerGuard("typed",__instance,__0,__2,ref callsTyped,ref protectedTyped);

    private static bool PlainGuard(SessionItemsConditionManager __instance,UniqueId __0,float __1)
        => ManagerGuard("plain",__instance,__0,__1,ref callsPlain,ref protectedPlain);

    private static bool ManagerGuard(string kind,SessionItemsConditionManager mgr,UniqueId id,
        float amount,ref long calls,ref long guarded)
    {
        var n=Interlocked.Increment(ref calls);
        try
        {
            var battle=mgr?._battleSession;
            var item=mgr?._itemsManager?.Find(id);
            var suppress=Protect(battle,id,item,amount,out var reason);
            if(suppress)
            {
                var saved=Interlocked.Increment(ref guarded);
                if(saved<=5||saved%100==0)
                    log?.LogInfo($"WearGuard protected {kind} n={n}, protected={saved}, qty={amount:0.###}.");
            }
            else if(n<=5||n%250==0)
                log?.LogInfo($"WearGuard observed {kind} n={n} active={Active()} auth={battle?.IsAuthoritative} reason={reason} qty={amount:0.###} comp={callsComponent}");
            return !suppress;
        }
        catch(Exception e){Failure(kind,e);return true;}
    }

    private static void ComponentProbe(float __0)
    {
        var n=Interlocked.Increment(ref callsComponent);
        if(n<=5||n%100==0)
            log?.LogInfo($"WearGuard component.Decrease n={n} active={Active()} qty={__0:0.###}, manager={callsTyped}/{callsPlain}, blocked={protectedTyped}/{protectedPlain}/{protectedLegacy}");
    }

    private static void Failure(string source,Exception e)
    {
        var n=Interlocked.Increment(ref failures);
        if(n<=5||n%100==0)
            log?.LogWarning($"WearGuard {source} validation fail-open #{n}: {e.GetType().Name}: {e.Message}");
    }
}
