#nullable enable
using System;using System.Collections.Generic;using UnityEngine;using UnityEngine.SceneManagement;using TMPro;
namespace Frontier.Reserve;
internal static class Presentation {
 static GameObject? root;static readonly List<UnityEngine.Material> materials=new();static Color oldAmbient;static float oldIntensity;static UnityEngine.Rendering.AmbientMode oldMode;static bool oldFog;static bool active;static int sceneHandle;
 internal static void Add(Scene scene,TMP_FontAsset? font){sceneHandle=scene.handle;root=new GameObject("Reserve.LightingAndSigns");SceneManager.MoveGameObjectToScene(root,scene);oldAmbient=RenderSettings.ambientLight;oldIntensity=RenderSettings.ambientIntensity;oldMode=RenderSettings.ambientMode;oldFog=RenderSettings.fog;active=true;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.26f,.29f,.32f);RenderSettings.ambientIntensity=1;RenderSettings.fog=false;
  foreach(var room in Content.Map.Rooms){if(font!=null){var go=new GameObject("Reserve.Sign."+room.Id);go.transform.SetParent(root.transform,false);go.transform.position=new Vector3(room.X,3.5f,room.Z+room.Depth*.5f-.35f);var text=go.AddComponent<TextMeshPro>();text.font=font;text.text=room.Name.ToUpperInvariant();text.fontSize=5;text.color=new Color(.8f,.85f,.82f);text.alignment=TextAlignmentOptions.Center;text.enableWordWrapping=false;text.rectTransform.sizeDelta=new Vector2(Math.Min(room.Width-2,24),1.5f);text.transform.localScale=Vector3.one*.6f;}
   // Low-cost fill lights use the same room centres as the source layout, without shadow-map spam.
   foreach(float dx in new[]{-room.Width*.26f,room.Width*.26f}){var go=new GameObject("Reserve.Fill."+room.Id);go.transform.SetParent(root.transform,false);go.transform.position=new Vector3(room.X+dx,3.5f,room.Z);var l=go.AddComponent<Light>();l.type=LightType.Point;l.color=new Color(.65f,.74f,.83f);l.intensity=.9f;l.range=18;l.shadows=LightShadows.None;}
  }
  // PhysX proxies are for the third-person camera only. Gameplay physics is installed separately in both ECS worlds.
  int layer=LayerMask.NameToLayer("Default");foreach(var box in Content.Map.Boxes){if(!box.Solid)continue;var go=new GameObject("Reserve.CameraCollision");go.transform.SetParent(root.transform,false);go.layer=layer<0?0:layer;go.transform.position=new Vector3(box.Center[0],box.Center[1],box.Center[2]);var c=go.AddComponent<UnityEngine.BoxCollider>();c.size=new Vector3(box.Size[0],box.Size[1],box.Size[2]);}
 }
 internal static void Clear(){if(root!=null)UnityEngine.Object.Destroy(root);root=null;foreach(var m in materials)if(m!=null)UnityEngine.Object.Destroy(m);materials.Clear();if(active&&SceneManager.GetActiveScene().handle==sceneHandle){RenderSettings.ambientMode=oldMode;RenderSettings.ambientLight=oldAmbient;RenderSettings.ambientIntensity=oldIntensity;RenderSettings.fog=oldFog;}active=false;}
}
