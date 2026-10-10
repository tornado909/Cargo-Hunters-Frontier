using System;

namespace Frontier.Survivor;

internal sealed class StockPart
{
	public string Mesh { get; set; } = "";

	public string[] Materials { get; set; } = Array.Empty<string>();
}
