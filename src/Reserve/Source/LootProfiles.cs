#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Utils;
using Game.ItemsSystem;
using Game.ItemsSystem.ItemComponents;
using Game.DroppedItemSpawnPointSystem.Data;
using Game.DroppedItemSpawnPointSystem.Data.ItemAtSpawnPoints;

namespace Frontier.Reserve;

// The native warehouse group 1a903b8d... creates a StorageBox through Roots and
// fills it through RootChildren. A container is not an InteractableComponent action.
// Only native, non-personal container-group recipes are eligible here.
internal static class LootProfiles
{
    internal sealed record Profile(UniqueId SourceGroup, UniqueId Template,
        ItemSpawnPointGroupData.Preset Root,
        ItemSpawnPointGroupData.Preset[] Children, string SourcePoint, string Visual);
    internal static Profile[] Current { get; private set; } = Array.Empty<Profile>();
    internal static void Invalidate() => Current = Array.Empty<Profile>();
    internal static UniqueId GroupId(Anchor anchor) => Identity.Id("loot-group:" + anchor.Id);
    internal static Profile For(Anchor anchor)
    {
        if (Current.Length == 0) throw new InvalidOperationException("Loot profiles are not prepared");
        return Current[(Identity.Ordinal(anchor, Content.Map) - 3465100) % Current.Length];
    }
    static bool Unconditional(Game.ItemsSystem.IItemsFilter? filter) =>
        filter == null || filter.TryCast<Game.ItemsSystem.ItemFilters.TrueFilter>() != null;

    internal static bool Resolve(ItemSpawnPointGroupDataRepository groups, ItemTemplatesRepository templates)
    {
        var source = groups.GetData()?.Groups;
        if (source == null || source.Count == 0 || source.Count > 4096)
            throw new InvalidOperationException("Native loot group repository is unavailable or exceeds its budget");
        var accepted = new List<Profile>();
        int inspected = 0;
        // Verified StorageBox_6x6 from the actual Warehouse group and its native asset.
        // Do not admit NPC inventories or arbitrary container-shaped items.
        for (int i = 0; i < source.Count; i++)
        {
            var group = source[i];
            if (group == null || group.IsPersonal || group.Points == null || group.Points.Count == 0 ||
                group.Points.Count > 512 || group.Roots == null || group.Roots.Count != 1 ||
                group.RootChildren == null || group.RootChildren.Count == 0 || group.RootChildren.Count > 64) continue;
            string point = group.Points[0]?.SpawnPointName ?? "";
            if (!point.StartsWith("01_Warehouse-", StringComparison.Ordinal) &&
                !point.StartsWith("02_Port-", StringComparison.Ordinal)) continue;
            var root = group.Roots[0];
            if (root == null || root.Count != 1 || root.Chance <= 0 || !Unconditional(root.UnlockRequirement)) continue;
            var recipe = root.ItemAtSpawnPoint?.TryCast<ItemByTemplateId>();
            if (recipe == null || recipe.ItemTemplateId != UniqueId.Parse("0be34936-e68b-48ec-ae53-907b52b3405d") || !templates.TryGetData(recipe.ItemTemplateId, out var item) || item == null) continue;
            inspected++;
            var contract = TemplateContract.Inspect(item);
            var simple = TemplateContract.Component<SimpleContainerComponent>(item);
            if (contract.Rejection != null || simple?._data == null ||
                simple._data.Size.Width < 1 || simple._data.Size.Height < 1) continue;
            // Keep locks and native filters unchanged; prefer ordinary accessible boxes.
            if (contract.HasLock && (!contract.OpenByDefault && !Unconditional(TemplateContract.Component<LockComponent>(item)?._data?.OpenItemFilter))) continue;
            var children = new List<ItemSpawnPointGroupData.Preset>();
            bool compatible = true;
            for (int j = 0; j < group.RootChildren.Count; j++)
            {
                var child = group.RootChildren[j];
                if (child == null || child.ItemAtSpawnPoint == null || child.Count < 1 || child.Count > 64 ||
                    child.Chance < 0 || !Unconditional(child.UnlockRequirement)) { compatible = false; break; }
                children.Add(child);
            }
            if (compatible && children.Any(c => c.Chance > 0))
                accepted.Add(new Profile(group.SpawnPointsGroupId, item.Id, root, children.ToArray(), point, contract.Visual));
        }
        Current = accepted.OrderBy(p => p.SourceGroup.ToString(), StringComparer.Ordinal).ToArray();
        Trace.Data["lootProfileDiscovery"] = new { nativeGroups = source.Count, inspectedContainers = inspected,
            compatibleGroups = Current.Length, source = "native item_spawn_point_groups Roots + RootChildren",
            uniqueTemplates = Current.Select(p => p.Template.ToString()).Distinct().ToArray() };
        return Current.Length > 0;
    }

    internal static void Install(ItemSpawnPointGroupDataRepository repository)
    {
        var original = repository.GetData() ?? throw new InvalidOperationException("Missing native loot group data");
        var rows = original.Groups;
        if (rows == null || rows.Count > 4096) throw new InvalidOperationException("Unexpected native loot group count");
        var anchors = Content.Map.Anchors.Where(a => a.Kind == "loot").ToArray();
        var ids = new HashSet<UniqueId>(anchors.Select(GroupId));
        int existing = 0;
        for (int i = 0; i < rows.Count; i++) if (rows[i] != null && ids.Contains(rows[i].SpawnPointsGroupId)) existing++;
        if (existing == anchors.Length) return;
        if (existing != 0) throw new InvalidOperationException("Partial Reserve loot group registration");
        using var hold = new LeaseBag();
        var data = hold.Keep(new ItemSpawnPointGroupData());
        data.Groups = new Il2CppSystem.Collections.Generic.List<ItemSpawnPointGroupData.Group>();
        for (int i = 0; i < rows.Count; i++) data.Groups.Add(rows[i]);
        foreach (var anchor in anchors)
        {
            var profile = For(anchor);
            var group = hold.Keep(new ItemSpawnPointGroupData.Group());
            group.SpawnPointsGroupId = GroupId(anchor);
            group.IsPersonal = false;
            group.Points = new Il2CppSystem.Collections.Generic.List<ItemSpawnPointGroupData.Point>();
            group.Points.Add(new ItemSpawnPointGroupData.Point { SpawnPointId = Identity.Id(anchor.Id),
                SpawnPointName = Identity.Map + "-" + anchor.Id });
            group.Roots = new Il2CppSystem.Collections.Generic.List<ItemSpawnPointGroupData.Preset>();
            group.Roots.Add(new ItemSpawnPointGroupData.Preset { ItemAtSpawnPoint = profile.Root.ItemAtSpawnPoint,
                UnlockRequirement = profile.Root.UnlockRequirement, Chance = .85f, Count = 1, IsUnique = true });
            group.RootChildren = new Il2CppSystem.Collections.Generic.List<ItemSpawnPointGroupData.Preset>();
            foreach (var child in profile.Children)
                group.RootChildren.Add(new ItemSpawnPointGroupData.Preset { ItemAtSpawnPoint = child.ItemAtSpawnPoint,
                    UnlockRequirement = child.UnlockRequirement, Chance = child.Chance, Count = child.Count, IsUnique = child.IsUnique });
            data.Groups.Add(group);
        }
        repository.InternalReplaceData(data, Identity.Salt(repository.GetDataHash(), "loot-groups"));
        if (repository.GetData()?.Groups?.Count != rows.Count + anchors.Length)
            throw new InvalidOperationException("Native loot group readback count mismatch");
        Trace.Add("lootGroupRepositoriesRegistered");
        Trace.Data["reserveLootGroups"] = anchors.Select(a => new { anchor = a.Id, group = GroupId(a).ToString(),
            donorGroup = For(a).SourceGroup.ToString(), template = For(a).Template.ToString(),
            spawnChance = .85f, contents = "unmodified native child recipes, counts, uniqueness and probabilities" }).ToArray();
    }
}
