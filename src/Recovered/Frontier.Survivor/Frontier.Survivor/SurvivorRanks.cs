using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Frontier.Loot;

namespace Frontier.Survivor;

internal static class SurvivorRanks
{
	private static MeleeRankCatalog? catalog;

	private static bool registered;

	internal static void Load()
	{
		if (catalog != null)
		{
			return;
		}
		MeleeRankCatalog meleeRankCatalog = JsonSerializer.Deserialize<MeleeRankCatalog>(File.ReadAllText(Path.Combine(Identity.Folder, "survivor-ranks.json")), new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		}) ?? throw new InvalidDataException("Survivor melee rank metadata missing");
		if (meleeRankCatalog.Format != "frontier.survivor-ranks/1" || meleeRankCatalog.Families.Count != 2 || meleeRankCatalog.Rows.Length != 56)
		{
			throw new InvalidDataException("Survivor melee rank dimensions");
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		MeleeRankRow[] rows = meleeRankCatalog.Rows;
		foreach (MeleeRankRow meleeRankRow in rows)
		{
			if (meleeRankRow.Rank < 1 || meleeRankRow.Rank > 28 || !Guid.TryParse(meleeRankRow.RankedId, out var result) || !Guid.TryParse(meleeRankRow.AiId, out result) || !hashSet.Add(meleeRankRow.RankedId) || !meleeRankCatalog.Families.TryGetValue(meleeRankRow.BaseId, out string[] value) || value.Length != 28 || value[meleeRankRow.Rank - 1] != meleeRankRow.RankedId)
			{
				throw new InvalidDataException("Survivor melee rank identity mismatch");
			}
			if (Math.Abs(meleeRankRow.HealthFactor - RankPolicy.EnemyHealth(meleeRankRow.Rank)) > 1E-08 || Math.Abs(meleeRankRow.DamageFactor - RankPolicy.EnemyDamage(meleeRankRow.Rank)) > 1E-08 || Math.Abs(meleeRankRow.MoveFactor - RankPolicy.EnemyMove(meleeRankRow.Rank)) > 1E-08 || Math.Abs(meleeRankRow.XpFactor - RankPolicy.EnemyXP(meleeRankRow.Rank)) > 1E-08)
			{
				throw new InvalidDataException("Melee rank factors differ from the installed Frontier policy");
			}
		}
		catalog = meleeRankCatalog;
	}

	internal static string[] Variants(string baseId)
	{
		Load();
		if (catalog.Families.TryGetValue(baseId, out string[] value))
		{
			return value;
		}
		LadderData data = RankLadder.Data;
		if (data != null && data.BotVariants.TryGetValue(baseId, out value))
		{
			return value;
		}
		throw new InvalidDataException("No existing or Survivor-owned native rank family: " + baseId);
	}

	internal static void RegisterPresentation()
	{
		if (registered)
		{
			return;
		}
		Load();
		if (!RankLadder.Active)
		{
			throw new InvalidOperationException("Existing Rank28 runtime has not activated");
		}
		IDictionary dictionary = (IDictionary)(AppDomain.CurrentDomain.GetAssemblies().Single((Assembly a) => a.GetName().Name == "Frontier.Core").GetType("Frontier.Core.ElitePresentation", throwOnError: true)
			.GetField("ByPreset", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) ?? throw new MissingFieldException("Existing rank presentation registry"));
		MeleeRankRow[] rows = catalog.Rows;
		foreach (MeleeRankRow meleeRankRow in rows)
		{
			string text = RankPolicy.Name(meleeRankRow.Rank);
			if (dictionary.Contains(meleeRankRow.RankedId) && !object.Equals(dictionary[meleeRankRow.RankedId], text))
			{
				throw new InvalidDataException("Conflicting enemy rank presentation ID");
			}
			dictionary[meleeRankRow.RankedId] = text;
		}
		registered = true;
		Trace.Data["survivorMeleeRankIntegration"] = new
		{
			newPresetIds = 56,
			rankFactorsVerifiedAgainstInstalledPolicy = true,
			existingPresetsModified = false,
			sharedRandomSpawnAllowlistModified = false
		};
	}
}
