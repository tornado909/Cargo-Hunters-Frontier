using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace Frontier.ReferenceFix;

[BepInPlugin("frontier.cargohunters.teammatemarkers", "Frontier Teammate Markers", "0.3.0")]
[BepInDependency("frontier.cargohunters.core", "0.6.0")]
public sealed class TeammatePlugin : BasePlugin
{
	private object? anchor;

	public override void Load()
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Expected O, but got Unknown
		//IL_004c: Expected O, but got Unknown
		try
		{
			R.RequireFeature("teammate-markers");
			Harmony val = new Harmony("frontier.teammates.identity-v10.7");
			PartyRoster.Install(val, this);
			Type type = R.Type("Frontier.Core", "Frontier.Core.FrontierOverlay");
			R.Hook(val, R.Method(type, "OnGUI", 0), typeof(PartyRoster), null, "Draw");
			anchor = typeof(BasePlugin).GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Single((MethodInfo m) => m.Name == "AddComponent" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0).MakeGenericMethod(type)
				.Invoke(this, null);
			((BasePlugin)this).Log.LogInfo((object)"Teammates v10.7: Steam-member verified live character identities; shared minimap roster, cyan in-world names/distances; no IsBot guessing or readiness/host changes.");
		}
		catch (Exception ex)
		{
			((BasePlugin)this).Log.LogError((object)("Teammate markers disabled: " + ex));
		}
	}
}
