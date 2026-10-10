using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace Frontier.ReferenceFix;

[BepInPlugin("frontier.cargohunters.module.autostack", "Frontier Auto Stack", "0.7.3")]
[BepInDependency("frontier.cargohunters.core", "0.6.0")]
public sealed class AutoStackPlugin : BasePlugin
{
	public override void Load()
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Expected O, but got Unknown
		try
		{
			R.RequireFeature("auto-stack");
			AutoStackRuntime.Install(new Harmony("frontier.autostack.live-equipment-v10.7"), this);
		}
		catch (Exception ex)
		{
			((BasePlugin)this).Log.LogError((object)("AutoStack disabled: " + ex));
		}
	}
}
