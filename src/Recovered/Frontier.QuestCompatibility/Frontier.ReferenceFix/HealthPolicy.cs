using System;

namespace Frontier.ReferenceFix;

internal static class HealthPolicy
{
	internal const int Destroyed = 16;

	internal static float Target(float current, float maximum, int status, bool dismembered, int skillLevel, bool bot, bool dead, double elapsed)
	{
		if (skillLevel < 1 || bot || dead || dismembered || (status & 0x10) != 0 || !float.IsFinite(current) || !float.IsFinite(maximum) || current < 0f || maximum <= 0f || current >= maximum || !double.IsFinite(elapsed) || elapsed <= 0.0)
		{
			return current;
		}
		return Math.Min(maximum, current + (float)((double)maximum * 0.01 * Math.Min(1.0, elapsed)));
	}

	internal static float ConfirmedIncrease(float before, float target, float after, float maximum)
	{
		if (!float.IsFinite(after) || after <= before || after > maximum + 0.001f || after > target + 0.001f)
		{
			return 0f;
		}
		return after - before;
	}
}
