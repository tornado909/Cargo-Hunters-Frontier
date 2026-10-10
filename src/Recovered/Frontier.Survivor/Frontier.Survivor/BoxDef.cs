using System;

namespace Frontier.Survivor;

public sealed class BoxDef
{
	public string Id { get; set; } = "";

	public float[] Center { get; set; } = Array.Empty<float>();

	public float[] Size { get; set; } = Array.Empty<float>();

	public string Material { get; set; } = "";

	public string Kind { get; set; } = "";

	public bool Solid { get; set; }
}
