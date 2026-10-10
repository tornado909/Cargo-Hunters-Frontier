using System;

namespace Frontier.Survivor;

internal sealed class StockManifest
{
	public string Format { get; set; } = "";

	public StockPrefab[] Prefabs { get; set; } = Array.Empty<StockPrefab>();

	public StockFile[] Meshes { get; set; } = Array.Empty<StockFile>();

	public StockFile[] Textures { get; set; } = Array.Empty<StockFile>();

	public StockMaterial[] Materials { get; set; } = Array.Empty<StockMaterial>();
}
