using System;
using System.Collections.Generic;
using System.Linq;
using Core.Utils;
using Game.DroppedItemSpawnPointSystem.Data;
using Game.DroppedItemSpawnPointSystem.Data.ItemAtSpawnPoints;
using Game.ItemsSystem;
using Game.ItemsSystem.ItemComponents;
using Game.ItemsSystem.ItemFilters;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppSystem.Collections.Generic;

namespace Frontier.Reserve;

internal static class LootProfiles
{
	internal sealed record Profile(UniqueId SourceGroup, UniqueId Template, ItemSpawnPointGroupData.Preset Root, ItemSpawnPointGroupData.Preset[] Children, string SourcePoint, string Visual);

	private static readonly HashSet<string> AllowedContainers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "0be34936-e68b-48ec-ae53-907b52b3405d", "24c8b6bb-58fe-41a8-b5eb-9363a8bb9ed0", "4f594fb7-dfe9-4276-9ffa-36530f718f2c", "d1cf147e-fbcd-4543-8693-2901784066eb", "c25f1f8a-6c72-4cb4-a7e7-5a9baa2cc4a0", "c4525e44-3451-40dd-9dd8-ebdc4edbe6c3" };

	internal static Profile[] Current { get; private set; } = Array.Empty<Profile>();

	internal static void Invalidate()
	{
		Current = Array.Empty<Profile>();
	}

	internal static UniqueId GroupId(Anchor anchor)
	{
		return Identity.Id("loot-group:" + anchor.Id);
	}

	internal static Profile For(Anchor anchor)
	{
		if (Current.Length == 0)
		{
			throw new InvalidOperationException("Loot profiles are not prepared");
		}
		return Current[(Identity.Ordinal(anchor, Content.Map) - 3465100) % Current.Length];
	}

	private static bool Unconditional(IItemsFilter? filter)
	{
		if (filter != null)
		{
			return ((Il2CppObjectBase)filter).TryCast<TrueFilter>() != null;
		}
		return true;
	}

	internal static bool Resolve(ItemSpawnPointGroupDataRepository groups, ItemTemplatesRepository templates)
	{
		Il2CppSystem.Collections.Generic.List<ItemSpawnPointGroupData.Group> list = groups.GetData()?.Groups;
		if (list == null || list.Count == 0 || list.Count > 4096)
		{
			throw new InvalidOperationException("Native loot group repository is unavailable or exceeds its budget");
		}
		System.Collections.Generic.List<Profile> list2 = new System.Collections.Generic.List<Profile>();
		int num = 0;
		for (int i = 0; i < list.Count; i++)
		{
			ItemSpawnPointGroupData.Group obj = list[i];
			if (obj == null || obj.IsPersonal || obj.Points == null || obj.Points.Count == 0 || obj.Points.Count > 512 || obj.Roots == null || obj.Roots.Count != 1 || obj.RootChildren == null || obj.RootChildren.Count == 0 || obj.RootChildren.Count > 64)
			{
				continue;
			}
			string text = obj.Points[0]?.SpawnPointName ?? "";
			if (!text.StartsWith("01_Warehouse-", StringComparison.Ordinal) && !text.StartsWith("02_Port-", StringComparison.Ordinal))
			{
				continue;
			}
			ItemSpawnPointGroupData.Preset preset = obj.Roots[0];
			if (preset == null || preset.Count != 1 || preset.Chance <= 0f || !Unconditional(preset.UnlockRequirement))
			{
				continue;
			}
			IItemAtSpawnPoint itemAtSpawnPoint = preset.ItemAtSpawnPoint;
			ItemByTemplateId itemByTemplateId = ((itemAtSpawnPoint != null) ? ((Il2CppObjectBase)itemAtSpawnPoint).TryCast<ItemByTemplateId>() : null);
			if (itemByTemplateId == null || !AllowedContainers.Contains(itemByTemplateId.ItemTemplateId.ToString()) || !templates.TryGetData(itemByTemplateId.ItemTemplateId, out var data) || data == null)
			{
				continue;
			}
			num++;
			TemplateContract.Inspection inspection = TemplateContract.Inspect(data);
			SimpleContainerComponent simpleContainerComponent = TemplateContract.Component<SimpleContainerComponent>(data);
			if (inspection.Rejection != null || simpleContainerComponent?._data == null || simpleContainerComponent._data.Size.Width < 1 || simpleContainerComponent._data.Size.Height < 1 || (inspection.HasLock && !inspection.OpenByDefault && !Unconditional(TemplateContract.Component<LockComponent>(data)?._data?.OpenItemFilter)))
			{
				continue;
			}
			System.Collections.Generic.List<ItemSpawnPointGroupData.Preset> list3 = new System.Collections.Generic.List<ItemSpawnPointGroupData.Preset>();
			bool flag = true;
			for (int j = 0; j < obj.RootChildren.Count; j++)
			{
				ItemSpawnPointGroupData.Preset preset2 = obj.RootChildren[j];
				if (preset2 == null || preset2.ItemAtSpawnPoint == null || preset2.Count < 1 || preset2.Count > 64 || preset2.Chance < 0f || !Unconditional(preset2.UnlockRequirement))
				{
					flag = false;
					break;
				}
				list3.Add(preset2);
			}
			if (flag && list3.Any((ItemSpawnPointGroupData.Preset c) => c.Chance > 0f))
			{
				list2.Add(new Profile(obj.SpawnPointsGroupId, data.Id, preset, list3.ToArray(), text, inspection.Visual));
			}
		}
		Current = (from g in list2.GroupBy<Profile, string>((Profile p) => p.Template.ToString(), StringComparer.Ordinal).OrderBy<IGrouping<string, Profile>, string>((IGrouping<string, Profile> g) => g.Key, StringComparer.Ordinal)
			select g.OrderBy<Profile, string>((Profile p) => p.SourceGroup.ToString(), StringComparer.Ordinal).First()).ToArray();
		Trace.Data["lootProfileDiscovery"] = new
		{
			nativeGroups = list.Count,
			inspectedContainers = num,
			compatibleGroups = Current.Length,
			source = "native item_spawn_point_groups Roots + RootChildren",
			uniqueTemplates = Current.Select((Profile p) => p.Template.ToString()).Distinct().ToArray()
		};
		return Current.Length != 0;
	}

	internal static void Install(ItemSpawnPointGroupDataRepository repository)
	{
		Il2CppSystem.Collections.Generic.List<ItemSpawnPointGroupData.Group> groups = (repository.GetData() ?? throw new InvalidOperationException("Missing native loot group data")).Groups;
		if (groups == null || groups.Count > 4096)
		{
			throw new InvalidOperationException("Unexpected native loot group count");
		}
		Anchor[] array = Content.Map.Anchors.Where((Anchor a) => a.Kind == "loot").ToArray();
		HashSet<UniqueId> hashSet = new HashSet<UniqueId>(array.Select(GroupId));
		int num = 0;
		for (int num2 = 0; num2 < groups.Count; num2++)
		{
			if (groups[num2] != null && hashSet.Contains(groups[num2].SpawnPointsGroupId))
			{
				num++;
			}
		}
		if (num == array.Length)
		{
			return;
		}
		if (num != 0)
		{
			throw new InvalidOperationException("Partial Reserve loot group registration");
		}
		using LeaseBag leaseBag = new LeaseBag();
		ItemSpawnPointGroupData itemSpawnPointGroupData = leaseBag.Keep(new ItemSpawnPointGroupData());
		itemSpawnPointGroupData.Groups = new Il2CppSystem.Collections.Generic.List<ItemSpawnPointGroupData.Group>();
		for (int num3 = 0; num3 < groups.Count; num3++)
		{
			itemSpawnPointGroupData.Groups.Add(groups[num3]);
		}
		Anchor[] array2 = array;
		foreach (Anchor anchor in array2)
		{
			Profile profile = For(anchor);
			ItemSpawnPointGroupData.Group obj = leaseBag.Keep(new ItemSpawnPointGroupData.Group());
			obj.SpawnPointsGroupId = GroupId(anchor);
			obj.IsPersonal = false;
			obj.Points = new Il2CppSystem.Collections.Generic.List<ItemSpawnPointGroupData.Point>();
			obj.Points.Add(new ItemSpawnPointGroupData.Point
			{
				SpawnPointId = Identity.Id(anchor.Id),
				SpawnPointName = "Frontier_Reserve-" + anchor.Id
			});
			obj.Roots = new Il2CppSystem.Collections.Generic.List<ItemSpawnPointGroupData.Preset>();
			obj.Roots.Add(new ItemSpawnPointGroupData.Preset
			{
				ItemAtSpawnPoint = profile.Root.ItemAtSpawnPoint,
				UnlockRequirement = profile.Root.UnlockRequirement,
				Chance = anchor.SpawnChance,
				Count = 1,
				IsUnique = true
			});
			obj.RootChildren = new Il2CppSystem.Collections.Generic.List<ItemSpawnPointGroupData.Preset>();
			ItemSpawnPointGroupData.Preset[] children = profile.Children;
			foreach (ItemSpawnPointGroupData.Preset preset in children)
			{
				obj.RootChildren.Add(new ItemSpawnPointGroupData.Preset
				{
					ItemAtSpawnPoint = preset.ItemAtSpawnPoint,
					UnlockRequirement = preset.UnlockRequirement,
					Chance = preset.Chance,
					Count = preset.Count,
					IsUnique = preset.IsUnique
				});
			}
			itemSpawnPointGroupData.Groups.Add(obj);
		}
		repository.InternalReplaceData(itemSpawnPointGroupData, Identity.Salt(repository.GetDataHash(), "loot-groups"));
		if (repository.GetData()?.Groups?.Count != groups.Count + array.Length)
		{
			throw new InvalidOperationException("Native loot group readback count mismatch");
		}
		Trace.Add("lootGroupRepositoriesRegistered");
		Trace.Data["reserveLootGroups"] = array.Select((Anchor a) => new
		{
			anchor = a.Id,
			group = GroupId(a).ToString(),
			donorGroup = For(a).SourceGroup.ToString(),
			template = For(a).Template.ToString(),
			spawnChance = a.SpawnChance,
			contents = "unmodified native child recipes, counts, uniqueness and probabilities"
		}).ToArray();
	}
}
