#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Core.Utils;
using Core.DataRepository;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Game.GameModes;
using Game.SceneManagement;
using Game.GlobalMap;
using Game.Matchmaking;
using Game.ItemsSystem;
using Game.ItemsSystem.ItemComponents;
using Game.ExitZoneSystem.ItemComponents;
using Game.SpawnPointSystem.BotSpawnPoints.Data;
using Game.DroppedItemSpawnPointSystem.Data;
using Game.DroppedItemSpawnPointSystem.Data.ItemAtSpawnPoints;
using UnityEngine;

namespace Frontier.Reserve;

internal static class Repositories
{
    static readonly ConcurrentQueue<(ILoadableRepository Repo, NativeRoot Root)> incoming = new();
    static readonly List<ILoadableRepository> captured = new();
    static readonly LeaseBag roots = new();
    static readonly List<MapSettingsDataRepository> maps = new();
    static readonly List<MatchmakingMapSettingsDataRepository> queues = new();
    static readonly List<BotSpawnPointDataRepository> botPoints = new();
    static readonly List<ItemSpawnPointDataRepository> lootPoints = new();
    static readonly List<ItemSpawnPointGroupDataRepository> lootGroups = new();
    static readonly List<ItemTemplatesRepository> templates = new();
    static readonly List<BotPresetDataRepository> bots = new();
    static readonly HashSet<IntPtr> seen = new();
    static readonly HashSet<(IntPtr Manager, IntPtr Container)> sceneManagers = new();
    static readonly Dictionary<IntPtr, (IntPtr Repository, IntPtr SceneManager, long Revision)> managerVersions = new();
    static readonly PreparationEpoch preparation = new();
    static long unversionedLoads;
    internal static UniqueId CrateId, ExitTemplate;
    internal static bool Ready { get; private set; }
    internal static string Reason { get; private set; } = "Загрузка данных…";
    internal static long Revision => preparation.Revision;
    internal static MapSettingsData? LastMap;
    internal const long NameKey = 3465000000000012200L, DescKey = 3465000000000012201L;
    static Game.Common.LocalizedReference Text(long key) => new() { TableReference = "Items", TableEntryReference = key };

    internal static void Enqueue(ILoadableRepository repo)
    {
        if (repo != null && !repo.WasCollected) incoming.Enqueue((repo, new NativeRoot(repo)));
    }

    internal static void Drain()
    {
        // Native repository notifications, not a periodic native catalog scan.
        for (int count = 0; count < 16 && incoming.TryDequeue(out var entry); count++)
        {
            try { Capture(entry.Repo); }
            catch (Exception error) { Trace.Error("repository binding", error); }
            finally { entry.Root.Dispose(); }
        }
    }

    static void AddUnique<T>(List<T> list, T value) where T : Il2CppObjectBase
    {
        if (!list.Any(existing => existing.Pointer == value.Pointer)) list.Add(value);
    }

    internal static void Capture(ILoadableRepository repository, bool loadBoundary = true)
    {
        if (repository == null || repository.WasCollected) return;
        if (!loadBoundary && seen.Contains(repository.Pointer)) return;
        string file = Path.GetFileName(repository.FilePath.Replace('\\', '/'));
        Il2CppStructArray<byte>? hash;
        IntPtr nativeData;
        switch (file)
        {
            case "maps_settings.json":
                var map = repository.TryCast<MapSettingsDataRepository>();
                if (map == null) return;
                AddUnique(maps, map); hash = map._data?.Hash; nativeData = map._data?.Pointer ?? IntPtr.Zero; break;
            case "matchmaking_map_settings.json":
                var queue = repository.TryCast<MatchmakingMapSettingsDataRepository>();
                if (queue == null) return;
                AddUnique(queues, queue); hash = queue._data?.Hash; nativeData = queue._data?.Pointer ?? IntPtr.Zero; break;
            case "bot_spawn_points.json":
                var points = repository.TryCast<BotSpawnPointDataRepository>();
                if (points == null) return;
                AddUnique(botPoints, points); hash = points._data?.Hash; nativeData = points._data?.Pointer ?? IntPtr.Zero; break;
            case "item_spawn_points.json":
                var loot = repository.TryCast<ItemSpawnPointDataRepository>();
                if (loot == null) return;
                AddUnique(lootPoints, loot); hash = loot._data?.Hash; nativeData = loot._data?.Pointer ?? IntPtr.Zero; break;
            case "item_templates.json":
                var items = repository.TryCast<ItemTemplatesRepository>();
                if (items == null) return;
                AddUnique(templates, items); hash = items._data?.Hash; nativeData = items._data?.Pointer ?? IntPtr.Zero; break;
            case "item_spawn_point_groups.json":
                var groups = repository.TryCast<ItemSpawnPointGroupDataRepository>();
                if (groups == null) return;
                AddUnique(lootGroups, groups); hash = groups._object?.Hash; nativeData = groups._object?.Pointer ?? IntPtr.Zero; break;
            case "bot_presets.json":
                var presets = repository.TryCast<BotPresetDataRepository>();
                if (presets == null) return;
                AddUnique(bots, presets); hash = presets._data?.Hash; nativeData = presets._data?.Pointer ?? IntPtr.Zero; break;
            default: return; // Do not retain or invalidate on unrelated repositories.
        }
        if (seen.Add(repository.Pointer)) { roots.Keep(repository); captured.Add(repository); }
        string version = hash != null && hash.Length > 0 ? Convert.ToHexString((byte[])hash) : "load:" + (++unversionedLoads);
        string identity = file + "@" + repository.Pointer.ToInt64().ToString("X");
        version += "@data:" + nativeData.ToInt64().ToString("X");
        if (!preparation.Observe(identity, version)) return;
        Ready = false;
        Reason = preparation.Reason;
        LootProfiles.Invalidate();
        CrateId = UniqueId.Empty;
        ExitTemplate = UniqueId.Empty;
        Trace.Data["dependencyRevision"] = Revision;
        Trace.Data["dependencyInvalidation"] = new { file, version, scope = "native repository load boundary" };
        Trace.Data["dependencyRepositoryCount"] = captured.Count;
        Trace.Data["preparationState"] = preparation.Current.ToString();
    }

    internal static void Register(GlobalMapManager manager)
    {
        if (manager == null || manager.WasCollected) return;
        var repo = manager._mapSettingsDataRepository?.TryCast<MapSettingsDataRepository>();
        var scene = manager._sceneManager;
        IntPtr repositoryPointer = repo?.Pointer ?? IntPtr.Zero;
        IntPtr scenePointer = scene?.Pointer ?? IntPtr.Zero;
        if (managerVersions.TryGetValue(manager.Pointer, out var previous) && previous == (repositoryPointer, scenePointer, Revision)) return;
        if (repo != null)
        {
            var loadable = repo.TryCast<ILoadableRepository>();
            if (loadable != null) Capture(loadable, false);
            AddMap(repo);
        }
        RegisterScene(scene);
        Ensure();
        managerVersions[manager.Pointer] = (repositoryPointer, scenePointer, Revision);
    }

    internal static void RegisterScene(Game.SceneManagement.SceneManager? manager)
    {
        if (manager == null || manager.WasCollected) return;
        var container = manager._sceneDescriptionsContainer;
        if (container == null || container.WasCollected) return;
        var key = (manager.Pointer, container.Pointer);
        if (sceneManagers.Contains(key)) return;
        var donorScene = container.Find("01_Warehouse");
        if (donorScene != null) Trace.Data["nativeDonorSceneDescription"] = new {
            donorScene.Name, mainResource=donorScene._mainScene?.Name,
            additional=donorScene._additionalScenes?.Select(x=>new {x.Name,source=x.LoadingSourceType.ToString(),mode=x.LoadingMode.ToString(),x.IsServerOnly}).ToArray()
        };
        var existing = container.Find(Identity.Map);
        if (existing != null)
        {
            if (existing.Id != Identity.MapId) throw new InvalidOperationException("Reserve scene identity collision");
            sceneManagers.Add(key);
            return;
        }
        var description = ScriptableObject.CreateInstance<SceneDescription>();
        description._id = Identity.MapId;
        description._name = Identity.Map;
        description._type = SceneType.Map;
        description.name = "Frontier Reserve";
        description._mainScene = new SceneDescription.SceneLoadingData { Name = SceneContract.Resource,
            LoadingSourceType = SceneDescription.LoadingSourceType.InBuild,
            LoadingMode = SceneDescription.LoadingMode.Single, IsServerOnly = false };
        description._additionalScenes = new Il2CppReferenceArray<SceneDescription.SceneLoadingData>(0);
        description._settingOverrides = new Il2CppSystem.Collections.Generic.List<Core.Settings.ISettings>();
        roots.Keep(description);
        container._descriptions.Add(description);
        sceneManagers.Add(key);
        Trace.Add("sceneDescriptionsRegistered");
    }

    internal static bool Ensure()
    {
        bool available = maps.Count > 0 && queues.Count > 0 && templates.Count > 0 && bots.Count > 0 && botPoints.Count > 0 && lootPoints.Count > 0 && lootGroups.Count > 0;
        if (!preparation.TryBegin(available, out long revision)) return Ready;
        Ready = false;
        Reason = preparation.Reason;
        Trace.Data["preparationState"] = preparation.Current.ToString();
        Trace.Data["preparationAttempts"] = preparation.AttemptCount;
        try
        {
            foreach (var repo in maps) AddMap(repo);
            foreach (var repo in queues) AddQueue(repo);
            if (LastMap == null) return Complete(revision, "Не найдены настройки локации");
            ValidateReserveExits(LastMap);
            if (!ResolveTemplates()) return Complete(revision, Reason);
            foreach (var repo in bots)
                foreach (UniqueId id in BotIds)
                    if (!repo.TryGetData(id, out var data) || data == null)
                    {
                        Trace.Data["missingBotPreset"] = id.ToString();
                        return Complete(revision, "Не найден комплект охраны");
                    }
            foreach (var repo in botPoints) AddBots(repo);
            foreach (var repo in lootPoints) AddLoot(repo);
            foreach (var repo in lootGroups) LootProfiles.Install(repo);
            Trace.Data["templateBindings"] = new { crate = CrateId.ToString(), exit = ExitTemplate.ToString(),
                botPresets = BotIds.Select(id => id.ToString()).ToArray(),
                lootPoints = Content.Map.Anchors.Count(a => a.Kind == "loot"),
                enemyPoints = Content.Map.Anchors.Count(a => a.Kind == "enemy") };
            return Complete(revision, null);
        }
        catch (Exception error)
        {
            Trace.Error("content bindings", error);
            return Complete(revision, "Ошибка подготовки локации");
        }
    }

    static bool Complete(long revision, string? failure)
    {
        bool committed = preparation.Complete(revision, failure);
        Ready = committed && preparation.Ready;
        Reason = preparation.Reason;
        Trace.Data["preparationState"] = preparation.Current.ToString();
        Trace.Data["preparationFailure"] = failure;
        Trace.Data["preparationRevision"] = revision;
        Trace.Data["preparationAttempts"] = preparation.AttemptCount;
        if (!Loader.Failed && !Loader.InProgress && WorldBridge.ActiveCount == 0)
            Trace.Data["status"] = Ready ? "READY_FOR_DEPLOYMENT" : "BLOCKED_BY_DEPENDENCIES";
        Trace.Save(true);
        return Ready;
    }

    // Existing grounded bot recipes; no changes to rank or equipment policy.
    internal static readonly UniqueId[] BotIds = {
        UniqueId.Parse("fbd53f2f-9259-4bb4-aa42-5de9e8a4350c"),
        UniqueId.Parse("55b50950-77c2-4ed9-8568-d1685c68aece"),
        UniqueId.Parse("8877183c-ba1f-411a-9b75-87fda366ad46")
    };
    static T? Component<T>(Item item) where T : Il2CppObjectBase => TemplateContract.Component<T>(item);

    static bool ResolveTemplates()
    {
        if (!LootProfiles.Resolve(lootGroups[0], templates[0]))
        { Reason = "Не найдены совместимые штатные профили контейнерного лута"; return false; }
        var all = templates[0].GetAllData();
        int count = Identity.Count(all);
        var exits = new List<(Item Item, float Seconds)>();
        for (int i = 0; i < count; i++)
        {
            var item = all[i]; if (item == null) continue;
            var exit = Component<ExitZoneComponent>(item);
            if (exit != null && exit.TimeToExit > 0 && exit.TimeToExit <= 60 && Component<BaseComponent>(item) != null)
                exits.Add((item, exit.TimeToExit));
        }
        Trace.Add("templateDiscoveryPasses");
        Trace.Data["bindingCandidates"] = new { totalTemplates = count, crates = LootProfiles.Current.Select(p => p.Template).Distinct().Count(), exits = exits.Count };
        if (exits.Count == 0) { Reason = "Не найдены игровые выходы"; return false; }
        var chosen = exits.OrderBy(e => Math.Abs(e.Seconds - 10)).ThenBy(e => e.Item.Id.ToString(), StringComparer.Ordinal).First();
        CrateId = LootProfiles.Current[0].Template;
        ExitTemplate = chosen.Item.Id;
        Trace.Data["crateVisual"] = LootProfiles.Current[0].Visual;
        Trace.Data["exitSeconds"] = chosen.Seconds;
        Trace.Data["containerInteractionContract"] = "Native stationary container Roots/RootChildren; the general InteractableComponent is not the container-inventory interaction contract";
        return ValidateResolvedTemplates();
    }
    internal static bool ValidateResolvedTemplates()
    {
        if (LootProfiles.Current.Length == 0 || ExitTemplate == UniqueId.Empty) return false;
        foreach (var repo in templates)
        {
            if (repo.WasCollected) throw new InvalidOperationException("Native item repository was collected");
            foreach (var id in LootProfiles.Current.Select(p => p.Template).Distinct())
            {
                if (!repo.TryGetData(id, out var crate) || crate == null) throw new InvalidOperationException("Container template missing in a client/server repository");
                var contract = TemplateContract.Inspect(crate);
                if (contract.Rejection != null) throw new InvalidOperationException("Container contract changed: " + contract.Rejection);
            }
            if (!repo.TryGetData(ExitTemplate, out var exit) || exit == null) throw new InvalidOperationException("Exit template missing in a client/server repository");
            var component = Component<ExitZoneComponent>(exit);
            if (component == null || component.TimeToExit <= 0 || component.TimeToExit > 60)
                throw new InvalidOperationException("Exit template contract changed");
        }
        return true;
    }

 // Only the two deliberately unconditional Reserve exits use this native filter.
 // The native exit selector invokes every preset's requirement without a null guard.
 static IItemsFilter OpenReserveExitRequirement()
 {
    var filter = roots.Keep(new Game.ItemsSystem.ItemFilters.TrueFilter());
    return filter.TryCast<IItemsFilter>() ?? throw new InvalidOperationException("Native exit requirement interface missing");
 }
 static void ValidateReserveExits(MapSettingsData map)
 {
    if (map.MapId != Identity.MapId || map.MapName != Identity.Map || map.MaxExitZonesPerPlayer != 2 ||
        map.ExitZoneOverrides == null || map.ExitZoneOverrides.Count != 1)
        throw new InvalidOperationException("Reserve exit assignment settings changed");
    var preset = map.ExitZoneOverrides[0];
    if (preset == null || preset.UnlockRequirement == null ||
        preset.UnlockRequirement.TryCast<Game.ItemsSystem.ItemFilters.TrueFilter>() == null ||
        preset.ExitZoneIds == null || preset.ExitZoneIds.Count != 2)
        throw new InvalidOperationException("Reserve exit preset requires a real native unconditional filter");
    var expected = Content.Map.Anchors.Where(a => a.Kind == "exit").Select(a => Identity.Ordinal(a, Content.Map)).ToHashSet();
    for (int i = 0; i < preset.ExitZoneIds.Count; i++)
        if (!expected.Remove(preset.ExitZoneIds[i])) throw new InvalidOperationException("Exit preset ID mismatch");
    if (expected.Count != 0) throw new InvalidOperationException("Exit preset incomplete");
    var items = new Il2CppSystem.Collections.Generic.List<IItem>();
    using var itemsRoot = new NativeRoot(items);
    if (!preset.UnlockRequirement.IsCompatible(items, (FilterPolicy)1))
        throw new InvalidOperationException("Native Reserve exit requirement rejected empty equipment");
    Trace.Data["nativeExitAssignmentContract"] = new { exits = 2, filter = "Game.ItemsSystem.ItemFilters.TrueFilter",
        nativeInterfaceReadback = true, unrelatedMapConditionsChanged = false };
 }
 static void AddMap(MapSettingsDataRepository repo){if(repo.TryGetData(Identity.MapId,out var old)&&old!=null){if(old.MapName!=Identity.Map)throw new InvalidOperationException("Reserve map ID collision");LastMap=old;return;}
 if(!repo.TryGetDataByMapName("01_Warehouse",out var donor)||donor==null)return;var map=new MapSettingsData{MapId=Identity.MapId,MapName=Identity.Map,Type=MapSettingsData.MapType.None,MainTimePhaseDuration=1800,ExtraTimePhaseDuration=300,MaxExitZonesPerPlayer=2,ExitZonesAssignMode=MapSettingsData.ExitZoneAssignMode.Default,EquipmentReturn=donor.EquipmentReturn,ShowOnGlobalMap=false,NotAvailableToDeploy=false,UnlockRequirements=null,LocalizedName=Text(NameKey),LocalizedDescription=Text(DescKey),UnlockedShortText=Text(NameKey),LockedShortText=Text(NameKey),IconAtlasName=donor.IconAtlasName,IconSpriteName=donor.IconSpriteName,NavigationSystemConfiguration="Frontier.Reserve.Navigation",GlobalMapSettings=new MapSettingsData.GlobalMapSettingsData{LocalizedName=Text(NameKey),Position=new Vector3Data{X=0,Y=0,Z=0},IsAvailableOutOfDevelopmentMode=true,IsAvailableInDemonstrationMode=true,ShowOnlyInEditor=false},SpawnPointOverrides=new Il2CppSystem.Collections.Generic.List<MapSettingsData.SpawnPointPresetData>(),ExitZoneOverrides=new Il2CppSystem.Collections.Generic.List<MapSettingsData.ExitZonePresetData>(),Scripts=new Il2CppSystem.Collections.Generic.List<MapSettingsData.MapScriptSettingsData>()};
 var ids=new Il2CppSystem.Collections.Generic.List<int>();foreach(var a in Content.Map.Anchors.Where(a=>a.Kind=="exit"))ids.Add(Identity.Ordinal(a,Content.Map));map.ExitZoneOverrides.Add(new MapSettingsData.ExitZonePresetData{ExitZoneIds=ids,MaxActiveExitZonesCount=new Il2CppSystem.Nullable<int>(2),UnlockRequirement=OpenReserveExitRequirement()});
 var rows=repo.GetAllData();var list=new Il2CppSystem.Collections.Generic.List<MapSettingsData>();for(int i=0;i<Identity.Count(rows,256);i++)list.Add(rows[i]);list.Add(map);repo.SetLoadedData(list,Identity.Salt(repo._data?.Hash,"map"));LastMap=roots.Keep(map);Trace.Add("mapRepositoriesRegistered");}
 static void AddQueue(MatchmakingMapSettingsDataRepository repo){if(repo.TryGetData(Identity.Map,out var old)&&old!=null)return;if(!repo.TryGetData("01_Warehouse",out var d)||d==null)return;var row=new MatchmakingMapSettingsData{MapName=Identity.Map,NonBotPlayersCounts=d.NonBotPlayersCounts,WaitDurations=d.WaitDurations};var rows=repo.GetAllData();var list=new Il2CppSystem.Collections.Generic.List<MatchmakingMapSettingsData>();for(int i=0;i<Identity.Count(rows,256);i++)list.Add(rows[i]);list.Add(row);repo.SetLoadedData(list,Identity.Salt(repo._data?.Hash,"queue"));roots.Keep(row);Trace.Add("queueRepositoriesRegistered");}
 static void AddBots(BotSpawnPointDataRepository repo)
 {
    var anchors = Content.Map.Anchors.Where(a => a.Kind == "enemy").ToArray();
    if (anchors.Length != 18) throw new InvalidOperationException("Reserve enemy placement contract changed");
    if (!repo.TryGetData(Identity.Id(anchors[0].Id), out var existing) || existing == null)
    {
        var rows = repo.GetAllData();
        var list = new Il2CppSystem.Collections.Generic.List<BotSpawnPointData>();
        using var listRoot = new NativeRoot(list);
        for (int i = 0; i < Identity.Count(rows); i++) list.Add(rows[i]);
        for (int i = 0; i < anchors.Length; i++)
        {
            var row = new BotSpawnPointData { Id = Identity.Id(anchors[i].Id),
                LeveledEquipments = NativeBotEquipment.Create(BotIds[i % BotIds.Length]) };
            list.Add(row);
        }
        repo.SetLoadedData(list, Identity.Salt(repo._data?.Hash, "bots"));
        Trace.Add("botSpawnRepositoriesRegistered");
    }
    // Read from the published repository, not merely from our construction objects.
    // This executes only at the dependency preparation boundary, never as a timer.
    for (int i = 0; i < anchors.Length; i++)
    {
        if (!repo.TryGetData(Identity.Id(anchors[i].Id), out var row) || row == null || row.Id != Identity.Id(anchors[i].Id))
            throw new InvalidOperationException("Native bot spawn repository binding missing: " + anchors[i].Id);
        NativeBotEquipment.Validate(row.LeveledEquipments, BotIds[i % BotIds.Length]);
    }
    Trace.Data["nativeBotEquipmentContract"] = new { points = anchors.Length, valueBytes = 24,
        writer = "Native List<LeveledEquipment>.Add with unboxed value",
        validation = "Boxed native get_Item; null filter and exact nonempty preset ID; published repository read-back",
        sharedEnemyHooksChanged = false };
 }
 static void AddLoot(ItemSpawnPointDataRepository repo)
 {
    var anchors = Content.Map.Anchors.Where(a => a.Kind == "loot").ToArray();
    if (repo.TryGetData(Identity.Id(anchors[0].Id), out var existing) && existing != null) return;
    var rows = repo.GetAllData();
    var list = new Il2CppSystem.Collections.Generic.List<ItemSpawnPointData>();
    int count = Identity.Count(rows);
    for (int i = 0; i < count; i++) list.Add(rows[i]);
    foreach (var anchor in anchors)
        list.Add(new ItemSpawnPointData { SpawnPointId = Identity.Id(anchor.Id), SpawnPointName = Identity.Map + "-" + anchor.Id,
            Chance = 0, Personal = false, Item = null });
    repo.SetLoadedData(list, Identity.Salt(repo._data?.Hash, "loot"));
    Trace.Data["lootDonorPolicy"] = "Native container groups own root and child generation; individual points do not generate duplicate loose items";
    Trace.Add("lootRepositoriesRegistered");
 }
}
