using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Unity.IL2CPP;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

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
		var enumerator = Marks.Values.GetEnumerator();
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
		if (glowSprite != null)
		{
			Object.Destroy(glowSprite);
		}
		if (glowTexture != null)
		{
			Object.Destroy(glowTexture);
		}
		glowSprite = null;
		glowTexture = null;
		font = null;
	}

	private static void Release(Mark m)
	{
		if (m.LabelObject != null)
		{
			Object.Destroy(m.LabelObject);
		}
		if (m.AuraObject != null)
		{
			Object.Destroy(m.AuraObject);
		}
		if (m.TextMaterial != null)
		{
			Object.Destroy(m.TextMaterial);
		}
		if (m.AuraMaterial != null)
		{
			Object.Destroy(m.AuraMaterial);
		}
	}

	internal static void Tick()
	{
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
			if (cam == null)
			{
				Clear();
				return;
			}
			string[] array2;
			if (unscaledTime >= collectAt)
			{
				collectAt = unscaledTime + 0.1f;
				EliteClientTracker.Target[] array = Enumerable.ToArray<EliteClientTracker.Target>(Enumerable.Take<EliteClientTracker.Target>((System.Collections.Generic.IEnumerable<EliteClientTracker.Target>)Enumerable.OrderBy<EliteClientTracker.Target, float>(Enumerable.Where<EliteClientTracker.Target>((System.Collections.Generic.IEnumerable<EliteClientTracker.Target>)EliteClientTracker.Collect(), (Func<EliteClientTracker.Target, bool>)((EliteClientTracker.Target t) => (t.Root.transform.position - cam.transform.position).sqrMagnitude < 3600f)), (Func<EliteClientTracker.Target, float>)((EliteClientTracker.Target t) => (t.Root.transform.position - cam.transform.position).sqrMagnitude)), 48));
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
					if (Marks.TryGetValue(target.Id, out mark))
					{
						if (mark.Target.Root == target.Root)
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
			foreach (string text2 in array2)
			{
				Mark mark3 = Marks[text2];
				GameObject root = mark3.Target.Root;
				if (root == null || !root.activeInHierarchy || !EliteClientTracker.Alive(mark3.Target.Player))
				{
					Release(mark3);
					Marks.Remove(text2);
					continue;
				}
				bool flag2 = !flag && (root.transform.position - cam.transform.position).sqrMagnitude < 3600f;
				mark3.LabelObject.SetActive(flag2);
				if (mark3.AuraObject != null)
				{
					mark3.AuraObject.SetActive(flag2);
				}
				if (!flag2)
				{
					continue;
				}
				Vector3 position = root.transform.position;
				float num3 = position.y + 1.9f;
				System.Collections.Generic.IEnumerator<Renderer> enumerator = root.GetComponentsInChildren<Renderer>(includeInactive: false).GetEnumerator();
				try
				{
					while (((System.Collections.IEnumerator)enumerator).MoveNext())
					{
						Renderer current = enumerator.Current;
						if (current != null && current.enabled && !(current is ParticleSystemRenderer) && !(current is TrailRenderer) && !(current is LineRenderer) && current.bounds.size.y < 5f)
						{
							num3 = Math.Max(num3, current.bounds.max.y + 0.18f);
						}
					}
				}
				finally
				{
					((System.IDisposable)enumerator)?.Dispose();
				}
				mark3.LabelObject.transform.position = new Vector3(position.x, num3 + 0.1f, position.z);
				mark3.LabelObject.transform.rotation = cam.transform.rotation;
				float num4 = Math.Clamp((position - cam.transform.position).magnitude * 0.012f, 0.09f, 0.48f);
				mark3.LabelObject.transform.localScale = new Vector3(num4, num4, num4);
				if (mark3.AuraObject != null)
				{
					Vector3 vector = new Vector3(position.x, position.y + 0.95f, position.z);
					Vector3 forward = cam.transform.forward;
					mark3.AuraObject.transform.position = vector + forward * 0.14f;
					mark3.AuraObject.transform.rotation = cam.transform.rotation;
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
		int num = Convert.ToInt32(hex.TrimStart('#'), 16);
		return new Color((float)((num >> 16) & 0xFF) / 255f, (float)((num >> 8) & 0xFF) / 255f, (float)(num & 0xFF) / 255f, 1f);
	}

	private static Mark? Create(EliteClientTracker.Target target)
	{
		if (font == null)
		{
			font = TMP_Settings.defaultFontAsset;
			System.Collections.Generic.IEnumerator<TMP_Text> enumerator = Object.FindObjectsOfType<TMP_Text>().GetEnumerator();
			try
			{
				while (((System.Collections.IEnumerator)enumerator).MoveNext())
				{
					TMP_Text current = enumerator.Current;
					if (current != null && current.font != null)
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
		if (font == null)
		{
			return null;
		}
		GameObject gameObject = new GameObject("Frontier_EliteName_" + target.Id);
		TextMeshPro textMeshPro = gameObject.AddComponent<TextMeshPro>();
		Material material = null;
		try
		{
			textMeshPro.font = font;
			textMeshPro.text = target.Rank.Name;
			textMeshPro.fontSize = 8f;
			textMeshPro.alignment = TextAlignmentOptions.Center;
			textMeshPro.enableWordWrapping = false;
			textMeshPro.rectTransform.sizeDelta = new Vector2(22f, 3f);
			material = new Material(font.material);
			material.name = "Frontier_EliteName_Material";
			textMeshPro.fontSharedMaterial = material;
			textMeshPro.isOverlay = false;
			material.SetFloat("_ZTestMode", 4f);
			material.SetFloat("unity_GUIZTestMode", 4f);
			material.renderQueue = 3000;
			textMeshPro.color = ColorOf(target.Rank.Hex);
			textMeshPro.outlineWidth = 0.12f;
			textMeshPro.outlineColor = new Color32(8, 8, 8, 230);
			Mark mark = new Mark(target, gameObject, textMeshPro, material);
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
			Object.Destroy(gameObject);
			if (material != null)
			{
				Object.Destroy(material);
			}
			throw;
		}
	}

	private static void AddAura(Mark mark, Color color)
	{
		Shader shader = Shader.Find("Sprites/Default");
		if (shader == null)
		{
			return;
		}
		if (glowTexture == null)
		{
			glowTexture = new Texture2D(64, 64, TextureFormat.RGBA32, mipChain: false);
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
			glowTexture.wrapMode = TextureWrapMode.Clamp;
			glowSprite = Sprite.Create(glowTexture, new Rect(0f, 0f, 64f, 64f), new Vector2(0.5f, 0.5f), 64f);
		}
		GameObject gameObject = new GameObject("Frontier_EliteAura_" + mark.Target.Id);
		try
		{
			SpriteRenderer spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
			spriteRenderer.sprite = glowSprite;
			spriteRenderer.sharedMaterial = (mark.AuraMaterial = new Material(shader));
			spriteRenderer.color = color;
			spriteRenderer.sortingOrder = 0;
			gameObject.transform.localScale = new Vector3(1.65f, 2.2f, 1f);
			mark.AuraObject = gameObject;
		}
		catch
		{
			Object.Destroy(gameObject);
			throw;
		}
	}
}
