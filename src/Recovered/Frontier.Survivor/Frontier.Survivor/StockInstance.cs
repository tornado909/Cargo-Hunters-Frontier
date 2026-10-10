using System;

namespace Frontier.Survivor;

internal sealed class StockInstance
{
	public string Id { get; set; } = "";

	public string Zone { get; set; } = "";

	public string Asset { get; set; } = "";

	public float[] Center { get; set; } = Array.Empty<float>();

	public float[] Size { get; set; } = Array.Empty<float>();

	public int Yaw { get; set; }

	public int Pitch { get; set; }
}
