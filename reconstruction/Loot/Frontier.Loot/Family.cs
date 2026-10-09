using System;

namespace Frontier.Loot;

public sealed class Family
{
	public int Category { get; set; }

	public int SubCategory { get; set; }

	public string[] Variants { get; set; } = Array.Empty<string>();
}
