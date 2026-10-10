using Frontier.UniversalRepairKit;

int tests = 0;
void Check(bool x, string name)
{
    tests++;
    if (!x) throw new Exception($"FAIL {name}");
}
var sample = new RepairPolicy.ItemCondition(2.5f, 4f, 7f, true);
var ok = RepairPolicy.Evaluate(sample, true, true, true, true);
Check(ok.Allowed && ok.NewCurrent == 7f && ok.NewMaximum == 7f, "red maximum and current restored");
Check(RepairPolicy.ShopPrice == 5000, "price 5000");
foreach (bool server in new[]{false,true})
foreach (bool owner in new[]{false,true})
foreach (bool kit in new[]{false,true})
foreach (bool consumed in new[]{false,true})
foreach (bool category in new[]{false,true})
{
    var c = sample with { IsRepairableEquipment = category };
    var output = RepairPolicy.Evaluate(c, server, owner, kit, consumed);
    bool allowed = server && owner && kit && consumed && category;
    Check(output.Allowed == allowed, "server/owner/kit/consumption/category matrix");
    if (!allowed) Check(output.NewCurrent == c.Current && output.NewMaximum == c.Maximum, "denials have no mutation");
}
var conditions = new[]{
    new RepairPolicy.ItemCondition(float.NaN,1f,2f,true),
    new RepairPolicy.ItemCondition(0f,float.PositiveInfinity,2f,true),
    new RepairPolicy.ItemCondition(-1f,1f,2f,true),
    new RepairPolicy.ItemCondition(3f,2f,4f,true),
    new RepairPolicy.ItemCondition(0f,4f,3f,true),
    new RepairPolicy.ItemCondition(0f,0f,3f,true),
    new RepairPolicy.ItemCondition(0f,1f,0f,true)
};
foreach (var invalid in conditions)
    Check(!RepairPolicy.Evaluate(invalid,true,true,true,true).Allowed, "nonfinite or inconsistent condition");
Check(!RepairPolicy.Evaluate(new(7f,7f,7f,true),true,true,true,true).Allowed, "full item not consumed");
Check(RepairPolicy.Evaluate(new(1f,6f,6f,true),true,true,true,true).NewCurrent==6f,"current-only damage");
Check(RepairPolicy.Evaluate(new(1f,2f,6f,true),true,true,true,true).NewMaximum==6f,"permanent wear");
Console.WriteLine($"PASS universal repair policy: {tests} assertions, server-bound/atomic-consumption gate");
