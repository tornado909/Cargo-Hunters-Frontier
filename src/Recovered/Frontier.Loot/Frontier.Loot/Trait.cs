using System.Collections.Generic;

namespace Frontier.Loot;

public sealed class Trait
{
	public int RankIndex { get; set; }

	public Dictionary<int, float> Native { get; set; } = new Dictionary<int, float>();

	public string Base { get; set; } = "";

	public int Tier { get; set; }

	public int Roll { get; set; }

	public int Category { get; set; }

	public int SubCategory { get; set; }

	public float Damage { get; set; }

	public float Crit { get; set; }

	public float CritPower { get; set; }

	public int Penetration { get; set; }

	public float SaveAmmo { get; set; }

	public float FireRate { get; set; }

	public bool Executor { get; set; }
}
