using System.Reflection;
using HarmonyLib;
using Frontier.Engine;
namespace Frontier.Core;

/// <summary>Evidence from real game paths. Counters do not imply a full playtest or network validation.</summary>
internal sealed class RuntimeObservations
{
    private readonly Plugin p;
    private readonly object sync = new();
    private readonly HashSet<int> skillViews = new();
    private readonly HashSet<string> shopCommodities = new(StringComparer.Ordinal);
    private readonly HashSet<string> instances = new(StringComparer.Ordinal);
    private readonly HashSet<string> eliteEquipment = new(StringComparer.Ordinal);
    private readonly HashSet<string> recipeViews = new(StringComparer.Ordinal);
    private readonly HashSet<string> refreshedShops = new(StringComparer.Ordinal);
    private readonly HashSet<string> itemIds;
    private readonly HashSet<string> eliteIds;
    private readonly HashSet<string> recipeIds;
    private readonly List<string> errors = new();
    private int aiInitializations, aiFailures;
    private object? lastAuthoritativeSession;
    internal RuntimeObservations(Plugin p)
    {
        this.p = p;
        HashSet<string> Ids(string name) => p.Engine.RuntimeContracts["addedIds"]![name]!.AsArray().Select(PatchEngine.Scalar).ToHashSet(StringComparer.OrdinalIgnoreCase);
        itemIds = Ids("item_templates"); eliteIds = Ids("bot_presets"); recipeIds = Ids("craft_recipes");
    }
    internal bool Healthy { get { lock (sync) return errors.Count == 0 && aiFailures == 0; } }
    internal object Snapshot
    {
        get { lock (sync) return new {
            status = errors.Count > 0 || aiFailures > 0 ? "BROKEN" : "PARTIAL_OBSERVATIONS_NOT_A_PLAYTEST_PASS",
            rigOS = new { status = skillViews.Count == 4 ? "VIEWS_INITIALIZED" : "NOT_FULLY_OBSERVED", initializedSkillIds = skillViews.OrderBy(x => x).ToArray(), expected = p.Engine.IsEnabled("progression") ? 4 : 0 },
            shop = new { createdCommodityIds = shopCommodities.OrderBy(x => x).ToArray(), requestsRefreshed = refreshedShops.Count },
            createdTemplateIds = instances.OrderBy(x => x).ToArray(),
            elitePresetsWithEquipment = eliteEquipment.OrderBy(x => x).ToArray(),
            recipeViewsInitialized = recipeViews.OrderBy(x => x).ToArray(),
            aiInitializations, aiExceptionsObserved = aiFailures, errors = errors.ToArray(),
            purchaseVerified = false, damageVerified = false, extractionVerified = false, multiplayerVerified = false
        }; }
    }
    private static RuntimeObservations O => Plugin.Current.Observations;
    private static HarmonyMethod Hook(string name) => new(typeof(RuntimeObservations).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!);
    internal void Install(Harmony h)
    {
        void Patch(string type, string name, int count, string? prefix = null, string? postfix = null, string? finalizer = null)
        {
            var method = Reflect.UniqueMethod(Reflect.FindType(type), name, count);
            h.Patch(method, prefix: prefix is null ? null : Hook(prefix), postfix: postfix is null ? null : Hook(postfix), finalizer: finalizer is null ? null : Hook(finalizer));
        }
        Patch("Game.UI.Controllers.Skills.SkillHubController", "UpdateSkillView", 0, prefix: nameof(SkillsPrefix));
        Patch("Game.UI.Views.SkillView", "InitializeFrom", 5, postfix: nameof(SkillViewPostfix));
        Patch("Game.Shopping.ShoppingManagerUtils", "GetPricelist", 10, nameof(ShopPrefix), nameof(ShopPostfix));
        Patch("Game.ItemsSystem.Factories.ItemFactory", "CreateInstance", 3, postfix: nameof(ItemPostfix));
        Patch("Game.AISystem.AiCharacterSpawnSystem", "InitializeDependency", 7, postfix: nameof(AiInitializePostfix));
        Patch("Game.AISystem.AiCharacterSpawnSystem", "GetPlayerEquipment", 3, postfix: nameof(EquipmentPostfix));
        Patch("Game.AISystem.AiCharacterSpawnSystem", "OnUpdate", 0, finalizer: nameof(AiFinalizer));
        Patch("Game.UI.CraftV2.CraftBenchCraftController", "InitializeFrom", 5, postfix: nameof(RecipePostfix));
    }
    private static void SkillsPrefix(object __instance)
    {
        var p = Plugin.Current;
        if (!p.Active || !p.Engine.IsEnabled("progression")) return;
        try
        {
            object? current = Reflect.Read(__instance, "_skills");
            bool HasNewSkills(object array) { var ids = Reflect.Values(array).Select(Reflect.Id).ToHashSet(); return new[] { "9001", "9002", "9003", "9004" }.All(ids.Contains); }
            if (current is not null && HasNewSkills(current)) return;
            object manager = Reflect.Read(__instance, "_skillHubManager") ?? throw new InvalidDataException("RigOS skill manager missing");
            object fresh = Reflect.Call(manager, "GetSkills") ?? throw new InvalidDataException("RigOS GetSkills returned null");
            if (!HasNewSkills(fresh)) throw new InvalidDataException("RigOS manager does not expose the four verified skill IDs");
            // Let native UpdateSkillView create and bind standard SkillViews. No mock UI or duplicate skill objects.
            Reflect.Write(__instance, "_skills", fresh);
        }
        catch (Exception e) { O.RecordError("RigOS", e); }
    }
    private static void SkillViewPostfix(object __instance, object __0)
    {
        if (!Plugin.Current.Active || __0 is null) return;
        try
        {
            int id = Convert.ToInt32(Reflect.Read(__0, "Id"));
            if (id < 9001 || id > 9004) return;
            RigOSLocalizationFix.Apply(__instance, id);
            bool added; lock (O.sync) added = O.skillViews.Add(id);
            if (added) { Plugin.Current.Log.LogInfo("RigOS native SkillView initialized: " + id); Plugin.Current.WriteReport(); }
        }
        catch (Exception e) { O.RecordError("RigOS view evidence", e); }
    }
    private static void ShopPrefix(object? __0, object __1, object __4)
    {
        var p = Plugin.Current;
        if (!p.Active || __0 is null || __1 is null) return;
        try
        {
            if (Reflect.Read(__0, "IsAuthoritative") is not true) return;
            string template = Reflect.Read(__1, "ShopTemplateId")?.ToString() ?? "";
            if (template != PatchEngine.Str(p.Engine.RuntimeContracts, "testShopId")) return;
            string id = Reflect.Id(__1);
            bool needsRefresh = p.Settings.RuntimeTestMode;
            if (!needsRefresh)
            {
                object? previous = Reflect.Call(__4, "GetPricelistData", Reflect.Read(__1, "PricelistId"));
                if (previous is not null)
                    needsRefresh = Reflect.Values(Reflect.Read(previous, "Commodities")!).Any(c => (Reflect.Read(c, "DataId")?.ToString() ?? "").StartsWith("Frontier_TEST_", StringComparison.Ordinal));
            }
            if (!needsRefresh) return;
            lock (O.sync) if (!O.refreshedShops.Add(id)) return;
            // GetPricelist's native code checks this flag and calls the original UpdatePricelist.
            Reflect.Write(__1, "UpdatePriceListOnNextRequest", true);
            p.Log.LogInfo("Requested native first-trader refresh (test offers added or stale test offers removed)");
        }
        catch (Exception e) { O.RecordError("Test shop refresh", e); }
    }
    private static void ShopPostfix(object? __result)
    {
        if (!Plugin.Current.Active || __result is null) return;
        try
        {
            foreach (object c in Reflect.Values(Reflect.Read(__result, "Commodities")!))
            {
                string id = Reflect.Read(c, "DataId")?.ToString() ?? "";
                if (!id.StartsWith("Frontier_", StringComparison.Ordinal)) continue;
                object dto = Reflect.Read(c, "ItemDto") ?? throw new InvalidDataException("Frontier commodity has no item DTO");
                string template = Reflect.Read(dto, "TemplateId")?.ToString() ?? "";
                if (!O.itemIds.Contains(template)) throw new InvalidDataException("Frontier shop commodity points to an unexpected template " + template);
                lock (O.sync) O.shopCommodities.Add(id);
            }
            Plugin.Current.WriteReport();
        }
        catch (Exception e) { O.RecordError("Live pricelist", e); }
    }
    private static void ItemPostfix(object? __result)
    {
        if (!Plugin.Current.Active || __result is null) return;
        try
        {
            string id = Reflect.Read(__result, "TemplateId")?.ToString() ?? "";
            if (O.itemIds.Contains(id)) lock (O.sync) O.instances.Add(id);
        }
        catch (Exception e) { O.RecordError("Item factory evidence", e); }
    }
    private static void AiInitializePostfix(object __0)
    {
        if (!Plugin.Current.Active || __0 is null) return;
        try
        {
            if (Reflect.Read(__0, "IsAuthoritative") is true)
            {
                lock (O.sync)
                {
                    if (Reflect.SameNative(O.lastAuthoritativeSession, __0)) return;
                    O.lastAuthoritativeSession = __0; O.aiInitializations++;
                }
                Plugin.Current.ResetEnemySession();
            }
        }
        catch (Exception e) { O.RecordError("AI authority", e); }
    }
    private static void EquipmentPostfix(object __1, object? __result)
    {
        if (!Plugin.Current.Active || __1 is null) return;
        try
        {
            string id = Reflect.Id(__1);
            if (!O.eliteIds.Contains(id)) return;
            if (__result is null) throw new InvalidDataException("Elite GetPlayerEquipment returned null: " + id);
            bool added; lock (O.sync) added = O.eliteEquipment.Add(id);
            if (added) { Plugin.Current.Log.LogInfo("Elite native equipment created: " + id); Plugin.Current.WriteReport(); }
        }
        catch (Exception e) { O.RecordError("Elite equipment", e); }
    }
    private static Exception? AiFinalizer(Exception? __exception)
    {
        if (__exception is not null && Plugin.Current.Active) { lock (O.sync) O.aiFailures++; O.RecordError("AI OnUpdate", __exception); }
        return __exception; // Never suppress a real game exception or fabricate success.
    }
    private static void RecipePostfix(object? __4)
    {
        if (!Plugin.Current.Active || __4 is null) return;
        try { string id = Reflect.Id(__4); if (O.recipeIds.Contains(id)) lock (O.sync) O.recipeViews.Add(id); }
        catch (Exception e) { O.RecordError("Recipe view evidence", e); }
    }
    private void RecordError(string stage, Exception e)
    {
        string text = stage + ": " + e.GetBaseException().Message;
        lock (sync) { if (errors.Count >= 20 || errors.Contains(text)) return; errors.Add(text); }
        p.Log.LogError("Runtime evidence BROKEN: " + text); p.WriteReport();
    }
}
