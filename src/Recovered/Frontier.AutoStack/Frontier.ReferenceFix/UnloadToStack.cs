using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Frontier.ReferenceFix;

internal static class UnloadToStack
{
	internal sealed record Proof(object Processor, object Context, object Equipment, object MagazineId, Dictionary<string, long> Loaded, Dictionary<string, long> Loose, double Expires);

	private const string Continuous = "Game.ItemsSystem.ItemOperations.UnloadCartridgesContinuousOperation";

	private static Type request = null;

	private static Type component = null;

	private static Type firearm = null;

	private static Type slot = null;

	private static Type itemType = null;

	private static MethodInfo getMagazine = null;

	private static MethodInfo getFirearm = null;

	private static MethodInfo canStack = null;

	private static Evidence report = null;

	private static readonly Dictionary<long, Proof> Pending = new Dictionary<long, Proof>();

	internal static void Install(Evidence e)
	{
		report = e;
		request = R.Type("Game.Shared", "Game.ItemsSystem.ItemOperations.UnloadCartridgesRequest");
		component = R.Type("Game.Shared", "Game.WeaponSystem.ItemComponents.MagazineComponent");
		firearm = R.Type("Game.Shared", "Game.WeaponSystem.ItemComponents.FirearmComponent");
		slot = R.Type("Game.Shared", "Game.WeaponSystem.ItemComponents.WeaponSlotType");
		itemType = R.Type("Game.Shared", "Game.ItemsSystem.IItem");
		MethodInfo methodInfo = itemType.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Single((MethodInfo m) => m.Name == "GetComponent" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
		getMagazine = methodInfo.MakeGenericMethod(component);
		getFirearm = methodInfo.MakeGenericMethod(firearm);
		canStack = R.Method(R.Type("Game.Shared", "Game.ItemsSystem.ItemOperations.StackItemsOperation"), "CanBeStacked", 2);
		report.Data["raidUnloadPolicy"] = "Use native stack-aware UnloadCartridgesRequest when all loaded templates fit compatible stacks in live local equipment; otherwise leave continuous operation native";
		report.Data["quantityWrites"] = 0;
		report.Data["magazineWrites"] = 0;
		report.Data["canExecuteResultOverridden"] = false;
	}

	internal static bool HasPending(long key)
	{
		return Pending.ContainsKey(key);
	}

	internal static void Forget(long key)
	{
		Pending.Remove(key);
	}

	private static object? CurrentSession(object context)
	{
		object obj = R.Get(context, "_dataProvider");
		if (obj == null)
		{
			return null;
		}
		try
		{
			object obj2 = R.ConvertFor(obj, R.Type("Game", "Game.GameModes.BattleSession"));
			if (obj2 == null || !UnloadPolicy.CurrentSessionMatches(R.Bool(R.Get(obj2, "IsAuthoritative")), R.Bool(R.Get(obj2, "IsBattleSession")), R.Pointer(R.Get(obj2, "ItemsManager")), R.Pointer(R.Get(context, "ItemsManager"))))
			{
				return null;
			}
			return obj2;
		}
		catch (InvalidCastException)
		{
			return null;
		}
	}

	internal static object? LiveEquipment(object processor, object context)
	{
		object obj = CurrentSession(context);
		if (obj != null)
		{
			int num = R.Int(R.Call(obj, "GetLocalPlayerId"));
			object obj2 = R.Call(obj, "GetPlayer", num);
			if (obj2 != null && !R.Bool(R.Get(obj2, "IsDead")) && R.Bool(R.Get(obj2, "EntityHasBeenSpawned")))
			{
				object obj3 = R.Call(obj, "GetPlayerEquipment", num);
				if (obj3 != null)
				{
					report.Data["liveEquipmentSource"] = "current client BattleSession.GetLocalPlayerId/GetPlayerEquipment";
					report.Data["liveLocalPlayerId"] = num;
					return obj3;
				}
			}
		}
		object obj4 = R.Get(processor, "_localConnectionId");
		if (obj4 == null)
		{
			return null;
		}
		object obj5 = R.Call(context, "FindPlayerIdByConnectionId", obj4);
		if (obj5 == null || !R.Bool(R.Get(obj5, "HasValue")))
		{
			return null;
		}
		report.Data["liveEquipmentSource"] = "native connection->player mapping";
		return R.Call(context, "FindEquipmentByPlayerId", R.Int(R.Get(obj5, "Value")));
	}

	private static (object? Item, object? Component) Magazine(object item)
	{
		object obj = R.ConvertFor(item, itemType);
		object obj2 = getMagazine.Invoke(obj, null);
		if (obj2 != null)
		{
			return (Item: item, Component: obj2);
		}
		object obj3 = getFirearm.Invoke(obj, null);
		if (obj3 == null)
		{
			return (Item: null, Component: null);
		}
		object obj4 = R.Call(obj3, "GetItemInSlot", Enum.Parse(slot, "Magazine"));
		if (obj4 != null)
		{
			return (Item: obj4, Component: getMagazine.Invoke(R.ConvertFor(obj4, itemType), null));
		}
		return (Item: null, Component: null);
	}

	private static Dictionary<string, long> Loaded(object magazine)
	{
		Dictionary<string, long> dictionary = new Dictionary<string, long>(StringComparer.Ordinal);
		foreach (object item in R.Values(R.Call(magazine, "GetCartridgesData"), 2048))
		{
			string key = R.Key(R.Get(item, "Id"));
			int num = R.Int(R.Get(item, "Count"));
			if (num <= 0)
			{
				throw new InvalidOperationException("Invalid native magazine count");
			}
			dictionary[key] = dictionary.GetValueOrDefault(key) + num;
		}
		return dictionary;
	}

	private static List<StackRow> EquipmentRows(object equipment)
	{
		List<StackRow> rows = new List<StackRow>();
		HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
		MethodInfo methodInfo = R.Method(equipment.GetType(), "GatherItems", 2);
		object obj = Activator.CreateInstance(methodInfo.GetParameters()[0].ParameterType);
		methodInfo.Invoke(equipment, new object[2] { obj, false });
		foreach (object item in R.Values(obj, 128))
		{
			if (!R.Bool(R.Get(item, "IsLocked")) && R.Bool(R.Get(item, "IsContainer")))
			{
				Visit(R.Get(item, "Container"), 0);
			}
		}
		return (from r in rows
			group r by r.Id into g
			select g.First()).ToList();
		void Visit(object container, int depth)
		{
			if (depth <= 4 && seen.Count <= 128)
			{
				string text = R.NativeName(container);
				if (!(text == "Game.ItemsSystem.SlotItemsContainer") && !text.Contains("CartridgesContainer", StringComparison.Ordinal) && seen.Add(R.Key(R.Get(container, "OwnerId"))))
				{
					rows.AddRange(AutoStackRuntime.Rows(container));
					int num = R.Int(R.Get(container, "ItemsCount"));
					if (num < 0 || num > 8192)
					{
						throw new InvalidOperationException("Container count out of range");
					}
					for (int i = 0; i < num; i++)
					{
						object obj2 = R.Call(container, "GetItemAtIndex", i);
						if (obj2 != null && !R.Bool(R.Get(obj2, "IsLocked")) && R.Bool(R.Get(obj2, "IsContainer")))
						{
							Visit(R.Get(obj2, "Container"), depth + 1);
						}
					}
				}
			}
		}
	}

	private static Dictionary<string, long> Totals(IEnumerable<StackRow> rows)
	{
		return (from r in rows
			group r by r.Template).ToDictionary<IGrouping<string, StackRow>, string, long>((IGrouping<string, StackRow> g) => g.Key, (IGrouping<string, StackRow> g) => ((IEnumerable<StackRow>)g).Sum((Func<StackRow, long>)((StackRow x) => x.Quantity)), StringComparer.Ordinal);
	}

	internal static Proof? Rewrite(object processor, object[] args)
	{
		if (args.Length == 0 || args[0] == null || R.NativeName(args[0]) != "Game.ItemsSystem.ItemOperations.UnloadCartridgesContinuousOperation")
		{
			return null;
		}
		if (Pending.ContainsKey(R.Pointer(processor)))
		{
			return null;
		}
		try
		{
			if (!R.Bool(R.Get(processor, "IsBattleSession")))
			{
				return null;
			}
			object obj = R.Get(processor, "_continuousOperationInProgress");
			if (obj != null && R.Get(obj, "Operation") != null)
			{
				return null;
			}
			object obj2 = R.Get(processor, "_context");
			object obj3 = LiveEquipment(processor, obj2);
			if (obj3 == null)
			{
				report.Add("unloadMissingLiveEquipment", 1L);
				return null;
			}
			object obj4 = R.Get(R.ConvertFor(args[0], R.Type("Game.Shared", "Game.ItemsSystem.ItemOperations.UnloadCartridgesContinuousOperation")), "ItemId");
			object obj5 = R.Call(obj2, "FindItem", obj4);
			if (obj5 == null || R.Bool(R.Get(obj5, "IsLocked")))
			{
				return null;
			}
			object obj6 = R.Call(obj2, "FindEquipmentContainingItem", obj4);
			bool flag = R.Bool(R.Call(obj3, "ContainsItem", obj4));
			if (!flag && obj6 == null)
			{
				report.Add("unloadMissingSourceOwner", 1L);
				return null;
			}
			report.Data["lastUnloadSourceIdentity"] = new
			{
				item = R.Key(obj4),
				localSource = flag,
				enclosing = R.Key(R.Get(obj6, "Id")),
				live = R.Key(R.Get(obj3, "Id"))
			};
			if (!flag)
			{
				object obj7 = CurrentSession(obj2);
				if (obj7 == null)
				{
					return null;
				}
				int num = R.Int(R.Call(obj7, "GetLocalPlayerId"));
				object obj8 = R.Call(obj2, "FindPlayerIdByEquipment", obj6);
				bool flag2 = obj8 != null && R.Bool(R.Get(obj8, "HasValue"));
				if (!UnloadPolicy.SourceAllowed(flag, flag2, flag2 && R.Bool(R.Call(obj2, "CanPlayerLootVictim", num, R.Int(R.Get(obj8, "Value"))))))
				{
					report.Add("unloadForeignSourceDenied", 1L);
					return null;
				}
				report.Add("unloadAuthorizedLootSources", 1L);
			}
			var (obj9, obj10) = Magazine(obj5);
			if (obj9 == null || obj10 == null || !R.Bool(R.Call(obj10, "CanUnload", true)))
			{
				return null;
			}
			Dictionary<string, long> dictionary = Loaded(obj10);
			if (dictionary.Count == 0 || dictionary.Values.Sum() != R.Int(R.Get(obj10, "CartridgesLeft")))
			{
				return null;
			}
			List<StackRow> list = EquipmentRows(obj3);
			Dictionary<string, long> dictionary2 = new Dictionary<string, long>(StringComparer.Ordinal);
			object obj11 = R.Get(obj2, "ItemsManager");
			foreach (IGrouping<string, object> group in from x in R.Values(R.Call(obj10, "GetCartridgesData"), 2048)
				group x by R.Key(R.Get(x, "Id")))
			{
				object obj12 = R.Call(obj11, "FindTemplate", R.Get(group.First(), "Id"));
				if (obj12 == null)
				{
					return null;
				}
				long num2 = 0L;
				foreach (StackRow item in list.Where((StackRow x) => x.Template == group.Key && x.Quantity < x.Capacity))
				{
					if (R.Bool(canStack.Invoke(null, new object[2]
					{
						R.ConvertFor(obj12, itemType),
						item.Item
					})))
					{
						num2 += item.Capacity - item.Quantity;
					}
				}
				dictionary2[group.Key] = num2;
			}
			if (!IntakePolicy.CapacityCovers(dictionary, dictionary2))
			{
				report.Add("unloadNativeContinuousFallbacks", 1L);
				return null;
			}
			object obj13 = Activator.CreateInstance(request, R.ConvertFor(obj5, itemType));
			args[0] = obj13;
			report.Add("unloadRoutesSelected", 1L);
			report.Data["lastUnloadRoute"] = new
			{
				source = R.Key(obj4),
				localSource = flag,
				equipment = R.Key(R.Get(obj3, "Id")),
				needed = dictionary,
				room = dictionary2,
				request = request.FullName
			};
			return new Proof(processor, obj2, obj3, R.Get(obj9, "Id"), dictionary, Totals(list), R.Now + 120.0);
		}
		catch (Exception e)
		{
			report.Error("unload routing", e);
			return null;
		}
	}

	internal static void Queued(Proof? proof, bool accepted)
	{
		if (!(proof == null))
		{
			if (!accepted)
			{
				report.Add("unloadNativeQueueRejected", 1L);
				report.Save();
			}
			else
			{
				Pending[R.Pointer(proof.Processor)] = proof;
				report.Add("nativeStackUnloadRequests", 1L);
				report.Save();
			}
		}
	}

	internal static void Verify(object processor, bool idle)
	{
		long key = R.Pointer(processor);
		if (!Pending.TryGetValue(key, out Proof proof))
		{
			return;
		}
		if (R.Now > proof.Expires)
		{
			Pending.Remove(key);
			report.Add("unloadVerificationTimeouts", 1L);
			report.Save();
		}
		else
		{
			if (!idle)
			{
				return;
			}
			Pending.Remove(key);
			try
			{
				object obj = R.Call(R.Get(proof.Context, "ItemsManager"), "Find", proof.MagazineId);
				object obj2 = ((obj == null) ? null : getMagazine.Invoke(R.ConvertFor(obj, itemType), null));
				if (obj2 == null)
				{
					report.Add("unloadReadbackMissingMagazine", 1L);
					return;
				}
				object obj3 = LiveEquipment(processor, proof.Context);
				if (obj3 != null)
				{
					Dictionary<string, long> afterLoaded = Loaded(obj2);
					Dictionary<string, long> afterLoose = Totals(EquipmentRows(obj3));
					bool flag = proof.Loaded.All<KeyValuePair<string, long>>((KeyValuePair<string, long> p) => p.Value + proof.Loose.GetValueOrDefault(p.Key) == afterLoaded.GetValueOrDefault(p.Key) + afterLoose.GetValueOrDefault(p.Key));
					bool flag2 = proof.Loaded.Any<KeyValuePair<string, long>>((KeyValuePair<string, long> p) => afterLoaded.GetValueOrDefault(p.Key) < p.Value);
					report.Data["lastUnloadReadback"] = new
					{
						magazine = R.Key(proof.MagazineId),
						beforeMagazine = proof.Loaded,
						afterMagazine = afterLoaded,
						beforeLoose = proof.Loose,
						afterLoose = afterLoose,
						conserved = flag,
						unloaded = flag2
					};
					report.Add((flag && flag2) ? "confirmedNativeStackUnloads" : "unconfirmedNativeStackUnloads", 1L);
				}
			}
			catch (Exception e)
			{
				report.Error("unload readback", e);
			}
			finally
			{
				report.Save();
			}
		}
	}
}
