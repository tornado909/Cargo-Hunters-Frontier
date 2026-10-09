using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Core.DataRepository;
using Core.Utils;
using Frontier.Modular;
using Game.AISystem;
using Game.AttributeSystem;
using Game.AttributeSystem.AttributeModification;
using Game.GameModes;
using Game.ItemsSystem;
using Game.ItemsSystem.ItemComponents;
using Game.SpawnPointSystem.BotSpawnPoints;
using Game.SpawnPointSystem.BotSpawnPoints.Data;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppSystem.Collections.Generic;

namespace Frontier.Loot;

public static class RankLadder
{
	private sealed class SpawnSelection
	{
		internal BotPresetData Preset;

		internal int Rank;

		internal string RankedId = "";

		internal string BaseId = "";

		internal int EquipmentEntries;

		internal string EquipmentId = "";

		internal string PlatformId = "";

		internal string LootId = "";
	}

	private static readonly Dictionary<string, Trait> Cache = new Dictionary<string, Trait>(StringComparer.Ordinal);

	private static readonly Queue<string> CacheOrder = new Queue<string>();

	private static readonly Dictionary<long, (string Owner, long Data)> NativeApplied = new Dictionary<long, (string, long)>();

	private static readonly Dictionary<(long Session, int Player, string Weapon), (long Until, Bonuses Value)> BotBonusCache = new Dictionary<(long, int, string), (long, Bonuses)>();

	private static readonly Dictionary<string, int> EquipmentRanks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

	private static readonly HashSet<string> SpawnIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private static readonly Dictionary<long, SpawnSelection> SelectedSpawns = new Dictionary<long, SpawnSelection>();

	private static readonly Dictionary<string, int> RankById = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

	private static string salt = Guid.NewGuid().ToString("N");

	private static bool active;

	[ThreadStatic]
	private static bool building;

	private static Type? presentation;

	private static Type? rankStyle;

	public static LadderData? Data { get; private set; }

	public static bool Active => active;

	public static void Load(string root, LootDatabase db)
	{
		if (!ModuleRuntime.Feature("rarity-ladder"))
		{
			return;
		}
		Data = JsonSerializer.Deserialize<LadderData>(File.ReadAllText(Path.Combine(root, "modules", "rarity-ladder", "rank28.json")), new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		}) ?? throw new InvalidDataException("Rank28 metadata missing");
		if (Data.Schema != 1 || Data.Version != "rank28-v1")
		{
			throw new InvalidDataException("Unknown instance rank policy");
		}
		BalanceConfiguration balanceConfiguration = BalanceConfiguration.Load(Path.Combine(root, "modules", "rarity-ladder", "legacy-l3-s3.json"));
		if (balanceConfiguration.Families.Count != Data.Families.Count)
		{
			throw new InvalidDataException("Balance anchor coverage mismatch");
		}
		foreach (KeyValuePair<string, string[]> family in Data.Families)
		{
			if (!balanceConfiguration.Families.TryGetValue(family.Key, out BalanceProfile value) || !db.Families.TryGetValue(family.Key, out Family value2) || value2.Variants[11] != value.AnchorTemplate || value2.Category != value.Category || value2.SubCategory != value.SubCategory)
			{
				throw new InvalidDataException("Legacy L3 anchor identity mismatch");
			}
		}
		InstanceStats.Configure(balanceConfiguration);
		RankPolicy.Pick(0.5, Data.OrdinaryWeights);
		RankPolicy.Pick(0.5, Data.EnemyWeights);
		foreach (KeyValuePair<string, string[]> family2 in Data.Families)
		{
			if (family2.Value.Length != 28 || !db.Families.ContainsKey(family2.Key))
			{
				throw new InvalidDataException("Invalid 28-entry family");
			}
			for (int i = 0; i < 28; i++)
			{
				if (!db.Variants.TryGetValue(family2.Value[i], out Trait value3) || value3.Base != family2.Key || value3.RankIndex != i + 1)
				{
					throw new InvalidDataException("Rank family index mismatch");
				}
			}
		}
		string[] spawnAllowlist = Data.SpawnAllowlist;
		foreach (string item in spawnAllowlist)
		{
			SpawnIds.Add(item);
		}
		foreach (KeyValuePair<string, int> botRank in Data.BotRanks)
		{
			RankById[botRank.Key] = botRank.Value;
		}
		Runtime.Inc("rank28Families", Data.Families.Count);
	}

	public static string Select(string baseId, string key, string context, out int tier)
	{
		tier = 0;
		if (Data == null || !Data.Families.TryGetValue(baseId, out string[] value))
		{
			return baseId;
		}
		double sample = LootMath.Sample(key, 0u, "rank28-quality-v1");
		int num = RankPolicy.Parse(context);
		double[] array = Data.OrdinaryWeights;
		if (num > 0)
		{
			array = new double[28];
			double num2 = Math.Max(1, num - 3);
			double num3 = 3.2;
			for (int i = 0; i < 28; i++)
			{
				array[i] = 0.12 * Data.OrdinaryWeights[i] + 0.88 * Math.Exp((0.0 - Math.Pow(((double)(i + 1) - num2) / num3, 2.0)) / 2.0);
			}
		}
		tier = RankPolicy.Pick(sample, array);
		return value[tier - 1];
	}

	public static Trait Resolve(IItem item, Trait t)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		if (t.RankIndex <= 0 || Data == null)
		{
			return t;
		}
		return Resolve(t, Runtime.Id(item.Id));
	}

	public static Trait Resolve(Trait t, string id)
	{
		if (t.RankIndex <= 0)
		{
			return t;
		}
		string text = id + "|" + t.Base + "|" + t.RankIndex;
		lock (Cache)
		{
			if (Cache.TryGetValue(text, out Trait value))
			{
				return value;
			}
			Trait trait = Roll(t, id);
			Cache[text] = trait;
			CacheOrder.Enqueue(text);
			while (CacheOrder.Count > 16384)
			{
				Cache.Remove(CacheOrder.Dequeue());
			}
			return trait;
		}
	}

	public static Trait Roll(Trait t, string id)
	{
		return InstanceStats.Roll(t, id);
	}

	public static bool NewItem(IItem? item)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		if (item != null && Runtime.Db != null && Runtime.Db.Variants.TryGetValue(Runtime.Id(item.TemplateId), out Trait value))
		{
			return value.RankIndex > 0;
		}
		return false;
	}

	public static void Materialize(IItem? item)
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Expected O, but got Unknown
		//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d2: Expected O, but got Unknown
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		//IL_016f: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Expected O, but got Unknown
		//IL_020d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0214: Expected O, but got Unknown
		//IL_0222: Unknown result type (might be due to invalid IL or missing references)
		//IL_0315: Unknown result type (might be due to invalid IL or missing references)
		//IL_031a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0327: Expected O, but got Unknown
		//IL_02ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0302: Unknown result type (might be due to invalid IL or missing references)
		//IL_0314: Expected O, but got Unknown
		//IL_0357: Unknown result type (might be due to invalid IL or missing references)
		//IL_025c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0261: Unknown result type (might be due to invalid IL or missing references)
		//IL_0267: Unknown result type (might be due to invalid IL or missing references)
		//IL_0272: Unknown result type (might be due to invalid IL or missing references)
		//IL_0279: Expected O, but got Unknown
		//IL_02a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c6: Expected O, but got Unknown
		if (item == null || !NewItem(item) || building || Runtime.InNativeBotAssembly)
		{
			return;
		}
		Item val = ((Il2CppObjectBase)item).TryCast<Item>();
		if (val == null || val._template == null || Runtime.Id(item.Id) == Runtime.Id(item.TemplateId))
		{
			return;
		}
		InstanceStructure.Ensure(item, val);
		Trait trait = Resolve(item, Runtime.Db.Variants[Runtime.Id(item.TemplateId)]);
		AttributeModifierComponent val2 = item.GetComponent<AttributeModifierComponent>();
		long key = ((val2 != null) ? ((Il2CppObjectBase)val2).Pointer.ToInt64() : 0);
		if (val2 != null && NativeApplied.TryGetValue(key, out (string, long) value) && value.Item1 == Runtime.Id(item.Id))
		{
			long item2 = value.Item2;
			ReadonlyData data = val2._data;
			if (item2 == ((data != null) ? ((Il2CppObjectBase)data).Pointer.ToInt64() : 0))
			{
				return;
			}
		}
		try
		{
			building = true;
			if (val2 == null)
			{
				val2 = new AttributeModifierComponent();
				((AbstractItemComponent)val2).Initialize(item);
				if (!item.AddComponent(((Il2CppObjectBase)val2).TryCast<IItemComponent>()))
				{
					throw new InvalidOperationException("Cannot attach deterministic modifier component");
				}
			}
			AttributeModifierComponent component = val._template.GetComponent<AttributeModifierComponent>();
			object obj;
			if (component == null)
			{
				obj = null;
			}
			else
			{
				ReadonlyData data2 = component._data;
				obj = ((data2 != null) ? data2.AttributeSourceModificationList : null);
			}
			List<AttributeModificationSettings> val3 = (List<AttributeModificationSettings>)obj;
			List<AttributeModificationSettings> val4 = new List<AttributeModificationSettings>();
			List<IAttributeModifier> val5 = new List<IAttributeModifier>();
			foreach (KeyValuePair<int, float> item3 in trait.Native)
			{
				if (item3.Key == 20)
				{
					AttributeAbsoluteModifier val6 = new AttributeAbsoluteModifier
					{
						AttributeId = (AttributeId)item3.Key,
						AbsoluteChange = item3.Value,
						ApplyBeforeRelativeModifiers = true
					};
					val5.Add(((Il2CppObjectBase)val6).TryCast<IAttributeModifier>() ?? throw new InvalidOperationException("Native modifier interface unavailable"));
				}
				else
				{
					AttributeRelativeModifier val7 = new AttributeRelativeModifier
					{
						AttributeId = (AttributeId)item3.Key,
						RelativeChange = item3.Value
					};
					val5.Add(((Il2CppObjectBase)val7).TryCast<IAttributeModifier>() ?? throw new InvalidOperationException("Native modifier interface unavailable"));
				}
			}
			AttributeModificationsSource val8 = new AttributeModificationsSource(val5);
			AttributeSourceActivationType val9 = (AttributeSourceActivationType)((trait.Category == 13) ? 1 : 2);
			bool flag = false;
			if (val3 != null)
			{
				if (val3.Count > 128)
				{
					throw new InvalidDataException("Modifier count bound");
				}
				for (int i = 0; i < val3.Count; i++)
				{
					AttributeModificationSettings val10 = val3[i];
					if (val10.ActivationType == val9 && (int)val10.StateType == 0 && !flag)
					{
						AttributeModificationsSourceGroup val11 = new AttributeModificationsSourceGroup();
						if (val10.AttributeModificationSource != null)
						{
							val11.Add(val10.AttributeModificationSource);
						}
						val11.Add(((Il2CppObjectBase)val8).TryCast<IAttributeModificationSource>());
						val4.Add(new AttributeModificationSettings
						{
							ActivationType = val9,
							StateType = (AttributeSourceStateType)0,
							AttributeModificationSource = ((Il2CppObjectBase)val11).TryCast<IAttributeModificationSource>()
						});
						flag = true;
					}
					else
					{
						val4.Add(val10);
					}
				}
			}
			if (!flag)
			{
				val4.Add(new AttributeModificationSettings
				{
					ActivationType = val9,
					StateType = (AttributeSourceStateType)0,
					AttributeModificationSource = ((Il2CppObjectBase)val8).TryCast<IAttributeModificationSource>()
				});
			}
			val2._data = new ReadonlyData
			{
				AttributeSourceModificationList = val4
			};
			if (NativeApplied.Count > 32768)
			{
				NativeApplied.Clear();
			}
			NativeApplied[((Il2CppObjectBase)val2).Pointer.ToInt64()] = (Runtime.Id(item.Id), ((Il2CppObjectBase)val2._data).Pointer.ToInt64());
			Runtime.Inc("rank28InstanceModifiers", 1L);
		}
		finally
		{
			building = false;
		}
	}

	public static Dictionary<int, double> Attributes(IItem item)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		if (!Runtime.Db.Variants.TryGetValue(Runtime.Id(item.TemplateId), out Trait value) || value.RankIndex <= 0)
		{
			return new Dictionary<int, double>();
		}
		return ((IEnumerable<KeyValuePair<int, float>>)Resolve(item, value).Native).ToDictionary((Func<KeyValuePair<int, float>, int>)((KeyValuePair<int, float> k) => k.Key), (Func<KeyValuePair<int, float>, double>)((KeyValuePair<int, float> k) => k.Value));
	}

	public static string Describe(IItem item, string fallback)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		if (!Runtime.Db.Variants.TryGetValue(Runtime.Id(item.TemplateId), out Trait value) || value.RankIndex <= 0)
		{
			return fallback;
		}
		Trait trait = Resolve(item, value);
		StringBuilder b = new StringBuilder();
		b.Append("<b>").Append(RankPolicy.Name(trait.RankIndex)).AppendLine("</b>");
		Row("Урон", trait.Damage * 100f);
		Row("Шанс крита", trait.Crit * 100f, " п.п.");
		Row("Множитель крита", trait.CritPower, "");
		Row("Экономия патронов", trait.SaveAmmo * 100f);
		Row("Темп огня", trait.FireRate * 100f);
		Row("Пробитие", trait.Penetration, " кл.");
		if (trait.Executor)
		{
			b.AppendLine("Экзекутор: попадания направляют урон в голову.");
		}
		foreach (KeyValuePair<int, float> item2 in trait.Native)
		{
			int key = item2.Key;
			if (key != 11 && key != 30)
			{
				Row(item2.Key switch
				{
					29 => "Запас энергии", 
					2 => "Расход энергии на спринт", 
					20 => "Грузоподъёмность", 
					1 => "Время обыска", 
					8 => "Время лечения", 
					5 => "Скорость спринта", 
					12 => "Время перезарядки", 
					10 => "Стабилизация отдачи", 
					17 => "Время прицеливания", 
					13 => "Смена оружия", 
					73 => "Дальность урона", 
					_ => "", 
				}, item2.Value * (float)((item2.Key == 20) ? 1 : 100), (item2.Key == 20) ? " кг" : "%");
			}
		}
		return b.ToString().TrimEnd();
		void Row(string n, double v, string suffix = "%")
		{
			if (n.Length > 0 && Math.Abs(v) > 1E-05)
			{
				b.Append(n).Append(": ").Append((v >= 0.0) ? "+" : "")
					.Append(v.ToString("0.##", CultureInfo.InvariantCulture))
					.AppendLine(suffix);
			}
		}
	}

	public static void Activate(Harmony h)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Expected O, but got Unknown
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Expected O, but got Unknown
		//IL_02a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ae: Expected O, but got Unknown
		//IL_02d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02de: Expected O, but got Unknown
		if (active || Data == null)
		{
			return;
		}
		Harmony obj = h;
		MethodInfo methodInfo = RankHookPlan.Factory();
		HarmonyMethod val = new HarmonyMethod(typeof(RankLadder).GetMethod("AfterCreate", BindingFlags.Static | BindingFlags.NonPublic));
		val.priority = 0;
		val.after = new string[1] { "frontier.cargohunters.loot050" };
		obj.Patch((MethodBase)methodInfo, (HarmonyMethod)null, val, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		h.Patch((MethodBase)RankHookPlan.Modifiers(), new HarmonyMethod(typeof(RankLadder).GetMethod("BeforeModifiers", BindingFlags.Static | BindingFlags.NonPublic)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().Single((Assembly a) => a.GetName().Name == "Frontier.Core");
		Assembly assembly2 = AppDomain.CurrentDomain.GetAssemblies().Single((Assembly a) => a.GetName().Name == "Frontier.Engine");
		presentation = assembly.GetType("Frontier.Core.ElitePresentation", throwOnError: true);
		rankStyle = presentation.GetNestedType("RankStyle", BindingFlags.Public | BindingFlags.NonPublic);
		IDictionary dictionary = (IDictionary)presentation.GetField("Styles", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
		IDictionary dictionary2 = (IDictionary)presentation.GetField("ByPreset", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
		foreach (KeyValuePair<string, LadderStyle> style in Data.Styles)
		{
			dictionary[style.Key] = Activator.CreateInstance(rankStyle, style.Key, style.Value.NameRu, style.Value.Color);
		}
		foreach (KeyValuePair<string, int> botRank in Data.BotRanks)
		{
			dictionary2[botRank.Key] = RankPolicy.Name(botRank.Value);
		}
		Type? type = assembly2.GetType("Frontier.Engine.EliteVisualProtocol", throwOnError: true);
		Managed(type, "ValidRank", "ValidRank");
		Managed(type, "Code", "Code");
		Managed(type, "Rank", "Decode");
		Managed(assembly.GetType("Frontier.Core.EnemyRoller", throwOnError: true), "Replace", "ReplaceEnemy");
		h.Patch((MethodBase)RankHookPlan.Equipment(), (HarmonyMethod)null, new HarmonyMethod(typeof(RankLadder).GetMethod("Equipment", BindingFlags.Static | BindingFlags.NonPublic)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		h.Patch((MethodBase)RankHookPlan.Raid(), new HarmonyMethod(typeof(RankLadder).GetMethod("NewRaid", BindingFlags.Static | BindingFlags.NonPublic)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		LadderVisuals.Install(h);
		active = true;
		Runtime.WriteReport();
		Runtime.Log.LogInfo((object)"Rank28 v10.8.12: per-model legacy L3 anchors at S3; specialized stats and existing-instance refresh; native spawn/alias pipeline unchanged.");
		void Managed(Type type2, string method, string fn)
		{
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Expected O, but got Unknown
			h.Patch((MethodBase)type2.GetMethod(method, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic), new HarmonyMethod(typeof(RankLadder).GetMethod(fn, BindingFlags.Static | BindingFlags.NonPublic)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void AfterCreate(IItem __result)
	{
		try
		{
			Materialize(__result);
		}
		catch (Exception e)
		{
			Runtime.Error("rank28.materialize", e);
		}
	}

	private static void BeforeModifiers(AttributeModifierComponent __instance)
	{
		try
		{
			Materialize(((AbstractItemComponent)__instance).Owner);
		}
		catch (Exception e)
		{
			Runtime.Error("rank28.modifier", e);
		}
	}

	private static bool ValidRank(string __0, ref bool __result)
	{
		if (RankPolicy.Parse(__0) == 0)
		{
			return true;
		}
		__result = true;
		return false;
	}

	private static bool Code(string __0, ref char __result)
	{
		int num = RankPolicy.Parse(__0);
		if (num == 0)
		{
			return true;
		}
		__result = RankPolicy.Encode(num);
		return false;
	}

	private static bool Decode(char __0, ref string __result)
	{
		int num = RankPolicy.Decode(__0);
		if (num == 0)
		{
			return true;
		}
		__result = RankPolicy.Name(num);
		return false;
	}

	private static void NewRaid()
	{
		salt = Guid.NewGuid().ToString("N");
		EquipmentRanks.Clear();
		SelectedSpawns.Clear();
		BotBonusCache.Clear();
		NativeApplied.Clear();
		LadderVisuals.Reset();
	}

	internal static BotPresetData CopySpawnRecipe(BotPresetData original, BotPresetData rankTemplate)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Expected O, but got Unknown
		BotSpawnRecipe<UniqueId, List<PresetEquipment>> botSpawnRecipe = new BotSpawnRecipe<UniqueId, List<PresetEquipment>>(original.Id, original.Alias, original.AISettingId, original.PlatformPresetId, original.EquipmentPresetId, original.LootPresetId, original.Equipments, original.OverrideBotAccountLevel).WithRankAI(rankTemplate.AISettingId);
		return new BotPresetData
		{
			Id = botSpawnRecipe.Id,
			Alias = botSpawnRecipe.Alias,
			AISettingId = botSpawnRecipe.AISettingId,
			PlatformPresetId = botSpawnRecipe.PlatformPresetId,
			EquipmentPresetId = botSpawnRecipe.EquipmentPresetId,
			LootPresetId = botSpawnRecipe.LootPresetId,
			Equipments = botSpawnRecipe.Equipments,
			OverrideBotAccountLevel = botSpawnRecipe.OverrideBotAccountLevel
		};
	}

	internal static bool SpawnRecipeMatches(BotPresetData original, BotPresetData selected)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		if (Runtime.Id(selected.Id) == Runtime.Id(original.Id) && Runtime.Id(selected.PlatformPresetId) == Runtime.Id(original.PlatformPresetId) && Runtime.Id(selected.EquipmentPresetId) == Runtime.Id(original.EquipmentPresetId) && Runtime.Id(selected.LootPresetId) == Runtime.Id(original.LootPresetId) && selected.OverrideBotAccountLevel == original.OverrideBotAccountLevel)
		{
			List<PresetEquipment> equipments = selected.Equipments;
			IntPtr intPtr = ((equipments != null) ? ((Il2CppObjectBase)equipments).Pointer : IntPtr.Zero);
			List<PresetEquipment> equipments2 = original.Equipments;
			return intPtr == ((equipments2 != null) ? ((Il2CppObjectBase)equipments2).Pointer : IntPtr.Zero);
		}
		return false;
	}

	internal static string CreationRank(BotPresetData preset)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		if (preset != null && SelectedSpawns.TryGetValue(((Il2CppObjectBase)preset).Pointer.ToInt64(), out SpawnSelection value))
		{
			return RankPolicy.Name(value.Rank);
		}
		if (preset != null)
		{
			return Runtime.Db.BotRanks.GetValueOrDefault(Runtime.Id(preset.Id), "normal");
		}
		return "normal";
	}

	private static bool ReplaceEnemy(object __0, object __1, object __2, ref string __3, ref object __result)
	{
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0203: Unknown result type (might be due to invalid IL or missing references)
		if (Data == null)
		{
			return true;
		}
		__result = null;
		__3 = null;
		try
		{
			object obj = ((__0 is Il2CppObjectBase) ? __0 : null);
			BotSpawnPointEquipmentManager val = ((obj != null) ? ((Il2CppObjectBase)obj).TryCast<BotSpawnPointEquipmentManager>() : null);
			object obj2 = ((__2 is Il2CppObjectBase) ? __2 : null);
			BotPresetData val2 = ((obj2 != null) ? ((Il2CppObjectBase)obj2).TryCast<BotPresetData>() : null);
			if (val == null || val2 == null || val._itemsManager == null || !val._itemsManager.IsAuthoritative || !SpawnIds.Contains(__1?.ToString() ?? ""))
			{
				return false;
			}
			if (!Data.BotVariants.TryGetValue(Runtime.Id(val2.Id), out string[] value))
			{
				return false;
			}
			if (SelectedSpawns.Count >= 4096)
			{
				Runtime.Inc("rank28SpawnSelectionCapacityFallback", 1L);
				return false;
			}
			int num = RankPolicy.Pick(LootMath.Sample(salt + "|" + __1?.ToString() + "|" + Runtime.Id(val2.Id), 0u, "rank28-enemy"), Data.EnemyWeights);
			IBotPresetDataRepository botPresetDataRepository = val._botPresetDataRepository;
			BotPresetData data = ((MultiObjectDataRepository<BotPresetData, UniqueId>)(object)(((botPresetDataRepository != null) ? ((Il2CppObjectBase)botPresetDataRepository).TryCast<BotPresetDataRepository>() : null) ?? throw new InvalidDataException("No concrete native bot repository"))).GetData(UniqueId.Parse(value[num - 1]));
			if (data == null || Runtime.Id(data.Id) != value[num - 1])
			{
				throw new InvalidDataException("Rank28 bot preset missing from native repository");
			}
			BotPresetData val3 = CopySpawnRecipe(val2, data);
			if (!SpawnRecipeMatches(val2, val3))
			{
				throw new InvalidDataException("Rank28 altered native spawn recipe");
			}
			SpawnSelection spawnSelection = new SpawnSelection
			{
				Preset = val3,
				Rank = num,
				RankedId = value[num - 1],
				BaseId = Runtime.Id(val2.Id),
				EquipmentEntries = (val2.Equipments?.Count ?? 0),
				EquipmentId = Runtime.Id(val2.EquipmentPresetId),
				PlatformId = Runtime.Id(val2.PlatformPresetId),
				LootId = Runtime.Id(val2.LootPresetId)
			};
			SelectedSpawns[((Il2CppObjectBase)val3).Pointer.ToInt64()] = spawnSelection;
			__result = val3;
			__3 = RankPolicy.Name(num) + " | native recipe | " + spawnSelection.BaseId;
			Runtime.Inc("rank28Enemies." + RankPolicy.Name(num), 1L);
			Runtime.Inc("rank28SpawnRecipesPreserved", 1L);
			if (spawnSelection.EquipmentEntries > 0)
			{
				Runtime.Inc("rank28SpawnRecipesWithRuntimeEquipment", 1L);
			}
			return false;
		}
		catch (Exception e)
		{
			Runtime.Inc("rank28NativePresetFallbacks", 1L);
			Runtime.Error("rank28.enemy-selection", e);
			return false;
		}
	}

	private static void Equipment(AiCharacterSpawnSystem __instance, BotPresetData __1, Equipment __result)
	{
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_014d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		if (Data == null || __1 == null)
		{
			return;
		}
		SelectedSpawns.TryGetValue(((Il2CppObjectBase)__1).Pointer.ToInt64(), out SpawnSelection value);
		try
		{
			if (__instance == null || __instance._battleSession == null || !__instance._battleSession.IsAuthoritative)
			{
				return;
			}
			int value2 = value?.Rank ?? 0;
			string text = value?.RankedId ?? Runtime.Id(__1.Id);
			if (value2 == 0 && !RankById.TryGetValue(text, out value2))
			{
				return;
			}
			Runtime.Inc("rank28EquipmentAttempts", 1L);
			if (__result == null)
			{
				Runtime.Inc("rank28EquipmentNull", 1L);
				Runtime.Error("rank28.native-equipment", new InvalidOperationException("Native equipment returned null; base=" + Runtime.Id(__1.Id) + "; equipment=" + Runtime.Id(__1.EquipmentPresetId) + "; platform=" + Runtime.Id(__1.PlatformPresetId) + "; loot=" + Runtime.Id(__1.LootPresetId) + "; entries=" + (__1.Equipments?.Count ?? 0)));
			}
			else
			{
				Runtime.Inc("rank28EquipmentCreated", 1L);
				string key = Runtime.Id(__result.Id);
				if (EquipmentRanks.Count > 4096)
				{
					EquipmentRanks.Clear();
				}
				EquipmentRanks[key] = value2;
				presentation.GetMethod("ObserveEquipment", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[3] { __instance, text, __result });
			}
		}
		catch (Exception e)
		{
			Runtime.Error("rank28.enemy-bind", e);
		}
		finally
		{
			if (value != null)
			{
				SelectedSpawns.Remove(((Il2CppObjectBase)__1).Pointer.ToInt64());
			}
		}
	}

	public static int EnemyRank(BattleSession s, int id)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		Equipment val = s.FindEquipmentByPlayerId(id);
		if (val == null)
		{
			return 0;
		}
		return EquipmentRanks.GetValueOrDefault(Runtime.Id(val.Id));
	}

	public static bool EligibleBot(BattleSession s, int id)
	{
		if (s.IsAuthoritative && s.IsBotPlayer(id))
		{
			return EnemyRank(s, id) > 0;
		}
		return false;
	}

	public static bool TryBotCache(BattleSession s, int id, UniqueId weapon, out Bonuses value)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		value = null;
		if (!EligibleBot(s, id))
		{
			return false;
		}
		(long, int, string) key = (((Il2CppObjectBase)s).Pointer.ToInt64(), id, Runtime.Id(weapon));
		if (BotBonusCache.TryGetValue(key, out (long, Bonuses) value2) && Environment.TickCount64 < value2.Item1)
		{
			value = value2.Item2;
			Runtime.Inc("rank28BotAggregateCacheHits", 1L);
			return true;
		}
		return false;
	}

	public static void StoreBotCache(BattleSession s, int id, UniqueId weapon, Bonuses value)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		if (EligibleBot(s, id))
		{
			if (BotBonusCache.Count >= 2048)
			{
				BotBonusCache.Clear();
			}
			BotBonusCache[(((Il2CppObjectBase)s).Pointer.ToInt64(), id, Runtime.Id(weapon))] = (Environment.TickCount64 + 200, value);
		}
	}

	public static void FinishAggregate(BattleSession s, int id, Bonuses b)
	{
		int num = (EligibleBot(s, id) ? EnemyRank(s, id) : 0);
		if (num > 0)
		{
			b.HasLadder = true;
			b.Damage = (float)((double)(1f + b.Damage) * RankPolicy.EnemyDamage(num) - 1.0);
		}
		b.ClampLadder(num > 0);
	}
}
