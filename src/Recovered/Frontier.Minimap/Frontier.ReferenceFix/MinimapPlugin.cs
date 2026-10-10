using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace Frontier.ReferenceFix;

[BepInPlugin("frontier.cargohunters.minimap", "Frontier Tactical Minimap", "0.4.5")]
[BepInDependency("frontier.cargohunters.core", "0.6.0")]
[BepInDependency("frontier.cargohunters.teammatemarkers", "0.3.0")]
public sealed class MinimapPlugin : BasePlugin
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
			R.RequireFeature("minimap");
			Harmony val = new Harmony("frontier.minimap.party-sight-v10.7");
			MinimapRuntime.Install(val, this);
			Type type = R.Type("Frontier.Core", "Frontier.Core.FrontierOverlay");
			R.Hook(val, R.Method(type, "OnGUI", 0), typeof(MinimapRuntime), null, "Draw");
			MethodInfo methodInfo = typeof(BasePlugin).GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Single((MethodInfo m) => m.Name == "AddComponent" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
			anchor = methodInfo.MakeGenericMethod(type).Invoke(this, null);
			((BasePlugin)this).Log.LogInfo((object)"Minimap v10.8.8: bounded native modal stack reader; passive HUD retained, visible full windows hide map; camera/occlusion/party/rank behavior retained.");
		}
		catch (Exception ex)
		{
			((BasePlugin)this).Log.LogError((object)("Minimap disabled: " + ex));
		}
	}
}
