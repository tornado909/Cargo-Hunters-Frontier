#nullable enable
using System;using System.IO;using System.Linq;using System.Threading;using System.Collections.Generic;using System.Security.Cryptography;using System.Text;using System.Text.Json;using BepInEx.Logging;using Il2CppInterop.Runtime;using Il2CppInterop.Runtime.InteropTypes;using Il2CppInterop.Runtime.InteropTypes.Arrays;using Core.Utils;
namespace Frontier.Survivor;
internal static class Identity {
 internal const string Map="Frontier_Survivor";internal const int MapId=3466001;internal const string Version="0.1.3";internal const string Revision="v10.10.1-survivor.4";
 internal static string Folder="";internal static string Hash="";
 internal static UniqueId Id(string name){var h=SHA256.HashData(Encoding.UTF8.GetBytes("frontier.survivor.v1:"+name));var b=h.Take(16).ToArray();b[7]=(byte)((b[7]&15)|0x50);b[8]=(byte)((b[8]&63)|0x80);return UniqueId.Parse(new Guid(b).ToString());}
 internal static bool Own(string? name)=>string.Equals(name,Map,StringComparison.Ordinal);
 internal static int Ordinal(Anchor a,LocationDefinition def)=>Array.IndexOf(def.Anchors,a)+3466100;
 internal static int Count<T>(Il2CppSystem.Collections.Generic.IReadOnlyList<T>? list,int limit=50000){if(list==null)return 0;var n=list.TryCast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<T>>()?.Count??0;if(n<0||n>limit)throw new InvalidDataException("Native repository exceeds limit");return n;}
 internal static Il2CppStructArray<byte> Salt(Il2CppStructArray<byte>? hash,string key){byte[] a=hash==null?Array.Empty<byte>():(byte[])hash;return (Il2CppStructArray<byte>)SHA256.HashData(a.Concat(Encoding.UTF8.GetBytes(Revision+"|"+Hash+"|"+key)).ToArray());}
}
internal static class Trace {
 internal static readonly Dictionary<string,object?> Data=new();internal static ManualLogSource Log=null!;private static double next;private static string pending="";private static int busy;private static string path="";private static readonly object gate=new();
 internal static void Init(ManualLogSource log){Log=log;path=Path.Combine(BepInEx.Paths.BepInExRootPath,"Frontier-survivor-report.json");Data["version"]=Identity.Version;Data["packRevision"]=Identity.Revision;Data["nativePlaytest"]=false;Data["mapId"]=Identity.MapId;Data["mapName"]=Identity.Map;Data["saveWritesByPlugin"]=0;Data["status"]="WAITING_FOR_REPOSITORIES";}
 internal static void Add(string key,int n=1){Data[key]=(Data.TryGetValue(key,out var v)&&v is long l?l:0L)+n;}
 internal static void Error(string stage,Exception e){string text=e.GetBaseException().Message;Data["lastError"]=stage+": "+text;Log.LogError(stage+": "+e);Save(true);}
 internal static void Save(bool force=false){var now=UnityEngine.Time.realtimeSinceStartupAsDouble;if(!force&&now<next)return;next=now+5;Data["utc"]=DateTime.UtcNow.ToString("O");Data["reportWriteMode"]=ReportFile.Mode;string text=JsonSerializer.Serialize(Data,new JsonSerializerOptions{WriteIndented=true});lock(gate)pending=text;if(Interlocked.CompareExchange(ref busy,1,0)!=0)return;ThreadPool.QueueUserWorkItem(_=>{try{while(true){string s;lock(gate){s=pending;pending="";}if(s.Length==0)break;string temp=path+".tmp";File.WriteAllText(temp,s,Encoding.UTF8);ReportFile.Publish(temp,path,s);}}catch(Exception e){Log.LogWarning("Reserve report: "+e.Message);}finally{Interlocked.Exchange(ref busy,0);}});}
}
internal sealed class LeaseBag:IDisposable {readonly List<NativeRoot> roots=new();internal T Keep<T>(T obj) where T:Il2CppObjectBase{roots.Add(new NativeRoot(obj));return obj;}public void Dispose(){foreach(var r in roots)r.Dispose();roots.Clear();}}
