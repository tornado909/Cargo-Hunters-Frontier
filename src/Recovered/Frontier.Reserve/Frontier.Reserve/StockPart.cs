using System;

namespace Frontier.Reserve;

internal sealed class StockPart
{
	public string Mesh { get; set; } = "";

	public string[] Materials { get; set; } = Array.Empty<string>();
}
