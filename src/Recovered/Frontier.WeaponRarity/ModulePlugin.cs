using BepInEx;
using BepInEx.Unity.IL2CPP;
using Frontier.Modular;

[BepInPlugin("frontier.cargohunters.module.weapon-rarity", "Frontier Weapon Rarity", "0.6.0")]
[BepInDependency("frontier.cargohunters.core", "0.6.0")]
[BepInDependency("frontier.cargohunters.module.rarity-core", "0.6.0")]
public sealed class ModulePlugin : BasePlugin
{
	public override void Load()
	{
		ModuleRuntime.AssertRegistered("weapon-rarity");
		((BasePlugin)this).Log.LogInfo((object)"Registered by Frontier Framework: weapon-rarity (native hooks are installed once by the shared runtime).");
	}
}
