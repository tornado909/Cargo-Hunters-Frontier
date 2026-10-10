using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace Frontier.Engine;

public static class RuntimeTestContent
{
	public static void Apply(string name, JsonNode root, JsonObject manifest, Func<string, bool> enabled)
	{
		if (name != "shop_templates")
		{
			return;
		}
		JsonArray jsonArray = root.AsArray().Single((JsonNode x) => PatchEngine.Scalar(x["Id"]) == PatchEngine.Str(manifest, "testShopId"))["PricelistTemplate"]["Commodities"].AsArray();
		foreach (JsonNode item in manifest["testOffers"].AsArray())
		{
			if (enabled(PatchEngine.Str(item, "module")))
			{
				string id = PatchEngine.Str(item, "id");
				if (jsonArray.Any((JsonNode x) => x?["Id"]?.GetValue<string>() == id))
				{
					throw new InvalidDataException("Duplicate test offer " + id);
				}
				jsonArray.Add(new JsonObject
				{
					["Id"] = id,
					["Count"] = item["count"].GetValue<int>(),
					["Chance"] = 1.0,
					["BuyFromShopPriceCoefficient"] = 0.05,
					["PositionViewPriority"] = -1000 + jsonArray.Count,
					["UnlockRequirement"] = new JsonObject
					{
						["$t"] = 32,
						["_filterPolicy"] = 1,
						["_minAccountLevel"] = 1
					},
					["Item"] = PatchEngine.Copy(item["item"])
				});
			}
		}
	}
}
