using System;

namespace Frontier.ReferenceFix;

internal sealed class MovementHeading
{
	private bool has;

	private float x;

	private float z;

	private double at;

	internal float Degrees { get; private set; }

	internal void Reset()
	{
		has = false;
	}

	internal void Update(float nx, float nz, float fallbackYaw, double now)
	{
		if (!MapPolicy.Finite(nx) || !MapPolicy.Finite(nz) || !MapPolicy.Finite(fallbackYaw))
		{
			return;
		}
		if (!has)
		{
			has = true;
			x = nx;
			z = nz;
			at = now;
			Degrees = MapPolicy.Normalize(fallbackYaw);
			return;
		}
		double num = now - at;
		float num2 = nx - x;
		float num3 = nz - z;
		if (!(num <= 0.0))
		{
			x = nx;
			z = nz;
			at = now;
			double num4 = Math.Sqrt(num2 * num2 + num3 * num3);
			if (num > 2.0 || num4 > Math.Max(25.0, num * 35.0))
			{
				Degrees = MapPolicy.Normalize(fallbackYaw);
			}
			else if (!(num4 < 0.025) && !(num4 / num < 0.2))
			{
				float b = MapPolicy.Normalize((float)(Math.Atan2(num2, num3) * 180.0 / Math.PI));
				Degrees = MapPolicy.Normalize(Degrees + MapPolicy.Delta(Degrees, b) * (float)(1.0 - Math.Exp(-12.0 * num)));
			}
		}
	}
}
