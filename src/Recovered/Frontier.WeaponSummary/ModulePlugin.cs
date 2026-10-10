using BepInEx;
using BepInEx.Unity.IL2CPP;
using Frontier.Modular;

[BepInPlugin("frontier.cargohunters.module.weapon-summary", "Frontier Weapon Bonus Summary", "0.6.0")]
[BepInDependency("frontier.cargohunters.core", "0.6.0")]
[BepInDependency("frontier.cargohunters.module.rarity-core", "0.6.0")]
public sealed class ModulePlugin : BasePlugin
{
	public override void Load()
	{
		ModuleRuntime.AssertRegistered("weapon-summary");
		((BasePlugin)this).Log.LogInfo((object)"Registered by Frontier Framework: weapon-summary (native hooks are installed once by the shared runtime).");
	}
}
