using System;
using Frontier.Loot;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Frontier.Survivor;

internal static class SurvivorHud
{
	private static GameObject? root;

	private static GameObject? choices;

	private static TextMeshProUGUI? status;

	private static TextMeshProUGUI? heading;

	private static readonly TextMeshProUGUI?[] labels = new TextMeshProUGUI[3];

	private static Image? xpFill;

	private static readonly LeaseBag leases = new LeaseBag();

	private static string run = "";

	private static string last = "";

	private static GameObject Node(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
	{
		GameObject gameObject = new GameObject(name);
		RectTransform rectTransform = gameObject.AddComponent<RectTransform>();
		rectTransform.SetParent(parent, worldPositionStays: false);
		Vector2 anchorMin = (rectTransform.anchorMax = anchor);
		rectTransform.anchorMin = anchorMin;
		rectTransform.pivot = pivot;
		rectTransform.anchoredPosition = position;
		rectTransform.sizeDelta = size;
		return gameObject;
	}

	private static TextMeshProUGUI Text(string name, Transform parent, Vector2 pos, Vector2 size, float fontSize)
	{
		TextMeshProUGUI textMeshProUGUI = Node(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, size).AddComponent<TextMeshProUGUI>();
		textMeshProUGUI.font = Menu.Font;
		textMeshProUGUI.fontSize = fontSize;
		textMeshProUGUI.color = new Color(1f, 0.65f, 0.16f);
		textMeshProUGUI.enableWordWrapping = true;
		textMeshProUGUI.raycastTarget = false;
		return textMeshProUGUI;
	}

	private static void Build()
	{
		run = SurvivorRun.RunId;
		root = new GameObject("Frontier.Survivor.HUD");
		root.AddComponent<RectTransform>();
		Canvas canvas = root.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 75;
		CanvasScaler canvasScaler = root.AddComponent<CanvasScaler>();
		canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
		canvasScaler.matchWidthOrHeight = 0.5f;
		root.AddComponent<GraphicRaycaster>();
		SceneManager.MoveGameObjectToScene(root, Loader.Scene);
		GameObject gameObject = Node("Run status", root.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -98f), new Vector2(490f, 105f));
		Image image = gameObject.AddComponent<Image>();
		image.color = new Color(0.02f, 0.025f, 0.03f, 0.8f);
		image.raycastTarget = false;
		status = Text("Run level XP", gameObject.transform, new Vector2(12f, -6f), new Vector2(466f, 80f), 19f);
		GameObject gameObject2 = Node("XP bar", gameObject.transform, new Vector2(0f, 0f), Vector2.zero, new Vector2(12f, 8f), new Vector2(466f, 5f));
		Image image2 = gameObject2.AddComponent<Image>();
		image2.color = new Color(0.18f, 0.2f, 0.22f, 0.95f);
		image2.raycastTarget = false;
		xpFill = Node("XP", gameObject2.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(466f, 5f)).AddComponent<Image>();
		xpFill.color = new Color(0.98f, 0.57f, 0.04f);
		xpFill.raycastTarget = false;
		choices = Node("Upgrade selection", root.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 205f), new Vector2(960f, 132f));
		heading = Text("Choose upgrade", choices.transform, new Vector2(0f, 0f), new Vector2(960f, 30f), 20f);
		heading.alignment = TextAlignmentOptions.Center;
		for (int i = 0; i < 3; i++)
		{
			int index = i;
			GameObject gameObject3 = Node("Upgrade " + i, choices.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(i * 320, -34f), new Vector2(310f, 94f));
			Image image3 = gameObject3.AddComponent<Image>();
			image3.color = new Color(0.055f, 0.065f, 0.07f, 0.95f);
			Button button = gameObject3.AddComponent<Button>();
			button.targetGraphic = image3;
			UnityAction call = leases.Keep((UnityAction)(Action)delegate
			{
				if (run == SurvivorRun.RunId)
				{
					SurvivorRun.Choose(index);
				}
			});
			button.onClick.AddListener(call);
			labels[i] = Text("Bonus", gameObject3.transform, new Vector2(12f, -8f), new Vector2(286f, 80f), 18f);
		}
		last = "";
	}

	internal static void Tick()
	{
		if (!SurvivorRun.Active || !Loader.Ready)
		{
			if (root != null)
			{
				Clear();
			}
			return;
		}
		if (root == null)
		{
			if (Menu.Font == null)
			{
				return;
			}
			Build();
		}
		if (run != SurvivorRun.RunId)
		{
			Clear();
			return;
		}
		RunProgress progress = SurvivorRun.Progress;
		if (progress.PendingChoices > 0)
		{
			Keyboard current = Keyboard.current;
			if (current != null)
			{
				if (current.f1Key.wasPressedThisFrame)
				{
					SurvivorRun.Choose(0);
				}
				else if (current.f2Key.wasPressedThisFrame)
				{
					SurvivorRun.Choose(1);
				}
				else if (current.f3Key.wasPressedThisFrame)
				{
					SurvivorRun.Choose(2);
				}
			}
		}
		int num = (int)SurvivorRun.Seconds;
		string text = $"ВЫЖИВАНИЕ   {num / 60:00}:{num % 60:00}   ВРАГИ: {RankPolicy.Name(SurvivorRun.Rank)}\nУРОВЕНЬ {progress.Level}   ОПЫТ {progress.Experience}/{progress.Required}   УБИЙСТВА {progress.Kills}\nБОЕЗАПАС НЕ РАСХОДУЕТСЯ";
		if (text != last)
		{
			status.text = text;
			last = text;
			xpFill.rectTransform.sizeDelta = new Vector2(466f * (float)progress.Experience / (float)progress.Required, 5f);
		}
		choices.SetActive(progress.PendingChoices > 0);
		if (progress.PendingChoices <= 0)
		{
			return;
		}
		heading.text = "ПОВЫШЕНИЕ УРОВНЯ — ВЫБЕРИТЕ БОНУС  [F1–F3]";
		for (int i = 0; i < 3; i++)
		{
			labels[i].transform.parent.gameObject.SetActive(i < progress.Offer.Length);
			if (i < progress.Offer.Length)
			{
				int num2 = progress.Offer[i];
				labels[i].text = $"[F{i + 1}]  {RunProgress.Names[num2]}\n{RunProgress.Descriptions[num2]}\nТолько этот забег · {progress.Upgrades[num2] + 1}/{RunProgress.Caps[num2]}";
			}
		}
	}

	internal static void Clear()
	{
		if (root != null)
		{
			UnityEngine.Object.Destroy(root);
		}
		root = (choices = null);
		status = (heading = null);
		xpFill = null;
		for (int i = 0; i < 3; i++)
		{
			labels[i] = null;
		}
		leases.Dispose();
		run = (last = "");
	}
}
