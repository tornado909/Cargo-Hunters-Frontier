using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontier.Engine;

public static class WeightedRoll
{
	public static int Pick(int roll, IReadOnlyList<int> weights)
	{
		if (roll < 0 || roll >= ((IEnumerable<int>)weights).Sum((Func<int, long>)((int w) => w)))
		{
			throw new ArgumentOutOfRangeException("roll");
		}
		for (int num = 0; num < weights.Count; num++)
		{
			if (roll < weights[num])
			{
				return num;
			}
			roll -= weights[num];
		}
		throw new InvalidOperationException("Invalid weights");
	}
}
