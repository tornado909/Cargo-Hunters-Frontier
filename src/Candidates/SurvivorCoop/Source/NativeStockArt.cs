#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace Frontier.Survivor;
internal sealed class StockFile { public string Id{get;set;}=""; public string File{get;set;}=""; public string Sha256{get;set;}=""; public string Name{get;set;}=""; public int Width{get;set;} public int Height{get;set;} }
internal sealed class StockTextureBinding { public string Property{get;set;}=""; public string Texture{get;set;}=""; public float[] Scale{get;set;}=Array.Empty<float>(); public float[] Offset{get;set;}=Array.Empty<float>(); }
internal sealed class StockMaterial { public string Id{get;set;}=""; public string Name{get;set;}=""; public string Shader{get;set;}=""; public StockTextureBinding[] Textures{get;set;}=Array.Empty<StockTextureBinding>(); public Dictionary<string,float[]> Colors{get;set;}=new(); public Dictionary<string,float> Floats{get;set;}=new(); public int RenderQueue{get;set;}=-1; public string? Keywords{get;set;} }
internal sealed class StockPart { public string Mesh{get;set;}=""; public string[] Materials{get;set;}=Array.Empty<string>(); }
internal sealed class StockPrefab { public string Id{get;set;}=""; public string Name{get;set;}=""; public StockPart[] Parts{get;set;}=Array.Empty<StockPart>(); public float[] BoundsMin{get;set;}=Array.Empty<float>(); public float[] BoundsMax{get;set;}=Array.Empty<float>(); public float[] Size{get;set;}=Array.Empty<float>(); }
internal sealed class StockManifest { public string Format{get;set;}=""; public StockPrefab[] Prefabs{get;set;}=Array.Empty<StockPrefab>(); public StockFile[] Meshes{get;set;}=Array.Empty<StockFile>(); public StockFile[] Textures{get;set;}=Array.Empty<StockFile>(); public StockMaterial[] Materials{get;set;}=Array.Empty<StockMaterial>(); }
internal sealed class StockInstance { public string Id{get;set;}=""; public string Zone{get;set;}=""; public string Asset{get;set;}=""; public float[] Center{get;set;}=Array.Empty<float>(); public float[] Size{get;set;}=Array.Empty<float>(); public int Yaw{get;set;} public int Pitch{get;set;} }
internal sealed class StockLayout { public string Format{get;set;}=""; public string CollisionFingerprint{get;set;}=""; public StockInstance[] Instances{get;set;}=Array.Empty<StockInstance>(); }

// Original model geometry, normals, UVs and original game material bindings only.
// No hidden loading of a donor gameplay scene and no primitive/cube visual fallback.
public sealed class NativeVisualLayer : IDisposable
{
    readonly NativeBuildBarrier barrier;
    readonly LeaseBag roots = new();
    readonly List<UnityEngine.Object> resources = new();
    readonly Dictionary<string,Material> materials = new(StringComparer.Ordinal);
    readonly Dictionary<string,Texture2D> textures = new(StringComparer.Ordinal);
    readonly Dictionary<string,MeshData> data = new(StringComparer.Ordinal);
    readonly Dictionary<string,Batch> batches = new(StringComparer.Ordinal);
    bool disposed;
    public GameObject Root { get; }
    public string CollisionFingerprint { get; }
    public int BoxCount { get; }
    sealed class MeshData { internal int Count; internal Vector3[] Positions=Array.Empty<Vector3>(),Normals=Array.Empty<Vector3>(); internal Vector2[] Uv=Array.Empty<Vector2>(); internal Vector4[] Tangents=Array.Empty<Vector4>(); internal Color[] Colors=Array.Empty<Color>(); internal int[][] Indices=Array.Empty<int[]>(); }
    sealed class Batch { internal string Material=""; internal string Zone=""; internal readonly List<Vector3> Positions=new(),Normals=new(); internal readonly List<Vector2> Uv=new(); internal readonly List<Vector4> Tangents=new(); internal readonly List<Color> Colors=new(); internal readonly List<int> Indices=new(); }
    static readonly JsonSerializerOptions Json = new(){PropertyNameCaseInsensitive=true};
    T Keep<T>(T value) where T:UnityEngine.Object { roots.Keep(value); resources.Add(value); return value; }
    static T Read<T>(string path) { var bytes=File.ReadAllBytes(path); if(bytes.Length>8_000_000)throw new InvalidDataException("Stock art manifest budget");return JsonSerializer.Deserialize<T>(bytes,Json)??throw new InvalidDataException("Invalid stock art JSON"); }
    static void Vec(float[] v,int n) {if(v.Length!=n||v.Any(x=>!float.IsFinite(x)||Math.Abs(x)>10000))throw new InvalidDataException("Invalid stock art vector");}
    static byte[] Bytes(string folder,StockFile entry)
    {
        string root=Path.GetFullPath(folder)+Path.DirectorySeparatorChar;
        string path=Path.GetFullPath(Path.Combine(folder,entry.File));
        if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase)||entry.Sha256.Length!=64)throw new InvalidDataException("Invalid stock file reference");
        var bytes=File.ReadAllBytes(path);
        if(bytes.Length>32_000_000||!string.Equals(Convert.ToHexString(SHA256.HashData(bytes)),entry.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Stock art hash/budget mismatch: "+entry.Name);
        return bytes;
    }
    public NativeVisualLayer(LocationDefinition def,NativeBuildBarrier gate,Vector3 origin,Shader verifiedShader)
    {
        barrier=gate; gate.Check();def.Validate(); CollisionFingerprint=def.CollisionFingerprint();
        Root=new GameObject("Frontier.Survivor.NativeStockEnvironment");Root.SetActive(false);Root.transform.position=origin;
        try
        {
            string folder=Identity.Folder;
            var manifest=Read<StockManifest>(Path.Combine(folder,"stock-art.json"));
            var layout=Read<StockLayout>(Path.Combine(Identity.Folder,"stock-layout.json"));
            if(manifest.Format!="frontier.native-art/1"||layout.Format!="frontier.stock-layout/1"||layout.CollisionFingerprint!=CollisionFingerprint||layout.Instances.Length<1||layout.Instances.Length>5000||manifest.Meshes.Length>128||manifest.Textures.Length>128||manifest.Materials.Length>128)
                throw new InvalidDataException("Native art / gameplay geometry contract mismatch");
            foreach(var entry in manifest.Textures)
            {
                if(entry.Width<1||entry.Height<1||entry.Width>4096||entry.Height>4096)throw new InvalidDataException("Stock texture dimensions");
                var image=Keep(new Texture2D(2,2,TextureFormat.RGBA32,true));image.name=entry.Name;
                if(!ImageConversion.LoadImage(image,(Il2CppStructArray<byte>)Bytes(folder,entry),true)||image.width!=entry.Width||image.height!=entry.Height)throw new InvalidDataException("Original texture decode failed: "+entry.Name);
                image.wrapMode=TextureWrapMode.Repeat;image.filterMode=FilterMode.Trilinear;image.anisoLevel=4;textures.Add(entry.Id,image);
            }
            foreach(var entry in manifest.Materials)
            {
                var shader=Shader.Find(entry.Shader);
                if(shader==null||!shader.isSupported)throw new InvalidDataException("Original game shader unavailable: "+entry.Shader);
                var material=Keep(new Material(shader));material.name="Reserve.Stock."+entry.Name;
                foreach(var property in entry.Floats)if(material.HasProperty(property.Key)){if(!float.IsFinite(property.Value))throw new InvalidDataException("Material float");material.SetFloat(property.Key,property.Value);}
                foreach(var property in entry.Colors)if(material.HasProperty(property.Key)){Vec(property.Value,4);var a=property.Value;material.SetVector(property.Key,new Vector4(a[0],a[1],a[2],a[3]));}
                foreach(var binding in entry.Textures)if(material.HasProperty(binding.Property))
                {Vec(binding.Scale,2);Vec(binding.Offset,2);material.SetTexture(binding.Property,textures[binding.Texture]);material.SetTextureScale(binding.Property,new Vector2(binding.Scale[0],binding.Scale[1]));material.SetTextureOffset(binding.Property,new Vector2(binding.Offset[0],binding.Offset[1]));}
                if(entry.RenderQueue>=0)material.renderQueue=entry.RenderQueue;
                if(!string.IsNullOrWhiteSpace(entry.Keywords))foreach(string keyword in entry.Keywords.Split(' ',StringSplitOptions.RemoveEmptyEntries))material.EnableKeyword(keyword);
                materials.Add(entry.Id,material);
            }
            foreach(var mesh in manifest.Meshes)data.Add(mesh.Id,Decode(Bytes(folder,mesh)));
            var prefabs=manifest.Prefabs.ToDictionary(x=>x.Id,StringComparer.Ordinal);var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var instance in layout.Instances)
            {
                if(!ids.Add(instance.Id)||!prefabs.TryGetValue(instance.Asset,out var prefab)||instance.Yaw%90!=0||instance.Pitch is not (0 or 180))throw new InvalidDataException("Invalid stock placement identity/orientation");
                Vec(instance.Center,3);Vec(instance.Size,3);Vec(prefab.BoundsMin,3);Vec(prefab.BoundsMax,3);Vec(prefab.Size,3);
                if(instance.Size.Any(x=>x<=0)||prefab.Size.Any(x=>x<=0))throw new InvalidDataException("Invalid stock placement dimensions");
                foreach(var part in prefab.Parts)
                {
                    var mesh=data[part.Mesh];if(part.Materials.Length!=mesh.Indices.Length)throw new InvalidDataException("Original model material mapping differs");
                    for(int sub=0;sub<part.Materials.Length;sub++)Append(instance,prefab,mesh,sub,part.Materials[sub]);
                }
            }
            foreach(var batch in batches.Values)Flush(batch);
            BoxCount=layout.Instances.Length;
            Trace.Data["nativeStockArt"]=new {prefabModels=manifest.Prefabs.Length,originalMeshes=data.Count,originalTextures=textures.Count,originalMaterials=materials.Count,instances=BoxCount,renderBatches=batches.Count,visualPrimitiveFallbacks=0,collisionFingerprint=CollisionFingerprint};
            data.Clear();batches.Clear();
        }
        catch{Dispose();throw;}
    }
    static MeshData Decode(byte[] bytes)
    {
        using var r=new BinaryReader(new MemoryStream(bytes,false));if(new string(r.ReadChars(4))!="FNM1")throw new InvalidDataException("Original mesh format");
        int n=checked((int)r.ReadUInt32()),s=checked((int)r.ReadUInt32());uint flags=r.ReadUInt32();
        if(n<1||n>60000||s<1||s>16||(flags&3)!=3||(flags&~31u)!=0)throw new InvalidDataException("Original mesh budget/attributes");
        float F(){float f=r.ReadSingle();if(!float.IsFinite(f))throw new InvalidDataException("Nonfinite original vertex");return f;}
        var mesh=new MeshData{Count=n,Positions=new Vector3[n],Normals=new Vector3[n],Uv=new Vector2[n],Tangents=new Vector4[n],Colors=new Color[n],Indices=new int[s][]};
        for(int i=0;i<n;i++)mesh.Positions[i]=new Vector3(F(),F(),F());
        for(int i=0;i<n;i++)mesh.Normals[i]=new Vector3(F(),F(),F());
        for(int i=0;i<n;i++)mesh.Uv[i]=new Vector2(F(),F());
        for(int i=0;i<n;i++)mesh.Tangents[i]=(flags&4)!=0?new Vector4(F(),F(),F(),F()):new Vector4(1,0,0,1);
        for(int i=0;i<n;i++)mesh.Colors[i]=(flags&8)!=0?new Color(F(),F(),F(),F()):Color.white;
        if((flags&16)!=0)for(int i=0;i<n;i++){F();F();} // Source baked lightmap coordinates are not reused in a different scene.
        for(int k=0;k<s;k++){int count=checked((int)r.ReadUInt32());if(count<3||count%3!=0||count>900000)throw new InvalidDataException("Original indices budget");var indices=new int[count];for(int i=0;i<count;i++){indices[i]=checked((int)r.ReadUInt32());if(indices[i]>=n)throw new InvalidDataException("Original mesh index");}mesh.Indices[k]=indices;}
        if(r.BaseStream.Position!=bytes.Length)throw new InvalidDataException("Trailing original mesh data");return mesh;
    }
    void Append(StockInstance a,StockPrefab p,MeshData mesh,int sub,string material)
    {
        if(!materials.ContainsKey(material))throw new InvalidDataException("Unknown stock material");
        string key=a.Zone+"|"+material;if(!batches.TryGetValue(key,out var batch))batches.Add(key,batch=new Batch{Zone=a.Zone,Material=material});
        if(batch.Positions.Count+mesh.Count>60000){Flush(batch);batch.Positions.Clear();batch.Normals.Clear();batch.Uv.Clear();batch.Tangents.Clear();batch.Colors.Clear();batch.Indices.Clear();}
        float sx=a.Size[0]/p.Size[0],sy=a.Size[1]/p.Size[1],sz=a.Size[2]/p.Size[2];
        float ox=(p.BoundsMin[0]+p.BoundsMax[0])*.5f,oy=(p.BoundsMin[1]+p.BoundsMax[1])*.5f,oz=(p.BoundsMin[2]+p.BoundsMax[2])*.5f;
        int yaw=(a.Yaw%360+360)%360;float c=yaw==0?1:yaw==180?-1:0,s=yaw==90?1:yaw==270?-1:0;int start=batch.Positions.Count;
        for(int i=0;i<mesh.Count;i++)
        {
            var v=mesh.Positions[i];float x=(v.x-ox)*sx,y=(v.y-oy)*sy,z=(v.z-oz)*sz;float pitchSign=a.Pitch==180?-1:1;y*=pitchSign;z*=pitchSign;
            batch.Positions.Add(new Vector3(a.Center[0]+c*x+s*z,a.Center[1]+y,a.Center[2]-s*x+c*z));
            var n=mesh.Normals[i];x=n.x/sx;y=n.y/sy*pitchSign;z=n.z/sz*pitchSign;float norm=MathF.Sqrt(x*x+y*y+z*z);if(norm<1e-8f)norm=1;
            batch.Normals.Add(new Vector3((c*x+s*z)/norm,y/norm,(-s*x+c*z)/norm));batch.Uv.Add(mesh.Uv[i]);
            var t=mesh.Tangents[i];x=t.x*sx;y=t.y*sy*pitchSign;z=t.z*sz*pitchSign;norm=MathF.Sqrt(x*x+y*y+z*z);if(norm<1e-8f)norm=1;
            batch.Tangents.Add(new Vector4((c*x+s*z)/norm,y/norm,(-s*x+c*z)/norm,t.w));batch.Colors.Add(mesh.Colors[i]);
        }
        foreach(int index in mesh.Indices[sub])batch.Indices.Add(start+index);
    }
    void Flush(Batch batch)
    {
        if(batch.Indices.Count==0)return;
        var mesh=Keep(new Mesh());mesh.name="Reserve.StockBatch."+batch.Zone;
        mesh.vertices=(Il2CppStructArray<Vector3>)batch.Positions.ToArray();mesh.normals=(Il2CppStructArray<Vector3>)batch.Normals.ToArray();mesh.uv=(Il2CppStructArray<Vector2>)batch.Uv.ToArray();mesh.tangents=(Il2CppStructArray<Vector4>)batch.Tangents.ToArray();mesh.colors=(Il2CppStructArray<Color>)batch.Colors.ToArray();mesh.triangles=(Il2CppStructArray<int>)batch.Indices.ToArray();mesh.RecalculateBounds();
        var go=new GameObject(mesh.name);go.transform.SetParent(Root.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=materials[batch.Material];renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;renderer.receiveShadows=true;
    }
    public void Activate(){barrier.Check();if(disposed)throw new ObjectDisposedException(nameof(NativeVisualLayer));Root.SetActive(true);}
    public void Dispose(){if(disposed)return;barrier.Check();if(Root!=null)UnityEngine.Object.Destroy(Root);foreach(var obj in resources)if(obj!=null)UnityEngine.Object.Destroy(obj);resources.Clear();roots.Dispose();materials.Clear();textures.Clear();data.Clear();batches.Clear();disposed=true;}
}
