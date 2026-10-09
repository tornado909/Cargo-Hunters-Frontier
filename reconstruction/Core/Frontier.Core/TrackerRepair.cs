using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Frontier.Engine;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppSystem;
using UnityEngine;

namespace Frontier.Core;

internal static class TrackerRepair
{
	private const BindingFlags Flags = (BindingFlags)60;

	private static readonly Dictionary<long, object[]> Contexts = new Dictionary<long, object[]>();

	private static readonly HashSet<string> Notes = new HashSet<string>();

	private static System.Type worldType;

	private static System.Type spawnType;

	private static MethodInfo hasCompanion;

	private static MethodInfo getCompanion;

	private static MethodInfo existingSystem;

	private static Type nativeSpawnType;

	private static float discoverAt;

	private static long captureCalls;

	private static long captures;

	private static long discoveryScans;

	private static long scanCalls;

	private static long unflaggedRankMatches;

	private static int inspected;

	private static int alive;

	private static int ranked;

	private static int missingEntity;

	private static int missingCompanion;

	private static int inactiveRoot;

	private static int targets;

	private static int peakTargets;

	private static string lastError = "";

	private static string gate = "NOT_STARTED";

	private static string lastMatchUtc = "";

	private static bool installed;

	internal static void Install(Harmony harmony)
	{
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Expected O, but got Unknown
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e0: Expected O, but got Unknown
		//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
		string directory = Path.Combine(Paths.BepInExRootPath, "interop");
		Reflect.LoadAssembly("Unity.Entities", directory);
		Reflect.LoadAssembly("Core", directory);
		worldType = Reflect.FindType("Unity.Entities.World");
		spawnType = Reflect.FindType("Game.Character.CharacterSpawnHandlerSystem");
		System.Type component = Reflect.FindType("Core.HybridEntity.HybridCompanionLink");
		System.Type manager = Reflect.FindType("Unity.Entities.EntityManager");
		hasCompanion = ComponentMethod(manager, "HasComponent", component);
		getCompanion = ComponentMethod(manager, "GetComponentObject", component);
		MethodInfo val = Reflect.UniqueMethod(spawnType, "InitializeDependency", 8);
		if (val.ReturnType != typeof(void) || ((MethodBase)val).GetParameters()[0].ParameterType.FullName != "Game.GameModes.BattleSession")
		{
			throw new InvalidOperationException("Unexpected client initialization contract");
		}
		ParameterInfo[] parameters = ((MethodBase)val).GetParameters();
		foreach (ParameterInfo val2 in parameters)
		{
			if (val2.ParameterType.IsByRef || val2.ParameterType.IsPointer || val2.ParameterType.IsValueType)
			{
				throw new InvalidOperationException("Only reference-only initialization observer is allowed");
			}
		}
		harmony.Patch((MethodBase)(object)val, (HarmonyMethod)null, new HarmonyMethod(typeof(TrackerRepair).GetMethod("Capture", (BindingFlags)60)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		MethodInfo val3 = null;
		MethodInfo[] methods = spawnType.GetMethods((BindingFlags)60);
		foreach (MethodInfo val4 in methods)
		{
			if (((MemberInfo)val4).DeclaringType == spawnType && ((MemberInfo)val4).Name == "OnDestroy" && ((MethodBase)val4).GetParameters().Length == 0)
			{
				val3 = val4;
			}
		}
		if (val3 == (MethodInfo)null)
		{
			throw new MissingMethodException("CharacterSpawnHandlerSystem.OnDestroy");
		}
		harmony.Patch((MethodBase)(object)val3, new HarmonyMethod(typeof(TrackerRepair).GetMethod("Remove", (BindingFlags)60)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		methods = worldType.GetMethods((BindingFlags)60);
		foreach (MethodInfo val5 in methods)
		{
			if (((MemberInfo)val5).Name == "GetExistingSystem" && !((MethodBase)val5).IsGenericMethod && ((MethodBase)val5).GetParameters().Length == 1 && ((MethodBase)val5).GetParameters()[0].ParameterType.FullName == "Il2CppSystem.Type")
			{
				existingSystem = val5;
			}
		}
		if (existingSystem != (MethodInfo)null)
		{
			nativeSpawnType = Il2CppType.From(spawnType);
		}
		installed = true;
		gate = "WAITING_FOR_CLIENT_WORLD";
	}

	private static MethodInfo ComponentMethod(System.Type manager, string name, System.Type component)
	{
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		MethodInfo[] methods = manager.GetMethods((BindingFlags)60);
		foreach (MethodInfo val in methods)
		{
			if (((MemberInfo)val).Name == name && ((MethodBase)val).IsGenericMethodDefinition && ((MethodBase)val).GetGenericArguments().Length == 1 && ((MethodBase)val).GetParameters().Length == 1 && ((MethodBase)val).GetParameters()[0].ParameterType.FullName == "Unity.Entities.Entity")
			{
				return val.MakeGenericMethod(new System.Type[1] { component });
			}
		}
		throw new MissingMethodException(manager.FullName, name);
	}

	private static void Capture(object __instance, object __0)
	{
		captureCalls++;
		try
		{
			Register(__instance, Reflect.Read(__instance, "World"), __0);
		}
		catch (System.Exception ex)
		{
			Warn("capture", ex);
		}
	}

	private static void Register(object system, object world, object session)
	{
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		if (world == null || session == null)
		{
			return;
		}
		object obj = Reflect.Read(world, "IsCreated");
		if (!(obj is bool) || !(bool)obj)
		{
			return;
		}
		obj = Reflect.Read(session, "IsAuthoritative");
		if (!(obj is bool) || (bool)obj)
		{
			return;
		}
		string text = Convert.ToString(Reflect.Read(world, "Name")) ?? "";
		if (!text.StartsWith("ClientWorld", (StringComparison)4))
		{
			return;
		}
		long num = ((Il2CppObjectBase)world).Pointer.ToInt64();
		object[] array = default(object[]);
		if (!Contexts.TryGetValue(num, ref array) || !Reflect.SameNative(array[2], session))
		{
			object obj2 = Activator.CreateInstance(((MethodBase)Reflect.UniqueMethod(session.GetType(), "GetPlayerIds", 1)).GetParameters()[0].ParameterType);
			if (obj2 == null)
			{
				throw new InvalidOperationException("Player-ID collection unavailable");
			}
			Contexts[num] = new object[4] { system, world, session, obj2 };
			captures++;
			gate = "CLIENT_WORLD_REGISTERED";
			((BasePlugin)Plugin.Current).Log.LogInfo((object)("Elite discovery v3.1: client world registered: " + text));
		}
	}

	private static void Remove(object __instance)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		Enumerator<long> enumerator = new List<long>((System.Collections.Generic.IEnumerable<long>)Contexts.Keys).GetEnumerator();
		try
		{
			while (enumerator.MoveNext())
			{
				long current = enumerator.Current;
				if (Reflect.SameNative(Contexts[current][0], __instance))
				{
					Contexts.Remove(current);
				}
			}
		}
		finally
		{
			((System.IDisposable)enumerator/*cast due to .constrained prefix*/).Dispose();
		}
		if (Contexts.Count == 0)
		{
			gate = "WAITING_FOR_CLIENT_WORLD";
		}
	}

	private static void Discover(float now)
	{
		if (now < discoverAt || existingSystem == (MethodInfo)null)
		{
			return;
		}
		discoverAt = now + 1f;
		discoveryScans++;
		try
		{
			object value = worldType.GetProperty("All", (BindingFlags)60).GetValue((object)null);
			if (value == null || Reflect.Count(value) > 64)
			{
				return;
			}
			System.Collections.Generic.IEnumerator<object> enumerator = Reflect.Values(value).GetEnumerator();
			try
			{
				while (((System.Collections.IEnumerator)enumerator).MoveNext())
				{
					object current = enumerator.Current;
					if (current == null)
					{
						continue;
					}
					object obj = Reflect.Read(current, "IsCreated");
					if (!(obj is bool) || !(bool)obj || !(Convert.ToString(Reflect.Read(current, "Name")) ?? "").StartsWith("ClientWorld", (StringComparison)4))
					{
						continue;
					}
					object obj2 = ((MethodBase)existingSystem).Invoke(current, new object[1] { nativeSpawnType });
					if (obj2 != null)
					{
						object obj3 = Reflect.AsConcrete(obj2, spawnType);
						object obj4 = Reflect.Read(obj3, "_battleSession");
						if (obj4 != null)
						{
							Register(obj3, current, obj4);
						}
					}
				}
			}
			finally
			{
				((System.IDisposable)enumerator)?.Dispose();
			}
		}
		catch (System.Exception ex)
		{
			Warn("world discovery", ex);
		}
	}

	internal static List<EliteClientTracker.Target> Collect()
	{
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		List<EliteClientTracker.Target> val = new List<EliteClientTracker.Target>();
		if (!installed || !Plugin.Current.Active)
		{
			return val;
		}
		Discover(Time.unscaledTime);
		scanCalls++;
		inspected = (alive = (ranked = (missingEntity = (missingCompanion = (inactiveRoot = (targets = 0))))));
		HashSet<string> val2 = new HashSet<string>((IEqualityComparer<string>)(object)StringComparer.Ordinal);
		Enumerator<long> enumerator = new List<long>((System.Collections.Generic.IEnumerable<long>)Contexts.Keys).GetEnumerator();
		try
		{
			while (enumerator.MoveNext())
			{
				long current = enumerator.Current;
				object[] array = Contexts[current];
				try
				{
					object obj = Reflect.Read(array[1], "IsCreated");
					if (!(obj is bool) || !(bool)obj)
					{
						Contexts.Remove(current);
						continue;
					}
					obj = Reflect.Read(array[2], "IsAuthoritative");
					if (!(obj is bool) || (bool)obj)
					{
						continue;
					}
					obj = Reflect.Read(array[2], "IsBattleSession");
					if (!(obj is bool) || !(bool)obj)
					{
						continue;
					}
					Reflect.Call(array[3], "Clear");
					Reflect.Call(array[2], "GetPlayerIds", array[3]);
					if (Reflect.Count(array[3]) > 2048)
					{
						throw new InvalidOperationException("Player-ID count exceeds bounded scan");
					}
					object obj2 = Reflect.Read(array[1], "EntityManager");
					int num = Convert.ToInt32(Reflect.Call(array[2], "GetLocalPlayerId"));
					System.Collections.Generic.IEnumerator<object> enumerator2 = Reflect.Values(array[3]).GetEnumerator();
					try
					{
						while (((System.Collections.IEnumerator)enumerator2).MoveNext())
						{
							int num2 = Convert.ToInt32(enumerator2.Current);
							inspected++;
							if (num2 == num)
							{
								continue;
							}
							try
							{
								object obj3 = Reflect.Call(array[2], "GetPlayer", num2);
								if (obj3 == null || !EliteClientTracker.Alive(obj3))
								{
									continue;
								}
								alive++;
								object obj4 = Reflect.Read(obj3, "Equipment");
								if (obj4 == null)
								{
									continue;
								}
								string text = EliteVisualProtocol.EquipmentId(Reflect.Id(obj4));
								ElitePresentation.RankStyle rankStyle = ElitePresentation.FindRank(text);
								if (rankStyle == null || val2.Contains(text))
								{
									continue;
								}
								ranked++;
								obj = Reflect.Read(obj3, "IsBot");
								if (!(obj is bool) || !(bool)obj)
								{
									unflaggedRankMatches++;
								}
								object obj5 = Reflect.Read(obj3, "Entity");
								obj = Reflect.Call(obj2, "Exists", obj5);
								if (!(obj is bool) || !(bool)obj)
								{
									missingEntity++;
									continue;
								}
								obj = ((MethodBase)hasCompanion).Invoke(obj2, new object[1] { obj5 });
								if (!(obj is bool) || !(bool)obj)
								{
									missingCompanion++;
									continue;
								}
								object obj6 = ((MethodBase)getCompanion).Invoke(obj2, new object[1] { obj5 });
								GameObject val3 = (GameObject)((obj6 == null) ? null : /*isinst with value type is only supported in some contexts*/);
								if ((Object)(object)val3 == (Object)null || !val3.activeInHierarchy)
								{
									inactiveRoot++;
									continue;
								}
								val2.Add(text);
								val.Add(new EliteClientTracker.Target(text, val3, obj3, rankStyle));
								if (Notes.Count < 24 && Notes.Add(text))
								{
									((BasePlugin)Plugin.Current).Log.LogInfo((object)string.Concat(new string[6]
									{
										"Elite discovery v3.1: rank-bound client actor ",
										rankStyle.Name,
										" equipment=",
										text,
										" player=",
										num2.ToString()
									}));
								}
							}
							catch (System.Exception ex)
							{
								Warn("player resolution", ex);
							}
						}
					}
					finally
					{
						((System.IDisposable)enumerator2)?.Dispose();
					}
				}
				catch (System.Exception ex2)
				{
					Warn("client scan", ex2);
				}
			}
		}
		finally
		{
			((System.IDisposable)enumerator/*cast due to .constrained prefix*/).Dispose();
		}
		targets = val.Count;
		if (targets > peakTargets)
		{
			peakTargets = targets;
		}
		if (targets > 0)
		{
			lastMatchUtc = System.DateTime.UtcNow.ToString("O");
		}
		gate = ((targets > 0) ? "CLIENT_TARGETS_RESOLVED" : ((Contexts.Count == 0) ? "WAITING_FOR_CLIENT_WORLD" : ((inspected == 0) ? "NO_SESSION_PLAYERS" : ((alive == 0) ? "NO_ALIVE_REMOTE_ACTORS" : ((ranked == 0) ? "NO_MATCHING_RANKS" : ((missingEntity > 0) ? "WAITING_FOR_ENTITY" : ((missingCompanion > 0) ? "WAITING_FOR_COMPANION" : "WAITING_FOR_ACTIVE_MODEL")))))));
		return val;
	}

	internal static object DecorateSnapshot(object original)
	{
		Dictionary<string, object> val = new Dictionary<string, object>();
		if (original != null)
		{
			PropertyInfo[] properties = original.GetType().GetProperties();
			foreach (PropertyInfo val2 in properties)
			{
				val[((MemberInfo)val2).Name] = val2.GetValue(original);
			}
		}
		val["discovery"] = Diagnostics();
		return val;
	}

	private static object Diagnostics()
	{
		return new Dictionary<string, object>
		{
			["bindingPolicy"] = "server equipment rank; local actor excluded; client IsBot not required",
			["status"] = gate,
			["captureCalls"] = captureCalls,
			["clientWorldsRegisteredTotal"] = captures,
			["activeClientWorlds"] = Contexts.Count,
			["worldDiscoveryScans"] = discoveryScans,
			["actorScans"] = scanCalls,
			["playersInspectedLastScan"] = inspected,
			["aliveActorsLastScan"] = alive,
			["rankMatchesLastScan"] = ranked,
			["unflaggedClientRankMatchesTotal"] = unflaggedRankMatches,
			["missingEntitiesLastScan"] = missingEntity,
			["missingCompanionsLastScan"] = missingCompanion,
			["inactiveModelsLastScan"] = inactiveRoot,
			["targetsLastScan"] = targets,
			["peakResolvedTargets"] = peakTargets,
			["lastTargetResolvedUtc"] = lastMatchUtc,
			["lastError"] = lastError
		};
	}

	private static void Warn(string stage, System.Exception ex)
	{
		lastError = stage + ": " + ex.GetBaseException().Message;
		ElitePresentation.Warn("Discovery " + stage, ex);
	}
}
