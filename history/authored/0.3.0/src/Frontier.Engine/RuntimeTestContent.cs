using System.Text.Json.Nodes;
namespace Frontier.Engine;

/// <summary>Explicit test routes, separate from the regular economy. No new persistent template IDs.</summary>
public static class RuntimeTestContent
{
    public static void Apply(string name, JsonNode root, JsonObject manifest, Func<string, bool> enabled)
    {
        if (name != "shop_templates") return;
        var shop = root.AsArray().Single(x => PatchEngine.Scalar(x!["Id"]) == PatchEngine.Str(manifest, "testShopId"))!;
        var list = shop["PricelistTemplate"]!["Commodities"]!.AsArray();
        foreach (var offer in manifest["testOffers"]!.AsArray())
        {
            if (!enabled(PatchEngine.Str(offer!, "module"))) continue;
            string id = PatchEngine.Str(offer!, "id");
            if (list.Any(x => x?["Id"]?.GetValue<string>() == id)) throw new InvalidDataException("Duplicate test offer " + id);
            list.Add(new JsonObject {
                ["Id"] = id, ["Count"] = offer!["count"]!.GetValue<int>(),
                ["Chance"] = 1.0, ["BuyFromShopPriceCoefficient"] = 0.05,
                ["PositionViewPriority"] = -1000 + list.Count,
                ["UnlockRequirement"] = new JsonObject { ["$t"] = 32, ["_filterPolicy"] = 1, ["_minAccountLevel"] = 1 },
                ["Item"] = PatchEngine.Copy(offer["item"])
            });
        }
    }
}
