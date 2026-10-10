using System;
using System.Collections.Generic;
using Core.Utils;
using Game.ItemsSystem;
using Game.ItemsSystem.Dtos;
using Game.ItemsSystem.Factories;
using Game.ItemsSystem.ItemComponents;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppSystem.Collections.Generic;

namespace Frontier.Loot;

internal static class LateRankBinding
{
	private static readonly System.Collections.Generic.Queue<object> Recent = new System.Collections.Generic.Queue<object>();

	internal static object[] Snapshot()
	{
		lock (Recent)
		{
			return Recent.ToArray();
		}
	}

	internal static void Apply(IItemsManager manager, Il2CppSystem.Collections.Generic.List<IItem> roots, HashSet<IntPtr> created, string rank, bool dissection, string channel)
	{
		if (manager == null || !manager.IsAuthoritative || roots == null || roots.Count > 4096 || !RankLadder.Active)
		{
			return;
		}
		HashSet<IntPtr> hashSet = new HashSet<IntPtr>();
		System.Collections.Generic.Queue<IItem> queue = new System.Collections.Generic.Queue<IItem>();
		for (int i = 0; i < roots.Count; i++)
		{
			if (roots[i] != null)
			{
				queue.Enqueue(roots[i]);
			}
		}
		while (queue.Count > 0 && hashSet.Count < 4096)
		{
			IItem item = queue.Dequeue();
			if (item == null || ((Il2CppObjectBase)item).WasCollected || !hashSet.Add(((Il2CppObjectBase)item).Pointer))
			{
				continue;
			}
			IItemsContainer container = item.Container;
			if (container != null)
			{
				int itemsCount = container.ItemsCount;
				if (itemsCount >= 0 && itemsCount <= 2048)
				{
					for (int j = 0; j < itemsCount; j++)
					{
						IItem itemAtIndex = container.GetItemAtIndex(j);
						if (itemAtIndex != null)
						{
							queue.Enqueue(itemAtIndex);
						}
					}
				}
			}
			if (created.Contains(((Il2CppObjectBase)item).Pointer))
			{
				Bind(manager, item, rank, dissection, channel);
			}
		}
	}

	private static void Bind(IItemsManager manager, IItem item, string rank, bool dissection, string channel)
	{
		Item item2 = ((Il2CppObjectBase)item).TryCast<Item>();
		if (item2 == null || item2._template == null || item2._baseComponent?._data == null)
		{
			return;
		}
		string text = Runtime.Id(item.TemplateId);
		string text2 = Runtime.Id(item.Id);
		if (text2 == text || Runtime.Db.Variants.ContainsKey(text))
		{
			return;
		}
		string valueOrDefault = Runtime.Db.LegacyBodyBases.GetValueOrDefault(text, text);
		if (!Runtime.Db.Families.TryGetValue(valueOrDefault, out Family value) || (dissection && value.Category != 13))
		{
			return;
		}
		string text3 = ((value.Category == 13) ? "bodyParts" : ((value.Category == 1) ? "weapons" : "modules"));
		if (!Runtime.Db.Settings.GetValueOrDefault(text3, defaultValue: true))
		{
			return;
		}
		int tier;
		string text4 = Runtime.Db.Select(valueOrDefault, text2 + "|" + valueOrDefault, rank, out tier);
		if (text4 == text || !Runtime.Db.Variants.TryGetValue(text4, out Trait value2) || value2.Base != valueOrDefault || value2.RankIndex < 1)
		{
			return;
		}
		if (!manager.TryGetTemplate(UniqueId.Parse(text4), out var itemTemplate) || itemTemplate == null)
		{
			Runtime.Inc("lateRankMissingTemplate", 1L);
			return;
		}
		BaseComponent.ReadonlyData readonlyData = itemTemplate.GetComponent<BaseComponent>()?._data;
		if (readonlyData == null)
		{
			return;
		}
		IItem template = item2._template;
		BaseComponent.ReadonlyData data = item2._baseComponent._data;
		IItem parent = item.Parent;
		IItemsContainer container = item.Container;
		IntPtr pointer = ((Il2CppObjectBase)item).Pointer;
		bool isRotated = item.IsRotated;
		bool isBroken = item.IsBroken;
		IItem item3 = manager.Find(item.Id);
		if (item3 != null && ((Il2CppObjectBase)item3).Pointer != pointer)
		{
			Runtime.Inc("lateRankDuplicateIdentitySkipped", 1L);
			return;
		}
		try
		{
			item2._template = itemTemplate;
			item2._baseComponent._data = readonlyData;
			if (!(((Il2CppObjectBase)item).Pointer != pointer) && !(Runtime.Id(item.Id) != text2) && !(Runtime.Id(item.TemplateId) != text4))
			{
				IItem parent2 = item.Parent;
				if (!(((parent2 != null) ? ((Il2CppObjectBase)parent2).Pointer : IntPtr.Zero) != ((parent != null) ? ((Il2CppObjectBase)parent).Pointer : IntPtr.Zero)))
				{
					IItemsContainer container2 = item.Container;
					if (!(((container2 != null) ? ((Il2CppObjectBase)container2).Pointer : IntPtr.Zero) != ((container != null) ? ((Il2CppObjectBase)container).Pointer : IntPtr.Zero)) && item.IsRotated == isRotated && item.IsBroken == isBroken)
					{
						IItem item4 = manager.Find(item.Id);
						if (!(((item4 != null) ? ((Il2CppObjectBase)item4).Pointer : IntPtr.Zero) != ((item3 != null) ? ((Il2CppObjectBase)item3).Pointer : IntPtr.Zero)))
						{
							ItemDto itemDto = ItemFactory.CreateDtoFrom(item);
							if (Runtime.Id(itemDto.Id) != text2 || Runtime.Id(itemDto.TemplateId) != text4)
							{
								throw new InvalidOperationException("Ranked native DTO identity mismatch");
							}
							Runtime.CreationContext currentCreation = Runtime.CurrentCreation;
							try
							{
								Runtime.CurrentCreation = null;
								RankLadder.Materialize(item);
							}
							catch (Exception e)
							{
								Runtime.Error("late-rank-modifiers", e);
							}
							finally
							{
								Runtime.CurrentCreation = currentCreation;
							}
							if (item3 == null)
							{
								Runtime.Inc("rankedCompletedBeforeRegistration", 1L);
							}
							Runtime.Inc("rankedCompletedItems." + channel + "." + text3, 1L);
							Runtime.Inc("rankedCompletedItemsVerified", 1L);
							lock (Recent)
							{
								Recent.Enqueue(new
								{
									item = text2,
									from = text,
									to = text4,
									rank = RankPolicy.Name(tier),
									channel = channel,
									objectIdentityPreserved = true,
									nativeDtoVerified = true
								});
								while (Recent.Count > 32)
								{
									Recent.Dequeue();
								}
								return;
							}
						}
					}
				}
			}
			throw new InvalidOperationException("Completed-item identity/topology verification failed");
		}
		catch (Exception e2)
		{
			item2._template = template;
			item2._baseComponent._data = data;
			Runtime.Inc("lateRankRolledBack", 1L);
			Runtime.Error("late-rank-binding", e2);
		}
	}
}
