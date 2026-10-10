using System;

namespace Frontier.ReferenceFix;

internal static class TerrainRaster
{
	internal static bool HasDetail(MapPixels p)
	{
		if (p.Width < 2 || p.Height < 2 || p.Rgba.Length != p.Width * p.Height * 4)
		{
			return false;
		}
		int num = 765;
		int num2 = 0;
		int num3 = 0;
		for (int i = 0; i < p.Height; i += Math.Max(1, p.Height / 32))
		{
			for (int j = 0; j < p.Width; j += Math.Max(1, p.Width / 32))
			{
				int num4 = (i * p.Width + j) * 4;
				if (p.Rgba[num4 + 3] >= 16)
				{
					int val = p.Rgba[num4] + p.Rgba[num4 + 1] + p.Rgba[num4 + 2];
					num = Math.Min(num, val);
					num2 = Math.Max(num2, val);
					num3++;
				}
			}
		}
		if (num3 > 4)
		{
			return num2 - num > 12;
		}
		return false;
	}

	internal static void Render(MapPixels src, byte[] dst, int size, float centerX, float centerZ, float heading, float minX, float maxX, float minZ, float maxZ, float u, float v, float uw, float vh, byte br, byte bg, byte bb)
	{
		if (size < 16 || size > 512 || dst.Length != size * size * 4 || src.Rgba.Length != src.Width * src.Height * 4 || !(maxX > minX) || !(maxZ > minZ))
		{
			throw new ArgumentException("Invalid raster dimensions");
		}
		double num = (double)heading * Math.PI / 180.0;
		double num2 = Math.Cos(num);
		double num3 = Math.Sin(num);
		double num4 = (double)size / 2.0 - 8.0;
		double num5 = 100.0 / num4;
		for (int i = 0; i < size; i++)
		{
			for (int j = 0; j < size; j++)
			{
				double num6 = ((double)j + 0.5 - (double)size / 2.0) * num5;
				double num7 = ((double)size / 2.0 - (double)i - 0.5) * num5;
				double num8 = (double)centerX + num6 * num2 - num7 * num3;
				double num9 = (double)centerZ - num6 * num3 - num7 * num2;
				int num10 = (i * size + j) * 4;
				dst[num10] = br;
				dst[num10 + 1] = bg;
				dst[num10 + 2] = bb;
				dst[num10 + 3] = byte.MaxValue;
				if (!(num8 < (double)minX) && !(num8 > (double)maxX) && !(num9 < (double)minZ) && !(num9 > (double)maxZ))
				{
					double num11 = (double)u + (num8 - (double)minX) / (double)(maxX - minX) * (double)uw;
					double num12 = (double)v + (num9 - (double)minZ) / (double)(maxZ - minZ) * (double)vh;
					if (!(num11 < 0.0) && !(num11 > 1.0) && !(num12 < 0.0) && !(num12 > 1.0))
					{
						int num13 = Math.Clamp((int)(num11 * (double)src.Width), 0, src.Width - 1);
						int num14 = (Math.Clamp((int)(num12 * (double)src.Height), 0, src.Height - 1) * src.Width + num13) * 4;
						int num15 = src.Rgba[num14 + 3];
						int num16 = 255 - num15;
						dst[num10] = (byte)((src.Rgba[num14] * num15 + br * num16 + 127) / 255);
						dst[num10 + 1] = (byte)((src.Rgba[num14 + 1] * num15 + bg * num16 + 127) / 255);
						dst[num10 + 2] = (byte)((src.Rgba[num14 + 2] * num15 + bb * num16 + 127) / 255);
					}
				}
			}
		}
	}
}
