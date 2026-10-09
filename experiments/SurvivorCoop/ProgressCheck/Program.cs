using Frontier.Survivor;
using System.Text.Json;
int checks=0;
void Check(bool pass,string name){checks++;if(!pass)throw new Exception(name);}
var host=new RunProgress(100);var guest=new RunProgress(200);var third=new RunProgress(300);
Check(host.Level==1&&guest.Level==1,"fresh separate players");
host.Award(40);
Check(host.Kills==1&&host.Level==2&&guest.Kills==0&&guest.Level==1&&third.Experience==0,"host kill must not award guest");
var originalGuest=guest.Export();
int bonus=host.Offer[0];Check(host.Choose(0),"host earned choice");
Check(host.Upgrades[bonus]==1&&guest.Upgrades.All(x=>x==0),"host bonus must not affect guest");
guest.Award(25);guest.Award(25);
Check(guest.Kills==2&&host.Kills==1&&third.Kills==0,"guest kills independent");
Check(guest.Level==2&&guest.Experience==10&&host.Experience==0,"individual XP");
Check(guest.Choose(1)&&!guest.Choose(1),"guest choice consumed exactly once");
Check(RunProgress.Valid(host.Export())&&RunProgress.Valid(guest.Export()),"valid export");
var imported=RunProgress.Import(host.Export());
Check(JsonSerializer.Serialize(imported.Export())==JsonSerializer.Serialize(host.Export()),"snapshot roundtrip");
imported.Upgrades[bonus]++;
Check(host.Upgrades[bonus]==1,"snapshot must not alias authority arrays");
Check(!RunProgress.Valid(host.Export() with{Level=0}),"invalid level rejected");
Check(!RunProgress.Valid(host.Export() with{Experience=-1}),"invalid XP rejected");
Check(!RunProgress.Valid(host.Export() with{Upgrades=new[]{999,0,0,0,0,0}}),"invalid bonus rejected");
Check(!RunProgress.Valid(host.Export() with{Pending=99}),"unearned choice rejected");
Check(!RunProgress.Valid(host.Export() with{Offer=new[]{0,0,1}}),"duplicate offer rejected");
for(int i=0;i<500;i++)
{
 var p=i%2==0?host:guest;var other=i%2==0?guest:host;
 string before=JsonSerializer.Serialize(other.Export());p.Award(50+i);
 Check(RunProgress.Valid(p.Export()),"earned state within bounds");
 while(p.PendingChoices>0){int serial=p.OfferSerial;Check(p.Choose(0),"consume earned upgrade");Check(p.PendingChoices==0||p.OfferSerial>serial,"offer identity advances");Check(RunProgress.Valid(p.Export()),"chosen state valid");}
 Check(JsonSerializer.Serialize(other.Export())==before,"other player's state untouched");
 Check(RunProgress.Valid(RunProgress.Import(p.Export()).Export()),"bounded roundtrip");
}
Check(new RunProgress(400).Level==1&&new RunProgress(400).Upgrades.All(x=>x==0),"new run resets personal bonuses");
var result=new{status="PASS",scope="Managed personal progression only; no network or multiplayer game acceptance",checks,hostKills=host.Kills,guestKills=guest.Kills,thirdPlayerKills=third.Kills};
Console.WriteLine(JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
