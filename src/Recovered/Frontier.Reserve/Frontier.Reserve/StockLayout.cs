using System;

namespace Frontier.Reserve;

internal sealed class StockLayout
{
	public string Format { get; set; } = "";

	public string CollisionFingerprint { get; set; } = "";

	public StockInstance[] Instances { get; set; } = Array.Empty<StockInstance>();
}
