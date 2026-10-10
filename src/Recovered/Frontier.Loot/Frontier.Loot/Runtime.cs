using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using BepInEx;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using Core.Utils;
using Frontier.Modular;
using Game.AISystem;
using Game.Character;
using Game.DissectSystem.Processes;
using Game.GameModes;
using Game.InteractionSystem.ItemOperations;
using Game.ItemsSystem;
using Game.ItemsSystem.ItemOperations;
using Game.SpawnPointSystem.BotSpawnPoints.Data;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppSystem.Collections.Generic;
using Unity.Entities;

namespace Frontier.Loot;

public static class Runtime
{
	internal sealed class CreationContext
	{
		internal readonly string Rank;

		internal readonly string Key;

		internal readonly bool Dissection;

		internal readonly bool NativeBotAssembly;

		internal readonly HashSet<IntPtr> Created = new HashSet<IntPtr>();

		internal readonly System.Collections.Generic.Dictionary<string, int> Occurrences = new System.Collections.Generic.Dictionary<string, int>();

		internal CreationContext(string rank, string key, bool dissect, bool nativeBotAssembly = false)
		{
			Rank = rank;
			Key = key;
			Dissection = dissect;
			NativeBotAssembly = nativeBotAssembly;
		}
	}

	public const string Adapter = "rarity-loot-v1.2-fieldmap-summary";

	internal static LootDatabase Db = null;

	internal static ManualLogSource Log = null;

	internal static bool Enabled;

	internal static bool CombatEnabled;

	private static bool installed;

	private static readonly System.Collections.Generic.Dictionary<IntPtr, (string Target, string Rank)> dissectOperations = new System.Collections.Generic.Dictionary<IntPtr, (string, string)>();

	private static readonly System.Collections.Generic.Queue<IntPtr> dissectOrder = new System.Collections.Generic.Queue<IntPtr>();

	[ThreadStatic]
	private static CreationContext? creation;

	private const bool botTemplateInputSuspended = false;

	private static readonly object sync = new object();

	internal static readonly System.Collections.Generic.Dictionary<string, long> Counters = new System.Collections.Generic.Dictionary<string, long>();

	internal static readonly System.Collections.Generic.List<string> Errors = new System.Collections.Generic.List<string>();

	private static double nextReport;

	internal static bool InNativeBotAssembly => creation?.NativeBotAssembly ?? false;

	internal static CreationContext? CurrentCreation
	{
		get
		{
			return creation;
		}
		set
		{
			creation = value;
		}
	}

	private static object? Read(object obj, string name)
	{
		Type type = obj.GetType();
		object obj2 = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(obj);
		if (obj2 == null)
		{
			PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if ((object)property == null)
			{
				return null;
			}
			obj2 = property.GetValue(obj);
		}
		return obj2;
	}

	public static void Install(object plugin)
	{
		//IL_0262: Unknown result type (might be due to invalid IL or missing references)
		//IL_0269: Expected O, but got Unknown
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Expected O, but got Unknown
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Expected O, but got Unknown
		//IL_021c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0221: Unknown result type (might be due to invalid IL or missing references)
		//IL_0230: Expected O, but got Unknown
		if (installed)
		{
			return;
		}
		Log = (ManualLogSource)(Read(plugin, "Log") ?? Logger.CreateLogSource("Frontier Loot"));
		string text = ModuleRuntime.RuntimeRoot(typeof(Runtime).Assembly.Location);
		JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(text, "compiled", "rarity-runtime.json")));
		string text2 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Runtime).Assembly.Location))).ToLowerInvariant();
		if (jsonDocument.RootElement.GetProperty("sha256").GetString() != text2)
		{
			throw new InvalidDataException("Frontier.Loot.dll hash differs from the co-op manifest. Install the complete ZIP on every PC.");
		}
		Db = LootDatabase.Load(Path.Combine(text, "compiled", "loot-rarity.json"));
		RankLadder.Load(text, Db);
		bool flag = (bool)(Read(plugin, "Active") ?? ((object)false));
		object obj = Read(plugin, "Engine") ?? throw new InvalidOperationException("Frontier engine not ready");
		Enabled = ((!flag) ? ModuleRuntime.RollsGate(false) : ((bool)obj.GetType().GetMethod("IsEnabled").Invoke(obj, new object[1] { "affixes" })));
		CombatEnabled = flag && Db.Settings.GetValueOrDefault("combat", defaultValue: true);
		Harmony val = new Harmony("frontier.cargohunters.loot050");
		if (flag)
		{
			Patch(val, typeof(AiCharacterSpawnSystem), "GetPlayerEquipment", 3, "BotPrefix", "BotOutcome", "CreationFinalizer");
			Patch(val, typeof(SpawnItemsDissectResult), "CreateItemOperation", 3, null, "DissectOperationPostfix");
			Patch(val, typeof(SpawnItemsFromPresetOperation), "Execute", 1, "SpawnPresetPrefix", null, "CreationFinalizer");
			CompletedGeneration.Install(val);
			AliasExpansionHook.Install(val);
			val.Patch((MethodBase)RankHookPlan.Factory(), (HarmonyMethod)null, new HarmonyMethod(typeof(Runtime).GetMethod("FactoryPostfix", BindingFlags.Static | BindingFlags.NonPublic))
			{
				priority = 0
			}, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			Combat.Install(val);
			try
			{
				WeaponSummary.Install(val, text);
			}
			catch (Exception e)
			{
				Error("ui.install", e);
			}
		}
		installed = true;
		ManualLogSource log = Log;
		bool flag2 = default(bool);
		BepInExInfoLogInterpolatedStringHandler val2 = new BepInExInfoLogInterpolatedStringHandler(72, 5, out flag2);
		if (flag2)
		{
			((BepInExLogInterpolatedStringHandler)val2).AppendLiteral("Loot adapter: ");
			((BepInExLogInterpolatedStringHandler)val2).AppendFormatted<string>("rarity-loot-v1.2-fieldmap-summary-modular");
			((BepInExLogInterpolatedStringHandler)val2).AppendLiteral("; ");
			((BepInExLogInterpolatedStringHandler)val2).AppendFormatted<int>(Db.Families.Count);
			((BepInExLogInterpolatedStringHandler)val2).AppendLiteral(" native families / ");
			((BepInExLogInterpolatedStringHandler)val2).AppendFormatted<int>(Db.Variants.Count);
			((BepInExLogInterpolatedStringHandler)val2).AppendLiteral(" persistent variants; rolls=");
			((BepInExLogInterpolatedStringHandler)val2).AppendFormatted<bool>(Enabled);
			((BepInExLogInterpolatedStringHandler)val2).AppendLiteral("; combat=");
			((BepInExLogInterpolatedStringHandler)val2).AppendFormatted<bool>(CombatEnabled);
		}
		log.LogInfo(val2);
		WriteReport();
	}

	internal static void Patch(Harmony h, Type t, string name, int arity, string? prefix = null, string? postfix = null, string? finalizer = null, Type? owner = null)
	{
		MethodInfo methodInfo = t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Single((MethodInfo x) => x.Name == name && x.GetParameters().Length == arity);
		h.Patch((MethodBase)methodInfo, HM(prefix), HM(postfix), (HarmonyMethod)null, HM(finalizer), (HarmonyMethod)null);
		HarmonyMethod? HM(string? n)
		{
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Expected O, but got Unknown
			if (n != null)
			{
				return new HarmonyMethod((owner ?? typeof(Runtime)).GetMethod(n, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
				{
					priority = 0
				};
			}
			return null;
		}
	}

	private static void BotPrefix(AiCharacterSpawnSystem __instance, BotPresetData __1, out CreationContext? __state)
	{
		__state = creation;
		creation = new CreationContext((Enabled && __1 != null) ? RankLadder.CreationRank(__1) : "normal", "", dissect: false, nativeBotAssembly: true);
		Inc("botEquipmentCalls", 1L);
		Inc("nativeBotAssemblyScopes", 1L);
	}

	private static void DissectOperationPostfix(IItem __2, AbstractItemOperation __result)
	{
		if (!Enabled || __2 == null || __result == null)
		{
			return;
		}
		try
		{
			IntPtr pointer = ((Il2CppObjectBase)__result).Pointer;
			if (!dissectOperations.ContainsKey(pointer))
			{
				dissectOrder.Enqueue(pointer);
			}
			dissectOperations[pointer] = (Id(__2.Id), Db.PlatformRanks.GetValueOrDefault(Id(__2.TemplateId), "normal"));
			while (dissectOrder.Count > 4096)
			{
				dissectOperations.Remove(dissectOrder.Dequeue());
			}
			Inc("dissectionOperationsRegistered", 1L);
		}
		catch (Exception e)
		{
			Error("dissection.register", e);
		}
	}

	private static void SpawnPresetPrefix(SpawnItemsFromPresetOperation __instance, ItemsOperationContext __0, out CreationContext? __state)
	{
		__state = creation;
		CreationContext? creationContext = creation;
		if ((creationContext != null && creationContext.NativeBotAssembly) || !Enabled || __0 == null || !__0.ItemsManager.IsAuthoritative)
		{
			return;
		}
		try
		{
			if (dissectOperations.TryGetValue(((Il2CppObjectBase)__instance).Pointer, out (string, string) value) && !(value.Item1 != Id(__instance._itemId)))
			{
				creation = new CreationContext(value.Item2, value.Item1, dissect: true);
				Inc("dissectionCalls", 1L);
			}
			else
			{
				creation = new CreationContext("normal", Id(__instance._itemId), dissect: false);
				Inc("worldLootSpawnScopes", 1L);
			}
		}
		catch (Exception e)
		{
			Error("dissection.execute", e);
		}
	}

	private static Exception? CreationFinalizer(Exception? __exception, CreationContext? __state)
	{
		creation = __state;
		return __exception;
	}

	private static void BotOutcome(AiCharacterSpawnSystem __instance, Equipment __result)
	{
		CreationContext creationContext = creation;
		if (creationContext == null || !creationContext.NativeBotAssembly || __result == null || __instance?._battleSession == null)
		{
			return;
		}
		try
		{
			IItemsManager itemsManager = __instance._battleSession.ItemsManager;
			if (itemsManager != null && itemsManager.IsAuthoritative)
			{
				Il2CppSystem.Collections.Generic.List<IItem> list = new Il2CppSystem.Collections.Generic.List<IItem>();
				__result.GatherItems(list, recursively: true);
				LateRankBinding.Apply(itemsManager, list, creationContext.Created, creationContext.Rank, creationContext.Dissection, "bot");
				Inc("nativeCompletedEquipmentsRanked", 1L);
			}
		}
		catch (Exception e)
		{
			Error("completed-equipment-rank", e);
		}
	}

	private static void FactoryPostfix(IItem __result)
	{
		if (__result == null)
		{
			return;
		}
		CreationContext creationContext = creation;
		if (creationContext != null)
		{
			if (creationContext.Created.Count < 8192)
			{
				creationContext.Created.Add(((Il2CppObjectBase)__result).Pointer);
			}
			if (creationContext.NativeBotAssembly)
			{
				Inc("nativeBotFactoryResultsPreserved", 1L);
			}
		}
	}

	internal static string Id(UniqueId id)
	{
		if (!Guid.TryParse(id.ToString(), out var result))
		{
			return id.ToString();
		}
		return result.ToString("D");
	}

	internal static BattleSession? Session(SystemBase s)
	{
		World world = s.World;
		if (world == null || !world.IsCreated)
		{
			return null;
		}
		CharacterSpawnHandlerSystem existingSystem = world.GetExistingSystem<CharacterSpawnHandlerSystem>();
		if (existingSystem == null || !existingSystem._dependencyInitialized)
		{
			return null;
		}
		return existingSystem._battleSession;
	}

	internal static bool Human(BattleSession s, int id)
	{
		if (!s.IsAuthoritative)
		{
			return id == s.GetLocalPlayerId();
		}
		if (!s.IsBotPlayer(id))
		{
			return s.GetPlayerAccountId(id).HasValue;
		}
		return false;
	}

	internal static Bonuses Aggregate(BattleSession s, int player, UniqueId weaponId)
	{
		if (RankLadder.TryBotCache(s, player, weaponId, out Bonuses value))
		{
			return value;
		}
		Bonuses total = WeaponSummary.Collect(s.ItemsManager.Find(weaponId)).Total;
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		Equipment equipment = s.FindEquipmentByPlayerId(player);
		if (equipment != null)
		{
			Il2CppSystem.Collections.Generic.List<EquipmentSlotType> bodyPartSlots = equipment.BodyPartSlots;
			for (int i = 0; i < bodyPartSlots.Count; i++)
			{
				IItem itemFromSlot = equipment.GetItemFromSlot(bodyPartSlots[i]);
				if (itemFromSlot != null && !itemFromSlot.IsBroken && hashSet.Add(Id(itemFromSlot.Id)) && Db.Variants.TryGetValue(Id(itemFromSlot.TemplateId), out Trait value2) && value2.Category == 13)
				{
					total.Add(RankLadder.Resolve(itemFromSlot, value2));
				}
			}
		}
		RankLadder.FinishAggregate(s, player, total);
		RankLadder.StoreBotCache(s, player, weaponId, total);
		return total;
	}

	internal static void Inc(string name, long amount = 1L)
	{
		lock (sync)
		{
			Counters[name] = Counters.GetValueOrDefault(name) + amount;
		}
	}

	internal static void Error(string area, Exception e)
	{
		string text = area + ": " + e.ToString();
		lock (sync)
		{
			Counters["errors." + area] = Counters.GetValueOrDefault("errors." + area) + 1;
			if (!Errors.Contains(text) && Errors.Count < 32)
			{
				Errors.Add(text);
				Log.LogWarning((object)("Loot " + text));
			}
		}
	}

	internal static void ReportTick()
	{
		double num = (double)Environment.TickCount64 / 1000.0;
		if (!(num < nextReport))
		{
			nextReport = num + 5.0;
			WriteReport();
		}
	}

	internal static void WriteReport()
	{
		try
		{
			lock (sync)
			{
				string text = Path.Combine(Paths.BepInExRootPath, "Frontier-loot-report.json");
				string text2 = text + ".tmp";
				File.WriteAllText(text2, JsonSerializer.Serialize(new
				{
					version = "0.6.0",
					adapter = "rarity-loot-v1.2-fieldmap-summary-modular",
					rollsEnabled = Enabled,
					combatEnabled = CombatEnabled,
					families = Db.Families.Count,
					variants = Db.Variants.Count,
					rank28Active = RankLadder.Active,
					startupRevision = "v10.8.12",
					botEquipmentPolicy = "completed native generation; same object and ID; rank template identity bound only after native assembly",
					botFactoryTemplateReplacementEnabled = false,
					factoryTemplateInputSelectionEnabled = false,
					factoryResultSubstitutionEnabled = false,
					nestedDefaultPartsRanked = false,
					assemblyRecipePartsRanked = false,
					assemblyRecipeGuard = "No rank selection until whole native equipment/generation returns successfully",
					completedGenerationRankBinding = true,
					botFactoryResultsReplaced = 0,
					newItemRanksDuringBotAssembly = false,
					newItemRanksAfterNativeAssembly = (Enabled && RankLadder.Active),
					botInputSelectionSuspended = false,
					nativeBotRetryByPlugin = false,
					completedRankAssignments = LateRankBinding.Snapshot(),
					nativePresetRankedItemsRemainAllowed = true,
					enemyAIRanksEnabled = RankLadder.Active,
					dissectionRarityEnabled = Enabled,
					recoveryScope = "completed bot equipment and completed authoritative world generation; saved items excluded",
					nativeBotRecipePreserved = true,
					legacyRankRollFallback = false,
					technicalCardText = false,
					aliasFilterRepair = AliasExpansionHook.Snapshot(),
					dtoFactoryHooksInstalled = false,
					factoryHookPolicy = "exact CreateInstance(repository, UniqueId, UniqueId); DTO overloads excluded",
					rank28Families = (RankLadder.Data?.Families.Count ?? 0),
					rank28RollPolicy = "frontier-rank28-instance-v1",
					balanceRevision = InstanceStats.Configuration?.Revision,
					balanceAnchorRank = "S3",
					legacyAnchor = "L3",
					balancedFamilies = (InstanceStats.Configuration?.Families.Count ?? 0),
					rank28NativeGameVerified = false,
					openCombatCircuits = Combat.OpenCircuits(),
					ecsFieldMaps = EcsAccess.Report(),
					weaponSummary = WeaponSummary.Status(),
					counters = Counters,
					errors = Errors,
					nativePlaytestClaim = false
				}, new JsonSerializerOptions
				{
					WriteIndented = true
				}));
				File.Move(text2, text, overwrite: true);
			}
		}
		catch (Exception ex)
		{
			Log.LogWarning((object)("Loot report: " + ex.Message));
		}
	}
}
