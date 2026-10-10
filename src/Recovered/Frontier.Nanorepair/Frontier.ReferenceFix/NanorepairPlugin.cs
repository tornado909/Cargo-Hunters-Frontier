using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace Frontier.ReferenceFix;

[BepInPlugin("frontier.cargohunters.nanorepair", "Frontier RigOS Nanorepair", "0.3.3")]
[BepInDependency("frontier.cargohunters.core", "0.6.0")]
[BepInDependency("frontier.cargohunters.module.rigos", "0.6.2")]
public sealed class NanorepairPlugin : BasePlugin
{
	public override void Load()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected O, but got Unknown
		Harmony val = new Harmony("frontier.nanorepair.server-lifecycle-v10.8.1");
		try
		{
			R.RequireFeature("rigos");
			NanoRuntime.Install(val, this);
		}
		catch (Exception ex)
		{
			val.UnpatchSelf();
			((BasePlugin)this).Log.LogError((object)("Nanorepair disabled: " + ex));
		}
	}
}
