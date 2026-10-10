using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace Frontier.Loot;

[BepInPlugin("frontier.cargohunters.rank28", "Frontier 28 Rank Ladder", "0.1.0")]
[BepInDependency("frontier.cargohunters.core", "0.6.0")]
[BepInDependency("frontier.cargohunters.module.rarity-core", "0.6.0")]
public sealed class LadderPlugin : BasePlugin
{
	public override void Load()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected O, but got Unknown
		Harmony val = new Harmony("frontier.rank28-v1");
		try
		{
			RankLadder.Activate(val);
		}
		catch (Exception ex)
		{
			val.UnpatchSelf();
			((BasePlugin)this).Log.LogError((object)("Rank28 integration failed: " + ex));
			throw;
		}
	}
}
