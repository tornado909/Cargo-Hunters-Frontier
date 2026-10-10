using BepInEx;
using BepInEx.Unity.IL2CPP;
using Frontier.Modular;

[BepInPlugin("frontier.cargohunters.module.armory", "Frontier Armory", "0.6.0")]
[BepInDependency("frontier.cargohunters.core", "0.6.0")]
[BepInDependency("frontier.cargohunters.module.ammo-supply", "0.6.0")]
public sealed class ModulePlugin : BasePlugin
{
	public override void Load()
	{
		ModuleRuntime.AssertRegistered("armory");
		((BasePlugin)this).Log.LogInfo((object)"Registered by Frontier Framework: armory (native hooks are installed once by the shared runtime).");
	}
}
