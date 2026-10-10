using System;

namespace Frontier.UniversalRepairKit;

/// <summary>
/// Pure, testable constraints for a single-use universal equipment repair kit.
/// Native inventory mutation must be performed ONLY by the game's authoritative
/// item-operation pipeline, after ownership, currency and consumption checks.
/// </summary>
public static class RepairPolicy
{
    public const int ShopPrice = 5_000;

    public readonly record struct ItemCondition(
        float Current,
        float Maximum,
        float PerfectMaximum,
        bool IsRepairableEquipment);

    public readonly record struct RepairProposal(
        bool Allowed,
        float NewCurrent,
        float NewMaximum,
        string Reason);

    public static RepairProposal Evaluate(
        ItemCondition item,
        bool authoritative,
        bool ownedByRequestingPlayer,
        bool authenticatedKitAvailable,
        bool kitConsumedInSameTransaction)
    {
        if (!authoritative)
            return Deny(item, "not-authoritative");
        if (!ownedByRequestingPlayer)
            return Deny(item, "item-owner-mismatch");
        if (!authenticatedKitAvailable || !kitConsumedInSameTransaction)
            return Deny(item, "repair-kit-not-atomically-consumed");
        if (!item.IsRepairableEquipment)
            return Deny(item, "target-category-incompatible");

        if (!float.IsFinite(item.Current)
            || !float.IsFinite(item.Maximum)
            || !float.IsFinite(item.PerfectMaximum)
            || item.PerfectMaximum <= 0f
            || item.Maximum <= 0f
            || item.Maximum > item.PerfectMaximum
            || item.Current < 0f
            || item.Current > item.Maximum)
            return Deny(item, "invalid-condition-state");

        if (item.Current == item.PerfectMaximum &&
            item.Maximum == item.PerfectMaximum)
            return Deny(item, "already-perfect");

        return new RepairProposal(
            Allowed: true,
            NewCurrent: item.PerfectMaximum,
            NewMaximum: item.PerfectMaximum,
            Reason: "restore-current-and-degraded-maximum");
    }

    private static RepairProposal Deny(ItemCondition item, string why)
        => new(false, item.Current, item.Maximum, why);
}
