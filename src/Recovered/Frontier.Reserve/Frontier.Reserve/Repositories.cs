using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.DataRepository;
using Core.Settings;
using Core.Utils;
using Game.Common;
using Game.DroppedItemSpawnPointSystem.Data;
using Game.ExitZoneSystem.ItemComponents;
using Game.GameModes;
using Game.GlobalMap;
using Game.ItemsSystem;
using Game.ItemsSystem.ItemComponents;
using Game.ItemsSystem.ItemFilters;
using Game.Matchmaking;
using Game.SceneManagement;
using Game.SpawnPointSystem.BotSpawnPoints.Data;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem;
using Il2CppSystem.Collections.Generic;
using UnityEngine;

namespace Frontier.Reserve;

internal static class Repositories
{
	private static readonly ConcurrentQueue<(ILoadableRepository Repo, NativeRoot Root)> incoming = new ConcurrentQueue<(ILoadableRepository, NativeRoot)>();

	private static readonly System.Collections.Generic.List<ILoadableRepository> captured = new System.Collections.Generic.List<ILoadableRepository>();

	private static readonly LeaseBag roots = new LeaseBag();

	private static readonly System.Collections.Generic.List<MapSettingsDataRepository> maps = new System.Collections.Generic.List<MapSettingsDataRepository>();

	private static readonly System.Collections.Generic.List<MatchmakingMapSettingsDataRepository> queues = new System.Collections.Generic.List<MatchmakingMapSettingsDataRepository>();

	private static readonly System.Collections.Generic.List<BotSpawnPointDataRepository> botPoints = new System.Collections.Generic.List<BotSpawnPointDataRepository>();

	private static readonly System.Collections.Generic.List<ItemSpawnPointDataRepository> lootPoints = new System.Collections.Generic.List<ItemSpawnPointDataRepository>();

	private static readonly System.Collections.Generic.List<ItemSpawnPointGroupDataRepository> lootGroups = new System.Collections.Generic.List<ItemSpawnPointGroupDataRepository>();

	private static readonly System.Collections.Generic.List<ItemTemplatesRepository> templates = new System.Collections.Generic.List<ItemTemplatesRepository>();

	private static readonly System.Collections.Generic.List<BotPresetDataRepository> bots = new System.Collections.Generic.List<BotPresetDataRepository>();

	private static readonly HashSet<System.IntPtr> seen = new HashSet<System.IntPtr>();

	private static readonly HashSet<(System.IntPtr Manager, System.IntPtr Container)> sceneManagers = new HashSet<(System.IntPtr, System.IntPtr)>();

	private static readonly System.Collections.Generic.Dictionary<System.IntPtr, (System.IntPtr Repository, System.IntPtr SceneManager, long Revision)> managerVersions = new System.Collections.Generic.Dictionary<System.IntPtr, (System.IntPtr, System.IntPtr, long)>();

	private static readonly PreparationEpoch preparation = new PreparationEpoch();

	private static long unversionedLoads;

	internal static UniqueId CrateId;

	internal static UniqueId ExitTemplate;

	internal static MapSettingsData? LastMap;

	internal const long NameKey = 3465000000000012200L;

	internal const long DescKey = 3465000000000012201L;

	internal static readonly UniqueId[] BotIds = new UniqueId[3]
	{
		UniqueId.Parse("fbd53f2f-9259-4bb4-aa42-5de9e8a4350c"),
		UniqueId.Parse("55b50950-77c2-4ed9-8568-d1685c68aece"),
		UniqueId.Parse("8877183c-ba1f-411a-9b75-87fda366ad46")
	};

	internal static bool Ready { get; private set; }

	internal static string Reason { get; private set; } = "Загрузка данных…";

	internal static long Revision => preparation.Revision;

	private static LocalizedReference Text(long key)
	{
		return new LocalizedReference
		{
			TableReference = "Items",
			TableEntryReference = key
		};
	}

	internal static void Enqueue(ILoadableRepository repo)
	{
		if (repo != null && !((Il2CppObjectBase)repo).WasCollected)
		{
			incoming.Enqueue((repo, new NativeRoot((Il2CppObjectBase)(object)repo)));
		}
	}

	internal static void Drain()
	{
		for (int i = 0; i < 16; i++)
		{
			if (!incoming.TryDequeue(out (ILoadableRepository, NativeRoot) result))
			{
				break;
			}
			try
			{
				Capture(result.Item1);
			}
			catch (System.Exception e)
			{
				Trace.Error("repository binding", e);
			}
			finally
			{
				result.Item2.Dispose();
			}
		}
	}

	private static void AddUnique<T>(System.Collections.Generic.List<T> list, T value) where T : Il2CppObjectBase
	{
		if (!list.Any((T existing) => ((Il2CppObjectBase)existing).Pointer == ((Il2CppObjectBase)value).Pointer))
		{
			list.Add(value);
		}
	}

	internal static void Capture(ILoadableRepository repository, bool loadBoundary = true)
	{
		if (repository == null || ((Il2CppObjectBase)repository).WasCollected || (!loadBoundary && seen.Contains(((Il2CppObjectBase)repository).Pointer)))
		{
			return;
		}
		string fileName = Path.GetFileName(repository.FilePath.Replace('\\', '/'));
		if (fileName == null)
		{
			return;
		}
		Il2CppStructArray<byte> val;
		System.IntPtr intPtr;
		switch (fileName.Length)
		{
		case 18:
		{
			if (!(fileName == "maps_settings.json"))
			{
				return;
			}
			MapSettingsDataRepository mapSettingsDataRepository = ((Il2CppObjectBase)repository).TryCast<MapSettingsDataRepository>();
			if (mapSettingsDataRepository == null)
			{
				return;
			}
			AddUnique(maps, mapSettingsDataRepository);
			val = mapSettingsDataRepository._data?.Hash;
			MultiObjectDataRepository<MapSettingsData, int>.DataStorage data6 = mapSettingsDataRepository._data;
			intPtr = ((data6 != null) ? ((Il2CppObjectBase)data6).Pointer : System.IntPtr.Zero);
			break;
		}
		case 29:
		{
			if (!(fileName == "matchmaking_map_settings.json"))
			{
				return;
			}
			MatchmakingMapSettingsDataRepository matchmakingMapSettingsDataRepository = ((Il2CppObjectBase)repository).TryCast<MatchmakingMapSettingsDataRepository>();
			if (matchmakingMapSettingsDataRepository == null)
			{
				return;
			}
			AddUnique(queues, matchmakingMapSettingsDataRepository);
			val = matchmakingMapSettingsDataRepository._data?.Hash;
			MultiObjectDataRepository<MatchmakingMapSettingsData, string>.DataStorage data2 = matchmakingMapSettingsDataRepository._data;
			intPtr = ((data2 != null) ? ((Il2CppObjectBase)data2).Pointer : System.IntPtr.Zero);
			break;
		}
		case 21:
		{
			if (!(fileName == "bot_spawn_points.json"))
			{
				return;
			}
			BotSpawnPointDataRepository botSpawnPointDataRepository = ((Il2CppObjectBase)repository).TryCast<BotSpawnPointDataRepository>();
			if (botSpawnPointDataRepository == null)
			{
				return;
			}
			AddUnique(botPoints, botSpawnPointDataRepository);
			val = botSpawnPointDataRepository._data?.Hash;
			MultiObjectDataRepository<BotSpawnPointData, UniqueId>.DataStorage data4 = botSpawnPointDataRepository._data;
			intPtr = ((data4 != null) ? ((Il2CppObjectBase)data4).Pointer : System.IntPtr.Zero);
			break;
		}
		case 22:
		{
			if (!(fileName == "item_spawn_points.json"))
			{
				return;
			}
			ItemSpawnPointDataRepository itemSpawnPointDataRepository = ((Il2CppObjectBase)repository).TryCast<ItemSpawnPointDataRepository>();
			if (itemSpawnPointDataRepository == null)
			{
				return;
			}
			AddUnique(lootPoints, itemSpawnPointDataRepository);
			val = itemSpawnPointDataRepository._data?.Hash;
			MultiObjectDataRepository<ItemSpawnPointData, UniqueId>.DataStorage data5 = itemSpawnPointDataRepository._data;
			intPtr = ((data5 != null) ? ((Il2CppObjectBase)data5).Pointer : System.IntPtr.Zero);
			break;
		}
		case 19:
		{
			if (!(fileName == "item_templates.json"))
			{
				return;
			}
			ItemTemplatesRepository itemTemplatesRepository = ((Il2CppObjectBase)repository).TryCast<ItemTemplatesRepository>();
			if (itemTemplatesRepository == null)
			{
				return;
			}
			AddUnique(templates, itemTemplatesRepository);
			val = itemTemplatesRepository._data?.Hash;
			MultiObjectDataRepository<Item, UniqueId>.DataStorage data3 = itemTemplatesRepository._data;
			intPtr = ((data3 != null) ? ((Il2CppObjectBase)data3).Pointer : System.IntPtr.Zero);
			break;
		}
		case 28:
		{
			if (!(fileName == "item_spawn_point_groups.json"))
			{
				return;
			}
			ItemSpawnPointGroupDataRepository itemSpawnPointGroupDataRepository = ((Il2CppObjectBase)repository).TryCast<ItemSpawnPointGroupDataRepository>();
			if (itemSpawnPointGroupDataRepository == null)
			{
				return;
			}
			AddUnique(lootGroups, itemSpawnPointGroupDataRepository);
			val = itemSpawnPointGroupDataRepository._object?.Hash;
			SingleObjectDataRepository<ItemSpawnPointGroupData>.DataStorage dataStorage = itemSpawnPointGroupDataRepository._object;
			intPtr = ((dataStorage != null) ? ((Il2CppObjectBase)dataStorage).Pointer : System.IntPtr.Zero);
			break;
		}
		case 16:
		{
			if (!(fileName == "bot_presets.json"))
			{
				return;
			}
			BotPresetDataRepository botPresetDataRepository = ((Il2CppObjectBase)repository).TryCast<BotPresetDataRepository>();
			if (botPresetDataRepository == null)
			{
				return;
			}
			AddUnique(bots, botPresetDataRepository);
			val = botPresetDataRepository._data?.Hash;
			MultiObjectDataRepository<BotPresetData, UniqueId>.DataStorage data = botPresetDataRepository._data;
			intPtr = ((data != null) ? ((Il2CppObjectBase)data).Pointer : System.IntPtr.Zero);
			break;
		}
		default:
			return;
		}
		if (seen.Add(((Il2CppObjectBase)repository).Pointer))
		{
			roots.Keep(repository);
			captured.Add(repository);
		}
		string text;
		if (val == null || ((Il2CppArrayBase<byte>)(object)val).Length <= 0)
		{
			long num = ++unversionedLoads;
			text = "load:" + num;
		}
		else
		{
			text = System.Convert.ToHexString((byte[])((Il2CppArrayBase<byte>)(object)val));
		}
		string text2 = text;
		string repositoryIdentity = fileName + "@" + ((Il2CppObjectBase)repository).Pointer.ToInt64().ToString("X");
		text2 = text2 + "@data:" + intPtr.ToInt64().ToString("X");
		if (preparation.Observe(repositoryIdentity, text2))
		{
			Ready = false;
			Reason = preparation.Reason;
			LootProfiles.Invalidate();
			CrateId = UniqueId.Empty;
			ExitTemplate = UniqueId.Empty;
			Trace.Data["dependencyRevision"] = Revision;
			Trace.Data["dependencyInvalidation"] = new
			{
				file = fileName,
				version = text2,
				scope = "native repository load boundary"
			};
			Trace.Data["dependencyRepositoryCount"] = captured.Count;
			Trace.Data["preparationState"] = preparation.Current.ToString();
		}
	}

	internal static void Register(GlobalMapManager manager)
	{
		if (manager == null || ((Il2CppObjectBase)manager).WasCollected)
		{
			return;
		}
		IMapSettingsDataRepository mapSettingsDataRepository = manager._mapSettingsDataRepository;
		MapSettingsDataRepository mapSettingsDataRepository2 = ((mapSettingsDataRepository != null) ? ((Il2CppObjectBase)mapSettingsDataRepository).TryCast<MapSettingsDataRepository>() : null);
		SceneManager sceneManager = manager._sceneManager;
		System.IntPtr intPtr = ((mapSettingsDataRepository2 != null) ? ((Il2CppObjectBase)mapSettingsDataRepository2).Pointer : System.IntPtr.Zero);
		System.IntPtr intPtr2 = ((sceneManager != null) ? ((Il2CppObjectBase)sceneManager).Pointer : System.IntPtr.Zero);
		if (managerVersions.TryGetValue(((Il2CppObjectBase)manager).Pointer, out (System.IntPtr, System.IntPtr, long) value))
		{
			(System.IntPtr, System.IntPtr, long) tuple = value;
			System.IntPtr intPtr3 = intPtr;
			System.IntPtr intPtr4 = intPtr2;
			long revision = Revision;
			if (tuple.Item1 == intPtr3 && tuple.Item2 == intPtr4 && tuple.Item3 == revision)
			{
				return;
			}
		}
		if (mapSettingsDataRepository2 != null)
		{
			ILoadableRepository loadableRepository = ((Il2CppObjectBase)mapSettingsDataRepository2).TryCast<ILoadableRepository>();
			if (loadableRepository != null)
			{
				Capture(loadableRepository, loadBoundary: false);
			}
			AddMap(mapSettingsDataRepository2);
		}
		RegisterScene(sceneManager);
		Ensure();
		managerVersions[((Il2CppObjectBase)manager).Pointer] = (intPtr, intPtr2, Revision);
	}

	internal static void RegisterScene(SceneManager? manager)
	{
		if (manager == null || ((Il2CppObjectBase)manager).WasCollected)
		{
			return;
		}
		SceneDescriptionsContainer sceneDescriptionsContainer = manager._sceneDescriptionsContainer;
		if (sceneDescriptionsContainer == null || ((Il2CppObjectBase)sceneDescriptionsContainer).WasCollected)
		{
			return;
		}
		(System.IntPtr, System.IntPtr) item = (((Il2CppObjectBase)manager).Pointer, ((Il2CppObjectBase)sceneDescriptionsContainer).Pointer);
		if (sceneManagers.Contains(item))
		{
			return;
		}
		SceneDescription sceneDescription = sceneDescriptionsContainer.Find("01_Warehouse");
		if (sceneDescription != null)
		{
			Trace.Data["nativeDonorSceneDescription"] = new
			{
				Name = sceneDescription.Name,
				mainResource = sceneDescription._mainScene?.Name,
				additional = ((System.Collections.Generic.IEnumerable<SceneDescription.SceneLoadingData>)sceneDescription._additionalScenes)?.Select((SceneDescription.SceneLoadingData x) => new
				{
					Name = x.Name,
					source = x.LoadingSourceType.ToString(),
					mode = x.LoadingMode.ToString(),
					IsServerOnly = x.IsServerOnly
				}).ToArray()
			};
		}
		SceneDescription sceneDescription2 = sceneDescriptionsContainer.Find("Frontier_Reserve");
		if (sceneDescription2 != null)
		{
			if (sceneDescription2.Id != 3465004)
			{
				throw new System.InvalidOperationException("Reserve scene identity collision");
			}
			sceneManagers.Add(item);
			return;
		}
		SceneDescription sceneDescription3 = ScriptableObject.CreateInstance<SceneDescription>();
		sceneDescription3._id = 3465004;
		sceneDescription3._name = "Frontier_Reserve";
		sceneDescription3._type = SceneType.Map;
		sceneDescription3.name = "Frontier Reserve";
		sceneDescription3._mainScene = new SceneDescription.SceneLoadingData
		{
			Name = "Frontier_Reserve.unity",
			LoadingSourceType = SceneDescription.LoadingSourceType.InBuild,
			LoadingMode = SceneDescription.LoadingMode.Single,
			IsServerOnly = false
		};
		sceneDescription3._additionalScenes = new Il2CppReferenceArray<SceneDescription.SceneLoadingData>(0L);
		sceneDescription3._settingOverrides = new Il2CppSystem.Collections.Generic.List<ISettings>();
		roots.Keep(sceneDescription3);
		sceneDescriptionsContainer._descriptions.Add(sceneDescription3);
		sceneManagers.Add(item);
		Trace.Add("sceneDescriptionsRegistered");
	}

	internal static bool Ensure()
	{
		bool dependenciesAvailable = maps.Count > 0 && queues.Count > 0 && templates.Count > 0 && bots.Count > 0 && botPoints.Count > 0 && lootPoints.Count > 0 && lootGroups.Count > 0;
		if (!preparation.TryBegin(dependenciesAvailable, out var revision))
		{
			return Ready;
		}
		Ready = false;
		Reason = preparation.Reason;
		Trace.Data["preparationState"] = preparation.Current.ToString();
		Trace.Data["preparationAttempts"] = preparation.AttemptCount;
		try
		{
			foreach (MapSettingsDataRepository map in maps)
			{
				AddMap(map);
			}
			foreach (MatchmakingMapSettingsDataRepository queue in queues)
			{
				AddQueue(queue);
			}
			if (LastMap == null)
			{
				return Complete(revision, "Не найдены настройки локации");
			}
			ValidateReserveExits(LastMap);
			if (!ResolveTemplates())
			{
				return Complete(revision, Reason);
			}
			foreach (BotPresetDataRepository bot in bots)
			{
				UniqueId[] botIds = BotIds;
				for (int i = 0; i < botIds.Length; i++)
				{
					UniqueId dataId = botIds[i];
					if (!bot.TryGetData(dataId, out var data) || data == null)
					{
						Trace.Data["missingBotPreset"] = dataId.ToString();
						return Complete(revision, "Не найден комплект охраны");
					}
				}
			}
			foreach (BotSpawnPointDataRepository botPoint in botPoints)
			{
				AddBots(botPoint);
			}
			foreach (ItemSpawnPointDataRepository lootPoint in lootPoints)
			{
				AddLoot(lootPoint);
			}
			foreach (ItemSpawnPointGroupDataRepository lootGroup in lootGroups)
			{
				LootProfiles.Install(lootGroup);
			}
			Trace.Data["templateBindings"] = new
			{
				crate = CrateId.ToString(),
				exit = ExitTemplate.ToString(),
				botPresets = BotIds.Select((UniqueId id) => id.ToString()).ToArray(),
				lootPoints = Content.Map.Anchors.Count((Anchor a) => a.Kind == "loot"),
				enemyPoints = Content.Map.Anchors.Count((Anchor a) => a.Kind == "enemy")
			};
			return Complete(revision, null);
		}
		catch (System.Exception e)
		{
			Trace.Error("content bindings", e);
			return Complete(revision, "Ошибка подготовки локации");
		}
	}

	private static bool Complete(long revision, string? failure)
	{
		Ready = preparation.Complete(revision, failure) && preparation.Ready;
		Reason = preparation.Reason;
		Trace.Data["preparationState"] = preparation.Current.ToString();
		Trace.Data["preparationFailure"] = failure;
		Trace.Data["preparationRevision"] = revision;
		Trace.Data["preparationAttempts"] = preparation.AttemptCount;
		if (!Loader.Failed && !Loader.InProgress && WorldBridge.ActiveCount == 0)
		{
			Trace.Data["status"] = (Ready ? "READY_FOR_DEPLOYMENT" : "BLOCKED_BY_DEPENDENCIES");
		}
		Trace.Save(force: true);
		return Ready;
	}

	private static T? Component<T>(Item item) where T : Il2CppObjectBase
	{
		return TemplateContract.Component<T>(item);
	}

	private static bool ResolveTemplates()
	{
		if (!LootProfiles.Resolve(lootGroups[0], templates[0]))
		{
			Reason = "Не найдены совместимые штатные профили контейнерного лута";
			return false;
		}
		Il2CppSystem.Collections.Generic.IReadOnlyList<Item> allData = templates[0].GetAllData();
		int num = Identity.Count(allData);
		System.Collections.Generic.List<(Item, float)> list = new System.Collections.Generic.List<(Item, float)>();
		for (int i = 0; i < num; i++)
		{
			Item item = allData[i];
			if (item != null)
			{
				ExitZoneComponent exitZoneComponent = Component<ExitZoneComponent>(item);
				if (exitZoneComponent != null && exitZoneComponent.TimeToExit > 0f && exitZoneComponent.TimeToExit <= 60f && Component<BaseComponent>(item) != null)
				{
					list.Add((item, exitZoneComponent.TimeToExit));
				}
			}
		}
		Trace.Add("templateDiscoveryPasses");
		Trace.Data["bindingCandidates"] = new
		{
			totalTemplates = num,
			crates = LootProfiles.Current.Select((LootProfiles.Profile p) => p.Template).Distinct().Count(),
			exits = list.Count
		};
		if (list.Count == 0)
		{
			Reason = "Не найдены игровые выходы";
			return false;
		}
		(Item, float) tuple = list.OrderBy<(Item, float), float>(((Item Item, float Seconds) e) => System.Math.Abs(e.Seconds - 10f)).ThenBy<(Item, float), string>(((Item Item, float Seconds) e) => e.Item.Id.ToString(), System.StringComparer.Ordinal).First();
		CrateId = LootProfiles.Current[0].Template;
		ExitTemplate = tuple.Item1.Id;
		Trace.Data["crateVisual"] = LootProfiles.Current[0].Visual;
		Trace.Data["exitSeconds"] = tuple.Item2;
		Trace.Data["containerInteractionContract"] = "Native stationary container Roots/RootChildren; the general InteractableComponent is not the container-inventory interaction contract";
		return ValidateResolvedTemplates();
	}

	internal static bool ValidateResolvedTemplates()
	{
		if (LootProfiles.Current.Length == 0 || ExitTemplate == UniqueId.Empty)
		{
			return false;
		}
		foreach (ItemTemplatesRepository template in templates)
		{
			if (((Il2CppObjectBase)template).WasCollected)
			{
				throw new System.InvalidOperationException("Native item repository was collected");
			}
			foreach (UniqueId item in LootProfiles.Current.Select((LootProfiles.Profile p) => p.Template).Distinct())
			{
				if (!template.TryGetData(item, out var data) || data == null)
				{
					throw new System.InvalidOperationException("Container template missing in a client/server repository");
				}
				TemplateContract.Inspection inspection = TemplateContract.Inspect(data);
				if (inspection.Rejection != null)
				{
					throw new System.InvalidOperationException("Container contract changed: " + inspection.Rejection);
				}
			}
			if (!template.TryGetData(ExitTemplate, out var data2) || data2 == null)
			{
				throw new System.InvalidOperationException("Exit template missing in a client/server repository");
			}
			ExitZoneComponent exitZoneComponent = Component<ExitZoneComponent>(data2);
			if (exitZoneComponent == null || exitZoneComponent.TimeToExit <= 0f || exitZoneComponent.TimeToExit > 60f)
			{
				throw new System.InvalidOperationException("Exit template contract changed");
			}
		}
		return true;
	}

	private static IItemsFilter OpenReserveExitRequirement()
	{
		return ((Il2CppObjectBase)roots.Keep(new TrueFilter())).TryCast<IItemsFilter>() ?? throw new System.InvalidOperationException("Native exit requirement interface missing");
	}

	private static void ValidateReserveExits(MapSettingsData map)
	{
		if (map.MapId != 3465004 || map.MapName != "Frontier_Reserve" || map.MaxExitZonesPerPlayer != 2 || map.ExitZoneOverrides == null || map.ExitZoneOverrides.Count != 1)
		{
			throw new System.InvalidOperationException("Reserve exit assignment settings changed");
		}
		MapSettingsData.ExitZonePresetData exitZonePresetData = map.ExitZoneOverrides[0];
		if (exitZonePresetData == null || exitZonePresetData.UnlockRequirement == null || ((Il2CppObjectBase)exitZonePresetData.UnlockRequirement).TryCast<TrueFilter>() == null || exitZonePresetData.ExitZoneIds == null || exitZonePresetData.ExitZoneIds.Count != 2)
		{
			throw new System.InvalidOperationException("Reserve exit preset requires a real native unconditional filter");
		}
		HashSet<int> hashSet = (from a in Content.Map.Anchors
			where a.Kind == "exit"
			select Identity.Ordinal(a, Content.Map)).ToHashSet();
		for (int num = 0; num < exitZonePresetData.ExitZoneIds.Count; num++)
		{
			if (!hashSet.Remove(exitZonePresetData.ExitZoneIds[num]))
			{
				throw new System.InvalidOperationException("Exit preset ID mismatch");
			}
		}
		if (hashSet.Count != 0)
		{
			throw new System.InvalidOperationException("Exit preset incomplete");
		}
		Il2CppSystem.Collections.Generic.List<IItem> list = new Il2CppSystem.Collections.Generic.List<IItem>();
		using (new NativeRoot((Il2CppObjectBase)(object)list))
		{
			if (!exitZonePresetData.UnlockRequirement.IsCompatible(list, FilterPolicy.WithAny))
			{
				throw new System.InvalidOperationException("Native Reserve exit requirement rejected empty equipment");
			}
			Trace.Data["nativeExitAssignmentContract"] = new
			{
				exits = 2,
				filter = "Game.ItemsSystem.ItemFilters.TrueFilter",
				nativeInterfaceReadback = true,
				unrelatedMapConditionsChanged = false
			};
		}
	}

	private static void AddMap(MapSettingsDataRepository repo)
	{
		if (repo.TryGetData(3465004, out var data) && data != null)
		{
			if (data.MapName != "Frontier_Reserve")
			{
				throw new System.InvalidOperationException("Reserve map ID collision");
			}
			LastMap = data;
		}
		else
		{
			if (!repo.TryGetDataByMapName("01_Warehouse", out var mapSettings) || mapSettings == null)
			{
				return;
			}
			MapSettingsData mapSettingsData = new MapSettingsData
			{
				MapId = 3465004,
				MapName = "Frontier_Reserve",
				Type = MapSettingsData.MapType.None,
				MainTimePhaseDuration = 1800f,
				ExtraTimePhaseDuration = 300f,
				MaxExitZonesPerPlayer = 2,
				ExitZonesAssignMode = MapSettingsData.ExitZoneAssignMode.Default,
				EquipmentReturn = mapSettings.EquipmentReturn,
				ShowOnGlobalMap = false,
				NotAvailableToDeploy = false,
				UnlockRequirements = null,
				LocalizedName = Text(3465000000000012200L),
				LocalizedDescription = Text(3465000000000012201L),
				UnlockedShortText = Text(3465000000000012200L),
				LockedShortText = Text(3465000000000012200L),
				IconAtlasName = mapSettings.IconAtlasName,
				IconSpriteName = mapSettings.IconSpriteName,
				NavigationSystemConfiguration = "Frontier.Reserve.Navigation",
				GlobalMapSettings = new MapSettingsData.GlobalMapSettingsData
				{
					LocalizedName = Text(3465000000000012200L),
					Position = new Vector3Data
					{
						X = 0f,
						Y = 0f,
						Z = 0f
					},
					IsAvailableOutOfDevelopmentMode = true,
					IsAvailableInDemonstrationMode = true,
					ShowOnlyInEditor = false
				},
				SpawnPointOverrides = new Il2CppSystem.Collections.Generic.List<MapSettingsData.SpawnPointPresetData>(),
				ExitZoneOverrides = new Il2CppSystem.Collections.Generic.List<MapSettingsData.ExitZonePresetData>(),
				Scripts = new Il2CppSystem.Collections.Generic.List<MapSettingsData.MapScriptSettingsData>()
			};
			Il2CppSystem.Collections.Generic.List<int> list = new Il2CppSystem.Collections.Generic.List<int>();
			foreach (Anchor item in Content.Map.Anchors.Where((Anchor a) => a.Kind == "exit"))
			{
				list.Add(Identity.Ordinal(item, Content.Map));
			}
			mapSettingsData.ExitZoneOverrides.Add(new MapSettingsData.ExitZonePresetData
			{
				ExitZoneIds = list,
				MaxActiveExitZonesCount = new Il2CppSystem.Nullable<int>(2),
				UnlockRequirement = OpenReserveExitRequirement()
			});
			Il2CppSystem.Collections.Generic.IReadOnlyList<MapSettingsData> allData = repo.GetAllData();
			Il2CppSystem.Collections.Generic.List<MapSettingsData> list2 = new Il2CppSystem.Collections.Generic.List<MapSettingsData>();
			for (int num = 0; num < Identity.Count(allData, 256); num++)
			{
				list2.Add(allData[num]);
			}
			list2.Add(mapSettingsData);
			repo.SetLoadedData(list2, Identity.Salt(repo._data?.Hash, "map"));
			LastMap = roots.Keep(mapSettingsData);
			Trace.Add("mapRepositoriesRegistered");
		}
	}

	private static void AddQueue(MatchmakingMapSettingsDataRepository repo)
	{
		if ((!repo.TryGetData("Frontier_Reserve", out var data) || data == null) && repo.TryGetData("01_Warehouse", out var data2) && data2 != null)
		{
			MatchmakingMapSettingsData matchmakingMapSettingsData = new MatchmakingMapSettingsData
			{
				MapName = "Frontier_Reserve",
				NonBotPlayersCounts = data2.NonBotPlayersCounts,
				WaitDurations = data2.WaitDurations
			};
			Il2CppSystem.Collections.Generic.IReadOnlyList<MatchmakingMapSettingsData> allData = repo.GetAllData();
			Il2CppSystem.Collections.Generic.List<MatchmakingMapSettingsData> list = new Il2CppSystem.Collections.Generic.List<MatchmakingMapSettingsData>();
			for (int i = 0; i < Identity.Count(allData, 256); i++)
			{
				list.Add(allData[i]);
			}
			list.Add(matchmakingMapSettingsData);
			repo.SetLoadedData(list, Identity.Salt(repo._data?.Hash, "queue"));
			roots.Keep(matchmakingMapSettingsData);
			Trace.Add("queueRepositoriesRegistered");
		}
	}

	private static void AddBots(BotSpawnPointDataRepository repo)
	{
		Anchor[] array = Content.Map.Anchors.Where((Anchor a) => a.Kind == "enemy").ToArray();
		if (array.Length != 18)
		{
			throw new System.InvalidOperationException("Reserve enemy placement contract changed");
		}
		if (!repo.TryGetData(Identity.Id(array[0].Id), out var data) || data == null)
		{
			Il2CppSystem.Collections.Generic.IReadOnlyList<BotSpawnPointData> allData = repo.GetAllData();
			Il2CppSystem.Collections.Generic.List<BotSpawnPointData> list = new Il2CppSystem.Collections.Generic.List<BotSpawnPointData>();
			using (new NativeRoot((Il2CppObjectBase)(object)list))
			{
				for (int num = 0; num < Identity.Count(allData); num++)
				{
					list.Add(allData[num]);
				}
				for (int num2 = 0; num2 < array.Length; num2++)
				{
					BotSpawnPointData item = new BotSpawnPointData
					{
						Id = Identity.Id(array[num2].Id),
						LeveledEquipments = NativeBotEquipment.Create(BotIds[num2 % BotIds.Length])
					};
					list.Add(item);
				}
				repo.SetLoadedData(list, Identity.Salt(repo._data?.Hash, "bots"));
				Trace.Add("botSpawnRepositoriesRegistered");
			}
		}
		for (int num3 = 0; num3 < array.Length; num3++)
		{
			if (!repo.TryGetData(Identity.Id(array[num3].Id), out var data2) || data2 == null || data2.Id != Identity.Id(array[num3].Id))
			{
				throw new System.InvalidOperationException("Native bot spawn repository binding missing: " + array[num3].Id);
			}
			NativeBotEquipment.Validate(data2.LeveledEquipments, BotIds[num3 % BotIds.Length]);
		}
		Trace.Data["nativeBotEquipmentContract"] = new
		{
			points = array.Length,
			valueBytes = 24,
			writer = "Native List<LeveledEquipment>.Add with unboxed value",
			validation = "Boxed native get_Item; null filter and exact nonempty preset ID; published repository read-back",
			sharedEnemyHooksChanged = false
		};
	}

	private static void AddLoot(ItemSpawnPointDataRepository repo)
	{
		Anchor[] array = Content.Map.Anchors.Where((Anchor a) => a.Kind == "loot").ToArray();
		if (!repo.TryGetData(Identity.Id(array[0].Id), out var data) || data == null)
		{
			Il2CppSystem.Collections.Generic.IReadOnlyList<ItemSpawnPointData> allData = repo.GetAllData();
			Il2CppSystem.Collections.Generic.List<ItemSpawnPointData> list = new Il2CppSystem.Collections.Generic.List<ItemSpawnPointData>();
			int num = Identity.Count(allData);
			for (int num2 = 0; num2 < num; num2++)
			{
				list.Add(allData[num2]);
			}
			Anchor[] array2 = array;
			foreach (Anchor anchor in array2)
			{
				list.Add(new ItemSpawnPointData
				{
					SpawnPointId = Identity.Id(anchor.Id),
					SpawnPointName = "Frontier_Reserve-" + anchor.Id,
					Chance = 0f,
					Personal = false,
					Item = null
				});
			}
			repo.SetLoadedData(list, Identity.Salt(repo._data?.Hash, "loot"));
			Trace.Data["lootDonorPolicy"] = "Native container groups own root and child generation; individual points do not generate duplicate loose items";
			Trace.Add("lootRepositoriesRegistered");
		}
	}
}
