using System;

namespace Frontier.SurvivorWearGuard;

// Testable pure policy. Native ownership and session checks are performed by
// the runtime adapter; this method must never infer membership from proximity.
internal static class ConditionPolicy
{
    internal static bool ShouldPreserve(bool survivorActive, bool inBattle,
        bool verifiedHuman, bool equippedByThatHuman, float decrement)
    {
        return survivorActive && inBattle && verifiedHuman &&
               equippedByThatHuman && float.IsFinite(decrement) && decrement > 0f;
    }
}
