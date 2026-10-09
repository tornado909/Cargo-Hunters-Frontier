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
		Type type = AppDomain.CurrentDomain.GetAssemblies().Single((Assembly a) => a.GetName().Name == "Frontier.Core").GetType("Frontier.Core.FrontierOverlay", throwOnError: true);
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
		if (Views.Remove(((Il2CppObjectBase)__instance).Pointer.ToInt64(), out Mark value) && (Object)(object)value.Root != (Object)null)
		{
			Object.Destroy((Object)(object)value.Root);
		}
	}

	public static void Reset()
	{
		foreach (Mark value in Views.Values)
		{
			if ((Object)(object)value.Root != (Object)null)
			{
				Object.Destroy((Object)(object)value.Root);
			}
		}
		Views.Clear();
	}

	private static Image Image(Transform parent, string name, Vector2 min, Vector2 max, Vector2 pos, Vector2 size, Color color)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = new GameObject(name, new Il2CppReferenceArray<Type>((Type[])(object)new Type[1] { Il2CppType.Of<RectTransform>() }));
		RectTransform obj = ((Il2CppObjectBase)val.transform).TryCast<RectTransform>() ?? throw new InvalidOperationException("UI rect missing");
		((Transform)obj).SetParent(parent, false);
		obj.anchorMin = min;
		obj.anchorMax = max;
		obj.anchoredPosition = pos;
		obj.sizeDelta = size;
		Image obj2 = val.AddComponent<Image>();
		((Graphic)obj2).raycastTarget = false;
		((MaskableGraphic)obj2).maskable = true;
		((Graphic)obj2).color = color;
		return obj2;
	}

	private static void Observe(ItemView __instance, IItem __0)
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Expected O, but got Unknown
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0164: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Unknown result type (might be due to invalid IL or missing references)
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01af: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0207: Unknown result type (might be due to invalid IL or missing references)
		//IL_0213: Unknown result type (might be due to invalid IL or missing references)
		//IL_0218: Unknown result type (might be due to invalid IL or missing references)
		//IL_023b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0240: Unknown result type (might be due to invalid IL or missing references)
		//IL_024d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0259: Unknown result type (might be due to invalid IL or missing references)
		//IL_025e: Unknown result type (might be due to invalid IL or missing references)
		//IL_026d: Unknown result type (might be due to invalid IL or missing references)
		//IL_026e: Unknown result type (might be due to invalid IL or missing references)
		//IL_028a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0299: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0302: Unknown result type (might be due to invalid IL or missing references)
		//IL_0313: Unknown result type (might be due to invalid IL or missing references)
		//IL_0318: Unknown result type (might be due to invalid IL or missing references)
		//IL_0361: Unknown result type (might be due to invalid IL or missing references)
		//IL_0370: Unknown result type (might be due to invalid IL or missing references)
		//IL_0375: Unknown result type (might be due to invalid IL or missing references)
		//IL_03be: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_038e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0393: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			Clear(__instance);
			if (__0 != null && __instance._inspected && !__instance._hideSpecialInformation && !((Object)(object)__instance._rectTransform == (Object)null) && Runtime.Db.Variants.TryGetValue(Runtime.Id(__0.TemplateId), out Trait value) && value.RankIndex > 0 && Views.Count < 256)
			{
				int num = RankPolicy.Grade(value.RankIndex);
				Color val = default(Color);
				ColorUtility.TryParseHtmlString(RankPolicy.Colors[num], ref val);
				GameObject val2 = new GameObject("FrontierRank28FX", new Il2CppReferenceArray<Type>((Type[])(object)new Type[1] { Il2CppType.Of<RectTransform>() }));
				RectTransform val3 = ((Il2CppObjectBase)val2.transform).TryCast<RectTransform>() ?? throw new InvalidOperationException("UI root rect missing");
				((Transform)val3).SetParent((Transform)(object)__instance._rectTransform, false);
				val3.anchorMin = Vector2.zero;
				val3.anchorMax = Vector2.one;
				val3.offsetMin = Vector2.zero;
				val3.offsetMax = Vector2.zero;
				((Transform)val3).SetAsLastSibling();
				Mark mark = new Mark
				{
					Root = val2,
					View = __instance,
					Grade = num,
					Color = val
				};
				Views[((Il2CppObjectBase)__instance).Pointer.ToInt64()] = mark;
				float num2 = ((num < 6) ? 1 : 2);
				mark.Edges.Add(Image((Transform)(object)val3, "top", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, 0f - num2), new Vector2(-2f, num2), val));
				mark.Edges.Add(Image((Transform)(object)val3, "bottom", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, num2), new Vector2(-2f, num2), val));
				if (num >= 1)
				{
					mark.Edges.Add(Image((Transform)(object)val3, "left", Vector2.zero, new Vector2(0f, 1f), new Vector2(num2, 0f), new Vector2(num2, -2f), val));
					mark.Edges.Add(Image((Transform)(object)val3, "right", new Vector2(1f, 0f), Vector2.one, new Vector2(0f - num2, 0f), new Vector2(num2, -2f), val));
				}
				if (num >= 2)
				{
					Color color = val;
					color.a = 0.28f;
					mark.Edges.Add(Image((Transform)(object)val3, "underlight", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 4f), new Vector2(-4f, 4f), color));
				}
				if (num >= 3)
				{
					mark.Edges.Add(Image((Transform)(object)val3, "upper-notch", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -5f), new Vector2((float)(14 + num * 2), 2f), val));
				}
				int num3 = ((num >= 6) ? ((num - 5) * 2) : 0);
				if (num == 5)
				{
					num3 = 1;
				}
				for (int i = 0; i < num3; i++)
				{
					mark.Sparks.Add(Image((Transform)(object)val3, "spark" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2((float)((num == 9) ? 3 : 2), (float)((num == 9) ? 3 : 2)), val));
				}
				if ((Object)(object)__instance._nameLabel != (Object)null)
				{
					((Graphic)__instance._nameLabel).color = val;
				}
				if ((Object)(object)__instance._shortNameLabel != (Object)null)
				{
					((Graphic)__instance._shortNameLabel).color = val;
				}
			}
		}
		catch (Exception e)
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
		catch (Exception e)
		{
			Runtime.Error("rank28.visual-tick", e);
		}
	}

	private static void TickViews()
	{
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
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
			if ((Object)(object)value.View == (Object)null || (Object)(object)value.Root == (Object)null)
			{
				Views.Remove(keyValuePair.Key);
			}
			else
			{
				if (!((Component)value.View).gameObject.activeInHierarchy)
				{
					continue;
				}
				if (value.Grade >= 4)
				{
					float num = 0.68f + 0.25f * (float)Math.Sin((double)unscaledTime * ((value.Grade >= 9) ? 3.0 : 1.5));
					for (int j = 0; j < value.Edges.Count; j++)
					{
						Color color = value.Color;
						color.a = ((j == 4) ? (num * 0.3f) : num);
						((Graphic)value.Edges[j]).color = color;
					}
				}
				Rect rect = value.View._rectTransform.rect;
				float num2 = Math.Max(4f, ((Rect)(ref rect)).width / 2f - 4f);
				float num3 = Math.Max(4f, ((Rect)(ref rect)).height / 2f - 4f);
				for (int k = 0; k < value.Sparks.Count; k++)
				{
					double num4 = (double)unscaledTime * 0.65 + (double)(k * 2) * Math.PI / (double)Math.Max(1, value.Sparks.Count);
					((Graphic)value.Sparks[k]).rectTransform.anchoredPosition = new Vector2((float)Math.Cos(num4) * num2, (float)Math.Sin(num4) * num3);
				}
			}
		}
	}

	public static void DrawEnemyGrade(string rank, float cx, float cy, float width, float scale)
	{
		//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01aa: Unknown result type (might be due to invalid IL or missing references)
		int num = RankPolicy.Grade(RankPolicy.Parse(rank));
		if (num < 0)
		{
			return;
		}
		Color color = default(Color);
		ColorUtility.TryParseHtmlString(RankPolicy.Colors[num], ref color);
		Color color2 = GUI.color;
		try
		{
			if (num >= 4)
			{
				color.a = 0.7f + 0.25f * (float)Math.Sin(Time.unscaledTime * 1.5f);
			}
			GUI.color = color;
			float num2 = ((num >= 1) ? 7 : 3);
			GUI.DrawTexture(new Rect(cx - width / 2f - 3f * scale, cy - 1f * scale, scale, num2 * scale), (Texture)(object)Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(cx + width / 2f + 2f * scale, cy - 1f * scale, scale, num2 * scale), (Texture)(object)Texture2D.whiteTexture);
			if (num >= 2)
			{
				GUI.DrawTexture(new Rect(cx - width / 2f, cy + 7f * scale, width, scale), (Texture)(object)Texture2D.whiteTexture);
			}
			if (num >= 3)
			{
				GUI.DrawTexture(new Rect(cx - 6f * scale, cy + 10f * scale, 12f * scale, scale), (Texture)(object)Texture2D.whiteTexture);
			}
			int num3 = ((num >= 6) ? ((num - 5) * 2) : ((num == 5) ? 1 : 0));
			for (int i = 0; i < num3; i++)
			{
				double num4 = (double)Time.unscaledTime * 0.7 + (double)(i * 2) * Math.PI / (double)num3;
				float num5 = cx + (float)Math.Cos(num4) * (width / 2f + 6f * scale);
				float num6 = cy + (float)Math.Sin(num4) * 11f * scale;
				GUI.DrawTexture(new Rect(num5, num6, (float)((num == 9) ? 3 : 2) * scale, (float)((num == 9) ? 3 : 2) * scale), (Texture)(object)Texture2D.whiteTexture);
			}
		}
		finally
		{
			GUI.color = color2;
		}
	}
}
