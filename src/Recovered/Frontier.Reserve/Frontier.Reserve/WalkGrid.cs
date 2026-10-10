using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Frontier.Reserve;

public sealed class WalkGrid
{
	private readonly LocationDefinition source;

	private readonly BoxDef[] floors;

	private readonly BoxDef[] obstacles;

	public float Step { get; }

	public float MinX { get; }

	public float MinZ { get; }

	public int Width { get; }

	public int Depth { get; }

	public bool[] Walkable { get; }

	public WalkGrid(LocationDefinition d, float step = 0.5f)
	{
		source = d;
		d.Validate();
		if ((double)step < 0.25 || step > 2f)
		{
			throw new ArgumentOutOfRangeException("step");
		}
		Step = step;
		floors = d.Boxes.Where((BoxDef b) => b.Solid && b.Kind == "floor").ToArray();
		obstacles = d.Boxes.Where((BoxDef b) => b.Solid && b.Kind != "floor" && b.Center[1] - b.Size[1] / 2f < d.Agent.Height && (double)(b.Center[1] + b.Size[1] / 2f) > 0.05).ToArray();
		if (floors.Length == 0)
		{
			throw new InvalidDataException("No floor");
		}
		MinX = floors.Min((BoxDef b) => b.Center[0] - b.Size[0] / 2f);
		MinZ = floors.Min((BoxDef b) => b.Center[2] - b.Size[2] / 2f);
		Width = (int)Math.Ceiling((floors.Max((BoxDef b) => b.Center[0] + b.Size[0] / 2f) - MinX) / step);
		Depth = (int)Math.Ceiling((floors.Max((BoxDef b) => b.Center[2] + b.Size[2] / 2f) - MinZ) / step);
		if ((long)Width * (long)Depth > 1000000)
		{
			throw new InvalidDataException("Navigation grid budget exceeded");
		}
		Walkable = new bool[Width * Depth];
		for (int num = 0; num < Depth; num++)
		{
			for (int num2 = 0; num2 < Width; num2++)
			{
				var (x, z) = Position(num2 + num * Width);
				Walkable[num2 + num * Width] = CanStand(x, z);
			}
		}
	}

	public bool CanStand(float x, float z)
	{
		float r = source.Agent.Radius;
		float[] array = new float[3]
		{
			0f - r,
			0f,
			r
		};
		foreach (float dx in array)
		{
			float[] array2 = new float[3]
			{
				0f - r,
				0f,
				r
			};
			foreach (float dz in array2)
			{
				if (!floors.Any((BoxDef b) => InXZ(b, x + dx, z + dz, 0.0001f)))
				{
					return false;
				}
			}
		}
		return !obstacles.Any((BoxDef b) => InXZ(b, x, z, r));
	}

	private static bool InXZ(BoxDef b, float x, float z, float pad)
	{
		if (Math.Abs(x - b.Center[0]) < b.Size[0] / 2f + pad)
		{
			return Math.Abs(z - b.Center[2]) < b.Size[2] / 2f + pad;
		}
		return false;
	}

	public (float X, float Z) Position(int i)
	{
		return (X: MinX + ((float)(i % Width) + 0.5f) * Step, Z: MinZ + ((float)(i / Width) + 0.5f) * Step);
	}

	public int Index(float x, float z)
	{
		int num = (int)Math.Floor((x - MinX) / Step);
		int num2 = (int)Math.Floor((z - MinZ) / Step);
		if (num < 0 || num >= Width || num2 < 0 || num2 >= Depth)
		{
			return -1;
		}
		return num + num2 * Width;
	}

	public IEnumerable<int> Neighbors(int i)
	{
		int x = i % Width;
		int z = i / Width;
		if (x > 0 && Walkable[i - 1])
		{
			yield return i - 1;
		}
		if (x < Width - 1 && Walkable[i + 1])
		{
			yield return i + 1;
		}
		if (z > 0 && Walkable[i - Width])
		{
			yield return i - Width;
		}
		if (z < Depth - 1 && Walkable[i + Width])
		{
			yield return i + Width;
		}
	}

	public int[] Flood(int start)
	{
		int[] array = Enumerable.Repeat(-1, Walkable.Length).ToArray();
		if (start < 0 || !Walkable[start])
		{
			return array;
		}
		Queue<int> queue = new Queue<int>();
		queue.Enqueue(start);
		array[start] = 0;
		while (queue.Count > 0)
		{
			int num = queue.Dequeue();
			foreach (int item in Neighbors(num))
			{
				if (array[item] < 0)
				{
					array[item] = array[num] + 1;
					queue.Enqueue(item);
				}
			}
		}
		return array;
	}

	public bool BlocksRay(float[] start, float[] end)
	{
		return source.Boxes.Where((BoxDef b) => b.Solid).Any((BoxDef b) => SegmentIntersects(b, start, end));
	}

	public static bool SegmentIntersects(BoxDef b, float[] a, float[] c)
	{
		float num = 0f;
		float num2 = 1f;
		for (int i = 0; i < 3; i++)
		{
			float num3 = c[i] - a[i];
			float num4 = b.Center[i] - b.Size[i] / 2f;
			float num5 = b.Center[i] + b.Size[i] / 2f;
			if ((double)Math.Abs(num3) < 1E-07)
			{
				if (a[i] < num4 || a[i] > num5)
				{
					return false;
				}
				continue;
			}
			float num6 = (num4 - a[i]) / num3;
			float num7 = (num5 - a[i]) / num3;
			if (num6 > num7)
			{
				float num8 = num7;
				num7 = num6;
				num6 = num8;
			}
			num = Math.Max(num, num6);
			num2 = Math.Min(num2, num7);
			if (num > num2)
			{
				return false;
			}
		}
		if (num2 >= 0f)
		{
			return num <= 1f;
		}
		return false;
	}
}
