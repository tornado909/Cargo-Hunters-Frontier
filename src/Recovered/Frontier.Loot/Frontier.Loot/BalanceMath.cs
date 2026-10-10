using System;
using System.IO;

namespace Frontier.Loot;

public static class BalanceMath
{
	public static double Probability(double anchor, double strength)
	{
		return Math.Clamp((strength <= 1.0) ? (anchor * strength) : (1.0 - Math.Pow(1.0 - anchor, strength)), 0.0, 0.95);
	}

	public static double ReductionScale(double strength)
	{
		if (!(strength <= 1.0))
		{
			return 1.0 + 0.1 * (1.0 - 1.0 / strength);
		}
		return strength;
	}

	public static int Capacity(int original, int legacy, double strength)
	{
		if (original < 1 || legacy < original || !double.IsFinite(strength) || strength <= 0.0)
		{
			throw new InvalidDataException("Invalid capacity anchor");
		}
		return checked(original + (int)Math.Round((double)(legacy - original) * strength, MidpointRounding.AwayFromZero));
	}

	public static double MaxHealth(double original, double legacy, double strength)
	{
		if (original <= 0.0 || legacy < original || !double.IsFinite(original + legacy + strength) || strength <= 0.0)
		{
			throw new InvalidDataException("Invalid health anchor");
		}
		return Math.Round(original + (legacy - original) * strength, 1, MidpointRounding.AwayFromZero);
	}
}
