using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace Frontier.ReferenceFix;

[BepInPlugin("frontier.cargohunters.module.rigos", "Frontier RigOS Skills", "0.6.2")]
[BepInDependency("frontier.cargohunters.core", "0.6.0")]
public sealed class RigOSPlugin : BasePlugin
{
	public override void Load()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected O, but got Unknown
		Harmony val = new Harmony("frontier.rigos.reference-v10.1");
		try
		{
			R.RequireFeature("rigos");
			RigOSRuntime.Install(val, this);
		}
		catch (Exception ex)
		{
			val.UnpatchSelf();
			((BasePlugin)this).Log.LogError((object)("RigOS initialization failed; this module's hooks were removed: " + ex));
		}
	}
}
