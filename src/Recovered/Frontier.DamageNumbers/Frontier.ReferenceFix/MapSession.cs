using System;
using HarmonyLib;

namespace Frontier.ReferenceFix;

internal static class MapSession
{
	private static object? battle;

	private static Evidence report = null;

	private static TerrainMap? cached;

	private static long profileId;

	private static long spriteId;

	internal static readonly MovementHeading Heading = new MovementHeading();

	private static bool fullScreen;

	private static object? lastUi;

	internal static bool InRaid { get; private set; }

	internal static string MapName { get; private set; } = "";

	internal static int Generation { get; private set; }

	internal static void Install(Harmony h, Evidence e)
	{
		report = e;
		Type type = R.Type("Game", "Game.Application.Client.ClientBattleState");
		R.Hook(h, R.Method(type, "EnterBattleState", 0), typeof(MapSession), null, "Enter");
		string[] array = new string[6] { "LeaveBattleState", "Leave", "Shutdown", "EnterResultsState", "EnterConnectionLostState", "EnterExitState" };
		foreach (string name in array)
		{
			R.Hook(h, R.Method(type, name, 0), typeof(MapSession), "Exit");
		}
		R.Hook(h, R.Method(R.Type("Game", "Game.Application.Client.ClientShelterState"), "Enter", 0), typeof(MapSession), "Exit");
		R.Hook(h, R.Method(R.Type("Game", "Game.Application.Client.ClientConnectToBattleServerState"), "Enter", 0), typeof(MapSession), "Exit");
		R.Hook(h, R.Method(R.Type("Game", "Game.UI.UiManager"), "SetFullScreenViewIsVisible", 1), typeof(MapSession), null, "FullScreen");
		report.Data["visibilityPolicy"] = "Explicit ClientBattleState lifecycle + live modal/input/frontend/current-view state; never infer raid from actor count";
	}

	private static void Enter(object __instance)
	{
		battle = __instance;
		InRaid = true;
		fullScreen = false;
		lastUi = null;
		cached = null;
		profileId = (spriteId = 0L);
		Generation++;
		Heading.Reset();
		try
		{
			MapName = R.Key(R.Get(__instance, "_mapName"));
		}
		catch (Exception e)
		{
			MapName = "";
			report.Error("map name", e);
		}
		report.Data["mapName"] = MapName;
		report.Add("raidEntries", 1L);
		report.Save(force: true);
	}

	private static void Exit()
	{
		InRaid = false;
		battle = null;
		lastUi = null;
		cached = null;
		fullScreen = false;
		profileId = (spriteId = 0L);
		MapName = "";
		Heading.Reset();
		report.Data["visibilityReason"] = "OUTSIDE_ACTIVE_RAID";
		report.Data["visible"] = false;
		report.Save();
	}

	private static void FullScreen(object __instance, bool __0)
	{
		if (lastUi != null && R.Pointer(lastUi) == R.Pointer(__instance))
		{
			fullScreen = __0;
		}
	}

	private static bool Visible(object? controller)
	{
		if (controller != null)
		{
			return R.Bool(R.Get(controller, "IsVisible"));
		}
		return false;
	}

	internal static bool Allowed(out string reason)
	{
		reason = "OUTSIDE_ACTIVE_RAID";
		if (!InRaid || battle == null)
		{
			return false;
		}
		try
		{
			object obj = R.Get(battle, "_uiManager");
			object obj2 = R.Get(battle, "_inGameUiController");
			if (obj == null || obj2 == null)
			{
				reason = "WAITING_FOR_RAID_UI";
				return false;
			}
			lastUi = obj;
			bool flag = R.Bool(R.Get(obj, "IsModalCanvasVisible")) || R.Count(R.Get(obj, "_modals")) > 0;
			bool locked = R.Bool(R.Get(obj, "IsInputLocked"));
			object obj3 = R.Get(battle, "_cursorStateController");
			bool flag2 = obj3 != null && R.Bool(R.Get(obj3, "_frontendIsActive"));
			bool flag3 = fullScreen || R.Bool(R.Call(obj2, "IsMainMenuVisible")) || Visible(R.Get(obj2, "_characterTabBar")) || Visible(R.Get(obj2, "_currentView"));
			string[] array = new string[8] { "_characterLootingController", "_characterLootingItemsContainerController", "_characterDissectingController", "_characterInteractWithSessionShopController", "_detailedMapController", "_gamePreferencesController", "_menuController", "_endGameEarnedExperienceView" };
			foreach (string name in array)
			{
				flag3 |= Visible(R.Get(obj2, name));
			}
			object obj4 = Navigation();
			bool flag4 = obj4 != null && R.Bool(R.Get(obj4, "fullMap"));
			bool flag5 = MapPolicy.Show(InRaid, flag2, flag, locked, flag3, flag4);
			reason = (flag5 ? "RAID_GAMEPLAY" : (flag ? "MODAL_WINDOW" : ((flag3 || flag4) ? "INVENTORY_OR_SCREEN" : (flag2 ? "FRONTEND_ACTIVE" : "UI_INPUT_LOCKED"))));
			report.Data["visibilityReason"] = reason;
			report.Data["visible"] = flag5;
			return flag5;
		}
		catch (Exception e)
		{
			reason = "VISIBILITY_CHECK_FAILED";
			report.Error("visibility", e);
			return false;
		}
	}

	private static object? NavigationController()
	{
		if (battle == null)
		{
			return null;
		}
		object obj = R.Get(battle, "_inGameUiController");
		if (obj == null)
		{
			return null;
		}
		object obj2 = R.Get(obj, "InGameHudController");
		if (obj2 == null)
		{
			return null;
		}
		return R.Get(obj2, "NavigationSystemController");
	}

	private static object? Navigation()
	{
		object obj = NavigationController();
		if (obj != null)
		{
			return R.Get(obj, "HudNavigationSystem");
		}
		return null;
	}

	internal static TerrainMap? Terrain()
	{
		if (!InRaid || battle == null)
		{
			return null;
		}
		try
		{
			object obj = NavigationController();
			if (obj == null)
			{
				return null;
			}
			object obj2 = R.Get(obj, "_navigationSystemSceneConfigurationLoader");
			object obj3 = ((obj2 == null) ? null : R.Get(obj2, "SceneConfiguration"));
			object obj4 = R.Get(obj, "HudNavigationSystem");
			object obj5 = ((obj3 == null) ? null : R.Get(obj3, "minimapProfile"));
			if (obj5 == null)
			{
				obj5 = ((obj4 == null) ? null : R.Get(obj4, "currentMinimapProfile"));
			}
			if (obj5 == null)
			{
				obj5 = ((obj4 == null) ? null : R.Get(obj4, "minimapProfile"));
			}
			if (obj5 == null)
			{
				report.Data["terrainStatus"] = "WAITING_FOR_NATIVE_PROFILE";
				return null;
			}
			object obj6 = R.Get(obj5, "MapTexture");
			if (obj6 == null)
			{
				report.Data["terrainStatus"] = "WAITING_FOR_NATIVE_SPRITE";
				return null;
			}
			long num = R.Pointer(obj5);
			long num2 = R.Pointer(obj6);
			if (cached != null && num == profileId && num2 == spriteId)
			{
				return cached;
			}
			object obj7 = R.Get(obj6, "texture");
			object? obj8 = R.Get(obj5, "MapBounds");
			object obj9 = R.Get(obj8, "min");
			object? obj10 = R.Get(obj8, "max");
			float num3 = R.Float(R.Get(obj9, "x"));
			float num4 = R.Float(R.Get(obj10, "x"));
			float num5 = R.Float(R.Get(obj9, "z"));
			float num6 = R.Float(R.Get(obj10, "z"));
			if (!MapPolicy.Finite(num3) || !MapPolicy.Finite(num4) || !MapPolicy.Finite(num5) || !MapPolicy.Finite(num6) || num4 <= num3 || num6 <= num5)
			{
				throw new InvalidOperationException("Invalid native map bounds");
			}
			object? obj11 = R.Get(obj6, "textureRect");
			object? obj12 = R.Get(obj6, "rect");
			object obj13 = R.Get(obj6, "textureRectOffset");
			float num7 = R.Float(R.Get(obj7, "width"));
			float num8 = R.Float(R.Get(obj7, "height"));
			float num9 = R.Float(R.Get(obj12, "width"));
			float num10 = R.Float(R.Get(obj12, "height"));
			float num11 = R.Float(R.Get(obj11, "width"));
			float num12 = R.Float(R.Get(obj11, "height"));
			if (num7 <= 0f || num8 <= 0f || num9 <= 0f || num10 <= 0f || num11 <= 0f || num12 <= 0f)
			{
				throw new InvalidOperationException("Empty native sprite bounds");
			}
			float num13 = R.Float(R.Get(obj11, "x")) / num7;
			float num14 = R.Float(R.Get(obj11, "y")) / num8;
			float num15 = num11 / num7;
			float num16 = num12 / num8;
			if (R.Bool(R.Get(obj6, "packed")))
			{
				string text = R.Key(R.Get(obj6, "packingRotation"));
				if (text == "FlipHorizontal" || text == "Rotate180")
				{
					num13 += num15;
					num15 = 0f - num15;
				}
				if (text == "FlipVertical" || text == "Rotate180")
				{
					num14 += num16;
					num16 = 0f - num16;
				}
				if (text != "None" && text != "FlipHorizontal" && text != "FlipVertical" && text != "Rotate180")
				{
					throw new InvalidOperationException("Unsupported native atlas rotation: " + text);
				}
			}
			float num17 = num4 - num3;
			float num18 = num6 - num5;
			float num19 = num3 + R.Float(R.Get(obj13, "x")) / num9 * num17;
			float num20 = num5 + R.Float(R.Get(obj13, "y")) / num10 * num18;
			cached = new TerrainMap(obj7, R.Get(obj5, "MapBackground"), num19, num19 + num11 / num9 * num17, num20, num20 + num12 / num10 * num18, num13, num14, num15, num16);
			profileId = num;
			spriteId = num2;
			report.Add("terrainBindings", 1L);
			report.Data["terrainStatus"] = "NATIVE_SCENE_SPRITE_BOUND";
			report.Data["terrainBinding"] = new
			{
				mapName = MapName,
				profile = R.Key(R.Get(obj5, "name")),
				sprite = R.Key(R.Get(obj6, "name")),
				texW = num7,
				texH = num8,
				minX = num3,
				maxX = num4,
				minZ = num5,
				maxZ = num6,
				u = num13,
				v = num14,
				uw = num15,
				vh = num16
			};
			return cached;
		}
		catch (Exception e)
		{
			cached = null;
			report.Data["terrainStatus"] = "NATIVE_PROFILE_ERROR";
			report.Error("terrain", e);
			return null;
		}
	}
}
