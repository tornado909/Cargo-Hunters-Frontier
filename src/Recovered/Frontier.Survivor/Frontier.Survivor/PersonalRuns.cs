using System;
using System.Collections.Generic;

namespace Frontier.Survivor;

internal sealed class PersonalRuns
{
	private readonly Dictionary<int, RunProgress> players = new Dictionary<int, RunProgress>();

	private readonly int seed;

	internal IEnumerable<KeyValuePair<int, RunProgress>> Players => players;

	internal PersonalRuns(int seed)
	{
		this.seed = seed;
	}

	internal RunProgress Get(int id)
	{
		if (id < 0)
		{
			throw new ArgumentOutOfRangeException("id");
		}
		if (!players.TryGetValue(id, out RunProgress value))
		{
			if (players.Count >= 16)
			{
				throw new InvalidOperationException("Human progress budget exceeded");
			}
			value = (players[id] = new RunProgress(seed ^ (id * 397)));
		}
		return value;
	}
}
