using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace Frontier.ReferenceFix;

[BepInPlugin("frontier.cargohunters.quest-compat", "Frontier Quest Template Compatibility", "0.1.0")]
[BepInDependency("frontier.cargohunters.core", "0.6.0")]
public sealed class QuestCompatibilityPlugin : BasePlugin
{
	public override void Load()
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Expected O, but got Unknown
		try
		{
			R.RequireFeature("quest-compat");
			QuestTemplates.Install(new Harmony("frontier.quest-template-family-v10.2"), this);
		}
		catch (Exception ex)
		{
			((BasePlugin)this).Log.LogError((object)("Quest template compatibility disabled: " + ex));
		}
	}
}
