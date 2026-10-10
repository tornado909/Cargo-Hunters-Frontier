using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using BepInEx.Logging;

namespace Frontier.ReferenceFix;

internal static class EquipmentGuide
{
	private sealed record NamedItem(string Id, string Name);

	private static Evidence? report;

	private static readonly HashSet<long> Done = new HashSet<long>();

	private static double retry;

	internal static void Install(ManualLogSource log)
	{
		report = new Evidence("Frontier-equipment-compat-report.json", log, "0.7.2", "native-template-slot-filter-guide-v10.7");
	}

	internal static void Observe(object? context)
	{
		if (report == null || context == null || R.Now < retry)
		{
			return;
		}
		try
		{
			object manager = R.Get(context, "ItemsManager");
			if (manager == null)
			{
				return;
			}
			long num = R.Pointer(manager);
			if (num == 0L || Done.Contains(num))
			{
				return;
			}
			retry = R.Now + 30.0;
			Type uid = R.Type("Core.Shared.Utils", "Core.Utils.UniqueId");
			Type type = R.Type("Game.Shared", "Game.ItemsSystem.IItem");
			Type type2 = R.Type("Game.Shared", "Game.ItemsSystem.ItemComponents.SlotItemsContainerComponent");
			MethodInfo methodInfo = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Single((MethodInfo m) => m.Name == "GetComponent" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0).MakeGenericMethod(type2);
			(NamedItem, object)[] array = (from n in JsonSerializer.Deserialize<NamedItem[]>("[{\"Id\":\"956dcf15-9173-5dad-82b1-400839b6b0f2\",\"Name\":\"Грузовой привод\"},{\"Id\":\"fca84a4c-5ba7-50aa-a5c3-a83526b7ea1a\",\"Name\":\"Быстрый заряжающий\"},{\"Id\":\"a56c33cd-df36-56eb-9742-06ab81e65da1\",\"Name\":\"Стабилизатор\"},{\"Id\":\"015fa3d0-1f87-56c4-a20e-2bfb02d68e88\",\"Name\":\"Сканер сборщика\"},{\"Id\":\"9cc7c3a0-81ec-5b9a-b5c3-cf9751a55b92\",\"Name\":\"Ускоритель\"},{\"Id\":\"3a7a692b-f15e-5c5e-ab47-f93454ddbeef\",\"Name\":\"Экономичный привод\"},{\"Id\":\"2637c937-5e9c-5299-a3f5-9e1140afa5fd\",\"Name\":\"Тихий ход\"},{\"Id\":\"d2db76c0-56b0-5a3c-bb61-60dfb36b8d4d\",\"Name\":\"Фильтр «Барьер»\"}]")
				select (Definition: n, Item: Template(n.Id)) into x
				where x.Item != null
				select x).ToArray();
			NamedItem[] obj = new NamedItem[6]
			{
				new NamedItem("ea18e576-2f91-534c-8cec-5d16ca3cd74c", "Шлем «Механик»"),
				new NamedItem("dc1111e7-232f-4592-acc3-db20d9378270", "GP helmet — штатная основа"),
				new NamedItem("8baa7af0-04e6-4d43-9c91-aa00c49aed9d", "Торс 03/01 — штатные слоты"),
				new NamedItem("fe006f6c-f1d0-42ca-b271-b5be8267e66b", "Голова 06 — штатный слот"),
				new NamedItem("d1b9df33-ad32-4eb4-9db3-95eba73cce96", "Правая рука 04 — штатные слоты"),
				new NamedItem("20fdb9ab-c689-40df-8910-38a60b0e0b6b", "Правая нога 03 — штатный слот")
			};
			List<object> list = new List<object>();
			NamedItem[] array2 = obj;
			foreach (NamedItem namedItem in array2)
			{
				object obj2 = Template(namedItem.Id);
				if (obj2 == null)
				{
					list.Add(new
					{
						Name = namedItem.Name,
						Id = namedItem.Id,
						status = "TEMPLATE_NOT_LOADED"
					});
					continue;
				}
				object obj3 = methodInfo.Invoke(R.ConvertFor(obj2, type), null);
				if (obj3 == null)
				{
					list.Add(new
					{
						Name = namedItem.Name,
						Id = namedItem.Id,
						status = "NO_NATIVE_SLOT_COMPONENT"
					});
					continue;
				}
				object? list2 = R.Get(R.Get(obj3, "_data"), "ContainerSlots");
				List<object> list3 = new List<object>();
				int num3 = 0;
				foreach (object item in R.Values(list2, 16))
				{
					object obj4 = R.Get(item, "ItemsFilter");
					List<string> list4 = new List<string>();
					(NamedItem, object)[] array3 = array;
					for (int num4 = 0; num4 < array3.Length; num4++)
					{
						(NamedItem, object) tuple = array3[num4];
						if (obj4 != null && R.Bool(R.Call(obj4, "IsCompatible", tuple.Item2)))
						{
							list4.Add(tuple.Item1.Name);
						}
					}
					list3.Add(new
					{
						slotIndex = num3++,
						filter = ((obj4 == null) ? "UNSPECIFIED" : R.NativeName(obj4)),
						compatibleFrontierUpgrades = list4
					});
				}
				list.Add(new
				{
					Name = namedItem.Name,
					Id = namedItem.Id,
					status = "NATIVE_FILTERS_READ",
					slots = list3
				});
			}
			report.Data["status"] = "NATIVE_SLOT_COMPATIBILITY_OBSERVED";
			report.Data["scope"] = "Only the listed templates and eight Frontier upgrades; not an exhaustive vanilla-module catalog";
			report.Data["itemWrites"] = 0;
			report.Data["itemCreations"] = 0;
			report.Data["templates"] = list;
			report.Data["checkedUpgradeCount"] = array.Length;
			if (Done.Count > 8)
			{
				Done.Clear();
			}
			Done.Add(num);
			report.Save(force: true);
			object? Template(string id)
			{
				return R.Call(manager, "FindTemplate", R.Call(uid, "Parse", id));
			}
		}
		catch (Exception e)
		{
			report.Error("compatibility guide", e);
		}
	}
}
