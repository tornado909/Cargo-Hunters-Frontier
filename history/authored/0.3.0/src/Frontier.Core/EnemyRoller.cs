using System.Reflection;
using System.Security.Cryptography;
using System.Globalization;
using System.Text.Json.Nodes;
using Frontier.Engine;
namespace Frontier.Core;
internal sealed class EnemyRoller
{
    private readonly HashSet<string> spawnPoints;
    private readonly JsonObject variants;
    private readonly string[] tiers;
    private readonly int[] weights;
    private readonly int configuredSeed;
    private readonly bool testMode;
    private string sessionSalt;
    private readonly object sync = new();
    private Type? repositoryType, idType;
    private MethodInfo? getData, parseId;
    public int ClientCallsSkipped { get; private set; }
    public int AuthoritativeCalls { get; private set; }
    public EnemyRoller(JsonObject catalog, PackSettings settings)
    {
        spawnPoints = ((JsonArray)catalog["allowedSpawnPointIds"]!).Select(n => n!.GetValue<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        variants = (JsonObject)catalog["variants"]!;
        tiers = ((JsonArray)catalog["tiers"]!).Select(n => PatchEngine.Str(n!, "id")).ToArray();
        if (tiers.Length != 4) throw new InvalidDataException("Expected four elite tiers");
        testMode = settings.RuntimeTestMode;
        weights = settings.EnemyWeights.ToArray(); configuredSeed = settings.EnemySeed;
        sessionSalt = NewSalt();
    }
    private string NewSalt() => configuredSeed == 0 ? Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()
        : configuredSeed.ToString(CultureInfo.InvariantCulture);
    public void BeginAuthoritativeSession() { lock (sync) sessionSalt = NewSalt(); }
    public object? Replace(object manager, object spawnPointId, object originalPreset, out string? description)
    {
        description = null;
        // The spawn manager holds its OWN items manager, unlike a process-global "host" flag.
        // A listen server can have both client and authoritative ECS worlds in the same process.
        object itemsManager = Reflect.Read(manager, "_itemsManager") ?? throw new InvalidDataException("Spawn items manager is null");
        object? authority = Reflect.Read(itemsManager, "IsAuthoritative");
        if (authority is not bool isAuthoritative) throw new MissingMemberException("IItemsManager.IsAuthoritative must return bool");
        if (!isAuthoritative) { ClientCallsSkipped++; return null; }
        AuthoritativeCalls++;
        string spawnId = spawnPointId.ToString()!;
        if (!spawnPoints.Contains(spawnId)) return null;
        string baseId = Reflect.Read(originalPreset, "Id")?.ToString() ?? "";
        if (variants[baseId] is not JsonObject choices) return null;
        int selected;
        lock (sync) selected = CoopProtocol.EnemyTier(sessionSalt, spawnId, baseId, weights);
        if (testMode) selected = 1; // Every eligible, allow-listed selection is a Veteran; story spawns remain untouched.
        if (selected == 0) return null;
        string tier = tiers[selected - 1], newId = choices[tier]!.GetValue<string>();
        repositoryType ??= Reflect.FindType("Game.SpawnPointSystem.BotSpawnPoints.Data.BotPresetDataRepository");
        idType ??= Reflect.FindType("Core.Utils.UniqueId");
        parseId ??= idType.GetMethods(Reflect.Flags).Single(m => m.IsStatic && m.Name == "Parse" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));
        getData ??= repositoryType.GetMethods(Reflect.Flags).Single(m => m.Name == "GetData" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == idType);
        object nativeRepo = Reflect.Read(manager, "_botPresetDataRepository") ?? throw new InvalidDataException("Bot repository is null");
        object repo = Reflect.AsConcrete(nativeRepo, repositoryType);
        object replacement = getData.Invoke(repo, new[] { parseId.Invoke(null, new object[] { newId }) }) ?? throw new InvalidDataException("Elite preset was not registered");
        if (!string.Equals(Reflect.Read(replacement, "Id")?.ToString(), newId, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Wrong elite preset returned");
        description = $"{tier} | authoritative spawn {spawnId} | {newId}";
        return replacement;
    }
}
