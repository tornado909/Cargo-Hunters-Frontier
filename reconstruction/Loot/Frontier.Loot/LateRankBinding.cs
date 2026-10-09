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
	private static readonly Queue<object> Recent = new Queue<object>();

	internal static object[] Snapshot()
	{
		lock (Recent)
		{
			return Recent.ToArray();
		}
	}

	internal static void Apply(IItemsManager manager, List<IItem> roots, HashSet<IntPtr> created, string rank, bool dissection, string channel)
	{
		if (manager == null || !manager.IsAuthoritative || roots == null || roots.Count > 4096 || !RankLadder.Active)
		{
			return;
		}
		HashSet<IntPtr> hashSet = new HashSet<IntPtr>();
		Queue<IItem> queue = new Queue<IItem>();
		for (int i = 0; i < roots.Count; i++)
		{
			if (roots[i] != null)
			{
				queue.Enqueue(roots[i]);
			}
		}
		while (queue.Count > 0 && hashSet.Count < 4096)
		{
			IItem val = queue.Dequeue();
			if (val == null || ((Il2CppObjectBase)val).WasCollected || !hashSet.Add(((Il2CppObjectBase)val).Pointer))
			{
				continue;
			}
			IItemsContainer container = val.Container;
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
			if (created.Contains(((Il2CppObjectBase)val).Pointer))
			{
				Bind(manager, val, rank, dissection, channel);
			}
		}
	}

	private static void Bind(IItemsManager manager, IItem item, string rank, bool dissection, string channel)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_020b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0295: Unknown result type (might be due to invalid IL or missing references)
		//IL_02dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f1: Unknown result type (might be due to invalid IL or missing references)
		Item val = ((Il2CppObjectBase)item).TryCast<Item>();
		if (val == null || val._template == null)
		{
			return;
		}
		BaseComponent baseComponent = val._baseComponent;
		if (((baseComponent != null) ? baseComponent._data : null) == null)
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
		IItem val2 = default(IItem);
		if (!manager.TryGetTemplate(UniqueId.Parse(text4), ref val2) || val2 == null)
		{
			Runtime.Inc("lateRankMissingTemplate", 1L);
			return;
		}
		BaseComponent component = val2.GetComponent<BaseComponent>();
		ReadonlyData val3 = ((component != null) ? component._data : null);
		if (val3 == null)
		{
			return;
		}
		IItem template = val._template;
		ReadonlyData data = val._baseComponent._data;
		IItem parent = item.Parent;
		IItemsContainer container = item.Container;
		IntPtr pointer = ((Il2CppObjectBase)item).Pointer;
		bool isRotated = item.IsRotated;
		bool isBroken = item.IsBroken;
		IItem val4 = manager.Find(item.Id);
		if (val4 != null && ((Il2CppObjectBase)val4).Pointer != pointer)
		{
			Runtime.Inc("lateRankDuplicateIdentitySkipped", 1L);
			return;
		}
		try
		{
			val._template = val2;
			val._baseComponent._data = val3;
			if (!(((Il2CppObjectBase)item).Pointer != pointer) && !(Runtime.Id(item.Id) != text2) && !(Runtime.Id(item.TemplateId) != text4))
			{
				IItem parent2 = item.Parent;
				if (!(((parent2 != null) ? ((Il2CppObjectBase)parent2).Pointer : IntPtr.Zero) != ((parent != null) ? ((Il2CppObjectBase)parent).Pointer : IntPtr.Zero)))
				{
					IItemsContainer container2 = item.Container;
					if (!(((container2 != null) ? ((Il2CppObjectBase)container2).Pointer : IntPtr.Zero) != ((container != null) ? ((Il2CppObjectBase)container).Pointer : IntPtr.Zero)) && item.IsRotated == isRotated && item.IsBroken == isBroken)
					{
						IItem obj = manager.Find(item.Id);
						if (!(((obj != null) ? ((Il2CppObjectBase)obj).Pointer : IntPtr.Zero) != ((val4 != null) ? ((Il2CppObjectBase)val4).Pointer : IntPtr.Zero)))
						{
							ItemDto val5 = ItemFactory.CreateDtoFrom(item);
							if (Runtime.Id(val5.Id) != text2 || Runtime.Id(val5.TemplateId) != text4)
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
							if (val4 == null)
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
			val._template = template;
			val._baseComponent._data = data;
			Runtime.Inc("lateRankRolledBack", 1L);
			Runtime.Error("late-rank-binding", e2);
		}
	}
}
