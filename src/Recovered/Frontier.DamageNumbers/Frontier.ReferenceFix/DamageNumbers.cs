using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx.Unity.IL2CPP;
using Core.HybridEntity;
using Core.Utils;
using Game.Character;
using Game.DamageSystem;
using Game.GameModes;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Frontier.ReferenceFix;

internal static class DamageNumbers
{
	private sealed record Context(object System, object World, object Session, bool Authority);

	private sealed record Shot(bool Critical, double At);

	private sealed record BeforeShot(object Entity, ulong Projectile, float Damage, float Ordinary, float Critical, bool WasPrepared);

	private sealed class Popup
	{
		internal int Target;

		internal uint ShotTick;

		internal float Damage;

		internal float X;

		internal float Y;

		internal float Z;

		internal bool Critical;

		internal double At;

		internal int Lane;
	}

	private sealed record Bar(int Id, Entity Entity, Vector3 At, float Height, float HP, float Maximum, string Rank, double Observed);

	private sealed class HealthBinding : IDisposable
	{
		internal readonly CharacterHealthDataProvider Provider;

		private readonly NativeLease root;

		internal double Seen;

		internal HealthBinding(BattleSession s, EntityManager em, Entity e)
		{
			Provider = new CharacterHealthDataProvider(s.ItemsManager, em, e);
			root = new NativeLease((Il2CppObjectBase)(object)Provider);
			Seen = R.Now;
		}

		public void Dispose()
		{
			root.Dispose();
		}
	}

	private static readonly System.Collections.Generic.Dictionary<long, Context> contexts = new System.Collections.Generic.Dictionary<long, Context>();

	private static readonly System.Collections.Generic.Dictionary<ulong, Shot> shots = new System.Collections.Generic.Dictionary<ulong, Shot>();

	private static readonly System.Collections.Generic.Queue<ulong> shotOrder = new System.Collections.Generic.Queue<ulong>();

	private static readonly System.Collections.Generic.Dictionary<string, (string Source, float Damage, double At)> crossSource = new System.Collections.Generic.Dictionary<string, (string, float, double)>();

	private static readonly System.Collections.Generic.List<Popup> popups = new System.Collections.Generic.List<Popup>();

	private static readonly HitDeduplicator seen = new HitDeduplicator();

	private static Evidence report = null;

	private static Type coreRuntime = null;

	private static Type coreCombat = null;

	private static Type cameraType = null;

	private static Type vector3 = null;

	private static Type gui = null;

	private static Type rect = null;

	private static Type color = null;

	private static Type matrix = null;

	private static Type screen = null;

	private static Type time = null;

	private static Type eventType = null;

	private static NativeComponent? requests;

	private static NativeComponent? events;

	private static NativeComponent? messages;

	private static NativeComponent? playerIds;

	private static MethodInfo hasCompanion = null;

	private static MethodInfo getCompanion = null;

	private static object? white;

	private static object? normalStyle;

	private static object? criticalStyle;

	private static int lastFrame = -1;

	private static int generation = -1;

	private static bool collecting;

	private static readonly System.Collections.Generic.List<Bar> bars = new System.Collections.Generic.List<Bar>();

	private static readonly System.Collections.Generic.Dictionary<(int Index, int Version), HealthBinding> healthBindings = new System.Collections.Generic.Dictionary<(int, int), HealthBinding>();

	private static readonly System.Collections.Generic.Dictionary<(int Index, int Version), (float Value, double Until)> headHeights = new System.Collections.Generic.Dictionary<(int, int), (float, double)>();

	private static Il2CppSystem.Collections.Generic.List<int>? barIds;

	private static double nextBars;

	private static double lastBars;

	private static double nextBarDrawReport;

	private static GUIStyle? barStyle;

	private static bool healthLayoutChecked;

	private static long barsWorld;

	private static long barsSession;

	private static NativeComponent? healthLayout;

	internal static void Install(Harmony h, DamageNumbersPlugin plugin)
	{
		report = new Evidence("Frontier-damage-numbers-report.json", ((BasePlugin)plugin).Log, "0.2.2", "damaged-only-distance-scaled-health-bars-v10.8.5");
		report.Data["damageWrites"] = 0;
		report.Data["randomRolls"] = 0;
		report.Data["nativePlaytestClaim"] = false;
		report.Data["damageSource"] = "DamageEvent.HitDamageApplied / RemoteDamageEventMessage.Damage; local attacker only";
		report.Data["criticalSource"] = "Observed multiplier in existing Frontier.Loot.Combat.BeforeProjectiles, after its original calculation";
		report.Data["status"] = "WAITING_FOR_RAID_HITS";
		report.Data["healthBarTargets"] = 0;
		report.Data["healthBarTargetsPeak"] = 0;
		report.Data["healthBarsDrawn"] = 0;
		report.Data["healthBarSnapshots"] = 0;
		report.Data["healthBarSource"] = "current CLIENT entity HealthState, typed native properties; native current-entity provider fallback only";
		report.Data["healthBarStyle"] = new
		{
			damagedOnly = true,
			range = 35f,
			fadeStartsAt = 22,
			textColor = "white",
			textInside = true,
			blackPanel = false,
			distanceScaled = true
		};
		MapSession.Install(h, report);
		Type type = R.Type("Game", "Game.Character.CharacterSpawnHandlerSystem");
		R.Hook(h, R.Method(type, "InitializeDependency", 8), typeof(DamageNumbers), null, "Capture");
		R.Hook(h, R.Method(type, "OnDestroy", 0), typeof(DamageNumbers), "Remove");
		R.Hook(h, R.Method(R.Type("Game", "Game.DamageSystem.DamageEventReplicationSystem"), "OnUpdate", 0), typeof(DamageNumbers), "AuthorityEvents");
		R.Hook(h, R.Method(R.Type("Game", "Game.DamageSystem.RemoteDamageEventMessageClientHandler"), "OnUpdate", 0), typeof(DamageNumbers), "ClientMessages");
		coreRuntime = R.Type("Frontier.Loot", "Frontier.Loot.Runtime");
		coreCombat = R.Type("Frontier.Loot", "Frontier.Loot.Combat");
		R.Hook(h, R.Method(coreCombat, "BeforeProjectiles", 1), typeof(DamageNumbers), "BeforeExistingProjectiles", "AfterExistingProjectiles");
		Type em = R.Type("Unity.Entities", "Unity.Entities.EntityManager");
		Type companion = R.Type("Core", "Core.HybridEntity.HybridCompanionLink");
		hasCompanion = Generic("HasComponent");
		getCompanion = Generic("GetComponentObject");
		cameraType = R.Type("UnityEngine.CoreModule", "UnityEngine.Camera");
		vector3 = R.Type("UnityEngine.CoreModule", "UnityEngine.Vector3");
		gui = R.Type("UnityEngine.IMGUIModule", "UnityEngine.GUI");
		eventType = R.Type("UnityEngine.IMGUIModule", "UnityEngine.Event");
		rect = R.Type("UnityEngine.CoreModule", "UnityEngine.Rect");
		color = R.Type("UnityEngine.CoreModule", "UnityEngine.Color");
		matrix = R.Type("UnityEngine.CoreModule", "UnityEngine.Matrix4x4");
		screen = R.Type("UnityEngine.CoreModule", "UnityEngine.Screen");
		time = R.Type("UnityEngine.CoreModule", "UnityEngine.Time");
		report.Save(force: true);
		MethodInfo Generic(string name)
		{
			return em.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Single((MethodInfo m) => m.Name == name && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 1 && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.FullName == "Unity.Entities.Entity").MakeGenericMethod(companion);
		}
	}

	private static void Capture(object __instance, object __0)
	{
		try
		{
			object obj = R.Get(__instance, "World");
			if (obj != null && R.Bool(R.Get(obj, "IsCreated")))
			{
				contexts[R.Pointer(obj)] = new Context(__instance, obj, __0, R.Bool(R.Get(__0, "IsAuthoritative")));
			}
		}
		catch (Exception e)
		{
			report.Error("client binding", e);
		}
	}

	private static void Remove(object __instance)
	{
		try
		{
			long p = R.Pointer(__instance);
			long[] array = (from x in contexts
				where R.Pointer(x.Value.System) == p
				select x.Key).ToArray();
			foreach (long key in array)
			{
				contexts.Remove(key);
			}
		}
		catch (Exception e)
		{
			report.Error("world removal", e);
		}
	}

	private static bool Ready()
	{
		if (!MapSession.InRaid)
		{
			return false;
		}
		if (generation != MapSession.Generation)
		{
			generation = MapSession.Generation;
			shots.Clear();
			shotOrder.Clear();
			popups.Clear();
			seen.Clear();
			crossSource.Clear();
			ResetBars();
		}
		return true;
	}

	private static Context? Client()
	{
		return contexts.Values.FirstOrDefault((Context c) => !c.Authority && R.Bool(R.Get(c.World, "IsCreated")) && R.Bool(R.Get(c.Session, "IsBattleSession")));
	}

	private static System.Collections.Generic.IEnumerable<object> Query(object system, string name)
	{
		object obj = R.Get(system, name);
		if (obj == null || R.Int(R.Call(obj, "CalculateEntityCount")) == 0)
		{
			yield break;
		}
		R.Call(system, "CompleteDependency");
		object obj2 = Enum.Parse(R.Method(obj.GetType(), "ToEntityArray", 1).GetParameters()[0].ParameterType, "Temp");
		object array = R.Call(obj, "ToEntityArray", obj2);
		if (array == null)
		{
			yield break;
		}
		try
		{
			foreach (object item in R.Values(array, 16384))
			{
				yield return item;
			}
		}
		finally
		{
			R.Call(array, "Dispose");
		}
	}

	private static NativeComponent RequestMap()
	{
		if (requests != null)
		{
			return requests;
		}
		System.Collections.Generic.List<string> list = new System.Collections.Generic.List<string> { "DataId", "OwnerId", "Tick", "RequestIndex", "DamageData.Damage" };
		list.AddRange(new string[11]
		{
			"_a", "_b", "_c", "_d", "_e", "_f", "_g", "_h", "_i", "_j",
			"_k"
		}.Select((string n) => "SpawnerId.Value." + n));
		requests = new NativeComponent("Game", "Game.ProjectilesSystem.ProjectileSpawnRequest", list.ToArray());
		report.Data["projectileContract"] = requests.Report();
		return requests;
	}

	private static string WeaponGuid(NativeComponent m, byte[] data)
	{
		byte[] array = new byte[16];
		Buffer.BlockCopy(data, m.Offset("SpawnerId.Value._a"), array, 0, 4);
		Buffer.BlockCopy(data, m.Offset("SpawnerId.Value._b"), array, 4, 2);
		Buffer.BlockCopy(data, m.Offset("SpawnerId.Value._c"), array, 6, 2);
		for (int i = 0; i < 8; i++)
		{
			array[8 + i] = m.Byte(data, "SpawnerId.Value._" + (char)(100 + i));
		}
		return new Guid(array).ToString("D");
	}

	private static void BeforeExistingProjectiles(object __0, out System.Collections.Generic.List<BeforeShot> __state)
	{
		__state = new System.Collections.Generic.List<BeforeShot>();
		if (!Ready())
		{
			return;
		}
		try
		{
			if (!R.Bool(R.Call(coreCombat, "Available", "projectiles")))
			{
				return;
			}
			object obj = R.Call(coreRuntime, "Session", __0);
			if (obj == null || !R.Bool(R.Get(obj, "IsBattleSession")))
			{
				return;
			}
			NativeComponent nativeComponent = RequestMap();
			object manager = R.Get(__0, "EntityManager");
			object obj2 = R.Get(__0, "World");
			object obj3 = R.Get(coreCombat, "Prepared");
			Type nestedType = coreCombat.GetNestedType("RequestKey", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			foreach (object item in Query(__0, "ProjectileSpawnSystem_OnUpdate_Query"))
			{
				if (!nativeComponent.Exists(manager, item))
				{
					continue;
				}
				byte[] data = nativeComponent.Read(manager, item);
				int num = nativeComponent.Int(data, "OwnerId");
				if (!R.Bool(R.Call(coreRuntime, "Human", obj, num)))
				{
					continue;
				}
				uint num2 = nativeComponent.UInt(data, "Tick");
				ulong num3 = DamagePolicy.Projectile(num2, num, nativeComponent.Byte(data, "RequestIndex"), nativeComponent.Int(data, "DataId"));
				if (!shots.ContainsKey(num3))
				{
					object obj4 = Activator.CreateInstance(nestedType, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new object[4]
					{
						new IntPtr(R.Pointer(obj2)),
						R.Int(R.Get(item, "Index")),
						R.Int(R.Get(item, "Version")),
						num2
					}, CultureInfo.InvariantCulture);
					if (!R.Bool(R.Call(obj3, "Contains", obj4)))
					{
						object obj5 = R.Call(R.Type("Core.Shared.Utils", "Core.Utils.UniqueId"), "Parse", WeaponGuid(nativeComponent, data));
						object obj6 = R.Call(coreRuntime, "Aggregate", obj, num, obj5);
						__state.Add(new BeforeShot(item, num3, nativeComponent.Float(data, "DamageData.Damage"), 1f + Math.Max(0f, R.Float(R.Get(obj6, "Damage"))), 1.5f + R.Float(R.Get(obj6, "CritPower")), WasPrepared: false));
					}
				}
			}
		}
		catch (Exception e)
		{
			__state.Clear();
			report.Error("shot snapshot", e);
		}
	}

	private static void AfterExistingProjectiles(object __0, System.Collections.Generic.List<BeforeShot> __state)
	{
		if (__state == null || __state.Count == 0)
		{
			return;
		}
		try
		{
			NativeComponent nativeComponent = RequestMap();
			object manager = R.Get(__0, "EntityManager");
			foreach (BeforeShot item in __state)
			{
				if (!nativeComponent.Exists(manager, item.Entity))
				{
					continue;
				}
				byte[] data = nativeComponent.Read(manager, item.Entity);
				bool? flag = DamagePolicy.Critical(item.Damage, nativeComponent.Float(data, "DamageData.Damage"), item.Ordinary, item.Critical);
				if (!flag.HasValue)
				{
					report.Add("uncertainShotModifiers", 1L);
					continue;
				}
				if (!shots.ContainsKey(item.Projectile))
				{
					shotOrder.Enqueue(item.Projectile);
				}
				shots[item.Projectile] = new Shot(flag.Value, R.Now);
				while (shotOrder.Count > 8192)
				{
					shots.Remove(shotOrder.Dequeue());
				}
				report.Add(flag.Value ? "criticalProjectileSnapshots" : "ordinaryProjectileSnapshots", 1L);
			}
		}
		catch (Exception e)
		{
			report.Error("shot confirmation", e);
		}
	}

	private static void AuthorityEvents(object __instance)
	{
		if (!Ready() || collecting)
		{
			return;
		}
		try
		{
			collecting = true;
			Context context = Client();
			if (context == null)
			{
				return;
			}
			int local = R.Int(R.Call(context.Session, "GetLocalPlayerId"));
			object obj = R.Get(__instance, "_battleSession");
			if (obj == null || !R.Bool(R.Get(obj, "IsAuthoritative")) || !R.Bool(R.Get(obj, "IsBattleSession")))
			{
				return;
			}
			if (events == null)
			{
				events = new NativeComponent("Game", "Game.DamageSystem.DamageEvent", "Target.Index", "Target.Version", "Assaulter.Index", "Assaulter.Version", "ProjectileId", "Tick", "BodyPartId", "HitTickApplied", "HitDamageApplied", "HitPosition.x", "HitPosition.y", "HitPosition.z", "IsHiddenForAssaulter", "IsPredicted");
			}
			if (playerIds == null)
			{
				playerIds = new NativeComponent("Game", "Game.Network.PlayerId", "Value");
			}
			object manager = R.Get(__instance, "EntityManager");
			foreach (object item in Query(__instance, "DamageEventReplicationSystem_OnUpdate_Query"))
			{
				if (!events.Exists(manager, item))
				{
					continue;
				}
				byte[] data = events.Read(manager, item);
				object entity = events.Entity(data, "Target");
				object entity2 = events.Entity(data, "Assaulter");
				if (!playerIds.Exists(manager, entity) || !playerIds.Exists(manager, entity2))
				{
					continue;
				}
				int num = playerIds.Int(playerIds.Read(manager, entity), "Value");
				int attacker = playerIds.Int(playerIds.Read(manager, entity2), "Value");
				float damage = events.Float(data, "HitDamageApplied");
				if (DamagePolicy.Accept(local, attacker, num, damage, events.Bool(data, "IsHiddenForAssaulter"), events.Bool(data, "IsPredicted")) && R.Bool(R.Call(obj, "IsBotPlayer", num)))
				{
					uint num2 = events.UInt(data, "HitTickApplied");
					if (num2 != 0)
					{
						Add(context, attacker, num, events.ULong(data, "ProjectileId"), num2, events.Int(data, "BodyPartId"), damage, events.Float(data, "HitPosition.x"), events.Float(data, "HitPosition.y"), events.Float(data, "HitPosition.z"), "authoritative");
					}
				}
			}
		}
		catch (Exception e)
		{
			report.Error("applied damage", e);
		}
		finally
		{
			collecting = false;
			report.Save();
		}
	}

	private static void ClientMessages(object __instance)
	{
		if (!Ready() || collecting)
		{
			return;
		}
		try
		{
			collecting = true;
			Context context = Client();
			if (context == null)
			{
				return;
			}
			int local = R.Int(R.Call(context.Session, "GetLocalPlayerId"));
			if (messages == null)
			{
				messages = new NativeComponent("Game", "Game.DamageSystem.RpcCommands.RemoteDamageEventMessage", "TargetPlayerId", "AssaulterPlayerId", "ProjectileId", "BodyPartId", "Tick", "Damage", "HitPosition.x", "HitPosition.y", "HitPosition.z");
			}
			object manager = R.Get(__instance, "EntityManager");
			foreach (object item in Query(__instance, "RemoteDamageEventMessageClientHandler_OnUpdate_Query"))
			{
				if (!messages.Exists(manager, item))
				{
					continue;
				}
				byte[] data = messages.Read(manager, item);
				int attacker = messages.Short(data, "AssaulterPlayerId");
				int num = messages.Short(data, "TargetPlayerId");
				float damage = messages.Float(data, "Damage");
				if (DamagePolicy.Accept(local, attacker, num, damage, hidden: false, predicted: false))
				{
					object obj = R.Get(R.Call(context.Session, "GetPlayer", num), "AccountId");
					if (obj == null || !R.Bool(R.Get(obj, "HasValue")))
					{
						Add(context, attacker, num, messages.ULong(data, "ProjectileId"), messages.UInt(data, "Tick"), messages.Byte(data, "BodyPartId"), damage, messages.Float(data, "HitPosition.x"), messages.Float(data, "HitPosition.y"), messages.Float(data, "HitPosition.z"), "replicated");
					}
				}
			}
		}
		catch (Exception e)
		{
			report.Error("replicated damage", e);
		}
		finally
		{
			collecting = false;
			report.Save();
		}
	}

	private static void Add(Context client, int attacker, int target, ulong projectile, uint tick, int part, float damage, float x, float y, float z, string source)
	{
		string key = $"{attacker}:{target}:{projectile}:{part}";
		if (projectile != 0L && crossSource.TryGetValue(key, out (string, float, double) value) && value.Item1 != source && R.Now - value.Item3 < 2.0 && Math.Abs(value.Item2 - damage) < 0.01f)
		{
			report.Add("crossSourceDuplicatesSuppressed", 1L);
			return;
		}
		crossSource[key] = (source, damage, R.Now);
		if (crossSource.Count > 4096)
		{
			string[] array = (from keyValuePair in crossSource
				where R.Now - keyValuePair.Value.At > 2.0
				select keyValuePair.Key).ToArray();
			foreach (string key2 in array)
			{
				crossSource.Remove(key2);
			}
		}
		string key3 = $"{attacker}:{target}:{projectile}:{tick}:{part}";
		if (!seen.Add(key3))
		{
			report.Add("duplicatesSuppressed", 1L);
		}
		else
		{
			if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z))
			{
				return;
			}
			Shot value2;
			bool critical = shots.TryGetValue(projectile, out value2) && R.Now - value2.At < 30.0 && value2.Critical;
			if (value2 == null)
			{
				report.Add("hitsWithoutCriticalSnapshot", 1L);
			}
			try
			{
				object obj = R.Call(client.Session, "GetPlayer", target);
				if (obj != null)
				{
					object obj2 = R.Get(obj, "Entity");
					object obj3 = R.Get(client.World, "EntityManager");
					if (R.Bool(R.Call(obj3, "Exists", obj2)) && R.Bool(hasCompanion.Invoke(obj3, new object[1] { obj2 })))
					{
						object obj4 = getCompanion.Invoke(obj3, new object[1] { obj2 });
						object obj5 = ((obj4 == null) ? null : R.Call(obj4, "GetTransform"));
						if (obj5 != null)
						{
							object obj6 = R.Get(obj5, "position");
							x = R.Float(R.Get(obj6, "x"));
							y = Math.Max(y + 0.25f, R.Float(R.Get(obj6, "y")) + 1.9f);
							z = R.Float(R.Get(obj6, "z"));
						}
					}
				}
			}
			catch (Exception e)
			{
				report.Error("hit anchor", e);
				y += 0.5f;
			}
			popups.RemoveAll((Popup p) => R.Now - p.At > 1.05);
			uint shotTick = (uint)projectile;
			Popup popup = popups.LastOrDefault((Popup p) => p.Target == target && p.ShotTick == shotTick && p.Critical == critical && R.Now - p.At < 0.07);
			if (popup != null)
			{
				popup.Damage += damage;
				report.Add("pelletsCombined", 1L);
			}
			else
			{
				int lane = popups.Count((Popup p) => p.Target == target) % 3;
				popups.Add(new Popup
				{
					Target = target,
					ShotTick = shotTick,
					Damage = damage,
					Critical = critical,
					X = x,
					Y = y,
					Z = z,
					At = R.Now,
					Lane = lane
				});
				while (popups.Count > 96)
				{
					popups.RemoveAt(0);
					report.Add("visualCapacityCulls", 1L);
				}
			}
			report.Add("acceptedHits", 1L);
			report.Add(critical ? "criticalHits" : "ordinaryHits", 1L);
			report.Add(source + "Hits", 1L);
			report.Data["status"] = "HITS_RECEIVED";
			report.Data["lastHit"] = new
			{
				attacker = attacker,
				target = target,
				projectile = projectile,
				tick = tick,
				part = part,
				damage = damage,
				critical = critical,
				source = source,
				utc = DateTime.UtcNow.ToString("O")
			};
		}
	}

	private static object Rect(float x, float y, float w, float h)
	{
		return Activator.CreateInstance(rect, x, y, w, h);
	}

	private static object Color(float r, float g, float b, float a = 1f)
	{
		return Activator.CreateInstance(color, r, g, b, a);
	}

	private static void Label(object style, float x, float y, string text, object tint)
	{
		R.Set(gui, "color", tint);
		R.Call(gui, "Label", Rect(x - 110f, y - 48f, 220f, 96f), text, style);
	}

	private static void Bolt(float x, float y, float scale, float alpha)
	{
		for (int i = 0; i < 18; i++)
		{
			float num = ((i < 9) ? (9f - (float)i * 0.6f) : (6f - (float)(i - 9) * 0.55f));
			float num2 = ((i < 9) ? (15f - (float)i * 0.55f) : (11f - (float)(i - 9) * 1.15f));
			if (!(num2 <= num))
			{
				R.Set(gui, "color", Color(1f, 0.75f, 0.08f, alpha));
				R.Call(gui, "DrawTexture", Rect(x + num * scale, y + (float)i * scale, (num2 - num) * scale, scale + 0.1f), white);
			}
		}
	}

	internal static void Draw()
	{
		try
		{
			if (R.Int(R.Get(R.Get(eventType, "current"), "type")) != 7)
			{
				return;
			}
			int num = R.Int(R.Get(time, "frameCount"));
			if (num == lastFrame)
			{
				return;
			}
			lastFrame = num;
			if (!Ready())
			{
				popups.Clear();
				ResetBars();
				return;
			}
			popups.RemoveAll((Popup p) => R.Now - p.At > 1.05);
			if (!MapSession.Allowed(out string _))
			{
				return;
			}
			object obj = SightFrame.ActiveCamera();
			if (obj == null)
			{
				report.Add("missingCameraFrames", 1L);
				return;
			}
			if (white == null)
			{
				white = R.Get(R.Type("UnityEngine.CoreModule", "UnityEngine.Texture2D"), "whiteTexture");
			}
			Type type = R.Type("UnityEngine.IMGUIModule", "UnityEngine.GUIStyle");
			if (normalStyle == null)
			{
				normalStyle = Activator.CreateInstance(type, R.Get(R.Get(gui, "skin"), "label"));
			}
			if (criticalStyle == null)
			{
				criticalStyle = Activator.CreateInstance(type, R.Get(R.Get(gui, "skin"), "label"));
			}
			float num2 = R.Int(R.Get(screen, "height"));
			float num3 = R.Int(R.Get(screen, "width"));
			float num4 = Math.Clamp(num2 / 1080f, 0.75f, 1.5f);
			object[] array = new object[2] { normalStyle, criticalStyle };
			foreach (object obj2 in array)
			{
				R.Set(obj2, "alignment", 4);
				R.Set(obj2, "fontStyle", 1);
				R.Set(obj2, "wordWrap", false);
				R.Set(obj2, "clipping", 1);
				R.Set(R.Get(obj2, "normal"), "textColor", Color(1f, 1f, 1f));
			}
			object value = R.Get(gui, "color");
			object value2 = R.Get(gui, "depth");
			object value3 = R.Get(gui, "matrix");
			object value4 = R.Get(gui, "enabled");
			object value5 = R.Get(gui, "contentColor");
			try
			{
				R.Set(gui, "matrix", R.Get(matrix, "identity"));
				R.Set(gui, "enabled", true);
				R.Set(gui, "depth", -32000);
				R.Set(gui, "contentColor", Color(1f, 1f, 1f));
				DrawEnemyBars(obj, num4, num3, num2);
				foreach (Popup popup in popups)
				{
					object obj3 = R.Call(obj, "WorldToScreenPoint", Activator.CreateInstance(vector3, popup.X, popup.Y, popup.Z));
					if (obj3 == null || R.Float(R.Get(obj3, "z")) <= 0f)
					{
						continue;
					}
					double num6 = R.Now - popup.At;
					float num7 = DamagePolicy.Alpha(num6, 1.05);
					float num8 = R.Float(R.Get(obj3, "x")) + (float)((popup.Lane - 1) * 17) * num4;
					float num9 = num2 - R.Float(R.Get(obj3, "y")) - 25f * num4 - (float)num6 * 42f * num4;
					if (!(num8 < 0f) && !(num8 > num3) && !(num9 < 0f) && !(num9 > num2) && !(num7 <= 0f))
					{
						object? obj4 = (popup.Critical ? criticalStyle : normalStyle);
						int num10 = DamagePolicy.FontSize(popup.Critical, num4);
						if (popup.Critical && num6 < 0.12)
						{
							num10 += (int)((1.0 - num6 / 0.12) * 8.0 * (double)num4);
						}
						R.Set(obj4, "fontSize", num10);
						string text = ((popup.Damage < 1f) ? popup.Damage.ToString("0.0", CultureInfo.InvariantCulture) : Math.Round(popup.Damage, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture));
						Label(obj4, num8 - 1f, num9 - 1f, text, Color(0f, 0f, 0f, num7));
						Label(obj4, num8 + 1f, num9 + 1f, text, Color(0f, 0f, 0f, num7));
						Label(obj4, num8, num9, text, popup.Critical ? Color(1f, 0.7f, 0.12f, num7) : Color(1f, 0.96f, 0.88f, num7));
						if (popup.Critical)
						{
							Bolt(num8 + (float)(text.Length * num10) * 0.29f + 3f, num9 - (float)num10 * 0.32f, 0.9f * num4, num7);
						}
						report.Add("drawnNumbers", 1L);
					}
				}
				report.Data["renderStatus"] = "DRAW_CALLS_COMPLETED_NOT_SCREENSHOT_VERIFIED";
			}
			finally
			{
				R.Set(gui, "color", value);
				R.Set(gui, "depth", value2);
				R.Set(gui, "matrix", value3);
				R.Set(gui, "enabled", value4);
				R.Set(gui, "contentColor", value5);
			}
		}
		catch (Exception e)
		{
			report.Error("number draw", e);
		}
		finally
		{
			report.Save();
		}
	}

	private static void ResetBars()
	{
		bars.Clear();
		nextBars = (lastBars = 0.0);
		barsWorld = (barsSession = 0L);
		headHeights.Clear();
		foreach (HealthBinding value in healthBindings.Values)
		{
			value.Dispose();
		}
		healthBindings.Clear();
		if (report != null)
		{
			report.Data["healthBarTargets"] = 0;
			report.Data["healthBarStatus"] = "OUTSIDE_RAID";
		}
	}

	private static void ValidateHealthLayout()
	{
		if (healthLayoutChecked)
		{
			return;
		}
		healthLayout = new NativeComponent("Game", "Game.DamageSystem.HealthState", "DeathTick", "LastDamageTick", "InternalHealth", "InternalMaxHealth");
		if (healthLayout.Size != Marshal.SizeOf<HealthState>())
		{
			throw new InvalidOperationException("HealthState native/managed size mismatch");
		}
		string[] array = new string[4] { "DeathTick", "LastDamageTick", "InternalHealth", "InternalMaxHealth" };
		foreach (string text in array)
		{
			if (healthLayout.Offset(text) != Marshal.OffsetOf<HealthState>(text).ToInt32())
			{
				throw new InvalidOperationException("HealthState field offset mismatch: " + text);
			}
		}
		report.Data["healthStateContract"] = healthLayout.Report();
		healthLayoutChecked = true;
	}

	private static bool ReadHealth(BattleSession session, EntityManager em, BattleSession.Player p, out float hp, out float max)
	{
		hp = (max = 0f);
		bool flag = em.HasComponent<HealthState>(p.Entity);
		float num = float.NaN;
		float num2 = float.NaN;
		if (flag)
		{
			ValidateHealthLayout();
			HealthState componentData = em.GetComponentData<HealthState>(p.Entity);
			num = componentData.Health;
			num2 = componentData.MaxHealth;
			report.Add("healthEntityReads", 1L);
			if (HealthBarPolicy.Valid(num, num2))
			{
				hp = num;
				max = num2;
				report.Add("healthEntityValuesValid", 1L);
				return true;
			}
			report.Add("healthEntityValuesIncomplete", 1L);
			if (float.IsFinite(num) && num == 0f)
			{
				report.Add("healthEntityDeadSkipped", 1L);
				return false;
			}
		}
		else
		{
			report.Add("healthEntityComponentMissing", 1L);
		}
		float num3 = float.NaN;
		float num4 = float.NaN;
		bool providerAvailable = false;
		if (session.ItemsManager != null)
		{
			(int, int) key = (p.Entity.Index, p.Entity.Version);
			if (!healthBindings.TryGetValue(key, out HealthBinding value))
			{
				if (healthBindings.Count >= 64)
				{
					report.Add("healthProviderCapacitySkipped", 1L);
					return false;
				}
				value = new HealthBinding(session, em, p.Entity);
				healthBindings.Add(key, value);
				report.Add("healthCurrentEntityProvidersCreated", 1L);
			}
			value.Seen = R.Now;
			num3 = value.Provider.GetHealth();
			num4 = value.Provider.GetMaxHealth();
			providerAvailable = true;
			report.Add("healthCurrentEntityProviderReads", 1L);
			if (!float.IsFinite(num4) || num4 <= 0f)
			{
				Il2CppSystem.Collections.Generic.List<int> bodyPartIdsDontUseForEach = value.Provider.GetBodyPartIdsDontUseForEach();
				Il2CppSystem.Collections.Generic.Dictionary<int, UniqueId> bodyPartsItemsIds = value.Provider.GetBodyPartsItemsIds();
				if (bodyPartIdsDontUseForEach != null && bodyPartsItemsIds != null && bodyPartIdsDontUseForEach.Count > 0 && bodyPartIdsDontUseForEach.Count <= 32 && bodyPartsItemsIds.Count == bodyPartIdsDontUseForEach.Count)
				{
					bool flag2 = true;
					HashSet<int> hashSet = new HashSet<int>();
					System.Collections.Generic.List<float> list = new System.Collections.Generic.List<float>();
					for (int i = 0; i < bodyPartIdsDontUseForEach.Count; i++)
					{
						int num5 = bodyPartIdsDontUseForEach[i];
						if (!hashSet.Add(num5) || !bodyPartsItemsIds.ContainsKey(num5) || !value.Provider.TryGetBodyPartState(num5, out var state))
						{
							flag2 = false;
							break;
						}
						list.Add(state.MaxHealth);
					}
					if (flag2)
					{
						float num6 = HealthBarPolicy.PartMaximum(list.ToArray());
						if (float.IsFinite(num6) && num6 > 0f && (!flag || !float.IsFinite(num) || num <= num6 + 0.1f))
						{
							num4 = num6;
							report.Add("healthMaximumFromCompleteNativeParts", 1L);
						}
					}
				}
			}
		}
		bool fromEntity;
		bool num7 = HealthBarPolicy.Choose(flag, num, num2, providerAvailable, num3, num4, out hp, out max, out fromEntity);
		if (!num7)
		{
			report.Add("healthUnavailableSkipped", 1L);
			report.Data["lastUnavailableHealth"] = new
			{
				enemy = p.Id,
				entity = p.Entity.ToString(),
				entityComponent = flag,
				entityHP = Finite(num),
				entityMaximum = Finite(num2),
				providerHP = Finite(num3),
				providerMaximum = Finite(num4)
			};
			return num7;
		}
		report.Add("healthProviderValuesValid", 1L);
		return num7;
	}

	private static float? Finite(float v)
	{
		if (!float.IsFinite(v))
		{
			return null;
		}
		return v;
	}

	private static float HeadHeight(Entity entity, Transform root)
	{
		(int, int) key = (entity.Index, entity.Version);
		if (headHeights.TryGetValue(key, out (float, double) value) && R.Now < value.Item2)
		{
			return value.Item1;
		}
		float num = 0f;
		try
		{
			Il2CppArrayBase<Renderer> componentsInChildren = root.GetComponentsInChildren<Renderer>(includeInactive: false);
			if (componentsInChildren != null && componentsInChildren.Length <= 128)
			{
				for (int i = 0; i < componentsInChildren.Length; i++)
				{
					Renderer renderer = componentsInChildren[i];
					if (!(renderer == null) && renderer.enabled && !(((Il2CppObjectBase)renderer).TryCast<SkinnedMeshRenderer>() == null))
					{
						Bounds bounds = renderer.bounds;
						float num2 = bounds.max.y - root.position.y;
						if (bounds.size.y > 0.25f && bounds.size.y < 4f && num2 > 0.65f && num2 < 3.5f)
						{
							num = Math.Max(num, num2);
						}
					}
				}
			}
		}
		catch (Exception e)
		{
			report.Error("health bar anchor", e);
		}
		float num3 = ((num > 0f) ? (num + 0.15f) : 1.95f);
		headHeights[key] = (num3, R.Now + 2.0);
		return num3;
	}

	private static void ReadBars(Camera camera)
	{
		if (R.Now < nextBars)
		{
			return;
		}
		nextBars = R.Now + 0.1;
		bars.Clear();
		lastBars = R.Now;
		report.Add("healthBarSnapshots", 1L);
		report.Data["healthBarTargets"] = 0;
		Context context = Client();
		if (context == null)
		{
			report.Data["healthBarStatus"] = "NO_CURRENT_CLIENT_WORLD";
			return;
		}
		object session = context.Session;
		object obj = ((session is Il2CppObjectBase) ? session : null);
		BattleSession battleSession = ((obj != null) ? ((Il2CppObjectBase)obj).TryCast<BattleSession>() : null);
		object world = context.World;
		object obj2 = ((world is Il2CppObjectBase) ? world : null);
		World world2 = ((obj2 != null) ? ((Il2CppObjectBase)obj2).TryCast<World>() : null);
		if (battleSession == null || world2 == null || !world2.IsCreated || battleSession.IsAuthoritative)
		{
			report.Data["healthBarStatus"] = "NO_CURRENT_CLIENT_SESSION";
			return;
		}
		long num = ((Il2CppObjectBase)world2).Pointer.ToInt64();
		long num2 = ((Il2CppObjectBase)battleSession).Pointer.ToInt64();
		if (num != barsWorld || num2 != barsSession)
		{
			ResetBars();
			barsWorld = num;
			barsSession = num2;
			nextBars = R.Now + 0.1;
			lastBars = R.Now;
		}
		EntityManager em = world2.EntityManager;
		int localPlayerId = battleSession.GetLocalPlayerId();
		BattleSession.Player player = battleSession.GetPlayer(localPlayerId);
		if (player == null || player.IsDead || !player.EntityHasBeenSpawned || !em.Exists(player.Entity))
		{
			report.Data["healthBarStatus"] = "WAITING_FOR_LOCAL_PLAYER";
			return;
		}
		SightFrame sightFrame = SightFrame.Create(world2, player.Entity, camera, report);
		if (sightFrame == null)
		{
			report.Data["healthBarStatus"] = "WAITING_FOR_VISIBILITY";
			return;
		}
		if (barIds == null)
		{
			barIds = new Il2CppSystem.Collections.Generic.List<int>();
		}
		barIds.Clear();
		battleSession.GetPlayerIds(barIds);
		if (barIds.Count > 2048)
		{
			report.Data["healthBarStatus"] = "PLAYER_LIMIT";
			return;
		}
		System.Collections.Generic.List<(BattleSession.Player, Transform, Vector3, float)> list = new System.Collections.Generic.List<(BattleSession.Player, Transform, Vector3, float)>();
		for (int i = 0; i < barIds.Count; i++)
		{
			BattleSession.Player player2 = battleSession.GetPlayer(barIds[i]);
			if (player2 == null || player2.Id == localPlayerId || player2.IsDead || !player2.EntityHasBeenSpawned)
			{
				continue;
			}
			if (player2.AccountId.HasValue)
			{
				report.Add("healthHumanPlayersExcluded", 1L);
			}
			else
			{
				if (!em.Exists(player2.Entity) || !em.HasComponent<HybridCompanionLink>(player2.Entity))
				{
					continue;
				}
				Transform transform = em.GetComponentObject<HybridCompanionLink>(player2.Entity)?.GetTransform();
				if (!(transform == null) && transform.gameObject.activeInHierarchy)
				{
					Vector3 position = transform.position;
					float num3 = Vector3.Distance(camera.transform.position, position);
					if (float.IsFinite(num3) && num3 < 35f)
					{
						list.Add((player2, transform, position, num3));
					}
				}
			}
		}
		report.Data["healthBarCandidatesLast"] = list.Count;
		foreach (var item in list.OrderBy<(BattleSession.Player, Transform, Vector3, float), float>(((BattleSession.Player Player, Transform Transform, Vector3 Pos, float Distance) q) => q.Distance))
		{
			if (bars.Count >= 32)
			{
				break;
			}
			if (!sightFrame.Visible(item.Item1.Entity, item.Item3.x, item.Item3.y, item.Item3.z))
			{
				continue;
			}
			report.Add("healthVisibleCandidates", 1L);
			try
			{
				if (item.Item1.CharacterHealth == null)
				{
					report.Add("healthPlayerProviderAbsent", 1L);
				}
				if (ReadHealth(battleSession, em, item.Item1, out var hp, out var max))
				{
					if (!HealthBarPolicy.Show(!item.Item1.IsDead, ally: false, visible: true, hp, max, item.Item4))
					{
						report.Add((HealthBarPolicy.Valid(hp, max) && hp >= max - 0.01f) ? "healthFullTargetsHidden" : "healthInvalidOrDeadSkipped", 1L);
					}
					else
					{
						bars.Add(new Bar(item.Item1.Id, item.Item1.Entity, item.Item3, HeadHeight(item.Item1.Entity, item.Item2), hp, max, RankColors.Read(item.Item1, report).Rank, R.Now));
					}
				}
			}
			catch (Exception e)
			{
				report.Add("healthReadFailures", 1L);
				report.Error("enemy health read", e);
			}
		}
		(int, int)[] array = (from x in healthBindings
			where R.Now - x.Value.Seen > 3.0 || !em.Exists(new Entity
			{
				Index = x.Key.Index,
				Version = x.Key.Version
			})
			select x.Key).ToArray();
		foreach ((int, int) key in array)
		{
			healthBindings[key].Dispose();
			healthBindings.Remove(key);
		}
		if (headHeights.Count > 128)
		{
			headHeights.Clear();
		}
		report.Data["healthBarTargets"] = bars.Count;
		report.Data["healthBarStatus"] = ((bars.Count > 0) ? "LIVE_HEALTH_TARGETS_READY" : "NO_VALID_VISIBLE_HEALTH");
		report.Data["healthBarTargetsPeak"] = Math.Max(Convert.ToInt32(report.Data["healthBarTargetsPeak"]), bars.Count);
		if (bars.Count > 0)
		{
			report.Data["lastHealthBar"] = new
			{
				enemy = bars[0].Id,
				entity = bars[0].Entity.ToString(),
				current = bars[0].HP,
				maximum = bars[0].Maximum,
				utc = DateTime.UtcNow.ToString("O")
			};
			report.Add("healthBarSnapshotsWithTargets", 1L);
		}
	}

	private static void DrawEnemyBars(object camera, float scale, float sw, float sh)
	{
		try
		{
			object obj = ((camera is Il2CppObjectBase) ? camera : null);
			Camera camera2 = ((obj != null) ? ((Il2CppObjectBase)obj).TryCast<Camera>() : null);
			if (camera2 == null)
			{
				return;
			}
			ReadBars(camera2);
			if (!HealthBarPolicy.Fresh(R.Now, lastBars))
			{
				return;
			}
			object obj2 = Client()?.World;
			object obj3 = ((obj2 is Il2CppObjectBase) ? obj2 : null);
			World world = ((obj3 != null) ? ((Il2CppObjectBase)obj3).TryCast<World>() : null);
			if (world == null || !world.IsCreated)
			{
				return;
			}
			EntityManager entityManager = world.EntityManager;
			if (barStyle == null)
			{
				barStyle = new GUIStyle(GUI.skin.label);
			}
			barStyle.alignment = TextAnchor.MiddleCenter;
			barStyle.wordWrap = false;
			barStyle.clipping = TextClipping.Clip;
			barStyle.padding = new RectOffset(0, 0, 0, 0);
			barStyle.margin = new RectOffset(0, 0, 0, 0);
			barStyle.normal.background = null;
			foreach (Bar bar in bars)
			{
				if (!HealthBarPolicy.Fresh(R.Now, bar.Observed) || !entityManager.Exists(bar.Entity) || !entityManager.HasComponent<HybridCompanionLink>(bar.Entity))
				{
					continue;
				}
				Transform transform = entityManager.GetComponentObject<HybridCompanionLink>(bar.Entity)?.GetTransform();
				if (transform == null)
				{
					continue;
				}
				float distance = Vector3.Distance(camera2.transform.position, transform.position);
				if (!HealthBarPolicy.Show(alive: true, ally: false, visible: true, bar.HP, bar.Maximum, distance))
				{
					continue;
				}
				Vector3 vector = camera2.WorldToViewportPoint(transform.position + new Vector3(0f, bar.Height, 0f));
				if (SightPolicy.InViewport(vector.x, vector.y, vector.z))
				{
					float num = HealthBarPolicy.DistanceScale(distance);
					float num2 = HealthBarPolicy.Opacity(distance);
					float num3 = vector.x * sw;
					float num4 = (1f - vector.y) * sh;
					float num5 = 82f * scale * num;
					float num6 = 14f * scale * num;
					Rect position = new Rect(num3 - num5 / 2f, num4 - num6 / 2f, num5, num6);
					string text = HealthBarPolicy.Number(bar.HP) + " / " + HealthBarPolicy.Number(bar.Maximum);
					GUI.color = new Color(0.35f, 0.035f, 0.025f, 0.65f * num2);
					GUI.DrawTexture(position, Texture2D.whiteTexture);
					GUI.color = new Color(0.83f, 0.1f, 0.065f, 0.92f * num2);
					GUI.DrawTexture(new Rect(position.x, position.y, num5 * HealthBarPolicy.Fill(bar.HP, bar.Maximum), num6), Texture2D.whiteTexture);
					GUI.color = new Color(1f, 1f, 1f, num2);
					barStyle.normal.textColor = UnityEngine.Color.white;
					barStyle.fontSize = Math.Max(7, (int)(11f * scale * num));
					GUI.Label(position, text, barStyle);
					report.Add("healthBarsDrawn", 1L);
					if (R.Now >= nextBarDrawReport)
					{
						nextBarDrawReport = R.Now + 0.5;
						report.Data["lastHealthBarDraw"] = new
						{
							enemy = bar.Id,
							hp = bar.HP,
							maximum = bar.Maximum,
							x = num3,
							y = num4,
							width = num5,
							height = num6,
							distance = distance,
							alpha = num2,
							damagedOnly = true,
							blackPanel = false,
							textInside = true,
							utc = DateTime.UtcNow.ToString("O")
						};
					}
				}
			}
		}
		catch (Exception e)
		{
			bars.Clear();
			report.Error("health bars", e);
		}
	}
}
