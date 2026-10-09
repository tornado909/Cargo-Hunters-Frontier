#nullable enable
using System;
using System.Linq;
using System.Collections.Generic;
namespace Frontier.Survivor;

// Run-local data only: never writes Account skills, persistent items or saved character levels.
internal sealed class RunProgress
{
 internal int Level=1, Experience, Kills, PendingChoices;
 internal readonly int[] Upgrades=new int[6];
 internal int[] Offer=Array.Empty<int>();
 readonly Random random;
 internal RunProgress(int seed){random=new Random(seed);}
 internal int Required=>40+25*(Level-1);
 internal float Damage=>.15f*Upgrades[0];
 internal float FireRate=>.10f*Upgrades[1];
 internal float Crit=>.05f*Upgrades[2];
 internal float CritPower=>.20f*Upgrades[3];
 internal int Penetration=>Upgrades[4];
 internal float ExperienceFactor=>1+.2f*Upgrades[5];
 internal static readonly string[] Names={"Усилитель урона","Разгон затвора","Точный удар","Сила критического удара","Бронебойность","Обучение"};
 internal static readonly string[] Descriptions={"+15% урона","+10% скорострельности","+5 п.п. шанса крита","+20% множителя крита","+1 к пробитию","+20% опыта в забеге"};
 internal static readonly int[] Caps={30,15,12,20,10,10};
 internal void Award(int nativeExperience)
 {
  Kills++;
  Experience=checked(Experience+(int)Math.Clamp(Math.Round(Math.Max(1,nativeExperience)*ExperienceFactor),1,100000));
  while(Level<100 && Experience>=Required){Experience-=Required;Level++;PendingChoices++;}
  if(Level==100)Experience=Math.Min(Experience,Required-1);
  MakeOffer();
 }
 internal bool Choose(int index)
 {
  if(PendingChoices<1||index<0||index>=Offer.Length)return false;
  int selected=Offer[index];if(Upgrades[selected]>=Caps[selected])return false;
  Upgrades[selected]++;PendingChoices--;Offer=Array.Empty<int>();MakeOffer();return true;
 }
 void MakeOffer()
 {
  if(PendingChoices<1||Offer.Length>0)return;
  var available=Enumerable.Range(0,6).Where(i=>Upgrades[i]<Caps[i]).ToList();
  if(available.Count==0){PendingChoices=0;return;}
  for(int i=available.Count-1;i>0;i--){int j=random.Next(i+1);(available[i],available[j])=(available[j],available[i]);}
  Offer=available.Take(3).ToArray();
 }
}
