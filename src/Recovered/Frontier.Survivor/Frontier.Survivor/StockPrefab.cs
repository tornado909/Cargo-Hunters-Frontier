using System;

namespace Frontier.Survivor;

internal sealed class StockPrefab
{
	public string Id { get; set; } = "";

	public string Name { get; set; } = "";

	public StockPart[] Parts { get; set; } = Array.Empty<StockPart>();

	public float[] BoundsMin { get; set; } = Array.Empty<float>();

	public float[] BoundsMax { get; set; } = Array.Empty<float>();

	public float[] Size { get; set; } = Array.Empty<float>();
}
