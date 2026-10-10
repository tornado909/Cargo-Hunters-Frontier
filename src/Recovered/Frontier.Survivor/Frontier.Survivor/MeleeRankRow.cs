namespace Frontier.Survivor;

internal sealed class MeleeRankRow
{
	public string BaseId { get; set; } = "";

	public string RankedId { get; set; } = "";

	public string AiId { get; set; } = "";

	public int Rank { get; set; }

	public double HealthFactor { get; set; }

	public double DamageFactor { get; set; }

	public double MoveFactor { get; set; }

	public double XpFactor { get; set; }
}
