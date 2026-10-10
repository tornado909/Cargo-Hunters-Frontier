using System;

namespace Frontier.Survivor;

public sealed class Anchor
{
	public int Yaw { get; set; }

	public float SpawnChance { get; set; } = 0.85f;

	public string Id { get; set; } = "";

	public string Kind { get; set; } = "";

	public string Room { get; set; } = "";

	public float[] Position { get; set; } = Array.Empty<float>();
}
