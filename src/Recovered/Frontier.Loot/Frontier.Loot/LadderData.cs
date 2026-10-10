using System;
using System.Collections.Generic;

namespace Frontier.Loot;

public sealed class LadderData
{
	public int Schema { get; set; }

	public string Version { get; set; } = "";

	public double[] OrdinaryWeights { get; set; } = Array.Empty<double>();

	public double[] EnemyWeights { get; set; } = Array.Empty<double>();

	public Dictionary<string, string[]> Families { get; set; } = new Dictionary<string, string[]>();

	public Dictionary<string, string[]> BotVariants { get; set; } = new Dictionary<string, string[]>();

	public Dictionary<string, int> BotRanks { get; set; } = new Dictionary<string, int>();

	public string[] SpawnAllowlist { get; set; } = Array.Empty<string>();

	public Dictionary<string, LadderStyle> Styles { get; set; } = new Dictionary<string, LadderStyle>();
}
