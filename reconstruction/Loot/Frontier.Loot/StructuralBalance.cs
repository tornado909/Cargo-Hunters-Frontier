using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;

namespace Frontier.Loot;

internal static class StructuralBalance
{
	internal readonly record struct Counts(int Magazines, int BodyParts, int ModifierComponents);

	internal static Counts Apply(JsonNode root, BalanceConfiguration? config)
	{
		if (config == null || !(root is JsonArray jsonArray))
		{
			return default(Counts);
		}
		Dictionary<string, JsonObject> dictionary = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
		foreach (JsonNode item3 in jsonArray)
		{
			if (item3 is JsonObject jsonObject && jsonObject["_id"] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value))
			{
				dictionary[value] = jsonObject;
			}
		}
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		List<(JsonObject, JsonObject, int, JsonObject, double, bool)> list = new List<(JsonObject, JsonObject, int, JsonObject, double, bool)>();
		foreach (JsonNode item4 in jsonArray)
		{
			if (!(item4 is JsonObject jsonObject2))
			{
				continue;
			}
			string text = Component(jsonObject2, 26276)?["Name"]?.GetValue<string>() ?? "";
			if (!text.StartsWith("Frontier/Rank28/", StringComparison.Ordinal))
			{
				continue;
			}
			string[] array = text.Split('/');
			if (array.Length != 4 || !config.Families.TryGetValue(array[2], out BalanceProfile value2))
			{
				throw new InvalidDataException("Unknown structural rank family");
			}
			int num4 = RankPolicy.Parse(array[3]);
			if (num4 == 0)
			{
				throw new InvalidDataException("Invalid structural rank");
			}
			if (!dictionary.TryGetValue(array[2], out var value3) || !dictionary.TryGetValue(value2.AnchorTemplate, out var value4))
			{
				throw new InvalidDataException("Missing original/legacy template");
			}
			JsonNode jsonNode = Component(jsonObject2, 1204)?["_data"];
			if (jsonNode?["CategoryId"]?.GetValue<int>() != value2.Category || jsonNode?["SubCategoryId"]?.GetValue<int>() != value2.SubCategory)
			{
				throw new InvalidDataException("Structural category mismatch");
			}
			JsonObject jsonObject3 = Component(jsonObject2, 47601)?["_data"]?.AsObject();
			JsonObject jsonObject4 = Component(jsonObject2, 60938)?["_data"]?.AsObject();
			int item = 0;
			double item2 = 0.0;
			int? magazineCapacity = value2.MagazineCapacity;
			if (magazineCapacity.HasValue)
			{
				int valueOrDefault = magazineCapacity.GetValueOrDefault();
				JsonNode jsonNode2 = Component(value3, 47601)?["_data"];
				JsonNode jsonNode3 = Component(value4, 47601)?["_data"];
				if (jsonObject3 == null || jsonNode2 == null || jsonNode3 == null || jsonNode3["Capacity"].GetValue<int>() != valueOrDefault)
				{
					throw new InvalidDataException("Magazine anchor mismatch");
				}
				item = BalanceMath.Capacity(jsonNode2["Capacity"].GetValue<int>(), valueOrDefault, config.Scale(num4));
			}
			double? bodyMaxHealth = value2.BodyMaxHealth;
			if (bodyMaxHealth.HasValue)
			{
				double valueOrDefault2 = bodyMaxHealth.GetValueOrDefault();
				JsonNode jsonNode4 = Component(value3, 60938)?["_data"];
				JsonNode jsonNode5 = Component(value4, 60938)?["_data"];
				if (jsonObject4 == null || jsonNode4 == null || jsonNode5 == null || Math.Abs(jsonNode5["MaxHealth"].GetValue<double>() - valueOrDefault2) > 0.0001)
				{
					throw new InvalidDataException("Body health anchor mismatch");
				}
				item2 = BalanceMath.MaxHealth(jsonNode4["MaxHealth"].GetValue<double>(), valueOrDefault2, config.Scale(num4));
			}
			list.Add((jsonObject2, jsonObject3, item, jsonObject4, item2, Component(jsonObject2, 14294) == null));
		}
		foreach (var item5 in list)
		{
			if (item5.Item3 > 0)
			{
				item5.Item2["Capacity"] = item5.Item3;
				num++;
			}
			if (item5.Item5 > 0.0)
			{
				item5.Item4["MaxHealth"] = item5.Item5;
				num2++;
			}
			if (item5.Item6)
			{
				item5.Item1["_components"].AsArray().Add(new JsonObject
				{
					{
						"$t",
						(JsonNode)14294
					},
					{
						"_data",
						new JsonObject { 
						{
							"AttributeSourceModificationList",
							new JsonArray()
						} }
					}
				});
				num3++;
			}
		}
		return new Counts(num, num2, num3);
	}

	internal static JsonObject? Component(JsonObject item, int tag)
	{
		if (!(item["_components"] is JsonArray jsonArray))
		{
			return null;
		}
		foreach (JsonNode item2 in jsonArray)
		{
			if (item2 is JsonObject jsonObject && jsonObject["$t"] is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var value) && value == tag)
			{
				return jsonObject;
			}
		}
		return null;
	}
}
