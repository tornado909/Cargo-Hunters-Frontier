using System.Threading;
namespace Frontier.SurvivorExitGuard;

// Only lifecycle transitions for the actual Frontier_Survivor map may stop
// its runner. The gate is rearmed at the next matching EnterBattleState.
internal sealed class ExitGate
{
    private int stopping;
    internal bool Stopping => Volatile.Read(ref stopping) != 0;
    internal bool Exit() => Interlocked.Exchange(ref stopping, 1) == 0;
    internal void Enter() => Interlocked.Exchange(ref stopping, 0);
}
