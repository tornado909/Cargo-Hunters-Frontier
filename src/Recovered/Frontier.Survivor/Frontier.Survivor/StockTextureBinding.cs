using System;

namespace Frontier.Survivor;

internal sealed class StockTextureBinding
{
	public string Property { get; set; } = "";

	public string Texture { get; set; } = "";

	public float[] Scale { get; set; } = Array.Empty<float>();

	public float[] Offset { get; set; } = Array.Empty<float>();
}
