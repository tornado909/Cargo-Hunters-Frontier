using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace Frontier.ReferenceFix;

[BepInPlugin("frontier.cargohunters.damage-numbers", "Frontier Damage Numbers", "0.2.2")]
[BepInDependency("frontier.cargohunters.core", "0.6.0")]
public sealed class DamageNumbersPlugin : BasePlugin
{
	private object? anchor;

	public override void Load()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected O, but got Unknown
		Harmony val = new Harmony("frontier.damage-numbers.v10.8.5");
		try
		{
			R.RequireFeature("damage-numbers");
			DamageNumbers.Install(val, this);
			Type type = R.Type("Frontier.Core", "Frontier.Core.FrontierOverlay");
			R.Hook(val, R.Method(type, "OnGUI", 0), typeof(DamageNumbers), null, "Draw");
			MethodInfo methodInfo = typeof(BasePlugin).GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Single((MethodInfo m) => m.Name == "AddComponent" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
			anchor = methodInfo.MakeGenericMethod(type).Invoke(this, null);
			((BasePlugin)this).Log.LogInfo((object)"Damage numbers v10.8.5 + live client HealthState HP bars: actual applied/replicated damage, observed shot critical multiplier, outlined floating text and raster lightning; no combat writes; cached reflection and non-blocking report file I/O.");
		}
		catch (Exception ex)
		{
			val.UnpatchSelf();
			((BasePlugin)this).Log.LogError((object)("Damage numbers disabled: " + ex));
		}
	}
}
