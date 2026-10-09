using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Unity.IL2CPP;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Core;

internal static class EliteWorldEffects
{
	private sealed class Mark
	{
		internal EliteClientTracker.Target Target;

		internal GameObject LabelObject;

		internal TextMeshPro Label;

		internal Material TextMaterial;

		internal GameObject? AuraObject;

		internal Material? AuraMaterial;

		internal Mark(EliteClientTracker.Target t, GameObject g, TextMeshPro text, Material material)
		{
			Target = t;
			LabelObject = g;
			Label = text;
			TextMaterial = material;
		}
	}

	private static readonly Dictionary<string, Mark> Marks = new Dictionary<string, Mark>((IEqualityComparer<string>)(object)StringComparer.Ordinal);

	private static Texture2D? glowTexture;

	private static Sprite? glowSprite;

	private static float collectAt;

	private static float reportAt;

	private static TMP_FontAsset? font;

	internal static EliteWorldDiagnostic Stats = new EliteWorldDiagnostic(0, 0, 0, 0, 0, CameraAvailable: false, System.Array.Empty<string>());

	internal static void Clear()
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		Enumerator<string, Mark> enumerator = Marks.Values.GetEnumerator();
		try
		{
			while (enumerator.MoveNext())
			{
				Release(enumerator.Current);
			}
		}
		finally
		{
			((System.IDisposable)enumerator/*cast due to .constrained prefix*/).Dispose();
		}
		Marks.Clear();
	}

	internal static void Shutdown()
	{
		Clear();
		if ((Object)(object)glowSprite != (Object)null)
		{
			Object.Destroy((Object)(object)glowSprite);
		}
		if ((Object)(object)glowTexture != (Object)null)
		{
			Object.Destroy((Object)(object)glowTexture);
		}
		glowSprite = null;
		glowTexture = null;
		font = null;
	}

	private static void Release(Mark m)
	{
		if ((Object)(object)m.LabelObject != (Object)null)
		{
			Object.Destroy((Object)(object)m.LabelObject);
		}
		if ((Object)(object)m.AuraObject != (Object)null)
		{
			Object.Destroy((Object)(object)m.AuraObject);
		}
		if ((Object)(object)m.TextMaterial != (Object)null)
		{
			Object.Destroy((Object)(object)m.TextMaterial);
		}
		if ((Object)(object)m.AuraMaterial != (Object)null)
		{
			Object.Destroy((Object)(object)m.AuraMaterial);
		}
	}

	internal static void Tick()
	{
		//IL_0263: Unknown result type (might be due to invalid IL or missing references)
		//IL_0273: Unknown result type (might be due to invalid IL or missing references)
		//IL_0278: Unknown result type (might be due to invalid IL or missing references)
		//IL_027d: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_038c: Unknown result type (might be due to invalid IL or missing references)
		//IL_039b: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_03cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_03da: Unknown result type (might be due to invalid IL or missing references)
		//IL_03df: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0416: Unknown result type (might be due to invalid IL or missing references)
		//IL_0431: Unknown result type (might be due to invalid IL or missing references)
		//IL_0438: Unknown result type (might be due to invalid IL or missing references)
		//IL_0445: Unknown result type (might be due to invalid IL or missing references)
		//IL_045c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0461: Unknown result type (might be due to invalid IL or missing references)
		//IL_046f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0471: Unknown result type (might be due to invalid IL or missing references)
		//IL_0478: Unknown result type (might be due to invalid IL or missing references)
		//IL_047d: Unknown result type (might be due to invalid IL or missing references)
		//IL_049e: Unknown result type (might be due to invalid IL or missing references)
		//IL_032b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0330: Unknown result type (might be due to invalid IL or missing references)
		//IL_0334: Unknown result type (might be due to invalid IL or missing references)
		//IL_0349: Unknown result type (might be due to invalid IL or missing references)
		//IL_034e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0352: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (!Plugin.Current.Active)
			{
				Clear();
				return;
			}
			float unscaledTime = Time.unscaledTime;
			ElitePresentation.TickCosmetics(unscaledTime);
			Camera cam = Camera.main;
			if ((Object)(object)cam == (Object)null)
			{
				Clear();
				return;
			}
			string[] array2;
			if (unscaledTime >= collectAt)
			{
				collectAt = unscaledTime + 0.1f;
				EliteClientTracker.Target[] array = Enumerable.ToArray<EliteClientTracker.Target>(Enumerable.Take<EliteClientTracker.Target>((System.Collections.Generic.IEnumerable<EliteClientTracker.Target>)Enumerable.OrderBy<EliteClientTracker.Target, float>(Enumerable.Where<EliteClientTracker.Target>((System.Collections.Generic.IEnumerable<EliteClientTracker.Target>)EliteClientTracker.Collect(), (Func<EliteClientTracker.Target, bool>)delegate(EliteClientTracker.Target t)
				{
					//IL_000b: Unknown result type (might be due to invalid IL or missing references)
					//IL_001b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0020: Unknown result type (might be due to invalid IL or missing references)
					//IL_0025: Unknown result type (might be due to invalid IL or missing references)
					Vector3 val4 = t.Root.transform.position - ((Component)cam).transform.position;
					return ((Vector3)(ref val4)).sqrMagnitude < 3600f;
				}), (Func<EliteClientTracker.Target, float>)delegate(EliteClientTracker.Target t)
				{
					//IL_000b: Unknown result type (might be due to invalid IL or missing references)
					//IL_001b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0020: Unknown result type (might be due to invalid IL or missing references)
					//IL_0025: Unknown result type (might be due to invalid IL or missing references)
					Vector3 val4 = t.Root.transform.position - ((Component)cam).transform.position;
					return ((Vector3)(ref val4)).sqrMagnitude;
				}), 48));
				HashSet<string> val = new HashSet<string>(Enumerable.Select<EliteClientTracker.Target, string>((System.Collections.Generic.IEnumerable<EliteClientTracker.Target>)array, (Func<EliteClientTracker.Target, string>)((EliteClientTracker.Target t) => t.Id)));
				array2 = Enumerable.ToArray<string>((System.Collections.Generic.IEnumerable<string>)Marks.Keys);
				foreach (string text in array2)
				{
					if (!val.Contains(text))
					{
						Release(Marks[text]);
						Marks.Remove(text);
					}
				}
				EliteClientTracker.Target[] array3 = array;
				Mark mark = default(Mark);
				foreach (EliteClientTracker.Target target in array3)
				{
					if (Marks.TryGetValue(target.Id, ref mark))
					{
						if ((Object)(object)mark.Target.Root == (Object)(object)target.Root)
						{
							mark.Target = target;
							continue;
						}
						Release(mark);
						Marks.Remove(target.Id);
					}
					try
					{
						Mark mark2 = Create(target);
						if (mark2 != null)
						{
							Marks[target.Id] = mark2;
						}
					}
					catch (System.Exception e)
					{
						ElitePresentation.Warn("World marker", e);
					}
				}
			}
			int num2 = 0;
			bool flag = Cursor.visible || Time.timeScale <= 0f;
			array2 = Enumerable.ToArray<string>((System.Collections.Generic.IEnumerable<string>)Marks.Keys);
			Vector3 val3 = default(Vector3);
			foreach (string text2 in array2)
			{
				Mark mark3 = Marks[text2];
				GameObject root = mark3.Target.Root;
				if ((Object)(object)root == (Object)null || !root.activeInHierarchy || !EliteClientTracker.Alive(mark3.Target.Player))
				{
					Release(mark3);
					Marks.Remove(text2);
					continue;
				}
				Vector3 val2;
				int num3;
				if (!flag)
				{
					val2 = root.transform.position - ((Component)cam).transform.position;
					num3 = ((((Vector3)(ref val2)).sqrMagnitude < 3600f) ? 1 : 0);
				}
				else
				{
					num3 = 0;
				}
				bool flag2 = (byte)num3 != 0;
				mark3.LabelObject.SetActive(flag2);
				if ((Object)(object)mark3.AuraObject != (Object)null)
				{
					mark3.AuraObject.SetActive(flag2);
				}
				if (!flag2)
				{
					continue;
				}
				Vector3 position = root.transform.position;
				float num4 = position.y + 1.9f;
				System.Collections.Generic.IEnumerator<Renderer> enumerator = root.GetComponentsInChildren<Renderer>(false).GetEnumerator();
				try
				{
					while (((System.Collections.IEnumerator)enumerator).MoveNext())
					{
						Renderer current = enumerator.Current;
						if ((Object)(object)current != (Object)null && current.enabled && !(current is ParticleSystemRenderer) && !(current is TrailRenderer) && !(current is LineRenderer))
						{
							Bounds bounds = current.bounds;
							if (((Bounds)(ref bounds)).size.y < 5f)
							{
								float num5 = num4;
								bounds = current.bounds;
								num4 = Math.Max(num5, ((Bounds)(ref bounds)).max.y + 0.18f);
							}
						}
					}
				}
				finally
				{
					((System.IDisposable)enumerator)?.Dispose();
				}
				mark3.LabelObject.transform.position = new Vector3(position.x, num4 + 0.1f, position.z);
				mark3.LabelObject.transform.rotation = ((Component)cam).transform.rotation;
				val2 = position - ((Component)cam).transform.position;
				float num6 = Math.Clamp(((Vector3)(ref val2)).magnitude * 0.012f, 0.09f, 0.48f);
				mark3.LabelObject.transform.localScale = new Vector3(num6, num6, num6);
				if ((Object)(object)mark3.AuraObject != (Object)null)
				{
					((Vector3)(ref val3))._002Ector(position.x, position.y + 0.95f, position.z);
					Vector3 forward = ((Component)cam).transform.forward;
					mark3.AuraObject.transform.position = val3 + forward * 0.14f;
					mark3.AuraObject.transform.rotation = ((Component)cam).transform.rotation;
				}
				num2++;
			}
			Stats = new EliteWorldDiagnostic(Marks.Count, Marks.Count, 0, 0, num2, CameraAvailable: true, new string[2] { "world-space TMP depth test", "separate sprite aura" });
			if (unscaledTime >= reportAt)
			{
				reportAt = unscaledTime + 15f;
				Plugin.Current.WriteReport();
			}
		}
		catch (System.Exception e2)
		{
			Clear();
			ElitePresentation.Warn("World effects", e2);
		}
	}

	private static Color ColorOf(string hex)
	{
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		int num = Convert.ToInt32(hex.TrimStart('#'), 16);
		return new Color((float)((num >> 16) & 0xFF) / 255f, (float)((num >> 8) & 0xFF) / 255f, (float)(num & 0xFF) / 255f, 1f);
	}

	private static Mark? Create(EliteClientTracker.Target target)
	{
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Expected O, but got Unknown
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Expected O, but got Unknown
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)font == (Object)null)
		{
			font = TMP_Settings.defaultFontAsset;
			System.Collections.Generic.IEnumerator<TMP_Text> enumerator = Object.FindObjectsOfType<TMP_Text>().GetEnumerator();
			try
			{
				while (((System.Collections.IEnumerator)enumerator).MoveNext())
				{
					TMP_Text current = enumerator.Current;
					if ((Object)(object)current != (Object)null && (Object)(object)current.font != (Object)null)
					{
						font = current.font;
						break;
					}
				}
			}
			finally
			{
				((System.IDisposable)enumerator)?.Dispose();
			}
		}
		if ((Object)(object)font == (Object)null)
		{
			return null;
		}
		GameObject val = new GameObject("Frontier_EliteName_" + target.Id);
		TextMeshPro val2 = val.AddComponent<TextMeshPro>();
		Material val3 = null;
		try
		{
			((TMP_Text)val2).font = font;
			((TMP_Text)val2).text = target.Rank.Name;
			((TMP_Text)val2).fontSize = 8f;
			((TMP_Text)val2).alignment = (TextAlignmentOptions)514;
			((TMP_Text)val2).enableWordWrapping = false;
			((TMP_Text)val2).rectTransform.sizeDelta = new Vector2(22f, 3f);
			val3 = new Material(((TMP_Asset)font).material);
			((Object)val3).name = "Frontier_EliteName_Material";
			((TMP_Text)val2).fontSharedMaterial = val3;
			((TMP_Text)val2).isOverlay = false;
			val3.SetFloat("_ZTestMode", 4f);
			val3.SetFloat("unity_GUIZTestMode", 4f);
			val3.renderQueue = 3000;
			((Graphic)val2).color = ColorOf(target.Rank.Hex);
			((TMP_Text)val2).outlineWidth = 0.12f;
			((TMP_Text)val2).outlineColor = new Color32((byte)8, (byte)8, (byte)8, (byte)230);
			Mark mark = new Mark(target, val, val2, val3);
			try
			{
				AddAura(mark, ColorOf(target.Rank.Hex));
			}
			catch (System.Exception e)
			{
				ElitePresentation.Warn("Aura", e);
			}
			((BasePlugin)Plugin.Current).Log.LogInfo((object)string.Concat(new string[5]
			{
				"Elite marker v3: ",
				target.Rank.Name,
				" id=",
				target.Id,
				" (one client actor; world depth; no ring)"
			}));
			return mark;
		}
		catch
		{
			Object.Destroy((Object)(object)val);
			if ((Object)(object)val3 != (Object)null)
			{
				Object.Destroy((Object)(object)val3);
			}
			throw;
		}
	}

	private static void AddAura(Mark mark, Color color)
	{
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Expected O, but got Unknown
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Expected O, but got Unknown
		//IL_013d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Expected O, but got Unknown
		//IL_014a: Expected O, but got Unknown
		//IL_0152: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		Shader val = Shader.Find("Sprites/Default");
		if ((Object)(object)val == (Object)null)
		{
			return;
		}
		if ((Object)(object)glowTexture == (Object)null)
		{
			glowTexture = new Texture2D(64, 64, (TextureFormat)4, false);
			for (int i = 0; i < 64; i++)
			{
				for (int j = 0; j < 64; j++)
				{
					float num = ((float)j - 31.5f) / 31.5f;
					float num2 = ((float)i - 31.5f) / 31.5f;
					float num3 = num * num + num2 * num2;
					float num4 = Math.Max(0f, 1f - num3);
					num4 = num4 * num4 * 0.22f;
					glowTexture.SetPixel(j, i, new Color(1f, 1f, 1f, num4));
				}
			}
			glowTexture.Apply();
			((Texture)glowTexture).wrapMode = (TextureWrapMode)1;
			glowSprite = Sprite.Create(glowTexture, new Rect(0f, 0f, 64f, 64f), new Vector2(0.5f, 0.5f), 64f);
		}
		GameObject val2 = new GameObject("Frontier_EliteAura_" + mark.Target.Id);
		try
		{
			SpriteRenderer obj = val2.AddComponent<SpriteRenderer>();
			obj.sprite = glowSprite;
			Material val3 = new Material(val);
			Material sharedMaterial = val3;
			mark.AuraMaterial = val3;
			((Renderer)obj).sharedMaterial = sharedMaterial;
			obj.color = color;
			((Renderer)obj).sortingOrder = 0;
			val2.transform.localScale = new Vector3(1.65f, 2.2f, 1f);
			mark.AuraObject = val2;
		}
		catch
		{
			Object.Destroy((Object)(object)val2);
			throw;
		}
	}
}
