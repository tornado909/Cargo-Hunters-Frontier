using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Frontier.Engine;
using Frontier.Modular;
using HarmonyLib;

namespace Frontier.Core;

internal sealed class RuntimeObservations
{
	private readonly Plugin p;

	private readonly object sync = new object();

	private readonly HashSet<int> skillViews = new HashSet<int>();

	private readonly HashSet<string> shopCommodities = new HashSet<string>((IEqualityComparer<string>)(object)StringComparer.Ordinal);

	private readonly HashSet<string> instances = new HashSet<string>((IEqualityComparer<string>)(object)StringComparer.Ordinal);

	private readonly HashSet<string> eliteEquipment = new HashSet<string>((IEqualityComparer<string>)(object)StringComparer.Ordinal);

	private readonly HashSet<string> recipeViews = new HashSet<string>((IEqualityComparer<string>)(object)StringComparer.Ordinal);

	private readonly HashSet<string> refreshedShops = new HashSet<string>((IEqualityComparer<string>)(object)StringComparer.Ordinal);

	private readonly HashSet<string> itemIds;

	private readonly HashSet<string> eliteIds;

	private readonly HashSet<string> recipeIds;

	private readonly List<string> errors = new List<string>();

	private int aiInitializations;

	private int aiFailures;

	private object? lastAuthoritativeSession;

	internal bool Healthy
	{
		get
		{
			lock (sync)
			{
				return errors.Count == 0 && aiFailures == 0;
			}
		}
	}

	internal object Snapshot
	{
		get
		{
			lock (sync)
			{
				return new
				{
					status = ((errors.Count > 0 || aiFailures > 0) ? "BROKEN" : "PARTIAL_OBSERVATIONS_NOT_A_PLAYTEST_PASS"),
					rigOS = new
					{
						status = ((skillViews.Count == 4) ? "VIEWS_INITIALIZED" : "NOT_FULLY_OBSERVED"),
						initializedSkillIds = Enumerable.ToArray<int>((System.Collections.Generic.IEnumerable<int>)Enumerable.OrderBy<int, int>((System.Collections.Generic.IEnumerable<int>)skillViews, (Func<int, int>)((int x) => x))),
						localizedResolutions = RigOSLocalizationFix.Repaired,
						expected = (ModuleRuntime.ContentGate(p.Engine, "progression") ? 4 : 0)
					},
					shop = new
					{
						createdCommodityIds = Enumerable.ToArray<string>((System.Collections.Generic.IEnumerable<string>)Enumerable.OrderBy<string, string>((System.Collections.Generic.IEnumerable<string>)shopCommodities, (Func<string, string>)((string x) => x))),
						requestsRefreshed = refreshedShops.Count
					},
					createdTemplateIds = Enumerable.ToArray<string>((System.Collections.Generic.IEnumerable<string>)Enumerable.OrderBy<string, string>((System.Collections.Generic.IEnumerable<string>)instances, (Func<string, string>)((string x) => x))),
					elitePresetsWithEquipment = Enumerable.ToArray<string>((System.Collections.Generic.IEnumerable<string>)Enumerable.OrderBy<string, string>((System.Collections.Generic.IEnumerable<string>)eliteEquipment, (Func<string, string>)((string x) => x))),
					recipeViewsInitialized = Enumerable.ToArray<string>((System.Collections.Generic.IEnumerable<string>)Enumerable.OrderBy<string, string>((System.Collections.Generic.IEnumerable<string>)recipeViews, (Func<string, string>)((string x) => x))),
					aiInitializations = aiInitializations,
					aiExceptionsObserved = aiFailures,
					errors = errors.ToArray(),
					purchaseVerified = false,
					damageVerified = false,
					extractionVerified = false,
					multiplayerVerified = false
				};
			}
		}
	}

	private static RuntimeObservations O => Plugin.Current.Observations;

	internal RuntimeObservations(Plugin p)
	{
		Plugin p2 = p;
		this.p = p2;
		itemIds = Ids("item_templates");
		eliteIds = Ids("bot_presets");
		recipeIds = Ids("craft_recipes");
		[CompilerGenerated]
		HashSet<string> Ids(string name)
		{
			return Enumerable.ToHashSet<string>(Enumerable.Select<JsonNode, string>((System.Collections.Generic.IEnumerable<JsonNode>)((JsonNode)p2.Engine.RuntimeContracts)["addedIds"][name].AsArray(), (Func<JsonNode, string>)PatchEngine.Scalar), (IEqualityComparer<string>)(object)StringComparer.OrdinalIgnoreCase);
		}
	}

	private static HarmonyMethod Hook(string name)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Expected O, but got Unknown
		return new HarmonyMethod(typeof(RuntimeObservations).GetMethod(name, (BindingFlags)40));
	}

	internal void Install(Harmony h)
	{
		Patch("Game.UI.Controllers.Skills.SkillHubController", "UpdateSkillView", 0, "SkillsPrefix");
		Patch("Game.UI.Views.SkillView", "InitializeFrom", 5, null, "SkillViewPostfix");
		Patch("Game.Shopping.ShoppingManagerUtils", "GetPricelist", 10, "ShopPrefix", "ShopPostfix");
		Patch("Game.ItemsSystem.Factories.ItemFactory", "CreateInstance", 3, null, "ItemPostfix");
		Patch("Game.AISystem.AiCharacterSpawnSystem", "InitializeDependency", 7, null, "AiInitializePostfix");
		Patch("Game.AISystem.AiCharacterSpawnSystem", "GetPlayerEquipment", 3, null, "EquipmentPostfix");
		Patch("Game.AISystem.AiCharacterSpawnSystem", "OnUpdate", 0, null, null, "AiFinalizer");
		Patch("Game.UI.CraftV2.CraftBenchCraftController", "InitializeFrom", 5, null, "RecipePostfix");
		[CompilerGenerated]
		void Patch(string type, string name, int count, string? prefix = null, string? postfix = null, string? finalizer = null)
		{
			MethodInfo val = Reflect.UniqueMethod(Reflect.FindType(type), name, count);
			h.Patch((MethodBase)(object)val, (prefix == null) ? null : Hook(prefix), (postfix == null) ? null : Hook(postfix), (HarmonyMethod)null, (finalizer == null) ? null : Hook(finalizer), (HarmonyMethod)null);
		}
	}

	private static void SkillsPrefix(object __instance)
	{
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		if (!ModuleRuntime.Feature("rigos"))
		{
			return;
		}
		Plugin current = Plugin.Current;
		if (!current.Active || !ModuleRuntime.ContentGate(current.Engine, "progression"))
		{
			return;
		}
		try
		{
			object obj = Reflect.Read(__instance, "_skills");
			if (obj == null || !HasNewSkills(obj))
			{
				object obj2 = Reflect.Call(Reflect.Read(__instance, "_skillHubManager") ?? throw new InvalidDataException("RigOS skill manager missing"), "GetSkills") ?? throw new InvalidDataException("RigOS GetSkills returned null");
				if (!HasNewSkills(obj2))
				{
					throw new InvalidDataException("RigOS manager does not expose the four verified skill IDs");
				}
				Reflect.Write(__instance, "_skills", obj2);
			}
		}
		catch (System.Exception e)
		{
			O.RecordError("RigOS", e);
		}
		[CompilerGenerated]
		static bool HasNewSkills(object array)
		{
			HashSet<string> val = Enumerable.ToHashSet<string>(Enumerable.Select<object, string>(Reflect.Values(array), (Func<object, string>)Reflect.Id));
			return Enumerable.All<string>((System.Collections.Generic.IEnumerable<string>)new string[4] { "9001", "9002", "9003", "9005" }, (Func<string, bool>)val.Contains);
		}
	}

	private static void SkillViewPostfix(object __instance, object __0)
	{
		if (!Plugin.Current.Active || __0 == null)
		{
			return;
		}
		try
		{
			int num = Convert.ToInt32(Reflect.Read(__0, "Id"));
			if (num >= 9001 && num <= 9005)
			{
				RigOSLocalizationFix.Apply(__instance, num);
				bool flag;
				lock (O.sync)
				{
					flag = O.skillViews.Add(num);
				}
				if (flag)
				{
					((BasePlugin)Plugin.Current).Log.LogInfo((object)("RigOS native SkillView initialized: " + num));
					Plugin.Current.WriteReport();
				}
			}
		}
		catch (System.Exception e)
		{
			O.RecordError("RigOS view evidence", e);
		}
	}

	private static string ShopMigrationPath(object shop)
	{
		return ModuleRuntime.ShopMigrationPath(Path.Combine(Paths.BepInExRootPath, "Frontier-migrations", "shop-0.4.0-" + PatchEngine.Hash(Encoding.UTF8.GetBytes(Reflect.Id(shop))) + ".done"));
	}

	private static void ShopPrefix(object? __0, object __1, object __4)
	{
		if (!ModuleRuntime.Feature("shops"))
		{
			return;
		}
		Plugin current = Plugin.Current;
		if (!current.Active || __0 == null || __1 == null)
		{
			return;
		}
		try
		{
			object obj = Reflect.Read(__0, "IsAuthoritative");
			if (!(obj is bool) || !(bool)obj || (Reflect.Read(__1, "ShopTemplateId")?.ToString() ?? "") != PatchEngine.Str((JsonNode)(object)current.Engine.RuntimeContracts, "testShopId"))
			{
				return;
			}
			string text = Reflect.Id(__1);
			bool flag = current.Settings.RuntimeTestMode || (current.Settings.ProductionMode && !File.Exists(ShopMigrationPath(__1)));
			if (!flag)
			{
				object obj2 = Reflect.Call(__4, "GetPricelistData", Reflect.Read(__1, "PricelistId"));
				if (obj2 != null)
				{
					flag = Enumerable.Any<object>(Reflect.Values(Reflect.Read(obj2, "Commodities")), (Func<object, bool>)((object c) => (Reflect.Read(c, "DataId")?.ToString() ?? "").StartsWith("Frontier_TEST_", (StringComparison)4)));
				}
			}
			if (!flag)
			{
				return;
			}
			lock (O.sync)
			{
				if (!O.refreshedShops.Add(text))
				{
					return;
				}
			}
			Reflect.Write(__1, "UpdatePriceListOnNextRequest", true);
			((BasePlugin)current).Log.LogInfo((object)"Requested native first-trader refresh (content migration / test-mode transition)");
		}
		catch (System.Exception e)
		{
			O.RecordError("Test shop refresh", e);
		}
	}

	private static void ShopPostfix(object? __result, object? __0, object __1)
	{
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		if (!ModuleRuntime.Feature("shops") || !Plugin.Current.Active || __result == null)
		{
			return;
		}
		try
		{
			System.Collections.Generic.IEnumerator<object> enumerator = Reflect.Values(Reflect.Read(__result, "Commodities")).GetEnumerator();
			try
			{
				while (((System.Collections.IEnumerator)enumerator).MoveNext())
				{
					object current = enumerator.Current;
					string text = Reflect.Read(current, "DataId")?.ToString() ?? "";
					if (text.StartsWith("Frontier_", (StringComparison)4))
					{
						string text2 = Reflect.Read(Reflect.Read(current, "ItemDto") ?? throw new InvalidDataException("Frontier commodity has no item DTO"), "TemplateId")?.ToString() ?? "";
						if (!O.itemIds.Contains(text2) && (!(text == "Frontier_AMMO_556_PROD") || !(text2 == "610a082d-33bd-460b-bb88-ef3151b583d4")))
						{
							throw new InvalidDataException("Frontier shop commodity points to an unexpected template " + text2);
						}
						lock (O.sync)
						{
							O.shopCommodities.Add(text);
						}
					}
				}
			}
			finally
			{
				((System.IDisposable)enumerator)?.Dispose();
			}
			if (Plugin.Current.Settings.ProductionMode && __0 != null)
			{
				object obj = Reflect.Read(__0, "IsAuthoritative");
				if (obj is bool && (bool)obj && (Reflect.Read(__1, "ShopTemplateId")?.ToString() ?? "") == PatchEngine.Str((JsonNode)(object)Plugin.Current.Engine.RuntimeContracts, "testShopId"))
				{
					string text3 = ShopMigrationPath(__1);
					Directory.CreateDirectory(Path.GetDirectoryName(text3));
					if (!File.Exists(text3))
					{
						File.WriteAllText(text3, "0.4.0-production native pricelist migration completed");
					}
				}
			}
			Plugin.Current.WriteReport();
		}
		catch (System.Exception e)
		{
			O.RecordError("Live pricelist", e);
		}
	}

	private static void ItemPostfix(object? __result)
	{
		if (!Plugin.Current.Active || __result == null)
		{
			return;
		}
		try
		{
			string text = Reflect.Read(__result, "TemplateId")?.ToString() ?? "";
			if (O.itemIds.Contains(text))
			{
				lock (O.sync)
				{
					O.instances.Add(text);
					return;
				}
			}
		}
		catch (System.Exception e)
		{
			O.RecordError("Item factory evidence", e);
		}
	}

	private static void AiInitializePostfix(object __0)
	{
		if (!Plugin.Current.Active || __0 == null)
		{
			return;
		}
		try
		{
			object obj = Reflect.Read(__0, "IsAuthoritative");
			if (!(obj is bool) || !(bool)obj)
			{
				return;
			}
			lock (O.sync)
			{
				if (Reflect.SameNative(O.lastAuthoritativeSession, __0))
				{
					return;
				}
				O.lastAuthoritativeSession = __0;
				O.aiInitializations++;
			}
			Plugin.Current.ResetEnemySession();
		}
		catch (System.Exception e)
		{
			O.RecordError("AI authority", e);
		}
	}

	private static void EquipmentPostfix(object __instance, object __1, object? __result)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		if (!Plugin.Current.Active || __1 == null)
		{
			return;
		}
		try
		{
			string text = Reflect.Id(__1);
			if (O.eliteIds.Contains(text))
			{
				if (__result == null)
				{
					throw new InvalidDataException("Elite GetPlayerEquipment returned null: " + text);
				}
				ElitePresentation.ObserveEquipment(__instance, text, __result);
				bool flag;
				lock (O.sync)
				{
					flag = O.eliteEquipment.Add(text);
				}
				if (flag)
				{
					((BasePlugin)Plugin.Current).Log.LogInfo((object)("Elite native equipment created: " + text));
					Plugin.Current.WriteReport();
				}
			}
		}
		catch (System.Exception e)
		{
			O.RecordError("Elite equipment", e);
		}
	}

	private static System.Exception? AiFinalizer(System.Exception? __exception)
	{
		if (__exception != null && Plugin.Current.Active)
		{
			lock (O.sync)
			{
				O.aiFailures++;
			}
			O.RecordError("AI OnUpdate", __exception);
		}
		return __exception;
	}

	private static void RecipePostfix(object? __4)
	{
		if (!Plugin.Current.Active || __4 == null)
		{
			return;
		}
		try
		{
			string text = Reflect.Id(__4);
			if (O.recipeIds.Contains(text))
			{
				lock (O.sync)
				{
					O.recipeViews.Add(text);
					return;
				}
			}
		}
		catch (System.Exception e)
		{
			O.RecordError("Recipe view evidence", e);
		}
	}

	private void RecordError(string stage, System.Exception e)
	{
		string text = stage + ": " + e.GetBaseException().Message;
		lock (sync)
		{
			if (errors.Count >= 20 || errors.Contains(text))
			{
				return;
			}
			errors.Add(text);
		}
		((BasePlugin)p).Log.LogError((object)("Runtime evidence BROKEN: " + text));
		p.WriteReport();
	}
}
