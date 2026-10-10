using System;
using System.Collections.Generic;

namespace Frontier.ReferenceFix;

internal sealed class StateLayout
{
	internal readonly int Size;

	internal readonly int Health;

	internal readonly int Maximum;

	internal readonly int Status;

	internal readonly int Dismembered;

	internal StateLayout(int size, int health, int maximum, int status, int dismembered)
	{
		Size = size;
		Health = health;
		Maximum = maximum;
		Status = status;
		Dismembered = dismembered;
		if (size < 13 || size > 128)
		{
			throw new ArgumentOutOfRangeException("size");
		}
		HashSet<int> hashSet = new HashSet<int>();
		(int, int)[] array = new(int, int)[4]
		{
			(health, 4),
			(maximum, 4),
			(status, 4),
			(dismembered, 1)
		};
		for (int i = 0; i < array.Length; i++)
		{
			(int, int) tuple = array[i];
			if (tuple.Item1 < 0 || tuple.Item1 + tuple.Item2 > size)
			{
				throw new ArgumentException("Field outside native state");
			}
			var (j, _) = tuple;
			for (; j < tuple.Item1 + tuple.Item2; j++)
			{
				if (!hashSet.Add(j))
				{
					throw new ArgumentException("Overlapping state fields");
				}
			}
		}
	}

	private void Check(byte[] data)
	{
		if (data.Length != Size)
		{
			throw new ArgumentException("Unexpected state buffer size");
		}
	}

	internal float ReadHealth(byte[] b)
	{
		Check(b);
		return BitConverter.ToSingle(b, Health);
	}

	internal float ReadMax(byte[] b)
	{
		Check(b);
		return BitConverter.ToSingle(b, Maximum);
	}

	internal int ReadStatus(byte[] b)
	{
		Check(b);
		return BitConverter.ToInt32(b, Status);
	}

	internal bool Detached(byte[] b)
	{
		Check(b);
		return b[Dismembered] != 0;
	}

	internal byte[] WithHealth(byte[] original, float value)
	{
		Check(original);
		if (!float.IsFinite(value))
		{
			throw new ArgumentException("Non-finite health");
		}
		byte[] array = (byte[])original.Clone();
		BitConverter.GetBytes(value).CopyTo(array, Health);
		return array;
	}

	internal bool SemanticFieldsEqual(byte[] a, byte[] b)
	{
		if (ReadMax(a).Equals(ReadMax(b)) && ReadStatus(a) == ReadStatus(b))
		{
			return Detached(a) == Detached(b);
		}
		return false;
	}

	internal bool OtherBytesEqual(byte[] a, byte[] b)
	{
		Check(a);
		Check(b);
		for (int i = 0; i < Size; i++)
		{
			if ((i < Health || i >= Health + 4) && a[i] != b[i])
			{
				return false;
			}
		}
		return true;
	}
}
