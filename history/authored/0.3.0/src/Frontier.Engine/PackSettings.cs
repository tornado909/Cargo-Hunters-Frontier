using System.Text.Json;
namespace Frontier.Engine;
public sealed class PackSettings
{
    public bool Enabled { get; set; } = true;
    // Legacy flag is read for migration only. It no longer authorizes active mode.
    public bool SoloOnlyAcknowledged { get; set; }
    public bool TestProfileAcknowledged { get; set; }
    public bool CoopEnabled { get; set; } = true;
    public bool ShowOverlay { get; set; } = true;
    public bool RuntimeTestMode { get; set; } = false;
    public bool DiagnosticsOnly { get; set; } = true;
    public string[] EnabledModules { get; set; } = new[] { "armory", "equipment", "affixes", "salvage", "elites", "workshop", "progression" };
    public int EnemySeed { get; set; }
    public int[] EnemyWeights { get; set; } = new[] { 70, 18, 8, 3, 1 };
    public static PackSettings Read(string path)
    {
        var settings = JsonSerializer.Deserialize<PackSettings>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = false })
            ?? throw new InvalidDataException("Invalid settings");
        settings.Validate();
        return settings;
    }
    public void Validate()
    {
        string[] known = { "armory", "equipment", "affixes", "salvage", "elites", "workshop", "progression" };
        if (EnabledModules is null || EnabledModules.Any(x => !known.Contains(x, StringComparer.Ordinal)) ||
            EnabledModules.Distinct(StringComparer.Ordinal).Count() != EnabledModules.Length)
            throw new InvalidDataException("EnabledModules must contain unique known IDs");
        if (EnemyWeights is null || EnemyWeights.Length != 5 || EnemyWeights.Any(x => x < 0) || EnemyWeights.Sum(x => (long)x) is <= 0 or > int.MaxValue)
            throw new InvalidDataException("EnemyWeights: five nonnegative integers with positive sum <= Int32.MaxValue required");
        if (Enabled && !DiagnosticsOnly && !TestProfileAcknowledged)
            throw new InvalidDataException("Back up a TEST profile on every participating PC and use Activate-Frontier.ps1 before active mode");
    }
}
public static class WeightedRoll
{
    public static int Pick(int roll, IReadOnlyList<int> weights)
    {
        if (roll < 0 || (long)roll >= weights.Sum(w => (long)w)) throw new ArgumentOutOfRangeException(nameof(roll));
        for (int i = 0; i < weights.Count; i++) { if (roll < weights[i]) return i; roll -= weights[i]; }
        throw new InvalidOperationException("Invalid weights");
    }
}
