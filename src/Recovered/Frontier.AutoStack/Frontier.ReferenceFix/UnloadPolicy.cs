namespace Frontier.ReferenceFix;

internal static class UnloadPolicy
{
	internal static bool CurrentSessionMatches(bool authoritative, bool battle, long sessionManager, long contextManager)
	{
		if (!authoritative && battle && sessionManager != 0L)
		{
			return sessionManager == contextManager;
		}
		return false;
	}

	internal static bool SourceAllowed(bool sameEquipment, bool victimKnown, bool nativeLootPermission)
	{
		if (!sameEquipment)
		{
			return victimKnown && nativeLootPermission;
		}
		return true;
	}
}
