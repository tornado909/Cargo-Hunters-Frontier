using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Frontier.Loot;

public static class LootMath
{
	public static double Sample(string key, uint state = 0u, string channel = "loot")
	{
		byte[] array = SHA256.HashData(Encoding.UTF8.GetBytes("frontier-rarity-v1|" + channel + "|" + key + "|" + state.ToString(CultureInfo.InvariantCulture)));
		return (double)(uint)(array[0] | (array[1] << 8) | (array[2] << 16) | (array[3] << 24)) / 4294967296.0;
	}

	public static int Tier(double value, double[] weights)
	{
		if (weights == null || weights.Length != 5)
		{
			throw new ArgumentException("Five rarity weights required");
		}
		double num = 0.0;
		foreach (double num2 in weights)
		{
			if (double.IsNaN(num2) || double.IsInfinity(num2) || num2 < 0.0)
			{
				throw new ArgumentException("Invalid weight");
			}
			num += num2;
		}
		if (num <= 0.0)
		{
			throw new ArgumentException("Empty distribution");
		}
		double num3 = Math.Clamp(value, 0.0, Math.BitDecrement(1.0)) * num;
		for (int j = 0; j < 5; j++)
		{
			if (num3 < weights[j])
			{
				return j;
			}
			num3 -= weights[j];
		}
		return 4;
	}

	public static int Quality(double value)
	{
		return Math.Min(2, (int)(Math.Clamp(value, 0.0, 0.9999999999) * 3.0));
	}

	public static double CriticalFactor(double chance, double power, double roll)
	{
		chance = Math.Clamp(chance, 0.0, 5.0);
		power = Math.Max(1.0, power);
		int num = (int)Math.Floor(chance);
		if (roll < chance - (double)num)
		{
			num++;
		}
		return 1.0 + (double)num * (power - 1.0);
	}

	public static bool Conserve(double chance, double roll, int ammunition)
	{
		if (ammunition > 0)
		{
			return roll < Math.Clamp(chance, 0.0, 0.95);
		}
		return false;
	}
}
