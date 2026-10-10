using BepInEx;
using BepInEx.Unity.IL2CPP;
using Frontier.Modular;

[BepInPlugin("frontier.cargohunters.module.legacy-catalog", "Frontier Legacy Save Catalog", "0.6.0")]
[BepInDependency("frontier.cargohunters.core", "0.6.0")]
public sealed class ModulePlugin : BasePlugin
{
	public override void Load()
	{
		ModuleRuntime.AssertRegistered("legacy-catalog");
		((BasePlugin)this).Log.LogInfo((object)"Registered by Frontier Framework: legacy-catalog (native hooks are installed once by the shared runtime).");
	}
}
