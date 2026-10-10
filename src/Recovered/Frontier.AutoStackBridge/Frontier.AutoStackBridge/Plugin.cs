using BepInEx;
using BepInEx.Unity.IL2CPP;

namespace Frontier.AutoStackBridge;

[BepInPlugin("frontier.cargohunters.autostack.bridge", "Frontier Auto Stack Runtime Bridge", "0.6.2")]
public sealed class Plugin : BasePlugin
{
	public override void Load()
	{
		((BasePlugin)this).Log.LogInfo((object)"Frontier Auto Stack bridge v2 disabled: v3 uses only native-pointer-gated exact operation hooks; mailbox/quest paths are never detoured.");
	}
}
