using System;
using System.Collections.Generic;

namespace Frontier.Survivor;

internal sealed class MeleeRankCatalog
{
	public string Format { get; set; } = "";

	public Dictionary<string, string[]> Families { get; set; } = new Dictionary<string, string[]>();

	public MeleeRankRow[] Rows { get; set; } = Array.Empty<MeleeRankRow>();
}
