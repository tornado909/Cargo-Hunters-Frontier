using System;
using Frontier.SurvivorWearGuard;
int checks = 0, successes = 0;
void Verify(bool yes, string test) { checks++; if (!yes) throw new Exception("FAIL: "+test); }
foreach (bool active in new[] { false,true })
foreach (bool battle in new[] { false,true })
foreach (bool human in new[] { false,true })
foreach (bool equipped in new[] { false,true })
foreach (float change in new[] { -10f,-0.01f,0f,0.000001f,0.04f,1f,float.NaN,float.PositiveInfinity,float.NegativeInfinity }) {
    bool expected = active && battle && human && equipped && float.IsFinite(change) && change > 0;
    bool actual = ConditionPolicy.ShouldPreserve(active,battle,human,equipped,change);
    Verify(expected == actual,$"a={active} battle={battle} human={human} equip={equipped} delta={change}");
    if (actual) successes++;
}
Verify(successes==3, "positive guard matrix");
Console.WriteLine($"WEARGUARD_POLICY_PASS checks={checks}, suppressCases={successes}; scope=managed-policy-only");
