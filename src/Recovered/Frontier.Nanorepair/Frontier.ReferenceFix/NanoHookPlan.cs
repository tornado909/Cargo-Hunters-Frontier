using System.Reflection;
using Frontier.Safety;
using Game.Application.Client;
using Game.Application.Server;

namespace Frontier.ReferenceFix;

internal static class NanoHookPlan
{
	internal static MethodInfo ServerTick()
	{
		return NativeHookContract.Exact(typeof(ServerBattleState), "UpdateBattleState", false, typeof(void));
	}

	internal static MethodInfo ClientTick()
	{
		return NativeHookContract.Exact(typeof(ClientBattleState), "UpdateBattleState", false, typeof(void));
	}

	internal static MethodInfo[] Cleanup()
	{
		return new MethodInfo[2]
		{
			NativeHookContract.Exact(typeof(ServerBattleState), "LeaveBattleState", false, typeof(void)),
			NativeHookContract.Exact(typeof(ServerBattleState), "Leave", false, typeof(void))
		};
	}
}
