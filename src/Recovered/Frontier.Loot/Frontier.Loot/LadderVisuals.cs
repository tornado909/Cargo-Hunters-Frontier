using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.ItemsSystem;
using Game.UI.Views;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Loot;

public static class LadderVisuals
{
	private sealed class Mark
	{
		internal ItemView View;

		internal GameObject Root;

		internal int Grade;

		internal Color Color;

		internal readonly List<Image> Edges = new List<Image>();

		internal readonly List<Image> Sparks = new List<Image>();
	}

	private static readonly Dictionary<long, Mark> Views = new Dictionary<long, Mark>();

	private static float next;

	public static void Install(Harmony h)
	{
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Expected O, but got Unknown
		Hook("InitializeFrom", "Observe");
		Hook("Clear", "Clear");
		Hook("OnDisable", "Clear");
		System.Type type = System.AppDomain.CurrentDomain.GetAssemblies().Single((Assembly a) => a.GetName().Name == "Frontier.Core").GetType("Frontier.Core.FrontierOverlay", throwOnError: true);
		h.Patch((MethodBase)type.GetMethod("OnGUI", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), (HarmonyMethod)null, new HarmonyMethod(typeof(LadderVisuals).GetMethod("Tick", BindingFlags.Static | BindingFlags.NonPublic)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		void Hook(string name, string fn)
		{
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Expected O, but got Unknown
			h.Patch((MethodBase)RankHookPlan.View(name), (HarmonyMethod)null, new HarmonyMethod(typeof(LadderVisuals).GetMethod(fn, BindingFlags.Static | BindingFlags.NonPublic)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void Clear(ItemView __instance)
	{
		if (Views.Remove(((Il2CppObjectBase)__instance).Pointer.ToInt64(), out Mark value) && value.Root != null)
		{
			UnityEngine.Object.Destroy(value.Root);
		}
	}

	public static void Reset()
	{
		foreach (Mark value in Views.Values)
		{
			if (value.Root != null)
			{
				UnityEngine.Object.Destroy(value.Root);
			}
		}
		Views.Clear();
	}

	private static Image Image(Transform parent, string name, Vector2 min, Vector2 max, Vector2 pos, Vector2 size, Color color)
	{
		GameObject gameObject = new GameObject(name, new Il2CppReferenceArray<Il2CppSystem.Type>(new Il2CppSystem.Type[1] { Il2CppType.Of<RectTransform>() }));
		RectTransform obj = ((Il2CppObjectBase)gameObject.transform).TryCast<RectTransform>() ?? throw new System.InvalidOperationException("UI rect missing");
		obj.SetParent(parent, worldPositionStays: false);
		obj.anchorMin = min;
		obj.anchorMax = max;
		obj.anchoredPosition = pos;
		obj.sizeDelta = size;
		Image image = gameObject.AddComponent<Image>();
		image.raycastTarget = false;
		image.maskable = true;
		image.color = color;
		return image;
	}

	private static void Observe(ItemView __instance, IItem __0)
	{
		try
		{
			Clear(__instance);
			if (__0 != null && __instance._inspected && !__instance._hideSpecialInformation && !(__instance._rectTransform == null) && Runtime.Db.Variants.TryGetValue(Runtime.Id(__0.TemplateId), out Trait value) && value.RankIndex > 0 && Views.Count < 256)
			{
				int num = RankPolicy.Grade(value.RankIndex);
				ColorUtility.TryParseHtmlString(RankPolicy.Colors[num], out var color);
				GameObject gameObject = new GameObject("FrontierRank28FX", new Il2CppReferenceArray<Il2CppSystem.Type>(new Il2CppSystem.Type[1] { Il2CppType.Of<RectTransform>() }));
				RectTransform rectTransform = ((Il2CppObjectBase)gameObject.transform).TryCast<RectTransform>() ?? throw new System.InvalidOperationException("UI root rect missing");
				rectTransform.SetParent(__instance._rectTransform, worldPositionStays: false);
				rectTransform.anchorMin = Vector2.zero;
				rectTransform.anchorMax = Vector2.one;
				rectTransform.offsetMin = Vector2.zero;
				rectTransform.offsetMax = Vector2.zero;
				rectTransform.SetAsLastSibling();
				Mark mark = new Mark
				{
					Root = gameObject,
					View = __instance,
					Grade = num,
					Color = color
				};
				Views[((Il2CppObjectBase)__instance).Pointer.ToInt64()] = mark;
				float num2 = ((num < 6) ? 1 : 2);
				mark.Edges.Add(Image(rectTransform, "top", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, 0f - num2), new Vector2(-2f, num2), color));
				mark.Edges.Add(Image(rectTransform, "bottom", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, num2), new Vector2(-2f, num2), color));
				if (num >= 1)
				{
					mark.Edges.Add(Image(rectTransform, "left", Vector2.zero, new Vector2(0f, 1f), new Vector2(num2, 0f), new Vector2(num2, -2f), color));
					mark.Edges.Add(Image(rectTransform, "right", new Vector2(1f, 0f), Vector2.one, new Vector2(0f - num2, 0f), new Vector2(num2, -2f), color));
				}
				if (num >= 2)
				{
					Color color2 = color;
					color2.a = 0.28f;
					mark.Edges.Add(Image(rectTransform, "underlight", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 4f), new Vector2(-4f, 4f), color2));
				}
				if (num >= 3)
				{
					mark.Edges.Add(Image(rectTransform, "upper-notch", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -5f), new Vector2(14 + num * 2, 2f), color));
				}
				int num3 = ((num >= 6) ? ((num - 5) * 2) : 0);
				if (num == 5)
				{
					num3 = 1;
				}
				for (int i = 0; i < num3; i++)
				{
					mark.Sparks.Add(Image(rectTransform, "spark" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2((num == 9) ? 3 : 2, (num == 9) ? 3 : 2), color));
				}
				if (__instance._nameLabel != null)
				{
					__instance._nameLabel.color = color;
				}
				if (__instance._shortNameLabel != null)
				{
					__instance._shortNameLabel.color = color;
				}
			}
		}
		catch (System.Exception e)
		{
			Runtime.Error("rank28.visual", e);
		}
	}

	private static void Tick()
	{
		try
		{
			TickViews();
		}
		catch (System.Exception e)
		{
			Runtime.Error("rank28.visual-tick", e);
		}
	}

	private static void TickViews()
	{
		float unscaledTime = Time.unscaledTime;
		if (unscaledTime < next)
		{
			return;
		}
		next = unscaledTime + 0.05f;
		KeyValuePair<long, Mark>[] array = Views.ToArray();
		for (int i = 0; i < array.Length; i++)
		{
			KeyValuePair<long, Mark> keyValuePair = array[i];
			Mark value = keyValuePair.Value;
			if (value.View == null || value.Root == null)
			{
				Views.Remove(keyValuePair.Key);
			}
			else
			{
				if (!value.View.gameObject.activeInHierarchy)
				{
					continue;
				}
				if (value.Grade >= 4)
				{
					float num = 0.68f + 0.25f * (float)System.Math.Sin((double)unscaledTime * ((value.Grade >= 9) ? 3.0 : 1.5));
					for (int j = 0; j < value.Edges.Count; j++)
					{
						Color color = value.Color;
						color.a = ((j == 4) ? (num * 0.3f) : num);
						value.Edges[j].color = color;
					}
				}
				Rect rect = value.View._rectTransform.rect;
				float num2 = System.Math.Max(4f, rect.width / 2f - 4f);
				float num3 = System.Math.Max(4f, rect.height / 2f - 4f);
				for (int k = 0; k < value.Sparks.Count; k++)
				{
					double num4 = (double)unscaledTime * 0.65 + (double)(k * 2) * System.Math.PI / (double)System.Math.Max(1, value.Sparks.Count);
					value.Sparks[k].rectTransform.anchoredPosition = new Vector2((float)System.Math.Cos(num4) * num2, (float)System.Math.Sin(num4) * num3);
				}
			}
		}
	}

	public static void DrawEnemyGrade(string rank, float cx, float cy, float width, float scale)
	{
		int num = RankPolicy.Grade(RankPolicy.Parse(rank));
		if (num < 0)
		{
			return;
		}
		ColorUtility.TryParseHtmlString(RankPolicy.Colors[num], out var color);
		Color color2 = GUI.color;
		try
		{
			if (num >= 4)
			{
				color.a = 0.7f + 0.25f * (float)System.Math.Sin(Time.unscaledTime * 1.5f);
			}
			GUI.color = color;
			float num2 = ((num >= 1) ? 7 : 3);
			GUI.DrawTexture(new Rect(cx - width / 2f - 3f * scale, cy - 1f * scale, scale, num2 * scale), Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(cx + width / 2f + 2f * scale, cy - 1f * scale, scale, num2 * scale), Texture2D.whiteTexture);
			if (num >= 2)
			{
				GUI.DrawTexture(new Rect(cx - width / 2f, cy + 7f * scale, width, scale), Texture2D.whiteTexture);
			}
			if (num >= 3)
			{
				GUI.DrawTexture(new Rect(cx - 6f * scale, cy + 10f * scale, 12f * scale, scale), Texture2D.whiteTexture);
			}
			int num3 = ((num >= 6) ? ((num - 5) * 2) : ((num == 5) ? 1 : 0));
			for (int i = 0; i < num3; i++)
			{
				double num4 = (double)Time.unscaledTime * 0.7 + (double)(i * 2) * System.Math.PI / (double)num3;
				float x = cx + (float)System.Math.Cos(num4) * (width / 2f + 6f * scale);
				float y = cy + (float)System.Math.Sin(num4) * 11f * scale;
				GUI.DrawTexture(new Rect(x, y, (float)((num == 9) ? 3 : 2) * scale, (float)((num == 9) ? 3 : 2) * scale), Texture2D.whiteTexture);
			}
		}
		finally
		{
			GUI.color = color2;
		}
	}
}
