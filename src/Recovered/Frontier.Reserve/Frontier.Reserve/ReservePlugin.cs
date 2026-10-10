using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace Frontier.Reserve;

[BepInPlugin("frontier.cargohunters.reserve", "Frontier Reserve", "0.4.3")]
[BepInDependency("frontier.cargohunters.core", "0.6.0")]
public sealed class ReservePlugin : BasePlugin
{
	public override void Load()
	{
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Expected O, but got Unknown
		Identity.Folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		if (!File.Exists(Path.Combine(Identity.Folder, "reserve.location.json")))
		{
			string text = Path.Combine(Paths.PluginPath, "FrontierPack", "modules", "reserve");
			if (File.Exists(Path.Combine(text, "reserve.location.json")))
			{
				Identity.Folder = text;
			}
		}
		Trace.Init(((BasePlugin)this).Log);
		Harmony val = new Harmony("frontier.reserve.v1");
		try
		{
			NativePrefabSources.Initialize();
			Content.Load();
			Hooks.Install(val);
			((BasePlugin)this).AddComponent<ReserveLoop>();
			((BasePlugin)this).Log.LogInfo((object)"Reserve 0.4.3 / v10.10.0-reserve-stock-art.4: event-bound dependency cache and native container-group loot; runtime acceptance is recorded separately.");
		}
		catch (Exception e)
		{
			val.UnpatchSelf();
			Trace.Data["status"] = "DISABLED_INITIALIZATION_FAILED";
			Trace.Error("initialization", e);
		}
	}
}
