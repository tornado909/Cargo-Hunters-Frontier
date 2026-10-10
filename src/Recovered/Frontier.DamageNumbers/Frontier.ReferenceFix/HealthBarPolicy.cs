using System;
using System.Globalization;

namespace Frontier.ReferenceFix;

internal static class HealthBarPolicy
{
	internal const float Range = 35f;

	internal static bool Valid(float hp, float max)
	{
		if (float.IsFinite(hp) && float.IsFinite(max) && hp >= 0f)
		{
			return max > 0f;
		}
		return false;
	}

	internal static float Fill(float hp, float max)
	{
		if (!Valid(hp, max))
		{
			return 0f;
		}
		return Math.Clamp(hp / max, 0f, 1f);
	}

	internal static bool Damaged(float hp, float max)
	{
		if (Valid(hp, max) && hp > 0f)
		{
			return hp < max - Math.Max(0.01f, max * 1E-05f);
		}
		return false;
	}

	internal static bool Show(bool alive, bool ally, bool visible, float hp, float max, float distance)
	{
		if (alive && !ally && visible && Damaged(hp, max) && float.IsFinite(distance) && distance >= 0f)
		{
			return distance < 35f;
		}
		return false;
	}

	internal static float DistanceScale(float distance)
	{
		if (!float.IsFinite(distance))
		{
			return 0f;
		}
		return 1f - 0.4f * Math.Clamp((distance - 8f) / 27f, 0f, 1f);
	}

	internal static float Opacity(float distance)
	{
		if (!float.IsFinite(distance))
		{
			return 0f;
		}
		return 1f - Math.Clamp((distance - 22f) / 13f, 0f, 1f);
	}

	internal static string Number(float hp)
	{
		return Math.Clamp(Math.Ceiling(float.IsFinite(hp) ? hp : 0f), 0.0, 2147483647.0).ToString(CultureInfo.InvariantCulture);
	}

	internal static bool Fresh(double now, double sampled)
	{
		if (double.IsFinite(now) && double.IsFinite(sampled) && now >= sampled)
		{
			return now - sampled <= 0.22;
		}
		return false;
	}

	internal static float PartMaximum(float[] maxima)
	{
		if (maxima == null || maxima.Length == 0 || maxima.Length > 32)
		{
			return float.NaN;
		}
		double num = 0.0;
		foreach (float num2 in maxima)
		{
			if (!float.IsFinite(num2) || num2 < 0f)
			{
				return float.NaN;
			}
			num += (double)num2;
		}
		if (!(num > 0.0) || !(num <= 3.4028234663852886E+38))
		{
			return float.NaN;
		}
		return (float)num;
	}

	internal static bool Choose(bool entityAvailable, float entityHp, float entityMax, bool providerAvailable, float providerHp, float providerMax, out float hp, out float max, out bool fromEntity)
	{
		hp = (max = 0f);
		fromEntity = false;
		if (entityAvailable && Valid(entityHp, entityMax))
		{
			hp = entityHp;
			max = entityMax;
			fromEntity = true;
			return true;
		}
		if (entityAvailable && float.IsFinite(entityHp) && entityHp == 0f)
		{
			return false;
		}
		if (providerAvailable && Valid(providerHp, providerMax))
		{
			hp = ((entityAvailable && float.IsFinite(entityHp) && entityHp >= 0f) ? entityHp : providerHp);
			max = providerMax;
			return true;
		}
		return false;
	}
}
