using BepInEx;
using BepInEx.Unity.IL2CPP;
using Frontier.Modular;

[BepInPlugin("frontier.cargohunters.module.equipment-traits", "Frontier Equipment Traits", "0.6.0")]
[BepInDependency("frontier.cargohunters.core", "0.6.0")]
public sealed class ModulePlugin : BasePlugin
{
	public override void Load()
	{
		ModuleRuntime.AssertRegistered("equipment-traits");
		((BasePlugin)this).Log.LogInfo((object)"Registered by Frontier Framework: equipment-traits (native hooks are installed once by the shared runtime).");
	}
}
