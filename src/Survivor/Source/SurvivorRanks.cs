#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using Frontier.Loot;
namespace Frontier.Survivor;
internal sealed class MeleeRankRow
{
 public string BaseId{get;set;}="";public string RankedId{get;set;}="";public string AiId{get;set;}="";public int Rank{get;set;}
 public double HealthFactor{get;set;}public double DamageFactor{get;set;}public double MoveFactor{get;set;}public double XpFactor{get;set;}
}
internal sealed class MeleeRankCatalog
{
 public string Format{get;set;}="";public Dictionary<string,string[]> Families{get;set;}=new();public MeleeRankRow[] Rows{get;set;}=Array.Empty<MeleeRankRow>();
}
internal static class SurvivorRanks
{
 static MeleeRankCatalog? catalog;
 static bool registered;
 internal static void Load()
 {
  if(catalog!=null)return;
  var data=JsonSerializer.Deserialize<MeleeRankCatalog>(File.ReadAllText(Path.Combine(Identity.Folder,"survivor-ranks.json")),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException("Survivor melee rank metadata missing");
  if(data.Format!="frontier.survivor-ranks/1"||data.Families.Count!=2||data.Rows.Length!=56)throw new InvalidDataException("Survivor melee rank dimensions");
  var ids=new HashSet<string>(StringComparer.Ordinal);
  foreach(var row in data.Rows)
  {
   if(row.Rank<1||row.Rank>28||!Guid.TryParse(row.RankedId,out _)||!Guid.TryParse(row.AiId,out _)||!ids.Add(row.RankedId)||!data.Families.TryGetValue(row.BaseId,out var family)||family.Length!=28||family[row.Rank-1]!=row.RankedId)throw new InvalidDataException("Survivor melee rank identity mismatch");
   if(Math.Abs(row.HealthFactor-RankPolicy.EnemyHealth(row.Rank))>1e-8||Math.Abs(row.DamageFactor-RankPolicy.EnemyDamage(row.Rank))>1e-8||Math.Abs(row.MoveFactor-RankPolicy.EnemyMove(row.Rank))>1e-8||Math.Abs(row.XpFactor-RankPolicy.EnemyXP(row.Rank))>1e-8)throw new InvalidDataException("Melee rank factors differ from the installed Frontier policy");
  }
  catalog=data;
 }
 internal static string[] Variants(string baseId)
 {
  Load();if(catalog!.Families.TryGetValue(baseId,out var ids))return ids;
  if(RankLadder.Data?.BotVariants.TryGetValue(baseId,out ids)==true)return ids;
  throw new InvalidDataException("No existing or Survivor-owned native rank family: "+baseId);
 }
 internal static void RegisterPresentation()
 {
  if(registered)return;Load();
  if(!RankLadder.Active)throw new InvalidOperationException("Existing Rank28 runtime has not activated");
  var core=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="Frontier.Core");
  var type=core.GetType("Frontier.Core.ElitePresentation",true)!;
  var table=(IDictionary)(type.GetField("ByPreset",BindingFlags.Static|BindingFlags.NonPublic)?.GetValue(null)??throw new MissingFieldException("Existing rank presentation registry"));
  foreach(var row in catalog!.Rows)
  {
   string value=RankPolicy.Name(row.Rank);
   if(table.Contains(row.RankedId)&&!Equals(table[row.RankedId],value))throw new InvalidDataException("Conflicting enemy rank presentation ID");
   table[row.RankedId]=value;
  }
  registered=true;Trace.Data["survivorMeleeRankIntegration"]=new{newPresetIds=56,rankFactorsVerifiedAgainstInstalledPolicy=true,existingPresetsModified=false,sharedRandomSpawnAllowlistModified=false};
 }
}
