using System;using System.IO;using System.Linq;using System.Text.Json;using System.Collections.Generic;using Frontier.Reserve;
class GeometryTests {
 static int checks;static void Check(bool a,string s){checks++;if(!a)throw new Exception(s);}
 static int Main(string[] args){try{
 var d=LocationDefinition.Load(args[0]);var g=new WalkGrid(d);var p=d.Anchors.Where(a=>a.Kind=="player").ToArray();var exits=d.Anchors.Where(a=>a.Kind=="exit").ToArray();
 Check(d.Rooms.Length==12,"Twelve rooms");Check(p.Length==4,"Four entries");Check(exits.Length==2,"Two exits");Check(d.CollisionFingerprint()==d.CollisionFingerprint(),"Deterministic fingerprint");
 foreach(var b in d.Boxes){Check(b.Center.All(float.IsFinite),"Coordinates finite");Check(b.Size.All(v=>v>0),"Extents positive");}
 foreach(var a in d.Anchors)Check(g.CanStand(a.Position[0],a.Position[2]),"Anchor blocked by geometry: "+a.Id);
 var routes=new List<object>();foreach(var start in p){int si=g.Index(start.Position[0],start.Position[2]);var dist=g.Flood(si);
 Check(dist.Count(i=>i>=0)==g.Walkable.Count(v=>v),"Walkable region disconnected");
 foreach(var a in d.Anchors){int ai=g.Index(a.Position[0],a.Position[2]);Check(ai>=0&&dist[ai]>=0,"Unreachable anchor: "+a.Id);}
 foreach(var e in exits){int steps=dist[g.Index(e.Position[0],e.Position[2])];routes.Add(new{entry=start.Id,exit=e.Id,gridRouteMetres=steps*g.Step});}
 foreach(var e in d.Anchors.Where(a=>a.Kind=="enemy")){float dx=e.Position[0]-start.Position[0],dz=e.Position[2]-start.Position[2];Check(Math.Sqrt(dx*dx+dz*dz)>=24,"Enemy too close to entry");}
 }
 var aabb=new BoxDef{Center=new[]{0f,1f,0f},Size=new[]{1f,2f,1f}};
 Check(WalkGrid.SegmentIntersects(aabb,new[]{-2f,1f,0f},new[]{2f,1f,0f}),"Wall ray");Check(!WalkGrid.SegmentIntersects(aabb,new[]{-2f,3f,0f},new[]{2f,3f,0f}),"Ray above wall");
 var bad=JsonSerializer.Deserialize<LocationDefinition>(JsonSerializer.Serialize(d))!;bad.Boxes[0].Size[0]=float.NaN;bool rejected=false;try{bad.Validate();}catch(InvalidDataException){rejected=true;}Check(rejected,"NaN rejected");
 var r=new{status="PASS",scope="Canonical geometry and conservative offline walkability; NOT Unity NavMesh or Cargo Hunters playtest",assertions=checks,rooms=d.Rooms.Length,solidBoxes=d.Boxes.Count(b=>b.Solid),decorativeBoxes=d.Boxes.Count(b=>!b.Solid),anchors=d.Anchors.Length,gridCellMetres=g.Step,walkableCells=g.Walkable.Count(v=>v),collisionSha256=d.CollisionFingerprint(),routes};
 File.WriteAllText(args[1],JsonSerializer.Serialize(r,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(r));return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
