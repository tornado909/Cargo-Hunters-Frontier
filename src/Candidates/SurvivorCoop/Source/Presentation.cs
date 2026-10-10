#nullable enable
using System;using System.Collections.Generic;using UnityEngine;using UnityEngine.SceneManagement;using TMPro;
namespace Frontier.Survivor;
internal static class Presentation
{
 static GameObject? root;static Color oldAmbient;static float oldIntensity;static UnityEngine.Rendering.AmbientMode oldMode;static bool oldFog;static Light? oldSun;static bool active;static int sceneHandle;
 internal static void Add(Scene scene,TMP_FontAsset? font)
 {
  sceneHandle=scene.handle;root=new GameObject("Reserve.StockLightingAndSigns");SceneManager.MoveGameObjectToScene(root,scene);
  oldAmbient=RenderSettings.ambientLight;oldIntensity=RenderSettings.ambientIntensity;oldMode=RenderSettings.ambientMode;oldFog=RenderSettings.fog;oldSun=RenderSettings.sun;active=true;
  RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.35f,.39f,.44f);RenderSettings.ambientIntensity=1;RenderSettings.fog=false;
  // A restrained, shadow-free fill makes the native dynamic-only materials readable;
  // the original baked lightmaps cannot be reused after moving their source models.
  var keyObject=new GameObject("Reserve.InteriorFill");keyObject.transform.SetParent(root.transform,false);keyObject.transform.rotation=Quaternion.Euler(48,-25,0);
  var key=keyObject.AddComponent<Light>();key.type=LightType.Directional;key.color=new Color(.78f,.86f,1);key.intensity=.8f;key.shadows=LightShadows.None;RenderSettings.sun=key;
  foreach(var room in Content.Map.Rooms)
  {
   if(font!=null)
   {
    var go=new GameObject("Reserve.Sign."+room.Id);go.transform.SetParent(root.transform,false);go.transform.position=new Vector3(room.X,3.8f,room.Z+room.Depth*.5f-.2f);
    var text=go.AddComponent<TextMeshPro>();text.font=font;text.text=room.Name.ToUpperInvariant();text.fontSize=4;text.color=new Color(.93f,.94f,.91f);text.alignment=TextAlignmentOptions.Center;text.enableWordWrapping=false;text.rectTransform.sizeDelta=new Vector2(Math.Min(room.Width-2,24),1.5f);text.transform.localScale=Vector3.one*.55f;
   }
   foreach(int side in new[]{-1,1})
   {
    var go=new GameObject("Reserve.FixtureLight."+room.Id+"."+side);go.transform.SetParent(root.transform,false);
    go.transform.position=new Vector3(room.X+side*room.Width*.23f,5.45f,room.Z+side*room.Depth*.22f);
    var lamp=go.AddComponent<Light>();lamp.type=LightType.Point;lamp.intensity=2.6f;lamp.range=16;lamp.shadows=LightShadows.None;
    lamp.color=room.Id is "08" or "02" or "04"?new Color(1,.82f,.58f):room.Id is "06" or "09"?new Color(.62f,.79f,1):new Color(.87f,.93f,1);
   }
  }
  foreach(var a in Content.Map.Anchors)if(a.Kind=="exit")
  {
   var go=new GameObject("Reserve.ExitLight."+a.Id);go.transform.SetParent(root.transform,false);go.transform.position=new Vector3(a.Position[0],3.2f,a.Position[2]);var l=go.AddComponent<Light>();l.type=LightType.Point;l.color=new Color(.35f,1,.6f);l.range=7;l.intensity=1.4f;l.shadows=LightShadows.None;
  }
  // Camera-only PhysX proxies share exactly the source geometry of both native ECS worlds.
  int layer=LayerMask.NameToLayer("Default");foreach(var box in Content.Map.Boxes)
  {if(!box.Solid)continue;var go=new GameObject("Reserve.CameraCollision");go.transform.SetParent(root.transform,false);go.layer=layer<0?0:layer;go.transform.position=new Vector3(box.Center[0],box.Center[1],box.Center[2]);var collider=go.AddComponent<UnityEngine.BoxCollider>();collider.size=new Vector3(box.Size[0],box.Size[1],box.Size[2]);}
  Trace.Data["stockLighting"]=new {roomFixtures=Content.Map.Rooms.Length*2,exitFixtures=2,fillLights=1,shadowMaps=0,scope="Reserve scene only"};
 }
 internal static void Clear()
 {
  if(root!=null)UnityEngine.Object.Destroy(root);root=null;
  if(active&&SceneManager.GetActiveScene().handle==sceneHandle){RenderSettings.ambientMode=oldMode;RenderSettings.ambientLight=oldAmbient;RenderSettings.ambientIntensity=oldIntensity;RenderSettings.fog=oldFog;RenderSettings.sun=oldSun;}
  oldSun=null;active=false;
 }
}
