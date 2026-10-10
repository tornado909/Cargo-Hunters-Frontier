using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace Frontier.ReferenceFix;

internal static class AutoStackRuntime
{
	private sealed class Work
	{
		internal object Processor;

		internal object Context;

		internal object? Receiver;

		internal readonly SortedDictionary<string, object> Containers = new SortedDictionary<string, object>(StringComparer.Ordinal);

		internal readonly Dictionary<string, object> Sorts = new Dictionary<string, object>(StringComparer.Ordinal);

		internal readonly HashSet<string> Rejected = new HashSet<string>(StringComparer.Ordinal);

		internal bool ScanAccount;

		internal bool ScanLive;

		internal bool AllowAll;

		internal object? UnloadOperation;

		internal readonly HashSet<string> SourceIds = new HashSet<string>(StringComparer.Ordinal);

		internal double Due;

		internal double Expires;

		internal Proof? Pending;
	}

	private sealed record Proof(object SourceId, object TargetId, int SourceBefore, int TargetBefore, string PairKey);

	private const string Prefix = "Game.ItemsSystem.ItemOperations.";

	private static Type processorType = null;

	private static Type stackType = null;

	private static Type sortType = null;

	private static Type componentType = null;

	private static Type itemType = null;

	private static MethodInfo execute = null;

	private static MethodInfo getStack = null;

	private static MethodInfo canStack = null;

	private static Evidence report = null;

	[ThreadStatic]
	private static int submitting;

	private static readonly Dictionary<long, Work> WorkByProcessor = new Dictionary<long, Work>();

	private static readonly object Sync = new object();

	internal static void Install(Harmony h, AutoStackPlugin plugin)
	{
		processorType = R.Type("Game.Shared", "Game.ItemsSystem.NonAuthoritativeItemsOperationProcessor");
		stackType = R.Type("Game.Shared", "Game.ItemsSystem.ItemOperations.StackItemsOperation");
		sortType = R.Type("Game.Shared", "Game.ItemsSystem.ItemOperations.SortItemsContainerOperation");
		componentType = R.Type("Game.Shared", "Game.ItemsSystem.ItemComponents.StackableComponent");
		itemType = R.Type("Game.Shared", "Game.ItemsSystem.IItem");
		execute = R.Method(processorType, "Execute", 3);
		canStack = R.Method(stackType, "CanBeStacked", 2);
		getStack = itemType.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Single((MethodInfo m) => m.Name == "GetComponent" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0).MakeGenericMethod(componentType);
		report = new Evidence("Frontier-autostack-report.json", ((BasePlugin)plugin).Log, "0.7.3", "explicit-intake-native-membership-unload-v10.8.7");
		report.Data["nativeMutation"] = false;
		report.Data["quantityWrites"] = 0;
		report.Data["rollbackOwner"] = "native NonAuthoritativeItemsOperationProcessor";
		report.Data["status"] = "WAITING_FOR_INVENTORY_ACTION";
		UnloadToStack.Install(report);
		EquipmentGuide.Install(((BasePlugin)plugin).Log);
		R.Hook(h, execute, typeof(AutoStackRuntime), "BeforeExecute", "AfterExecute");
		R.Hook(h, R.Method(processorType, "CanExecute", 2), typeof(AutoStackRuntime), "BeforeCanExecute");
		R.Hook(h, R.Method(processorType, "Update", 1), typeof(AutoStackRuntime), null, "AfterUpdate");
		R.Hook(h, R.Method(processorType, "ClearOperations", 0), typeof(AutoStackRuntime), "ClearProcessor");
		R.Hook(h, R.Method(processorType, "SetLocalConnectionId", 1), typeof(AutoStackRuntime), "ClearProcessor");
		report.Data["hooks"] = 5;
		report.Data["manualMoveAutoMerge"] = false;
		report.Data["allowedAutomaticTriggers"] = new string[2] { "sort", "claim mailbox/quest reward" };
		report.Save(force: true);
		((BasePlugin)plugin).Log.LogInfo((object)"AutoStack v10.8.7: current native client session equipment, authorized loot-source checks, native stack-aware unload; manual slots and cooperative lifecycle preserved.");
	}

	private static void ClearProcessor(object __instance)
	{
		long key = R.Pointer(__instance);
		lock (Sync)
		{
			WorkByProcessor.Remove(key);
		}
		UnloadToStack.Forget(key);
		report.Add("processorResets", 1L);
	}

	private static void BeforeExecute(object __instance, object[] __args, out UnloadToStack.Proof? __state)
	{
		__state = ((submitting == 0) ? UnloadToStack.Rewrite(__instance, __args) : null);
	}

	private static void BeforeCanExecute(object __instance, object[] __args)
	{
		if (submitting == 0)
		{
			UnloadToStack.Rewrite(__instance, __args);
		}
	}

	private static void AfterExecute(object __instance, object __0, object? __2, bool __result, UnloadToStack.Proof? __state)
	{
		UnloadToStack.Queued(__state, __result);
		if (submitting != 0 || !__result || __0 == null)
		{
			return;
		}
		try
		{
			EquipmentGuide.Observe(R.Get(__instance, "_context"));
			string text = R.NativeName(__0);
			long key = R.Pointer(__instance);
			lock (Sync)
			{
				IntakeKind intakeKind = IntakePolicy.Classify(text);
				switch (intakeKind)
				{
				case IntakeKind.Manual:
					WorkByProcessor.Remove(key);
					if (text == "Game.ItemsSystem.ItemOperations.SplitItemOperationRequest")
					{
						report.Add("manualSplitsPreserved", 1L);
					}
					report.Add("manualActionsPreserved", 1L);
					report.Data["lastManualAction"] = text;
					report.Save();
					return;
				case IntakeKind.Ignore:
					return;
				default:
				{
					object obj = R.Get(__instance, "_context") ?? throw new InvalidOperationException("Client operation context missing");
					if (!WorkByProcessor.TryGetValue(key, out Work value) || R.Pointer(value.Context) != R.Pointer(obj))
					{
						Dictionary<long, Work> workByProcessor = WorkByProcessor;
						Work obj2 = new Work
						{
							Processor = __instance,
							Context = obj
						};
						value = obj2;
						workByProcessor[key] = obj2;
					}
					value.Receiver = __2;
					value.Due = R.Now + 0.1;
					value.Expires = R.Now + 120.0;
					value.Rejected.Clear();
					object obj3 = R.ConvertFor(__0, R.Type("Game.Shared", text));
					value.AllowAll |= intakeKind == IntakeKind.Sort || intakeKind == IntakeKind.Reward;
					if (text == "Game.ItemsSystem.ItemOperations.SortItemsContainerOperation")
					{
						object obj4 = R.Get(obj3, "ItemsContainerId");
						value.Containers[R.Key(obj4)] = obj4;
						value.Sorts[R.Key(obj4)] = R.Get(obj3, "SortingData");
					}
					else if (intakeKind == IntakeKind.Unload)
					{
						value.ScanLive = true;
						value.UnloadOperation = obj3;
					}
					else
					{
						value.ScanAccount = true;
					}
					report.Add("callbacks", 1L);
					report.Data["lastTrigger"] = text;
					report.Data["status"] = "WAITING_FOR_NATIVE_CONFIRMATION";
					break;
				}
				}
			}
			report.Save();
		}
		catch (Exception e)
		{
			report.Error("capture", e);
		}
	}

	private static void AfterUpdate(object __instance)
	{
		if (submitting != 0)
		{
			return;
		}
		try
		{
			long key = R.Pointer(__instance);
			Work value;
			lock (Sync)
			{
				WorkByProcessor.TryGetValue(key, out value);
			}
			if (value == null && !UnloadToStack.HasPending(key))
			{
				return;
			}
			bool flag = IsIdle(__instance);
			UnloadToStack.Verify(__instance, flag);
			if (value == null || R.Now < value.Due)
			{
				return;
			}
			value.Due = R.Now + 0.1;
			if (R.Now > value.Expires)
			{
				Finish(key, "QUEUE_TIMEOUT");
			}
			else
			{
				if (!flag)
				{
					return;
				}
				object obj = R.Get(value.Context, "ItemsManager");
				Proof pending = value.Pending;
				if ((object)pending != null)
				{
					value.Pending = null;
					int num = Quantity(R.Call(obj, "Find", pending.SourceId));
					int num2 = Quantity(R.Call(obj, "Find", pending.TargetId));
					if (num < pending.SourceBefore && num2 > pending.TargetBefore && (long)num + (long)num2 == (long)pending.SourceBefore + (long)pending.TargetBefore)
					{
						report.Add("confirmedMerges", 1L);
					}
					else
					{
						value.Rejected.Add(pending.PairKey);
						report.Add("unconfirmedOrRejected", 1L);
					}
				}
				if (value.ScanAccount)
				{
					object obj2 = R.Get(__instance, "_localConnectionId");
					object obj3 = R.Call(value.Context, "GetAccount", obj2);
					if (obj3 == null)
					{
						return;
					}
					object obj4 = R.Call(obj3, "GetInventory");
					if (obj4 != null)
					{
						GatherContainers(obj4, value, 0);
					}
					object obj5 = R.Call(obj3, "GetEquipment");
					if (obj5 != null)
					{
						MethodInfo methodInfo = R.Method(obj5.GetType(), "GatherItems", 2);
						object obj6 = Activator.CreateInstance(methodInfo.GetParameters()[0].ParameterType);
						methodInfo.Invoke(obj5, new object[2] { obj6, false });
						foreach (object item in R.Values(obj6, 128))
						{
							if (R.Bool(R.Get(item, "IsContainer")))
							{
								GatherContainers(R.Get(item, "Container"), value, 0);
							}
						}
					}
					object obj7 = UnloadToStack.LiveEquipment(__instance, value.Context);
					if (obj7 != null)
					{
						GatherEquipment(obj7, value);
					}
					value.ScanAccount = false;
				}
				if (value.UnloadOperation != null)
				{
					string text = R.Key(R.Get(value.UnloadOperation, "NewCartridgeId"));
					if (!string.IsNullOrEmpty(text))
					{
						value.SourceIds.Add(text);
					}
					value.UnloadOperation = null;
				}
				if (value.ScanLive)
				{
					object obj8 = UnloadToStack.LiveEquipment(__instance, value.Context);
					if (obj8 == null)
					{
						Finish(key, "NO_LIVE_RAID_EQUIPMENT");
						return;
					}
					GatherEquipment(obj8, value);
					value.ScanLive = false;
				}
				int num3 = 0;
				KeyValuePair<string, object>[] array = value.Containers.ToArray();
				foreach (KeyValuePair<string, object> keyValuePair in array)
				{
					object obj9 = R.Call(obj, "Find", keyValuePair.Value);
					if (obj9 == null || !R.Bool(R.Get(obj9, "IsContainer")))
					{
						continue;
					}
					object obj10 = R.Get(obj9, "Container");
					if (obj10 == null)
					{
						continue;
					}
					List<StackRow> list = Rows(obj10);
					report.Add("itemsInspected", list.Count);
					foreach (var item2 in value.AllowAll ? StackPlan.Pairs(list) : UnloadPairs(list, value.SourceIds))
					{
						if (++num3 > 8192)
						{
							Finish(key, "CANDIDATE_LIMIT");
							return;
						}
						string text2 = item2.Source.Id + ">" + item2.Target.Id;
						if (!value.Rejected.Contains(text2) && R.Bool(canStack.Invoke(null, new object[2]
						{
							item2.Source.Item,
							item2.Target.Item
						})))
						{
							object operation = Activator.CreateInstance(stackType, item2.Source.IdValue, item2.Target.IdValue, false);
							report.Add("mergeAttempts", 1L);
							if (Submit(value, operation))
							{
								value.Pending = new Proof(item2.Source.IdValue, item2.Target.IdValue, item2.Source.Quantity, item2.Target.Quantity, text2);
								report.Add("queuedMerges", 1L);
								report.Data["status"] = "STACK_REQUEST_QUEUED";
								report.Save();
								return;
							}
							value.Rejected.Add(text2);
							report.Add("nativeQueueRejected", 1L);
						}
					}
				}
				if (value.Sorts.Count > 0)
				{
					KeyValuePair<string, object> keyValuePair2 = value.Sorts.OrderBy<KeyValuePair<string, object>, string>((KeyValuePair<string, object> x) => x.Key, StringComparer.Ordinal).First();
					value.Sorts.Remove(keyValuePair2.Key);
					if (value.Containers.TryGetValue(keyValuePair2.Key, out object value2))
					{
						object operation2 = Activator.CreateInstance(sortType, value2, keyValuePair2.Value);
						if (Submit(value, operation2))
						{
							report.Add("finalSortsQueued", 1L);
							report.Save();
							return;
						}
					}
				}
				Finish(key, "IDLE_NO_MERGEABLE_PAIRS");
			}
		}
		catch (Exception e)
		{
			lock (Sync)
			{
				WorkByProcessor.Remove(R.Pointer(__instance));
			}
			report.Error("queue", e);
			report.Data["status"] = "STOPPED_ON_ERROR";
			report.Save(force: true);
		}
	}

	private static bool IsIdle(object processor)
	{
		if (R.Count(R.Get(processor, "_pendingOperations")) != 0 || R.Count(R.Get(processor, "_responses")) != 0 || R.Count(R.Get(processor, "_receivedOperations")) != 0)
		{
			return false;
		}
		object obj = R.Get(processor, "_continuousOperationInProgress");
		if (obj != null)
		{
			return R.Get(obj, "Operation") == null;
		}
		return true;
	}

	private static IEnumerable<(StackRow Source, StackRow Target)> UnloadPairs(List<StackRow> rows, HashSet<string> sourceIds)
	{
		foreach (StackRow source in rows.Where((StackRow x) => sourceIds.Contains(x.Id)))
		{
			foreach (StackRow item in from x in rows
				where x.Id != source.Id && !sourceIds.Contains(x.Id) && x.Template == source.Template && x.Quantity < x.Capacity
				orderby x.Quantity descending
				select x)
			{
				yield return (Source: source, Target: item);
			}
		}
	}

	private static void GatherEquipment(object equipment, Work w)
	{
		MethodInfo methodInfo = R.Method(equipment.GetType(), "GatherItems", 2);
		object obj = Activator.CreateInstance(methodInfo.GetParameters()[0].ParameterType);
		methodInfo.Invoke(equipment, new object[2] { obj, false });
		foreach (object item in R.Values(obj, 128))
		{
			if (!R.Bool(R.Get(item, "IsLocked")) && R.Bool(R.Get(item, "IsContainer")))
			{
				GatherContainers(R.Get(item, "Container"), w, 0);
			}
		}
	}

	private static bool Submit(Work w, object operation)
	{
		submitting++;
		try
		{
			return R.Bool(execute.Invoke(w.Processor, new object[3] { operation, null, w.Receiver }));
		}
		finally
		{
			submitting--;
		}
	}

	private static void Finish(long key, string status)
	{
		lock (Sync)
		{
			WorkByProcessor.Remove(key);
		}
		report.Data["status"] = status;
		report.Save(force: true);
	}

	private static object? Component(object? item)
	{
		if (item == null)
		{
			return null;
		}
		return getStack.Invoke(R.ConvertFor(item, itemType), null);
	}

	private static int Quantity(object? item)
	{
		object obj = Component(item);
		if (obj != null)
		{
			return R.Int(R.Get(obj, "Quantity"));
		}
		return 0;
	}

	internal static List<StackRow> Rows(object container)
	{
		List<StackRow> list = new List<StackRow>();
		int num = R.Int(R.Get(container, "ItemsCount"));
		if (num < 0 || num > 8192)
		{
			throw new InvalidOperationException("Container bound: " + num);
		}
		for (int i = 0; i < num; i++)
		{
			object obj = R.Call(container, "GetItemAtIndex", i);
			if (obj == null || R.Bool(R.Get(obj, "IsLocked")))
			{
				continue;
			}
			object obj2 = Component(obj);
			if (obj2 != null)
			{
				int num2 = R.Int(R.Get(obj2, "Quantity"));
				int num3 = R.Int(R.Get(obj2, "StackCapacity"));
				if (num2 > 0 && num3 > 1)
				{
					object obj3 = R.Get(obj, "Id");
					list.Add(new StackRow(R.Key(obj3), R.Key(R.Get(obj, "TemplateId")), num2, num3, R.ConvertFor(obj, itemType), obj3));
				}
			}
		}
		return list;
	}

	private static void GatherContainers(object container, Work w, int depth)
	{
		if (depth > 4 || w.Containers.Count > 128)
		{
			return;
		}
		string text = R.NativeName(container);
		if (text == "Game.ItemsSystem.SlotItemsContainer" || text.Contains("CartridgesContainer", StringComparison.Ordinal))
		{
			return;
		}
		object obj = R.Get(container, "OwnerId");
		string key = R.Key(obj);
		if (w.Containers.ContainsKey(key))
		{
			return;
		}
		w.Containers[key] = obj;
		int num = R.Int(R.Get(container, "ItemsCount"));
		if (num < 0 || num > 8192)
		{
			throw new InvalidOperationException("Container bound");
		}
		for (int i = 0; i < num; i++)
		{
			object obj2 = R.Call(container, "GetItemAtIndex", i);
			if (obj2 != null && R.Bool(R.Get(obj2, "IsContainer")))
			{
				GatherContainers(R.Get(obj2, "Container"), w, depth + 1);
			}
		}
	}
}
