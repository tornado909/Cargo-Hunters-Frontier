using System;
using System.Collections.Generic;

namespace Frontier.ReferenceFix;

internal sealed class HitDeduplicator
{
	private readonly HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

	private readonly Queue<string> order = new Queue<string>();

	internal bool Add(string key)
	{
		if (!seen.Add(key))
		{
			return false;
		}
		order.Enqueue(key);
		while (order.Count > 8192)
		{
			seen.Remove(order.Dequeue());
		}
		return true;
	}

	internal void Clear()
	{
		seen.Clear();
		order.Clear();
	}
}
