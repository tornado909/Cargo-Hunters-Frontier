using System;

namespace Frontier.ReferenceFix;

internal static class MapPolicy
{
	internal static bool Show(bool raid, bool frontend, bool modal, bool locked, bool window, bool fullMap)
	{
		if (raid)
		{
			return !(frontend || modal || locked || window || fullMap);
		}
		return false;
	}

	internal static bool Finite(float f)
	{
		if (!float.IsNaN(f))
		{
			return !float.IsInfinity(f);
		}
		return false;
	}

	internal static float Normalize(float a)
	{
		return (a % 360f + 360f) % 360f;
	}

	internal static float Delta(float a, float b)
	{
		return (Normalize(b - a) + 540f) % 360f - 180f;
	}

	internal static (float X, float Y) Project(float dx, float dz, float heading, float pixelsPerMetre)
	{
		double num = (double)heading * Math.PI / 180.0;
		double num2 = Math.Cos(num);
		double num3 = Math.Sin(num);
		return (X: (float)(((double)dx * num2 - (double)dz * num3) * (double)pixelsPerMetre), Y: (float)((0.0 - ((double)dx * num3 + (double)dz * num2)) * (double)pixelsPerMetre));
	}
}
