using System;
using System.Collections.Generic;

namespace Frontier.Reserve;

internal sealed class StockMaterial
{
	public string Id { get; set; } = "";

	public string Name { get; set; } = "";

	public string Shader { get; set; } = "";

	public StockTextureBinding[] Textures { get; set; } = Array.Empty<StockTextureBinding>();

	public Dictionary<string, float[]> Colors { get; set; } = new Dictionary<string, float[]>();

	public Dictionary<string, float> Floats { get; set; } = new Dictionary<string, float>();

	public int RenderQueue { get; set; } = -1;

	public string? Keywords { get; set; }
}
