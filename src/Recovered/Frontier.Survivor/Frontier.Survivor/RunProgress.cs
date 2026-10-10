using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontier.Survivor;

internal sealed class RunProgress
{
	internal int Level = 1;

	internal int Experience;

	internal int Kills;

	internal int PendingChoices;

	internal int OfferSerial;

	internal readonly int[] Upgrades = new int[6];

	internal int[] Offer = Array.Empty<int>();

	private readonly Random random;

	internal static readonly string[] Names = new string[6] { "Усилитель урона", "Разгон затвора", "Точный удар", "Сила критического удара", "Бронебойность", "Обучение" };

	internal static readonly string[] Descriptions = new string[6] { "+15% урона", "+10% скорострельности", "+5 п.п. шанса крита", "+20% множителя крита", "+1 к пробитию", "+20% опыта в забеге" };

	internal static readonly int[] Caps = new int[6] { 30, 15, 12, 20, 10, 10 };

	internal int Required => 40 + 25 * (Level - 1);

	internal float Damage => 0.15f * (float)Upgrades[0];

	internal float FireRate => 0.1f * (float)Upgrades[1];

	internal float Crit => 0.05f * (float)Upgrades[2];

	internal float CritPower => 0.2f * (float)Upgrades[3];

	internal int Penetration => Upgrades[4];

	internal float ExperienceFactor => 1f + 0.2f * (float)Upgrades[5];

	internal RunProgress(int seed)
	{
		random = new Random(seed);
	}

	internal void Award(int nativeExperience)
	{
		checked
		{
			Kills++;
			Experience += (int)Math.Clamp(Math.Round((float)Math.Max(1, nativeExperience) * ExperienceFactor), 1.0, 100000.0);
		}
		while (Level < 100 && Experience >= Required)
		{
			Experience -= Required;
			Level++;
			PendingChoices++;
		}
		if (Level == 100)
		{
			Experience = Math.Min(Experience, Required - 1);
		}
		MakeOffer();
	}

	internal bool Choose(int index)
	{
		if (PendingChoices < 1 || index < 0 || index >= Offer.Length)
		{
			return false;
		}
		int num = Offer[index];
		if (Upgrades[num] >= Caps[num])
		{
			return false;
		}
		Upgrades[num]++;
		PendingChoices--;
		Offer = Array.Empty<int>();
		MakeOffer();
		return true;
	}

	private void MakeOffer()
	{
		if (PendingChoices < 1 || Offer.Length != 0)
		{
			return;
		}
		List<int> list = (from i in Enumerable.Range(0, 6)
			where Upgrades[i] < Caps[i]
			select i).ToList();
		if (list.Count == 0)
		{
			PendingChoices = 0;
			return;
		}
		for (int num = list.Count - 1; num > 0; num--)
		{
			int num2 = random.Next(num + 1);
			List<int> list2 = list;
			int index = num;
			int index2 = num2;
			int value = list[num2];
			int value2 = list[num];
			list2[index] = value;
			list[index2] = value2;
		}
		Offer = list.Take(3).ToArray();
		checked
		{
			OfferSerial++;
		}
	}

	internal ProgressState Export()
	{
		return new ProgressState(Level, Experience, Kills, PendingChoices, OfferSerial, Upgrades.ToArray(), Offer.ToArray());
	}

	internal static bool Valid(ProgressState? p)
	{
		if (p == null || p.Level < 1 || p.Level > 100 || p.Kills < 0 || p.Experience < 0 || p.Experience >= 40 + 25 * (p.Level - 1) || p.Pending < 0 || p.Pending > 99 || p.OfferSerial < 0 || p.OfferSerial > 100 || p.Upgrades == null || p.Upgrades.Length != 6 || p.Offer == null || p.Offer.Length > 3)
		{
			return false;
		}
		for (int i = 0; i < 6; i++)
		{
			if (p.Upgrades[i] < 0 || p.Upgrades[i] > Caps[i])
			{
				return false;
			}
		}
		if (p.Upgrades.Sum() + p.Pending > p.Level - 1 || p.Offer.Distinct().Count() != p.Offer.Length || p.Offer.Any((int num) => num < 0 || num >= 6 || p.Upgrades[num] >= Caps[num]))
		{
			return false;
		}
		if (p.Pending != 0)
		{
			if (p.Offer.Length != 0)
			{
				return p.OfferSerial > 0;
			}
			return false;
		}
		return p.Offer.Length == 0;
	}

	internal static RunProgress Import(ProgressState p)
	{
		if (!Valid(p))
		{
			throw new ArgumentException("Invalid authoritative progress snapshot");
		}
		RunProgress runProgress = new RunProgress(0)
		{
			Level = p.Level,
			Experience = p.Experience,
			Kills = p.Kills,
			PendingChoices = p.Pending,
			OfferSerial = p.OfferSerial,
			Offer = p.Offer.ToArray()
		};
		Array.Copy(p.Upgrades, runProgress.Upgrades, 6);
		return runProgress;
	}
}
