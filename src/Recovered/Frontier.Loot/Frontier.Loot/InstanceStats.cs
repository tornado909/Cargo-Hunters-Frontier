using System;
using System.Collections.Generic;
using System.IO;

namespace Frontier.Loot;

public static class InstanceStats
{
	public static BalanceConfiguration? Configuration { get; private set; }

	public static void Configure(BalanceConfiguration configuration)
	{
		configuration.Validate();
		Configuration = configuration;
	}

	public static Trait Roll(Trait t, string id)
	{
		if (t.RankIndex <= 0)
		{
			return t;
		}
		BalanceConfiguration c = Configuration ?? throw new InvalidOperationException("Balance profiles not initialized");
		if (!c.Families.TryGetValue(t.Base, out BalanceProfile value) || value.Category != t.Category || value.SubCategory != t.SubCategory)
		{
			throw new InvalidDataException("No matching legacy anchor for ranked item");
		}
		double s = c.Scale(t.RankIndex);
		Trait trait = new Trait
		{
			Base = t.Base,
			Tier = t.Tier,
			Roll = t.Roll,
			Category = t.Category,
			SubCategory = t.SubCategory,
			RankIndex = t.RankIndex
		};
		trait.Damage = V(value.Damage, "damage");
		trait.Crit = V(value.Crit, "crit");
		trait.CritPower = V(value.CritPower, "critPower");
		trait.FireRate = V(value.FireRate, "fire");
		trait.SaveAmmo = (float)Math.Round(BalanceMath.Probability(value.SaveAmmo, s) * J("save"), 5, MidpointRounding.AwayFromZero);
		trait.SaveAmmo = Math.Clamp(trait.SaveAmmo, 0f, 0.95f);
		trait.Penetration = (int)Math.Round((double)value.Penetration * s, MidpointRounding.AwayFromZero);
		trait.Executor = value.Executor && t.RankIndex >= 19;
		foreach (KeyValuePair<int, double> item in value.Native)
		{
			double num = ((item.Value < 0.0) ? BalanceMath.ReductionScale(s) : s);
			int num2 = ((item.Key == 11) ? 10 : ((item.Key == 30) ? 29 : item.Key));
			double num3 = item.Value * num * J("attr:" + num2);
			if (item.Value < 0.0)
			{
				num3 = Math.Max(-0.8, num3);
			}
			trait.Native[item.Key] = (float)Math.Round(num3, 5, MidpointRounding.AwayFromZero);
		}
		return trait;
		double J(string ch)
		{
			return 1.0 + (2.0 * RankPolicy.Sample(id, t.Base + "|" + t.RankIndex + "|" + ch) - 1.0) * c.RollSpread;
		}
		float V(double anchor, string ch)
		{
			return (float)Math.Round(anchor * s * J(ch), 5, MidpointRounding.AwayFromZero);
		}
	}
}
