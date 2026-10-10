using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Frontier.Loot;

public sealed class BalanceConfiguration
{
	public int Schema { get; set; }

	public string Revision { get; set; } = "";

	public int AnchorRank { get; set; }

	public double RollSpread { get; set; }

	public double[] Power { get; set; } = Array.Empty<double>();

	public Dictionary<string, BalanceProfile> Families { get; set; } = new Dictionary<string, BalanceProfile>();

	public static BalanceConfiguration Load(string path)
	{
		BalanceConfiguration? obj = JsonSerializer.Deserialize<BalanceConfiguration>(File.ReadAllText(path), new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		}) ?? throw new InvalidDataException("Missing balance profiles");
		obj.Validate();
		return obj;
	}

	public void Validate()
	{
		if (Schema != 1 || Revision != "legacy-l3-s3-v1" || AnchorRank != 21 || Power.Length != 28 || Power[20] != 1.0 || RollSpread < 0.0 || RollSpread > 0.05 || Families.Count == 0)
		{
			throw new InvalidDataException("Invalid L3/S3 balance configuration");
		}
		double num = 0.0;
		double[] power = Power;
		foreach (double num2 in power)
		{
			if (!double.IsFinite(num2) || num2 <= num || num2 > 3.0)
			{
				throw new InvalidDataException("Non-increasing balance ladder");
			}
			num = num2;
		}
		foreach (KeyValuePair<string, BalanceProfile> family in Families)
		{
			BalanceProfile value = family.Value;
			if (Guid.TryParse(family.Key, out var result) && Guid.TryParse(value.AnchorTemplate, out result))
			{
				int i = value.Category;
				if ((i == 1 || i == 13 || i == 20) && value.Native != null)
				{
					power = new double[5] { value.Damage, value.Crit, value.CritPower, value.SaveAmmo, value.FireRate };
					foreach (double num3 in power)
					{
						if (!double.IsFinite(num3) || num3 < 0.0)
						{
							throw new InvalidDataException("Invalid combat anchor");
						}
					}
					if (value.SaveAmmo >= 0.95 || value.Penetration < 0 || value.Penetration > 8)
					{
						throw new InvalidDataException("Invalid probability/penetration anchor");
					}
					foreach (KeyValuePair<int, double> item in value.Native)
					{
						if (!double.IsFinite(item.Value) || (item.Key != 20 && Math.Abs(item.Value) >= 1.0))
						{
							throw new InvalidDataException("Invalid native attribute anchor");
						}
					}
					int? magazineCapacity = value.MagazineCapacity;
					if (magazineCapacity.HasValue)
					{
						int valueOrDefault = magazineCapacity.GetValueOrDefault();
						if (valueOrDefault < 1 || valueOrDefault > 4096)
						{
							goto IL_029a;
						}
					}
					double? bodyMaxHealth = value.BodyMaxHealth;
					if (!bodyMaxHealth.HasValue)
					{
						continue;
					}
					double valueOrDefault2 = bodyMaxHealth.GetValueOrDefault();
					if (double.IsFinite(valueOrDefault2) && !(valueOrDefault2 <= 0.0))
					{
						continue;
					}
					goto IL_029a;
				}
			}
			throw new InvalidDataException("Invalid model anchor");
			IL_029a:
			throw new InvalidDataException("Invalid structural anchor");
		}
	}

	public double Scale(int rank)
	{
		if (RankPolicy.Grade(rank) >= 0)
		{
			return Power[rank - 1];
		}
		throw new ArgumentOutOfRangeException("rank");
	}
}
