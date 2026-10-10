using System;
using System.Collections.Generic;

namespace Frontier.ReferenceFix;

internal static class SkillPolicy
{
	internal const int NanoId = 9005;

	internal static bool AcceptNative(long pointer, long expected, long actual, bool assignable)
	{
		return pointer != 0L && expected != 0L && actual != 0 && assignable;
	}

	internal static List<T> Merge<T>(IReadOnlyList<T> previous, IReadOnlyList<T> initialized, Func<T, int> id, Func<T, T> unlearnedNano)
	{
		Dictionary<int, T> dictionary = Unique(previous, id);
		Dictionary<int, T> dictionary2 = Unique(initialized, id);
		if (!dictionary.ContainsKey(9005) && !dictionary2.ContainsKey(9005))
		{
			throw new InvalidOperationException("Native initializer returned no 9005");
		}
		List<T> list = new List<T>();
		HashSet<int> hashSet = new HashSet<int>();
		foreach (T previou in previous)
		{
			list.Add(previou);
			hashSet.Add(id(previou));
		}
		foreach (T item in initialized)
		{
			int num = id(item);
			if (hashSet.Add(num))
			{
				list.Add((num == 9005) ? unlearnedNano(item) : item);
			}
		}
		return list;
	}

	private static Dictionary<int, T> Unique<T>(IReadOnlyList<T> list, Func<T, int> id)
	{
		Dictionary<int, T> dictionary = new Dictionary<int, T>();
		foreach (T item in list)
		{
			if (!dictionary.TryAdd(id(item), item))
			{
				throw new InvalidOperationException("Duplicate skill ID " + id(item));
			}
		}
		return dictionary;
	}
}
