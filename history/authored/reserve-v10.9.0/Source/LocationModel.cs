#nullable enable
using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Text.Json;using System.Security.Cryptography;
namespace Frontier.Reserve;
public sealed class BoxDef { public string Id{get;set;}="";public float[] Center{get;set;}=Array.Empty<float>();public float[] Size{get;set;}=Array.Empty<float>();public string Material{get;set;}="";public string Kind{get;set;}="";public bool Solid{get;set;}}
public sealed class Anchor {public string Id{get;set;}="";public string Kind{get;set;}="";public string Room{get;set;}="";public float[] Position{get;set;}=Array.Empty<float>();}
public sealed class RoomDef {public string Id{get;set;}="";public string Name{get;set;}="";public float X{get;set;}public float Z{get;set;}public float Width{get;set;}public float Depth{get;set;}}
public sealed class AgentDef {public float Radius{get;set;}public float Height{get;set;}public float MaxStep{get;set;}}
public sealed class MaterialDef {public string Color{get;set;}="ffffff";public float Roughness{get;set;}public string? Emission{get;set;}}
public sealed class LinkDef {public string From{get;set;}="";public string To{get;set;}="";}
public sealed class LocationDefinition {
 public string Format{get;set;}="";public string Revision{get;set;}="";public string Name{get;set;}="";public string Units{get;set;}="";public string UpAxis{get;set;}="";
 public AgentDef Agent{get;set;}=new();public RoomDef[] Rooms{get;set;}=Array.Empty<RoomDef>();public LinkDef[] Links{get;set;}=Array.Empty<LinkDef>();
 public BoxDef[] Boxes{get;set;}=Array.Empty<BoxDef>();public Anchor[] Anchors{get;set;}=Array.Empty<Anchor>();public Dictionary<string,MaterialDef> Materials{get;set;}=new();
 public static LocationDefinition Load(string path){var bytes=File.ReadAllBytes(path);if(bytes.Length>8_000_000)throw new InvalidDataException("Location exceeds 8 MB");var result=JsonSerializer.Deserialize<LocationDefinition>(bytes,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException("Empty location");result.Validate();return result;}
 public void Validate(){
  if(Format!="frontier.reserve.location/1"||Units!="metres"||UpAxis!="Y")throw new InvalidDataException("Unsupported location coordinates or format");
  if(Boxes.Length<1||Boxes.Length>12000||Anchors.Length>1024||Rooms.Length<1||Rooms.Length>128)throw new InvalidDataException("Object budget exceeded");
  if(!float.IsFinite(Agent.Radius)||Agent.Radius<.2f||Agent.Radius>2||!float.IsFinite(Agent.Height)||Agent.Height<1||Agent.Height>5)throw new InvalidDataException("Invalid agent dimensions");
  var ids=new HashSet<string>(StringComparer.Ordinal);
  foreach(var b in Boxes){if(string.IsNullOrWhiteSpace(b.Id)||!ids.Add(b.Id))throw new InvalidDataException("Duplicate/empty box ID");V3(b.Center);V3(b.Size);if(b.Size.Any(v=>v<=0||v>1000))throw new InvalidDataException("Invalid box size: "+b.Id);if(!Materials.ContainsKey(b.Material))throw new InvalidDataException("Unknown material: "+b.Material);}
  ids.Clear();foreach(var a in Anchors){if(!ids.Add(a.Id))throw new InvalidDataException("Duplicate anchor");V3(a.Position);if(!Rooms.Any(r=>r.Id==a.Room))throw new InvalidDataException("Anchor room missing");if(a.Kind is not ("player" or "enemy" or "loot" or "exit" or "patrol"))throw new InvalidDataException("Unknown anchor kind");}
  if(Anchors.Count(a=>a.Kind=="exit")<1||Anchors.Count(a=>a.Kind=="player")<1)throw new InvalidDataException("No entry or exit");
 }
 static void V3(float[] v){if(v.Length!=3||v.Any(x=>!float.IsFinite(x)||Math.Abs(x)>10000))throw new InvalidDataException("Invalid vector");}
 public string CollisionFingerprint(){using var h=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);foreach(var b in Boxes.Where(b=>b.Solid).OrderBy(b=>b.Id,StringComparer.Ordinal)){h.AppendData(System.Text.Encoding.UTF8.GetBytes(b.Id+"\0"));foreach(var v in b.Center.Concat(b.Size))h.AppendData(BitConverter.GetBytes(v));}return Convert.ToHexString(h.GetHashAndReset()).ToLowerInvariant();}
}
public sealed class WalkGrid {
 public float Step{get;}public float MinX{get;}public float MinZ{get;}public int Width{get;}public int Depth{get;}public bool[] Walkable{get;}
 readonly LocationDefinition source;readonly BoxDef[] floors;readonly BoxDef[] obstacles;
 public WalkGrid(LocationDefinition d,float step=.5f){source=d;d.Validate();if(step<.25||step>2)throw new ArgumentOutOfRangeException(nameof(step));Step=step;
  floors=d.Boxes.Where(b=>b.Solid&&b.Kind=="floor").ToArray();obstacles=d.Boxes.Where(b=>b.Solid&&b.Kind!="floor"&&b.Center[1]-b.Size[1]/2<d.Agent.Height&&b.Center[1]+b.Size[1]/2>.05).ToArray();
  if(floors.Length==0)throw new InvalidDataException("No floor");MinX=floors.Min(b=>b.Center[0]-b.Size[0]/2);MinZ=floors.Min(b=>b.Center[2]-b.Size[2]/2);
  Width=(int)Math.Ceiling((floors.Max(b=>b.Center[0]+b.Size[0]/2)-MinX)/step);Depth=(int)Math.Ceiling((floors.Max(b=>b.Center[2]+b.Size[2]/2)-MinZ)/step);
  if((long)Width*Depth>1_000_000)throw new InvalidDataException("Navigation grid budget exceeded");Walkable=new bool[Width*Depth];for(int j=0;j<Depth;j++)for(int i=0;i<Width;i++){var (x,z)=Position(i+j*Width);Walkable[i+j*Width]=CanStand(x,z);}}
 public bool CanStand(float x,float z){float r=source.Agent.Radius;
  // Conservative square footprint covers the circular agent including diagonals.
  foreach(var dx in new[]{-r,0,r})foreach(var dz in new[]{-r,0,r})if(!floors.Any(b=>InXZ(b,x+dx,z+dz,.0001f)))return false;
  return !obstacles.Any(b=>InXZ(b,x,z,r));}
 static bool InXZ(BoxDef b,float x,float z,float pad)=>Math.Abs(x-b.Center[0])<b.Size[0]/2+pad&&Math.Abs(z-b.Center[2])<b.Size[2]/2+pad;
 public (float X,float Z) Position(int i)=>(MinX+(i%Width+.5f)*Step,MinZ+(i/Width+.5f)*Step);
 public int Index(float x,float z){int i=(int)Math.Floor((x-MinX)/Step),j=(int)Math.Floor((z-MinZ)/Step);return i>=0&&i<Width&&j>=0&&j<Depth?i+j*Width:-1;}
 public IEnumerable<int> Neighbors(int i){int x=i%Width,z=i/Width;if(x>0&&Walkable[i-1])yield return i-1;if(x<Width-1&&Walkable[i+1])yield return i+1;if(z>0&&Walkable[i-Width])yield return i-Width;if(z<Depth-1&&Walkable[i+Width])yield return i+Width;}
 public int[] Flood(int start){var dist=Enumerable.Repeat(-1,Walkable.Length).ToArray();if(start<0||!Walkable[start])return dist;var q=new Queue<int>();q.Enqueue(start);dist[start]=0;while(q.Count>0){var n=q.Dequeue();foreach(var k in Neighbors(n))if(dist[k]<0){dist[k]=dist[n]+1;q.Enqueue(k);}}return dist;}
 public bool BlocksRay(float[] start,float[] end){return source.Boxes.Where(b=>b.Solid).Any(b=>SegmentIntersects(b,start,end));}
 public static bool SegmentIntersects(BoxDef b,float[] a,float[] c){float lo=0,hi=1;for(int k=0;k<3;k++){var delta=c[k]-a[k];var min=b.Center[k]-b.Size[k]/2;var max=b.Center[k]+b.Size[k]/2;if(Math.Abs(delta)<1e-7){if(a[k]<min||a[k]>max)return false;continue;}var p=(min-a[k])/delta;var q=(max-a[k])/delta;if(p>q)(p,q)=(q,p);lo=Math.Max(lo,p);hi=Math.Min(hi,q);if(lo>hi)return false;}return hi>=0&&lo<=1;}
}
