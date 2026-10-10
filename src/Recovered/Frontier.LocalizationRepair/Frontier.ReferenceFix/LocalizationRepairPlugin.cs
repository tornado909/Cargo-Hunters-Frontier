using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace Frontier.ReferenceFix;

[BepInPlugin("frontier.cargohunters.localizationrepair", "Frontier Localization Repair", "0.1.0")]
[BepInDependency("frontier.cargohunters.core", "0.6.0")]
public sealed class LocalizationRepairPlugin : BasePlugin
{
	public override void Load()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected O, but got Unknown
		Harmony val = new Harmony("frontier.localization.names-and-nested-v10.8");
		try
		{
			R.RequireFeature("localization-repair");
			LocaleRuntime.Install(val, this);
		}
		catch (Exception ex)
		{
			val.UnpatchSelf();
			((BasePlugin)this).Log.LogError((object)("Localization repair disabled: " + ex));
		}
	}
}
