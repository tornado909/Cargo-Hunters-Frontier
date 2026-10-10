namespace Frontier.Survivor;

internal sealed class StockFile
{
	public string Id { get; set; } = "";

	public string File { get; set; } = "";

	public string Sha256 { get; set; } = "";

	public string Name { get; set; } = "";

	public int Width { get; set; }

	public int Height { get; set; }
}
