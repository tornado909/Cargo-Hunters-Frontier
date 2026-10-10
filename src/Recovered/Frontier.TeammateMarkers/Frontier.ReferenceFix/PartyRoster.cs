using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace Frontier.ReferenceFix;

public static class PartyRoster
{
	private sealed record Context(object System, object World, object Session);

	private static readonly Dictionary<long, Context> Contexts = new Dictionary<long, Context>();

	private static Evidence report = null;

	private static PartyTransport? transport;

	private static MethodInfo hasCompanion = null;

	private static MethodInfo getCompanion = null;

	private static AllyPoint[] allies = Array.Empty<AllyPoint>();

	private static Dictionary<ulong, PartyIdentity> identities = new Dictionary<ulong, PartyIdentity>();

	private static long worldKey;

	private static double sampled;

	private static double nextTick;

	private static double nextNetwork;

	private static string session = "";

	private static string published = "";

	private static ulong publishedLobby;

	private static AllyPoint? local;

	private static GUIStyle? textStyle;

	private static int lastFrame = -1;

	public static AllyPoint[] ForWorld(long world, string raidSession)
	{
		if (!PartyPolicy.FreshSnapshot(world, worldKey, raidSession, session, R.Now - sampled))
		{
			return Array.Empty<AllyPoint>();
		}
		return allies.ToArray();
	}

	internal static void Install(Harmony h, TeammatePlugin plugin)
	{
		report = new Evidence("Frontier-teammate-markers-report.json", ((BasePlugin)plugin).Log, "0.3.0", "member-identity-native-client-actors-v10.7");
		MapSession.Install(h, report);
		Type type = R.Type("Game", "Game.Character.CharacterSpawnHandlerSystem");
		Type manager = R.Type("Unity.Entities", "Unity.Entities.EntityManager");
		Type companion = R.Type("Core", "Core.HybridEntity.HybridCompanionLink");
		hasCompanion = M("HasComponent");
		getCompanion = M("GetComponentObject");
		R.Hook(h, R.Method(type, "InitializeDependency", 8), typeof(PartyRoster), null, "Capture");
		R.Hook(h, R.Method(type, "OnDestroy", 0), typeof(PartyRoster), "Remove");
		R.Hook(h, R.Method(R.Type("Game", "Game.Application.Client.ClientBattleState"), "UpdateBattleState", 0), typeof(PartyRoster), null, "Tick");
		string[] array = new string[6] { "LeaveBattleState", "Leave", "Shutdown", "EnterResultsState", "EnterConnectionLostState", "EnterExitState" };
		foreach (string name in array)
		{
			R.Hook(h, R.Method(R.Type("Game", "Game.Application.Client.ClientBattleState"), name, 0), typeof(PartyRoster), "Clear");
		}
		report.Data["identitySource"] = "current Steam lobby members + per-member native raid/session/player/equipment IDs";
		report.Data["positionsSource"] = "current client world HybridCompanionLink; no network coordinate extrapolation";
		report.Data["visibility"] = "allies have no enemy FOV/wall filter; HUD windows still hide labels";
		report.Data["status"] = "WAITING_FOR_RAID";
		report.Save(force: true);
		MethodInfo M(string n)
		{
			return manager.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Single((MethodInfo m) => m.Name == n && m.IsGenericMethodDefinition && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.FullName == "Unity.Entities.Entity").MakeGenericMethod(companion);
		}
	}

	private static void Capture(object __instance, object __0)
	{
		try
		{
			object obj = R.Get(__instance, "World");
			if (obj != null && R.Bool(R.Get(obj, "IsCreated")) && !R.Bool(R.Get(__0, "IsAuthoritative")) && R.Key(R.Get(obj, "Name")).StartsWith("ClientWorld", StringComparison.Ordinal))
			{
				Contexts[R.Pointer(obj)] = new Context(__instance, obj, __0);
				report.Add("clientCaptures", 1L);
				nextTick = 0.0;
			}
		}
		catch (Exception e)
		{
			report.Error("capture", e);
		}
	}

	private static void Remove(object __instance)
	{
		long[] array = (from x in Contexts
			where R.Pointer(x.Value.System) == R.Pointer(__instance)
			select x.Key).ToArray();
		foreach (long key in array)
		{
			Contexts.Remove(key);
		}
		allies = Array.Empty<AllyPoint>();
		local = null;
		worldKey = 0L;
	}

	private static void Clear()
	{
		allies = Array.Empty<AllyPoint>();
		identities.Clear();
		local = null;
		worldKey = 0L;
		session = "";
		nextNetwork = (nextTick = 0.0);
		try
		{
			if (publishedLobby != 0L && transport != null)
			{
				transport.Publish(publishedLobby, "");
			}
		}
		catch (Exception e)
		{
			report.Error("clear member identity", e);
		}
		publishedLobby = 0uL;
		published = "";
		report.Data["status"] = "OUTSIDE_RAID";
		report.Data["allies"] = Array.Empty<object>();
		report.Save();
	}

	private static (string Equipment, string Character, string Name) Identity(object player)
	{
		object obj = R.Get(player, "Equipment");
		string item = ((obj == null) ? "" : R.Key(R.Get(obj, "Id")));
		string item2 = R.Key(R.Get(player, "CharacterItemId"));
		object obj2 = R.Get(player, "NicknameData");
		string item3 = ((obj2 == null) ? "" : PartyPolicy.Label(R.Key(R.Get(obj2, "FullName"))));
		return (Equipment: item, Character: item2, Name: item3);
	}

	private static AllyPoint? Point(Context c, int id, string name)
	{
		object obj = R.Call(c.Session, "GetPlayer", id);
		if (obj == null || R.Bool(R.Get(obj, "IsDead")) || !R.Bool(R.Get(obj, "EntityHasBeenSpawned")))
		{
			return null;
		}
		object obj2 = R.Get(obj, "Entity");
		object obj3 = R.Get(c.World, "EntityManager");
		if (!R.Bool(R.Call(obj3, "Exists", obj2)) || !R.Bool(hasCompanion.Invoke(obj3, new object[1] { obj2 })))
		{
			return null;
		}
		object obj4 = getCompanion.Invoke(obj3, new object[1] { obj2 });
		if (obj4 == null)
		{
			return null;
		}
		object obj5 = R.Call(obj4, "GetTransform");
		if (obj5 == null)
		{
			return null;
		}
		object obj6 = R.Get(obj5, "gameObject");
		if (obj6 == null || !R.Bool(R.Get(obj6, "activeInHierarchy")))
		{
			return null;
		}
		object obj7 = R.Get(obj5, "position");
		(string, string, string) tuple = Identity(obj);
		return new AllyPoint(id, R.Float(R.Get(obj7, "x")), R.Float(R.Get(obj7, "y")), R.Float(R.Get(obj7, "z")), (name.Length == 0) ? tuple.Item3 : name, tuple.Item1, tuple.Item2);
	}

	private static void Tick()
	{
		if (!MapSession.InRaid || R.Now < nextTick)
		{
			return;
		}
		nextTick = R.Now + 0.1;
		try
		{
			object battle = MapSession.Battle;
			if (battle == null)
			{
				return;
			}
			string sessionId = MapSession.SessionId;
			object obj = R.Get(battle, "_world");
			if (obj == null || !R.Bool(R.Get(obj, "IsCreated")))
			{
				return;
			}
			if (!Contexts.TryGetValue(R.Pointer(obj), out Context value))
			{
				report.Data["status"] = "WAITING_FOR_CURRENT_CLIENT_WORLD";
				return;
			}
			int num = R.Int(R.Call(value.Session, "GetLocalPlayerId"));
			AllyPoint allyPoint = Point(value, num, "");
			if (allyPoint == null)
			{
				allies = Array.Empty<AllyPoint>();
				report.Data["status"] = "WAITING_FOR_LOCAL_ENTITY";
				return;
			}
			if (!PartyPolicy.Same(session, sessionId))
			{
				identities.Clear();
				allies = Array.Empty<AllyPoint>();
				nextNetwork = 0.0;
				published = "";
			}
			session = sessionId;
			local = allyPoint;
			worldKey = R.Pointer(obj);
			object obj2 = R.Get(battle, "_coopService");
			if (obj2 == null || !R.Bool(R.Get(obj2, "IsInLobby")))
			{
				allies = Array.Empty<AllyPoint>();
				report.Data["status"] = "SOLO";
				return;
			}
			ulong num2 = Convert.ToUInt64(R.Get(R.Get(R.ConvertFor(obj2, R.Type("Game", "Game.CoopSystem.CoopService")), "_currentLobby"), "Value"));
			ulong num3 = Convert.ToUInt64(R.Get(R.Call(obj2, "GetLocalMember"), "Id"));
			if (num2 == 0L || num3 == 0L)
			{
				return;
			}
			if (R.Now >= nextNetwork)
			{
				nextNetwork = R.Now + 1.0;
				if (transport == null)
				{
					transport = new PartyTransport();
				}
				PartyIdentity partyIdentity = new PartyIdentity(1, sessionId, MapSession.MapName, num, allyPoint.Equipment, allyPoint.Character, PartyPolicy.Label(allyPoint.Name));
				if (!PartyPolicy.Valid(partyIdentity, sessionId, MapSession.MapName))
				{
					report.Data["status"] = "WAITING_FOR_LOCAL_IDENTITY";
					return;
				}
				string text = JsonSerializer.Serialize(partyIdentity);
				if (text != published || publishedLobby != num2)
				{
					transport.Publish(num2, text);
					published = text;
					publishedLobby = num2;
					report.Add("identityPublications", 1L);
				}
				KeyValuePair<ulong, string>[] array = transport.Read(num2);
				Dictionary<ulong, PartyIdentity> dictionary = new Dictionary<ulong, PartyIdentity>();
				KeyValuePair<ulong, string>[] array2 = array;
				for (int i = 0; i < array2.Length; i++)
				{
					KeyValuePair<ulong, string> keyValuePair = array2[i];
					if (keyValuePair.Key != num3)
					{
						PartyIdentity partyIdentity2 = PartyPolicy.Decode(keyValuePair.Value, sessionId, MapSession.MapName);
						if (partyIdentity2 != null)
						{
							dictionary[keyValuePair.Key] = partyIdentity2;
						}
						else
						{
							report.Add("unmatchedPeerSessions", 1L);
						}
					}
				}
				identities = dictionary;
				report.Data["lobbyMembers"] = array.Length;
				report.Data["peerIdentities"] = dictionary.Count;
			}
			List<AllyPoint> list = new List<AllyPoint>();
			foreach (KeyValuePair<ulong, PartyIdentity> identity in identities)
			{
				PartyIdentity value2 = identity.Value;
				if (value2.PlayerId != num)
				{
					AllyPoint allyPoint2 = Point(value, value2.PlayerId, PartyPolicy.Label(value2.Name));
					if (allyPoint2 == null)
					{
						report.Add("unresolvedPeerActors", 1L);
					}
					else if (!PartyPolicy.Matches(value2, allyPoint2.Id, allyPoint2.Equipment, allyPoint2.Character))
					{
						report.Add("peerActorIdentityMismatch", 1L);
					}
					else
					{
						list.Add(allyPoint2);
					}
				}
			}
			allies = (from p in list
				group p by p.Id into g
				select g.First()).ToArray();
			sampled = R.Now;
			report.Data["status"] = "LIVE_PARTY_ROSTER";
			report.Data["localPlayerId"] = num;
			report.Data["allyCount"] = allies.Length;
			report.Data["allies"] = allies.Select((AllyPoint p) => new { p.Id, p.Name, p.X, p.Y, p.Z }).ToArray();
			report.Add("snapshots", 1L);
		}
		catch (Exception e)
		{
			allies = Array.Empty<AllyPoint>();
			report.Error("party collection", e);
		}
		finally
		{
			report.Save();
		}
	}

	private static void Box(Rect r, Color c)
	{
		GUI.color = c;
		GUI.DrawTexture(r, Texture2D.whiteTexture);
	}

	private static void Text(float x, float y, string text, float scale)
	{
		if (textStyle == null)
		{
			textStyle = new GUIStyle(GUI.skin.label);
		}
		textStyle.fontSize = (int)(16f * scale);
		textStyle.alignment = TextAnchor.MiddleCenter;
		textStyle.richText = false;
		Rect position = new Rect(x - 160f * scale, y, 320f * scale, 28f * scale);
		GUI.color = Color.black;
		Vector2[] array = new Vector2[4]
		{
			new Vector2(-1f, 0f),
			new Vector2(1f, 0f),
			new Vector2(0f, -1f),
			new Vector2(0f, 1f)
		};
		for (int i = 0; i < array.Length; i++)
		{
			Vector2 vector = array[i];
			GUI.Label(new Rect(position.x + vector.x, position.y + vector.y, position.width, position.height), text, textStyle);
		}
		GUI.color = new Color(0.25f, 0.9f, 1f, 1f);
		GUI.Label(position, text, textStyle);
	}

	public static void Draw()
	{
		try
		{
			if (Event.current == null || Event.current.type != EventType.Repaint || Time.frameCount == lastFrame)
			{
				return;
			}
			lastFrame = Time.frameCount;
			if (!MapSession.InRaid || !MapSession.Allowed(out string _) || local == null || R.Now - sampled > 0.5)
			{
				return;
			}
			Camera main = Camera.main;
			if (main == null || !main.isActiveAndEnabled)
			{
				return;
			}
			Color color = GUI.color;
			Matrix4x4 matrix = GUI.matrix;
			bool enabled = GUI.enabled;
			int depth = GUI.depth;
			try
			{
				GUI.matrix = Matrix4x4.identity;
				GUI.enabled = true;
				GUI.depth = -32767;
				float num = Math.Max(0.75f, Math.Min(1.5f, (float)Screen.height / 1080f));
				int num2 = 0;
				AllyPoint[] array = allies;
				foreach (AllyPoint allyPoint in array)
				{
					float magnitude = new Vector3(allyPoint.X - local.X, allyPoint.Y - local.Y, allyPoint.Z - local.Z).magnitude;
					Vector3 vector = main.WorldToViewportPoint(new Vector3(allyPoint.X, allyPoint.Y + 2.25f, allyPoint.Z));
					bool flag = vector.z <= 0f || vector.x < 0.04f || vector.x > 0.96f || vector.y < 0.06f || vector.y > 0.94f;
					float num3 = vector.x - 0.5f;
					float num4 = 0.5f - vector.y;
					if (vector.z <= 0f)
					{
						num3 = 0f - num3;
						num4 = 0f - num4;
					}
					float num6;
					float num7;
					if (flag)
					{
						float num5 = Math.Max(Math.Abs(num3) / 0.46f, Math.Abs(num4) / 0.4f);
						if (num5 < 0.001f)
						{
							num5 = 1f;
						}
						num6 = (0.5f + num3 / num5) * (float)Screen.width;
						num7 = (0.5f + num4 / num5) * (float)Screen.height;
					}
					else
					{
						num6 = vector.x * (float)Screen.width;
						num7 = (1f - vector.y) * (float)Screen.height;
					}
					Color c = new Color(0.25f, 0.9f, 1f);
					float num8 = 5f * num;
					Box(new Rect(num6 - num8 - 1f, num7 - num8 - 1f, num8 * 2f + 2f, num8 * 2f + 2f), Color.black);
					Box(new Rect(num6 - num8, num7 - num8, num8 * 2f, num8 * 2f), c);
					Text(num6, num7 + 8f * num, (flag ? "› " : "") + (string.IsNullOrWhiteSpace(allyPoint.Name) ? "Союзник" : allyPoint.Name) + "  " + Math.Round(magnitude) + " м", num);
					num2++;
				}
				report.Data["markersDrawnLastFrame"] = num2;
				report.Add("renderedFrames", 1L);
				report.Add("markerDrawCalls", num2);
			}
			finally
			{
				GUI.color = color;
				GUI.matrix = matrix;
				GUI.enabled = enabled;
				GUI.depth = depth;
			}
		}
		catch (Exception e)
		{
			report.Error("party markers", e);
		}
		finally
		{
			report.Save();
		}
	}
}
