using BepInEx;
using BepInEx.Unity.IL2CPP;
using Frontier.Modular;

[BepInPlugin("frontier.cargohunters.module.elite-visuals", "Frontier Elite Visuals", "0.6.0")]
[BepInDependency("frontier.cargohunters.core", "0.6.0")]
[BepInDependency("frontier.cargohunters.module.elites", "0.6.0")]
public sealed class ModulePlugin : BasePlugin
{
	public override void Load()
	{
		ModuleRuntime.AssertRegistered("elite-visuals");
		((BasePlugin)this).Log.LogInfo((object)"Registered by Frontier Framework: elite-visuals (native hooks are installed once by the shared runtime).");
	}
}
