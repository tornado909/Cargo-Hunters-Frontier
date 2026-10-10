namespace Frontier.Survivor;

internal static class SurvivorIdentityPolicy
{
	internal static bool Bound(ulong member, ulong local, int claimedPlayer, int localPlayer, ulong nativeSteamId)
	{
		if (member != 0L && local != 0L && claimedPlayer >= 0)
		{
			if (member != local)
			{
				if (nativeSteamId != 0L)
				{
					return nativeSteamId == member;
				}
				return false;
			}
			return claimedPlayer == localPlayer;
		}
		return false;
	}
}
