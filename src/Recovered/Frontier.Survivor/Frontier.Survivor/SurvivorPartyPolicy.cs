using System;

namespace Frontier.Survivor;

internal static class SurvivorPartyPolicy
{
	internal static bool ShouldEnd(bool hadLivingPlayer, int living, int awaitingSpawn)
	{
		if (hadLivingPlayer && living == 0)
		{
			return awaitingSpawn == 0;
		}
		return false;
	}

	internal static int EnemyBudget(int living, double seconds, int nativeCap)
	{
		int num = Math.Min(28, 5 + 2 * Math.Max(0, living - 1) + (int)(seconds / 40.0));
		if (nativeCap <= 0)
		{
			return num;
		}
		return Math.Min(num, nativeCap);
	}
}
