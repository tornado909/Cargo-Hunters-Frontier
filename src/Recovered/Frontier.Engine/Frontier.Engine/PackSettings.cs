using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Frontier.Engine;

public sealed class PackSettings
{
	public bool ProductionMode { get; set; }

	public bool Enabled { get; set; } = true;

	public bool SoloOnlyAcknowledged { get; set; }

	public bool TestProfileAcknowledged { get; set; }

	public bool CoopEnabled { get; set; } = true;

	public bool ShowOverlay { get; set; } = true;

	public bool RuntimeTestMode { get; set; }

	public bool DiagnosticsOnly { get; set; } = true;

	public string[] EnabledModules { get; set; } = new string[7] { "armory", "equipment", "affixes", "salvage", "elites", "workshop", "progression" };

	public int EnemySeed { get; set; }

	public int[] EnemyWeights { get; set; } = new int[5] { 70, 18, 8, 3, 1 };

	public static PackSettings Read(string path)
	{
		PackSettings? obj = JsonSerializer.Deserialize<PackSettings>(File.ReadAllText(path), new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = false
		}) ?? throw new InvalidDataException("Invalid settings");
		obj.Validate();
		return obj;
	}

	public void Validate()
	{
		if (ProductionMode && (RuntimeTestMode || DiagnosticsOnly))
		{
			throw new InvalidDataException("ProductionMode cannot enable runtime test or diagnostics-only settings");
		}
		string[] known = new string[7] { "armory", "equipment", "affixes", "salvage", "elites", "workshop", "progression" };
		if (EnabledModules == null || EnabledModules.Any((string x) => !known.Contains<string>(x, StringComparer.Ordinal)) || EnabledModules.Distinct<string>(StringComparer.Ordinal).Count() != EnabledModules.Length)
		{
			throw new InvalidDataException("EnabledModules must contain unique known IDs");
		}
		bool flag = EnemyWeights == null || EnemyWeights.Length != 5 || EnemyWeights.Any((int x) => x < 0);
		if (!flag)
		{
			long num = ((IEnumerable<int>)EnemyWeights).Sum((Func<int, long>)((int x) => x));
			flag = ((num <= 0 || num > int.MaxValue) ? true : false);
		}
		if (flag)
		{
			throw new InvalidDataException("EnemyWeights: five nonnegative integers with positive sum <= Int32.MaxValue required");
		}
		if (Enabled && !DiagnosticsOnly && !TestProfileAcknowledged && !ProductionMode)
		{
			throw new InvalidDataException("Back up a TEST profile on every participating PC and use Activate-Frontier.ps1 before active mode");
		}
	}
}
