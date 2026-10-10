using System.Collections.Generic;

namespace Frontier.Loot;

public sealed class BalanceProfile
{
	public int Category { get; set; }

	public int SubCategory { get; set; }

	public string AnchorTemplate { get; set; } = "";

	public double Damage { get; set; }

	public double Crit { get; set; }

	public double CritPower { get; set; }

	public int Penetration { get; set; }

	public double SaveAmmo { get; set; }

	public double FireRate { get; set; }

	public bool Executor { get; set; }

	public Dictionary<int, double> Native { get; set; } = new Dictionary<int, double>();

	public int? MagazineCapacity { get; set; }

	public double? BodyMaxHealth { get; set; }
}
