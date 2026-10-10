using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace Frontier.ReferenceFix;

internal static class MinimapRuntime
{
	private sealed record Context(object System, object World, object Session, object Ids);

	private sealed record Point(int Id, float X, float Y, float Z, float Yaw, bool Human, object Entity, object Player, string Rank, object? Tint, bool Ally = false, string Label = "");

	private static readonly Dictionary<long, Context> Contexts = new Dictionary<long, Context>();

	private static Evidence report = null;

	private static MethodInfo hasCompanion = null;

	private static MethodInfo getCompanion = null;

	private static Type gui = null;

	private static Type rect = null;

	private static Type color = null;

	private static Type screen = null;

	private static Type time = null;

	private static Type eventType = null;

	private static Type texture = null;

	private static Type matrix = null;

	private static Point? local;

	private static float cameraHeading;

	private static double lastFrameAt;

	private static Camera? currentCamera;

	private static Type vector2 = null;

	private static Type guiUtility = null;

	private static TerrainTexture raster = null;

	private static TerrainMap? terrain;

	private static double nextTerrain;

	private static int generation = -1;

	private static List<Point> points = new List<Point>();

	private static object? white;

	private static double nextCollect;

	private static int lastFrame = -1;

	private static int lastConsideredFrame = -1;

	internal static void Install(Harmony h, MinimapPlugin plugin)
	{
		report = new Evidence("Frontier-minimap-report.json", ((BasePlugin)plugin).Log, "0.4.5", "camera-party-native-modal-stack-v10.8.8");
		Type type = R.Type("Game", "Game.Character.CharacterSpawnHandlerSystem");
		Type entityManager = R.Type("Unity.Entities", "Unity.Entities.EntityManager");
		Type companion = R.Type("Core", "Core.HybridEntity.HybridCompanionLink");
		hasCompanion = Generic("HasComponent");
		getCompanion = Generic("GetComponentObject");
		gui = R.Type("UnityEngine.IMGUIModule", "UnityEngine.GUI");
		eventType = R.Type("UnityEngine.IMGUIModule", "UnityEngine.Event");
		rect = R.Type("UnityEngine.CoreModule", "UnityEngine.Rect");
		color = R.Type("UnityEngine.CoreModule", "UnityEngine.Color");
		screen = R.Type("UnityEngine.CoreModule", "UnityEngine.Screen");
		time = R.Type("UnityEngine.CoreModule", "UnityEngine.Time");
		texture = R.Type("UnityEngine.CoreModule", "UnityEngine.Texture2D");
		matrix = R.Type("UnityEngine.CoreModule", "UnityEngine.Matrix4x4");
		vector2 = R.Type("UnityEngine.CoreModule", "UnityEngine.Vector2");
		guiUtility = R.Type("UnityEngine.IMGUIModule", "UnityEngine.GUIUtility");
		MapSession.Install(h, report);
		raster = new TerrainTexture(report);
		report.Data["enemyVisibilityPolicy"] = "camera viewport AND closest hit in native CLIENT collision world; unknown/unfinished physics hides enemies; 100ms visibility sample lifetime";
		report.Data["enemyRankColors"] = "Frontier.Core.ElitePresentation.FindRank / RankStyle.Hex (existing equipment-bound ranks)";
		report.Data["renderedFrames"] = 0L;
		report.Data["repaintEvents"] = 0L;
		report.Data["drawCalls"] = 0L;
		R.Hook(h, R.Method(type, "InitializeDependency", 8), typeof(MinimapRuntime), null, "Capture");
		R.Hook(h, R.Method(type, "OnDestroy", 0), typeof(MinimapRuntime), "Remove");
		report.Data["status"] = "WAITING_FOR_CLIENT_WORLD";
		report.Data["hooks"] = 2;
		report.Save(force: true);
		MethodInfo Generic(string name)
		{
			return entityManager.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Single((MethodInfo m) => m.Name == name && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 1 && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.FullName == "Unity.Entities.Entity").MakeGenericMethod(companion);
		}
	}

	private static void Capture(object __instance, object __0)
	{
		try
		{
			report.Add("captureCalls", 1L);
			object obj = R.Get(__instance, "World");
			if (obj != null && R.Bool(R.Get(obj, "IsCreated")) && !R.Bool(R.Get(__0, "IsAuthoritative")) && R.Key(R.Get(obj, "Name")).StartsWith("ClientWorld", StringComparison.Ordinal))
			{
				object ids = Activator.CreateInstance(R.Method(__0.GetType(), "GetPlayerIds", 1).GetParameters()[0].ParameterType);
				Contexts[R.Pointer(obj)] = new Context(__instance, obj, __0, ids);
				nextCollect = 0.0;
				report.Data["status"] = "CLIENT_WORLD_CAPTURED";
				report.Data["activeClientWorlds"] = Contexts.Count;
				report.Save(force: true);
			}
		}
		catch (Exception e)
		{
			report.Error("client context", e);
		}
	}

	private static void Remove(object __instance)
	{
		long pointer = R.Pointer(__instance);
		long[] array = (from x in Contexts
			where R.Pointer(x.Value.System) == pointer
			select x.Key).ToArray();
		foreach (long key in array)
		{
			Contexts.Remove(key);
		}
		local = null;
		points.Clear();
		nextCollect = 0.0;
	}

	private static Point? Resolve(Context context, object manager, int id)
	{
		object obj = R.Call(context.Session, "GetPlayer", id);
		if (obj == null || R.Bool(R.Get(obj, "IsDead")) || !R.Bool(R.Get(obj, "EntityHasBeenSpawned")))
		{
			return null;
		}
		object obj2 = R.Get(obj, "Entity");
		if (!R.Bool(R.Call(manager, "Exists", obj2)))
		{
			return null;
		}
		if (!R.Bool(hasCompanion.Invoke(manager, new object[1] { obj2 })))
		{
			return null;
		}
		object obj3 = getCompanion.Invoke(manager, new object[1] { obj2 });
		if (obj3 == null)
		{
			return null;
		}
		object obj4 = R.Call(obj3, "GetTransform");
		if (obj4 == null)
		{
			return null;
		}
		object obj5 = R.Get(obj4, "gameObject");
		if (obj5 == null || !R.Bool(R.Get(obj5, "activeInHierarchy")))
		{
			return null;
		}
		object obj6 = R.Get(obj4, "position");
		object obj7 = R.Get(obj4, "eulerAngles");
		object obj8 = R.Get(obj, "AccountId");
		bool human = obj8 != null && R.Bool(R.Get(obj8, "HasValue"));
		return new Point(id, R.Float(R.Get(obj6, "x")), R.Float(R.Get(obj6, "y")), R.Float(R.Get(obj6, "z")), R.Float(R.Get(obj7, "y")), human, obj2, obj, "normal", null);
	}

	private static void Collect()
	{
		local = null;
		points.Clear();
		report.Add("snapshots", 1L);
		report.Data["status"] = "WAITING_FOR_CLIENT_WORLD";
		KeyValuePair<long, Context>[] array = Contexts.ToArray();
		for (int i = 0; i < array.Length; i++)
		{
			KeyValuePair<long, Context> keyValuePair = array[i];
			Context value = keyValuePair.Value;
			if (!R.Bool(R.Get(value.World, "IsCreated")))
			{
				Contexts.Remove(keyValuePair.Key);
			}
			else
			{
				if (R.Bool(R.Get(value.Session, "IsAuthoritative")) || !R.Bool(R.Get(value.Session, "IsBattleSession")))
				{
					continue;
				}
				report.Data["status"] = "WAITING_FOR_LOCAL_PLAYER";
				int num = R.Int(R.Call(value.Session, "GetLocalPlayerId"));
				object manager = R.Get(value.World, "EntityManager");
				Point point = Resolve(value, manager, num);
				if (point == null)
				{
					continue;
				}
				local = point;
				currentCamera = SightFrame.ActiveCamera();
				SightFrame sightFrame = ((currentCamera == null) ? null : SightFrame.Create(value.World, point.Entity, currentCamera, report));
				if (currentCamera != null)
				{
					cameraHeading = SightFrame.HeadingOf(currentCamera, cameraHeading);
				}
				AllyPoint[] array2 = PartyRoster.ForWorld(R.Pointer(value.World), MapSession.SessionId);
				HashSet<int> hashSet = array2.Select((AllyPoint p) => p.Id).ToHashSet();
				AllyPoint[] array3 = array2;
				foreach (AllyPoint val in array3)
				{
					points.Add(new Point(val.Id, val.X, val.Y, val.Z, 0f, Human: true, null, null, "ally", Color(0.25f, 0.9f, 1f), Ally: true, val.Name));
				}
				report.Data["visibleAllies"] = array2.Length;
				R.Call(value.Ids, "Clear");
				R.Call(value.Session, "GetPlayerIds", value.Ids);
				foreach (object item in R.Values(value.Ids, 2048))
				{
					int num3 = R.Int(item);
					if (num3 == num || hashSet.Contains(num3))
					{
						continue;
					}
					Point point2 = Resolve(value, manager, num3);
					if (!(point2 == null))
					{
						float dx = point2.X - point.X;
						float dz = point2.Z - point.Z;
						if (point2.Human)
						{
							report.Add("unclassifiedAccountActorsHidden", 1L);
						}
						else if (SightPolicy.EnemyInRange(point2.Human, num3, num, dx, dz) && sightFrame != null && sightFrame.Visible(point2.Entity, point2.X, point2.Y, point2.Z))
						{
							(string, object) tuple = RankColors.Read(point2.Player, report);
							List<Point> list = points;
							Point point3 = point2 with { Rank = tuple.Item1, Tint = tuple.Item2 };
							list.Add(point3);
						}
					}
				}
				report.Data["status"] = "LIVE_CLIENT_POSITIONS";
				report.Data["playersInLastSnapshot"] = R.Count(value.Ids);
				report.Data["visibleEnemies"] = points.Count((Point p) => !p.Ally);
				report.Data["visibleRanks"] = (from p in points
					where !p.Ally
					select p.Rank).ToArray();
				object value2;
				int val2 = (report.Data.TryGetValue("maxPlayers", out value2) ? Convert.ToInt32(value2) : 0);
				report.Data["maxPlayers"] = Math.Max(val2, points.Count + 1);
				break;
			}
		}
		report.Data["activeClientWorlds"] = Contexts.Count;
	}

	private static object Rect(float x, float y, float w, float h)
	{
		return Activator.CreateInstance(rect, x, y, w, h);
	}

	private static object Color(float r, float g, float b, float a = 1f)
	{
		return Activator.CreateInstance(color, r, g, b, a);
	}

	private static void Fill(float x, float y, float w, float h, object c)
	{
		R.Set(gui, "color", c);
		R.Call(gui, "DrawTexture", Rect(x, y, w, h), white);
		report.Add("drawCalls", 1L);
	}

	private static void Label(float x, float y, float w, float h, string text)
	{
		R.Set(gui, "color", Color(1f, 1f, 1f));
		R.Call(gui, "Label", Rect(x, y, w, h), text);
	}

	internal static void Draw()
	{
		double now = R.Now;
		try
		{
			report.Add("onGuiCalls", 1L);
			object obj = R.Get(eventType, "current");
			if (obj == null)
			{
				report.Add("drawSkippedNoEvent", 1L);
				return;
			}
			object x = R.Get(obj, "type");
			report.Data["lastEventName"] = R.Key(x);
			report.Data["lastEventValue"] = R.Int(x);
			if (R.Int(x) != 7)
			{
				report.Add("nonRepaintEvents", 1L);
				return;
			}
			report.Add("repaintEvents", 1L);
			int num = R.Int(R.Get(time, "frameCount"));
			if (num == lastFrame)
			{
				report.Add("sameFrameSkipped", 1L);
			}
			else
			{
				if (num == lastConsideredFrame)
				{
					return;
				}
				lastConsideredFrame = num;
				double now2 = R.Now;
				if (lastFrameAt > 0.0 && MapSession.InRaid && now2 - lastFrameAt > 0.25)
				{
					report.Add("observedFrameGapsOver250ms", 1L);
					report.Timing("observedFrameGapMs", (now2 - lastFrameAt) * 1000.0);
				}
				lastFrameAt = now2;
				if (!MapSession.InRaid)
				{
					if (terrain != null)
					{
						raster.Clear();
					}
					local = null;
					points.Clear();
					terrain = null;
					report.Add("hiddenOutsideRaid", 1L);
					report.Data["visible"] = false;
					report.Data["visibilityReason"] = "OUTSIDE_ACTIVE_RAID";
					return;
				}
				if (generation != MapSession.Generation)
				{
					generation = MapSession.Generation;
					raster.Clear();
					terrain = null;
					nextTerrain = 0.0;
					nextCollect = 0.0;
				}
				if (!MapSession.Allowed(out string reason))
				{
					report.Add("hiddenForUi", 1L);
					report.Data["visibilityReason"] = reason;
					report.Data["visible"] = false;
					points.Clear();
					nextCollect = 0.0;
					return;
				}
				currentCamera = SightFrame.ActiveCamera();
				if (currentCamera == null)
				{
					points.Clear();
					report.Data["visible"] = false;
					report.Data["visibilityReason"] = "NO_ACTIVE_CAMERA";
					return;
				}
				cameraHeading = SightFrame.HeadingOf(currentCamera, cameraHeading);
				if (R.Now >= nextCollect)
				{
					nextCollect = R.Now + 0.1;
					double now3 = R.Now;
					Collect();
					report.Timing("collectMs", (R.Now - now3) * 1000.0);
				}
				if (R.Now >= nextTerrain)
				{
					nextTerrain = R.Now + 0.5;
					terrain = MapSession.Terrain();
				}
				if (local == null)
				{
					report.Add("drawSkippedNoLocal", 1L);
					return;
				}
				if (white == null)
				{
					white = R.Get(texture, "whiteTexture");
				}
				if (white != null)
				{
					object value = R.Get(gui, "color");
					object value2 = R.Get(gui, "depth");
					object value3 = R.Get(gui, "matrix");
					object value4 = R.Get(gui, "enabled");
					object value5 = R.Get(gui, "backgroundColor");
					object value6 = R.Get(gui, "contentColor");
					try
					{
						R.Set(gui, "depth", -32768);
						R.Set(gui, "matrix", R.Get(matrix, "identity"));
						R.Set(gui, "enabled", true);
						R.Set(gui, "backgroundColor", Color(1f, 1f, 1f));
						R.Set(gui, "contentColor", Color(1f, 1f, 1f));
						float num2 = Math.Min(224f, Math.Max(144f, (float)R.Int(R.Get(screen, "height")) * 0.22f));
						float num3 = Math.Max(12f, (float)R.Int(R.Get(screen, "width")) - num2 - 18f);
						float num4 = 86f;
						float num5 = num2 / 2f;
						float num6 = num5 - 8f;
						float num7 = cameraHeading;
						Fill(num3, num4, num2, num2 + 48f, Color(0.025f, 0.04f, 0.06f, 0.94f));
						Label(num3 + 8f, num4 + 4f, num2 - 16f, 22f, "FRONTIER  |  100 м");
						float num8 = num4 + 28f;
						object obj2 = ((terrain == null) ? null : raster.Get(terrain, (int)num2, local.X, local.Z, num7));
						Fill(num3, num8, num2, num2, Color(0.025f, 0.04f, 0.06f));
						if (obj2 != null)
						{
							try
							{
								R.Set(gui, "color", Color(1f, 1f, 1f));
								R.Call(gui, "DrawTexture", Rect(num3, num8, num2, num2), obj2);
								report.Add("terrainDrawCalls", 1L);
							}
							catch (Exception e)
							{
								obj2 = null;
								report.Error("background draw", e);
							}
						}
						object c = Color(0.25f, 0.4f, 0.45f, 0.7f);
						if (obj2 == null)
						{
							for (int i = 1; i < 4; i++)
							{
								Fill(num3 + (float)i * num2 / 4f, num8, 1f, num2, c);
								Fill(num3, num8 + (float)i * num2 / 4f, num2, 1f, c);
							}
						}
						Fill(num3, num8, num2, 1f, c);
						Fill(num3, num8 + num2 - 1f, num2, 1f, c);
						Fill(num3, num8, 1f, num2, c);
						Fill(num3 + num2 - 1f, num8, 1f, num2, c);
						foreach (Point point in points)
						{
							float num9 = point.X - local.X;
							float num10 = point.Z - local.Z;
							if (point.Ally)
							{
								float num11 = (float)Math.Sqrt(num9 * num9 + num10 * num10);
								bool num12 = num11 > 100f;
								float num13 = (num12 ? (100f / num11) : 1f);
								(float, float) tuple = MapPolicy.Project(num9 * num13, num10 * num13, num7, (num6 - 5f) / 100f);
								Fill(num3 + num5 + tuple.Item1 - 4f, num8 + num5 + tuple.Item2 - 4f, 8f, 8f, Color(0f, 0f, 0f));
								Fill(num3 + num5 + tuple.Item1 - 3f, num8 + num5 + tuple.Item2 - 3f, 6f, 6f, Color(0.25f, 0.9f, 1f));
								if (num12)
								{
									Label(num3 + num5 + tuple.Item1 - 18f, num8 + num5 + tuple.Item2 - 18f, 50f, 18f, Math.Round(num11) + " м");
								}
								report.Add("allyDrawCalls", 1L);
							}
							else if (!(num9 * num9 + num10 * num10 > 10000f))
							{
								Vector3 vector = currentCamera.WorldToViewportPoint(new Vector3(point.X, point.Y + 1.05f, point.Z));
								Vector3 vector2 = currentCamera.WorldToViewportPoint(new Vector3(point.X, point.Y + 1.65f, point.Z));
								if (SightPolicy.InViewport(vector.x, vector.y, vector.z) || SightPolicy.InViewport(vector2.x, vector2.y, vector2.z))
								{
									(float, float) tuple2 = MapPolicy.Project(num9, num10, num7, num6 / 100f);
									Fill(num3 + num5 + tuple2.Item1 - 2.5f, num8 + num5 + tuple2.Item2 - 2.5f, 5f, 5f, point.Tint ?? Color(0.82f, 0.84f, 0.88f));
								}
							}
						}
						for (int j = 0; j < 6; j++)
						{
							Fill(num3 + num5 - (float)j, num8 + num5 - 7f + (float)j, 1 + j * 2, 1f, Color(0.45f, 1f, 0.55f));
						}
						Fill(num3 + num5 - 1f, num8 + num5 - 1f, 3f, 6f, Color(0.45f, 1f, 0.55f));
						(float, float) tuple3 = MapPolicy.Project(0f, 1f, num7, num6 - 10f);
						Label(num3 + num5 + tuple3.Item1 - 5f, num8 + num5 + tuple3.Item2 - 8f, 18f, 18f, "N");
						Label(num3 + 8f, num4 + num2 + 28f, num2 - 16f, 20f, (obj2 == null) ? "Карта ещё не загружена" : "Вверх — направление камеры");
						report.Data["headingDegrees"] = num7;
						report.Data["headingSource"] = "active rendered camera horizontal forward (yaw), sampled every displayed frame";
						lastFrame = num;
						report.Add("renderedFrames", 1L);
						report.Add("battleFrames", 1L);
						report.Data["lastDrawUtc"] = DateTime.UtcNow.ToString("O");
						report.Data["lastDrawRect"] = new
						{
							x = num3,
							y = num4,
							width = num2,
							height = num2 + 48f
						};
						report.Data["drawStatus"] = "DRAW_CALLS_COMPLETED_NOT_SCREENSHOT_VERIFIED";
						return;
					}
					finally
					{
						R.Set(gui, "color", value);
						R.Set(gui, "depth", value2);
						R.Set(gui, "matrix", value3);
						R.Set(gui, "enabled", value4);
						R.Set(gui, "backgroundColor", value5);
						R.Set(gui, "contentColor", value6);
					}
				}
				report.Add("missingWhiteTexture", 1L);
			}
		}
		catch (Exception e2)
		{
			report.Error("radar", e2);
		}
		finally
		{
			report.Timing("drawMs", (R.Now - now) * 1000.0);
			report.Save();
		}
	}
}
