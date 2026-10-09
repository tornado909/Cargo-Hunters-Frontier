namespace Frontier.Engine;

public static class ConnectionPolicy
{
	public static bool AllowLocalShelter(bool nativeShelterConnection, bool isSteam, bool loopback, bool catalogReady)
	{
		return nativeShelterConnection && !isSteam && loopback && catalogReady;
	}

	public static bool DeploymentReady(bool isOwner, bool ownerLocked, bool rosterFrozen, string phase)
	{
		if (!isOwner)
		{
			return phase == "locked";
		}
		return ownerLocked && rosterFrozen;
	}
}
