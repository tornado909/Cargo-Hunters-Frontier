using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Frontier.Loot;

public static class RankPolicy
{
	public static readonly string[] Grades = new string[10] { "F", "E", "D", "C", "B", "A", "S", "SS", "SSS", "Z" };

	public static readonly string[] Colors = new string[10] { "#87939E", "#70BB77", "#49C5CE", "#609EFF", "#AD79E7", "#E478CC", "#EDCA61", "#F69247", "#F15D73", "#FFF2B3" };

	public const string Namespace = "frontier-rank28-instance-v1";

	private static readonly double[,] DamageRanges = new double[28, 2]
	{
		{ 2.0, 5.0 },
		{ 3.0, 6.0 },
		{ 4.0, 7.0 },
		{ 8.0, 10.0 },
		{ 9.0, 12.0 },
		{ 10.0, 14.0 },
		{ 15.0, 18.0 },
		{ 17.0, 20.0 },
		{ 19.0, 22.0 },
		{ 24.0, 28.0 },
		{ 27.0, 31.0 },
		{ 30.0, 34.0 },
		{ 36.0, 41.0 },
		{ 40.0, 45.0 },
		{ 44.0, 49.0 },
		{ 52.0, 58.0 },
		{ 57.0, 63.0 },
		{ 62.0, 68.0 },
		{ 72.0, 79.0 },
		{ 78.0, 85.0 },
		{ 84.0, 91.0 },
		{ 96.0, 104.0 },
		{ 103.0, 111.0 },
		{ 110.0, 118.0 },
		{ 123.0, 132.0 },
		{ 131.0, 140.0 },
		{ 139.0, 148.0 },
		{ 158.0, 172.0 }
	};

	public static int Grade(int rank)
	{
		switch (rank)
		{
		default:
			return -1;
		case 1:
		case 2:
		case 3:
		case 4:
		case 5:
		case 6:
		case 7:
		case 8:
		case 9:
		case 10:
		case 11:
		case 12:
		case 13:
		case 14:
		case 15:
		case 16:
		case 17:
		case 18:
		case 19:
		case 20:
		case 21:
		case 22:
		case 23:
		case 24:
		case 25:
		case 26:
		case 27:
			return (rank - 1) / 3;
		case 28:
			return 9;
		}
	}

	public static int Sub(int rank)
	{
		if (rank != 28)
		{
			return (rank - 1) % 3 + 1;
		}
		return 0;
	}

	public static string Name(int rank)
	{
		if (Grade(rank) >= 0)
		{
			return Grades[Grade(rank)] + ((rank == 28) ? "" : Sub(rank).ToString(CultureInfo.InvariantCulture));
		}
		return "";
	}

	public static int Parse(string? name)
	{
		if (name == "Z")
		{
			return 28;
		}
		for (int i = 1; i <= 27; i++)
		{
			if (Name(i) == name)
			{
				return i;
			}
		}
		return 0;
	}

	public static char Encode(int rank)
	{
		if (Grade(rank) >= 0)
		{
			return "ABCDEFGHIJKLMNOPQRSTUVWXYZab"[rank - 1];
		}
		throw new ArgumentOutOfRangeException("rank");
	}

	public static int Decode(char code)
	{
		return "ABCDEFGHIJKLMNOPQRSTUVWXYZab".IndexOf(code) + 1;
	}

	public static int BonusCount(int rank)
	{
		return Grade(rank) + 1;
	}

	public static (double Min, double Max) DamageRange(int rank)
	{
		if (Grade(rank) < 0)
		{
			throw new ArgumentOutOfRangeException("rank");
		}
		return (Min: DamageRanges[rank - 1, 0], Max: DamageRanges[rank - 1, 1]);
	}

	public static double Sample(string identity, string channel)
	{
		if (!Guid.TryParse(identity, out var result) || result == Guid.Empty)
		{
			throw new ArgumentException("Persistent nonempty item GUID required");
		}
		return (double)BinaryPrimitives.ReadUInt32LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes("frontier-rank28-instance-v1|" + result.ToString("N") + "|" + channel))) / 4294967296.0;
	}

	public static double Roll(string id, string channel, double min, double max)
	{
		return Math.Round(min + (max - min) * Sample(id, channel), 4, MidpointRounding.AwayFromZero);
	}

	public static double[] Weights(double ratio)
	{
		if (ratio <= 0.0 || ratio >= 1.0)
		{
			throw new ArgumentOutOfRangeException("ratio");
		}
		double[] array = new double[28];
		for (int i = 0; i < 28; i++)
		{
			array[i] = Math.Pow(ratio, i);
		}
		return array;
	}

	public static int Pick(double sample, IReadOnlyList<double> weights)
	{
		if (weights.Count != 28)
		{
			throw new ArgumentException("28 weights required");
		}
		if (!double.IsFinite(sample))
		{
			throw new ArgumentException("Nonfinite sample");
		}
		double num = 0.0;
		foreach (double weight in weights)
		{
			if (!double.IsFinite(weight) || weight < 0.0)
			{
				throw new ArgumentException("Invalid weight");
			}
			num += weight;
		}
		if (num <= 0.0)
		{
			throw new ArgumentException("Empty weights");
		}
		double num2 = Math.Clamp(sample, 0.0, Math.BitDecrement(1.0)) * num;
		for (int i = 0; i < 28; i++)
		{
			if (num2 < weights[i])
			{
				return i + 1;
			}
			num2 -= weights[i];
		}
		return 28;
	}

	public static double Chance(int rank, double ratio)
	{
		double num = 0.0;
		for (int i = 0; i < 28; i++)
		{
			num += Math.Pow(ratio, i);
		}
		return Math.Pow(ratio, rank - 1) / num;
	}

	public static string Visual(int rank)
	{
		if (Grade(rank) >= 0)
		{
			return (new string[10] { "thin-edges", "full-frame", "underlight", "notched-frame", "soft-pulse", "orbit-1", "orbit-2", "orbit-4", "orbit-6", "crown-orbit-8" })[Grade(rank)];
		}
		return "";
	}

	public static double EnemyHealth(int rank)
	{
		return Math.Round(1.0 + 0.025 * (double)rank + 0.0026 * (double)rank * (double)rank, 4);
	}

	public static double EnemyDamage(int rank)
	{
		return Math.Round(1.0 + 0.015 * (double)rank + 0.00035 * (double)rank * (double)rank, 4);
	}

	public static double EnemyMove(int rank)
	{
		return Math.Round(1.0 + Math.Min(0.16, 0.006 * (double)rank), 4);
	}

	public static double EnemyXP(int rank)
	{
		return Math.Round(1.0 + 0.065 * (double)rank + 0.004 * (double)rank * (double)rank, 4);
	}
}
