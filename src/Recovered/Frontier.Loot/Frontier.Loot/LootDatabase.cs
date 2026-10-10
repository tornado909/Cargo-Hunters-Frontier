using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Frontier.Loot;

public sealed class LootDatabase
{
	public int Schema { get; set; }

	public string Version { get; set; } = "";

	public Dictionary<string, bool> Settings { get; set; } = new Dictionary<string, bool>();

	public Dictionary<string, double[]> Weights { get; set; } = new Dictionary<string, double[]>();

	public Dictionary<string, Family> Families { get; set; } = new Dictionary<string, Family>();

	public Dictionary<string, Trait> Variants { get; set; } = new Dictionary<string, Trait>();

	public Dictionary<string, string> LegacyBodyBases { get; set; } = new Dictionary<string, string>();

	public Dictionary<string, string> BotRanks { get; set; } = new Dictionary<string, string>();

	public Dictionary<string, string> PlatformRanks { get; set; } = new Dictionary<string, string>();

	public static LootDatabase Load(string path)
	{
		LootDatabase lootDatabase = JsonSerializer.Deserialize<LootDatabase>(File.ReadAllText(path), new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		}) ?? throw new InvalidDataException("Missing loot database");
		if (lootDatabase.Schema != 1 || lootDatabase.Version != "0.5.0")
		{
			throw new InvalidDataException("Wrong loot database version");
		}
		foreach (double[] value2 in lootDatabase.Weights.Values)
		{
			LootMath.Tier(0.5, value2);
		}
		foreach (KeyValuePair<string, Family> family in lootDatabase.Families)
		{
			if (family.Value.Variants.Length != 12)
			{
				throw new InvalidDataException("Incomplete rarity family");
			}
			for (int i = 0; i < 12; i++)
			{
				if (!lootDatabase.Variants.TryGetValue(family.Value.Variants[i], out Trait value) || value.Base != family.Key || value.Tier != 1 + i / 3 || value.Roll != i % 3)
				{
					throw new InvalidDataException("Rarity family mismatch");
				}
			}
		}
		return lootDatabase;
	}

	public string Select(string baseId, string key, string rank, out int tier)
	{
		if (RankLadder.Active && RankLadder.Data != null && RankLadder.Data.Families.ContainsKey(baseId))
		{
			return RankLadder.Select(baseId, key, rank, out tier);
		}
		tier = 0;
		if (!Families.TryGetValue(baseId, out Family value))
		{
			return baseId;
		}
		tier = LootMath.Tier(LootMath.Sample(key), Weights.TryGetValue(rank, out double[] value2) ? value2 : Weights["normal"]);
		if (tier != 0)
		{
			return value.Variants[(tier - 1) * 3 + LootMath.Quality(LootMath.Sample(key, 0u, "quality"))];
		}
		return baseId;
	}
}
