using System;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Frontier.Reserve;

internal static class Presentation
{
	private static GameObject? root;

	private static Color oldAmbient;

	private static float oldIntensity;

	private static AmbientMode oldMode;

	private static bool oldFog;

	private static Light? oldSun;

	private static bool active;

	private static int sceneHandle;

	internal static void Add(Scene scene, TMP_FontAsset? font)
	{
		sceneHandle = scene.handle;
		root = new GameObject("Reserve.StockLightingAndSigns");
		SceneManager.MoveGameObjectToScene(root, scene);
		oldAmbient = RenderSettings.ambientLight;
		oldIntensity = RenderSettings.ambientIntensity;
		oldMode = RenderSettings.ambientMode;
		oldFog = RenderSettings.fog;
		oldSun = RenderSettings.sun;
		active = true;
		RenderSettings.ambientMode = AmbientMode.Flat;
		RenderSettings.ambientLight = new Color(0.35f, 0.39f, 0.44f);
		RenderSettings.ambientIntensity = 1f;
		RenderSettings.fog = false;
		GameObject gameObject = new GameObject("Reserve.InteriorFill");
		gameObject.transform.SetParent(root.transform, worldPositionStays: false);
		gameObject.transform.rotation = Quaternion.Euler(48f, -25f, 0f);
		Light light = gameObject.AddComponent<Light>();
		light.type = LightType.Directional;
		light.color = new Color(0.78f, 0.86f, 1f);
		light.intensity = 0.8f;
		light.shadows = LightShadows.None;
		RenderSettings.sun = light;
		RoomDef[] rooms = Content.Map.Rooms;
		foreach (RoomDef roomDef in rooms)
		{
			if (font != null)
			{
				GameObject gameObject2 = new GameObject("Reserve.Sign." + roomDef.Id);
				gameObject2.transform.SetParent(root.transform, worldPositionStays: false);
				gameObject2.transform.position = new Vector3(roomDef.X, 3.8f, roomDef.Z + roomDef.Depth * 0.5f - 0.2f);
				TextMeshPro textMeshPro = gameObject2.AddComponent<TextMeshPro>();
				textMeshPro.font = font;
				textMeshPro.text = roomDef.Name.ToUpperInvariant();
				textMeshPro.fontSize = 4f;
				textMeshPro.color = new Color(0.93f, 0.94f, 0.91f);
				textMeshPro.alignment = TextAlignmentOptions.Center;
				textMeshPro.enableWordWrapping = false;
				textMeshPro.rectTransform.sizeDelta = new Vector2(Math.Min(roomDef.Width - 2f, 24f), 1.5f);
				textMeshPro.transform.localScale = Vector3.one * 0.55f;
			}
			int[] array = new int[2] { -1, 1 };
			for (int j = 0; j < array.Length; j++)
			{
				int num = array[j];
				GameObject gameObject3 = new GameObject("Reserve.FixtureLight." + roomDef.Id + "." + num);
				gameObject3.transform.SetParent(root.transform, worldPositionStays: false);
				gameObject3.transform.position = new Vector3(roomDef.X + (float)num * roomDef.Width * 0.23f, 5.45f, roomDef.Z + (float)num * roomDef.Depth * 0.22f);
				Light light2 = gameObject3.AddComponent<Light>();
				light2.type = LightType.Point;
				light2.intensity = 2.6f;
				light2.range = 16f;
				light2.shadows = LightShadows.None;
				Light light3 = light2;
				bool flag;
				switch (roomDef.Id)
				{
				case "08":
				case "02":
				case "04":
					flag = true;
					break;
				default:
					flag = false;
					break;
				}
				Color color;
				if (flag)
				{
					color = new Color(1f, 0.82f, 0.58f);
				}
				else
				{
					string id = roomDef.Id;
					bool flag2 = ((id == "06" || id == "09") ? true : false);
					color = (flag2 ? new Color(0.62f, 0.79f, 1f) : new Color(0.87f, 0.93f, 1f));
				}
				light3.color = color;
			}
		}
		Anchor[] anchors = Content.Map.Anchors;
		foreach (Anchor anchor in anchors)
		{
			if (anchor.Kind == "exit")
			{
				GameObject gameObject4 = new GameObject("Reserve.ExitLight." + anchor.Id);
				gameObject4.transform.SetParent(root.transform, worldPositionStays: false);
				gameObject4.transform.position = new Vector3(anchor.Position[0], 3.2f, anchor.Position[2]);
				Light light4 = gameObject4.AddComponent<Light>();
				light4.type = LightType.Point;
				light4.color = new Color(0.35f, 1f, 0.6f);
				light4.range = 7f;
				light4.intensity = 1.4f;
				light4.shadows = LightShadows.None;
			}
		}
		int num2 = LayerMask.NameToLayer("Default");
		BoxDef[] boxes = Content.Map.Boxes;
		foreach (BoxDef boxDef in boxes)
		{
			if (boxDef.Solid)
			{
				GameObject gameObject5 = new GameObject("Reserve.CameraCollision");
				gameObject5.transform.SetParent(root.transform, worldPositionStays: false);
				gameObject5.layer = ((num2 >= 0) ? num2 : 0);
				gameObject5.transform.position = new Vector3(boxDef.Center[0], boxDef.Center[1], boxDef.Center[2]);
				gameObject5.AddComponent<BoxCollider>().size = new Vector3(boxDef.Size[0], boxDef.Size[1], boxDef.Size[2]);
			}
		}
		Trace.Data["stockLighting"] = new
		{
			roomFixtures = Content.Map.Rooms.Length * 2,
			exitFixtures = 2,
			fillLights = 1,
			shadowMaps = 0,
			scope = "Reserve scene only"
		};
	}

	internal static void Clear()
	{
		if (root != null)
		{
			UnityEngine.Object.Destroy(root);
		}
		root = null;
		if (active && SceneManager.GetActiveScene().handle == sceneHandle)
		{
			RenderSettings.ambientMode = oldMode;
			RenderSettings.ambientLight = oldAmbient;
			RenderSettings.ambientIntensity = oldIntensity;
			RenderSettings.fog = oldFog;
			RenderSettings.sun = oldSun;
		}
		oldSun = null;
		active = false;
	}
}
