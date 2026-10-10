using System;
using Game.GlobalMap.UI;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Frontier.Reserve;

internal sealed class ReserveButton : System.IDisposable
{
	private readonly GameObject root;

	private readonly GameObject captionRoot;

	private readonly TextMeshProUGUI text;

	private readonly TextMeshProUGUI caption;

	private readonly Button button;

	private readonly UnityAction click;

	private readonly NativeRoot clickRoot;

	private (bool, bool, bool, bool)? lastState;

	internal bool Alive
	{
		get
		{
			if (root != null)
			{
				return root;
			}
			return false;
		}
	}

	internal ReserveButton(GlobalMapFakeController owner, System.Action clicked)
	{
		Transform transform = owner.View.transform;
		root = new GameObject("Frontier.Reserve.DirectDeploy", new Il2CppReferenceArray<Il2CppSystem.Type>(new Il2CppSystem.Type[4]
		{
			Il2CppType.Of<RectTransform>(),
			Il2CppType.Of<CanvasRenderer>(),
			Il2CppType.Of<Image>(),
			Il2CppType.Of<Button>()
		}));
		RectTransform component = root.GetComponent<RectTransform>();
		component.SetParent(transform, worldPositionStays: false);
		component.anchorMin = (component.anchorMax = Vector2.zero);
		component.pivot = Vector2.zero;
		component.anchoredPosition = new Vector2(36f, 48f);
		component.sizeDelta = new Vector2(260f, 44f);
		Image component2 = root.GetComponent<Image>();
		component2.color = new Color(0.18f, 0.085f, 0.008f, 0.96f);
		component2.raycastTarget = true;
		Outline outline = root.AddComponent<Outline>();
		outline.effectColor = new Color(1f, 0.56f, 0f, 1f);
		outline.effectDistance = new Vector2(1f, -1f);
		button = root.GetComponent<Button>();
		button.targetGraphic = component2;
		ColorBlock colors = button.colors;
		colors.m_NormalColor = Color.white;
		colors.m_HighlightedColor = new Color(1f, 0.8f, 0.45f, 1f);
		colors.m_PressedColor = new Color(0.72f, 0.52f, 0.18f, 1f);
		colors.m_DisabledColor = new Color(0.55f, 0.55f, 0.55f, 0.75f);
		colors.colorMultiplier = 1f;
		colors.fadeDuration = 0.08f;
		button.colors = colors;
		text = Label(root.transform, "Label", owner, 20f);
		RectTransform component3 = text.GetComponent<RectTransform>();
		component3.anchorMin = Vector2.zero;
		component3.anchorMax = Vector2.one;
		component3.offsetMin = new Vector2(8f, 0f);
		component3.offsetMax = new Vector2(-8f, 0f);
		text.alignment = TextAlignmentOptions.Center;
		text.color = new Color(1f, 0.62f, 0.12f, 1f);
		text.text = "РЕЗЕРВ";
		captionRoot = new GameObject("Frontier.Reserve.MenuStatus", new Il2CppReferenceArray<Il2CppSystem.Type>(new Il2CppSystem.Type[1] { Il2CppType.Of<RectTransform>() }));
		RectTransform component4 = captionRoot.GetComponent<RectTransform>();
		component4.SetParent(transform, worldPositionStays: false);
		component4.anchorMin = (component4.anchorMax = Vector2.zero);
		component4.pivot = Vector2.zero;
		component4.anchoredPosition = new Vector2(36f, 96f);
		component4.sizeDelta = new Vector2(500f, 30f);
		caption = Label(captionRoot.transform, "Status", owner, 14f);
		RectTransform component5 = caption.GetComponent<RectTransform>();
		component5.anchorMin = Vector2.zero;
		component5.anchorMax = Vector2.one;
		component5.offsetMin = (component5.offsetMax = Vector2.zero);
		caption.alignment = TextAlignmentOptions.MidlineLeft;
		caption.color = new Color(0.94f, 0.88f, 0.74f, 1f);
		caption.text = "Подготовка локации…";
		click = clicked;
		clickRoot = new NativeRoot((Il2CppObjectBase)(object)click);
		button.onClick = new Button.ButtonClickedEvent();
		button.onClick.AddListener(click);
	}

	private static TextMeshProUGUI Label(Transform parent, string name, GlobalMapFakeController owner, float size)
	{
		GameObject gameObject = new GameObject(name, new Il2CppReferenceArray<Il2CppSystem.Type>(new Il2CppSystem.Type[3]
		{
			Il2CppType.Of<RectTransform>(),
			Il2CppType.Of<CanvasRenderer>(),
			Il2CppType.Of<TextMeshProUGUI>()
		}));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		TextMeshProUGUI component = gameObject.GetComponent<TextMeshProUGUI>();
		TMP_FontAsset tMP_FontAsset = owner._unitsCount?.font;
		if (tMP_FontAsset == null && owner._closeButton?._nameLabels != null && ((Il2CppArrayBase<TextMeshProUGUI>)(object)owner._closeButton._nameLabels).Length > 0)
		{
			tMP_FontAsset = ((Il2CppArrayBase<TextMeshProUGUI>)(object)owner._closeButton._nameLabels)[0]?.font;
		}
		if (tMP_FontAsset != null)
		{
			component.font = tMP_FontAsset;
		}
		component.fontSize = size;
		component.raycastTarget = false;
		component.enableWordWrapping = false;
		return component;
	}

	internal void State(bool visible, bool ready, bool deploying, bool blocked)
	{
		if (!Alive)
		{
			return;
		}
		(bool, bool, bool, bool) tuple = (visible, ready, deploying, blocked);
		(bool, bool, bool, bool)? tuple2 = lastState;
		(bool, bool, bool, bool) tuple3 = tuple;
		bool hasValue = tuple2.HasValue;
		if (hasValue)
		{
			if (!hasValue)
			{
				return;
			}
			(bool, bool, bool, bool) valueOrDefault = tuple2.GetValueOrDefault();
			if (valueOrDefault.Item1 == tuple3.Item1 && valueOrDefault.Item2 == tuple3.Item2 && valueOrDefault.Item3 == tuple3.Item3 && valueOrDefault.Item4 == tuple3.Item4)
			{
				return;
			}
		}
		lastState = tuple;
		root.SetActive(visible);
		captionRoot.SetActive(visible);
		button.interactable = !deploying;
		text.text = (deploying ? "ОТПРАВКА…" : "РЕЗЕРВ");
		caption.text = (deploying ? "Подготовка к рейду…" : (blocked ? "Не удалось подготовить локацию." : (ready ? "Подземный комплекс · 30 минут" : "Подготовка локации…")));
	}

	public void Dispose()
	{
		if (root != null)
		{
			UnityEngine.Object.Destroy(root);
		}
		if (captionRoot != null)
		{
			UnityEngine.Object.Destroy(captionRoot);
		}
		clickRoot.Dispose();
	}
}
