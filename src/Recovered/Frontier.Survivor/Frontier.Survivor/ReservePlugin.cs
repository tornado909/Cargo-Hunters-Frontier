using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace Frontier.Survivor;

[BepInPlugin("frontier.cargohunters.survivor", "Frontier Survivor", "0.2.0")]
[BepInDependency("frontier.cargohunters.core", "0.6.0")]
[BepInDependency("frontier.cargohunters.teammatemarkers", "0.3.0")]
public sealed class ReservePlugin : BasePlugin
{
	public override void Load()
	{
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Expected O, but got Unknown
		Identity.Folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		if (!File.Exists(Path.Combine(Identity.Folder, "reserve.location.json")))
		{
			string text = Path.Combine(Paths.PluginPath, "FrontierPack", "modules", "survivor");
			if (File.Exists(Path.Combine(text, "reserve.location.json")))
			{
				Identity.Folder = text;
			}
		}
		Trace.Init(((BasePlugin)this).Log);
		Harmony val = new Harmony("frontier.survivor.v1");
		try
		{
			NativePrefabSources.Initialize();
			Content.Load();
			Hooks.Install(val);
			SurvivorHooks.Install(val);
			((BasePlugin)this).AddComponent<ReserveLoop>();
			((BasePlugin)this).Log.LogInfo((object)"Reserve 0.2.0 / v10.10.2-survivor-coop.2: event-bound dependency cache and native container-group loot; runtime acceptance is recorded separately.");
		}
		catch (Exception e)
		{
			val.UnpatchSelf();
			Trace.Data["status"] = "DISABLED_INITIALIZATION_FAILED";
			Trace.Error("initialization", e);
		}
	}
}
