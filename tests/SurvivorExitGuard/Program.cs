using Frontier.SurvivorExitGuard;
int count=0;
void Check(bool ok,string name) { count++;if(!ok)throw new Exception("FAIL "+name); }
var gate=new ExitGate();
Check(!gate.Stopping,"initial");
Check(gate.Exit(),"first transition");
Check(gate.Stopping,"exit blocks tick");
Check(!gate.Exit(),"exit idempotent");
Check(gate.Stopping,"still blocked");
gate.Enter();
Check(!gate.Stopping,"next battle rearmed");
Check(gate.Exit(),"second run exit accepted");
Check(!gate.Exit(),"duplicate ignored");
gate.Enter();
Check(!gate.Stopping,"new run resumed");
for(int i=0;i<1000;i++)
{
    Check(gate.Exit(),"exit "+i);
    Check(gate.Stopping,"stopped "+i);
    Check(!gate.Exit(),"dedup "+i);
    gate.Enter();
    Check(!gate.Stopping,"rearmed "+i);
}
Console.WriteLine($"SURVIVOR_EXIT_GATE_PASS checks={count}");
