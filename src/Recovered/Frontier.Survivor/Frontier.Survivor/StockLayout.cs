using System;

namespace Frontier.Survivor;

internal sealed class StockLayout
{
	public string Format { get; set; } = "";

	public string CollisionFingerprint { get; set; } = "";

	public StockInstance[] Instances { get; set; } = Array.Empty<StockInstance>();
}
