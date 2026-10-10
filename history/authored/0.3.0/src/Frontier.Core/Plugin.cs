using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Frontier.Engine;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppInterop.Runtime.InteropTypes;
using System.Diagnostics;

namespace Frontier.Core;

[BepInPlugin("frontier.cargohunters.core", "Cargo Hunters Frontier", "0.3.0")]
public sealed class Plugin : BasePlugin
{
    internal static Plugin Current = null!;
    internal PackSettings Settings = null!;
    internal PatchEngine Engine = null!;
    internal CoopController Coop = null!;
    internal RuntimeAudit Audit = null!;
    internal RuntimeObservations Observations = null!;
    internal SaveSnapshots Saves = null!;
    private readonly Dictionary<string, string> loadedCatalogHashes = new(StringComparer.Ordinal);
    internal string CatalogDigest
    {
        get { lock (reportLock) return Active && Engine is not null && Audit is not null && Audit.CatalogReady && Observations.Healthy ? CoopProtocol.CatalogDigest(loadedCatalogHashes, Engine.RepositoryNames) : ""; }
    }
    private sealed record RepositoryLoadState(string Name, string Hash, bool Applied, object Repository, JsonNode Expected, Stopwatch Timer);
    internal readonly Dictionary<string, string> Repositories = new(StringComparer.Ordinal);
    internal readonly List<string> ObservedPaths = new();
    internal string Mode = "initializing";
    internal string LastEnemy = "none";
    internal int EnemyChanges, LocalizationHits;
    internal bool Active => Settings is not null && Settings.Enabled && !Settings.DiagnosticsOnly && !fatal;
    private Harmony? harmony;
    private JsonObject strings = new();
    private EnemyRoller? enemies;
    private string reportPath = "";
    private readonly object reportLock = new();
    private bool fatal;

    public override void Load()
    {
        Current = this;
        reportPath = Path.Combine(Paths.BepInExRootPath, "Frontier-report.json");
        try
        {
            string root = Path.GetDirectoryName(GetType().Assembly.Location)!;
            string packPath = Path.Combine(root, "packs");
            string configPath = Path.Combine(Paths.ConfigPath, "FrontierPack.json");
            if (!File.Exists(configPath))
            {
                Directory.CreateDirectory(Paths.ConfigPath);
                File.Copy(Path.Combine(packPath, "pack-settings.example.json"), configPath);
            }
            Settings = PackSettings.Read(configPath);
            if (!Settings.Enabled) { Mode = "DISABLED: do not load a Frontier save"; Log.LogWarning(Mode); WriteReport(); return; }
            Engine = new PatchEngine(packPath, Settings.EnabledModules, Settings.RuntimeTestMode);
            Audit = new RuntimeAudit(this);
            Observations = new RuntimeObservations(this);
            Saves = new SaveSnapshots(this);
            strings = PatchEngine.ReadObject(Path.Combine(packPath, "localization.json"));
            VerifyGameFiles();
            string interop = Path.Combine(Paths.BepInExRootPath, "interop");
            foreach (string name in new[] { "Core.Shared.Utils", "DataRepository", "Game.Shared", "Game", "Unity.Localization" }) Reflect.LoadAssembly(name, interop);
            harmony = new Harmony("frontier.cargohunters.core");
            enemies = new EnemyRoller(PatchEngine.ReadObject(Path.Combine(packPath, "elites.json")), Settings);
            Coop = new CoopController(this, root, packPath);
            InstallHooks();
            Mode = Active ? "ACTIVE - Steam co-op release candidate; matching peers required" : "DIAGNOSTIC - repositories are NOT changed";
            Log.LogWarning($"Frontier {CoopProtocol.PackVersion} | {Mode} | {Engine.GameVersion}");
            if (Settings.ShowOverlay)
            {
#if FRONTIER_OVERLAY
                try { AddComponent<FrontierOverlay>(); }
                catch (Exception e) { Log.LogWarning("Optional overlay unavailable: " + e.Message); }
#else
                Log.LogInfo("Built without optional Unity GUI/input references. Use Frontier-report.json instead of F8.");
#endif
            }
            WriteReport();
        }
        catch (Exception e) { Fail("Initialization", e); }
    }
    private void VerifyGameFiles()
    {
        string game = Paths.GameRootPath;
        string version = Path.Combine(game, "version.txt");
        if (!File.Exists(version) || File.ReadAllText(version).Trim() != Engine.GameVersion)
            throw new InvalidDataException($"Requires analyzed version {Engine.GameVersion}; no automatic update adaptation.");
        foreach (var pair in Engine.FileHashes)
        {
            string path = Path.Combine(game, pair.Key.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path) || PatchEngine.Hash(File.ReadAllBytes(path)) != pair.Value!.GetValue<string>())
                throw new InvalidDataException("Game binary fingerprint mismatch: " + pair.Key);
        }
    }
    private static HarmonyMethod PatchMethod(string name) => new(typeof(Plugin).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!);
    private void InstallHooks()
    {
        Type loader = Reflect.FindType("Game.Data.DataRepositoryLoader");
        MethodInfo parse = Reflect.UniqueMethod(loader, "ParseDataFromBytes", 2);
        if (parse.ReturnType != typeof(void) || parse.GetParameters()[1].ParameterType != typeof(Il2CppStructArray<byte>))
            throw new MissingMethodException("ParseDataFromBytes signature differs from the analyzed IL2CPP byte[] method");
        Type localization = Reflect.FindType("UnityEngine.Localization.Settings.LocalizedStringDatabase");
        MethodInfo generate = Reflect.UniqueMethod(localization, "GenerateLocalizedString", 6);
        MethodInfo missing = Reflect.UniqueMethod(localization, "ProcessUntranslatedText", 5);
        if (generate.ReturnType != typeof(string) || missing.ReturnType != typeof(string)) throw new MissingMethodException("Localization signatures changed");
        // Install parse last so there is never an intentionally patched catalog without localization hooks.
        harmony!.Patch(generate, prefix: PatchMethod(nameof(LocalizationPrefix)));
        harmony.Patch(missing, prefix: PatchMethod(nameof(MissingTextPrefix)));
        if (Engine.IsEnabled("elites"))
        {
            Type manager = Reflect.FindType("Game.SpawnPointSystem.BotSpawnPoints.BotSpawnPointEquipmentManager");
            MethodInfo enemy = Reflect.UniqueMethod(manager, "TryGetBotPreset", 3);
            if (enemy.ReturnType != typeof(bool) || !enemy.GetParameters()[2].ParameterType.IsByRef || enemy.GetParameters()[2].ParameterType.GetElementType()?.FullName != "Game.SpawnPointSystem.BotSpawnPoints.Data.BotPresetData")
                throw new MissingMethodException("TryGetBotPreset signature changed");
            // Bind the native out parameter itself. Do not write a boxed __args array.
            MethodInfo typedEnemy = typeof(Plugin).GetMethod(nameof(EnemyPostfix), BindingFlags.Static | BindingFlags.NonPublic)!
                .MakeGenericMethod(enemy.GetParameters()[2].ParameterType.GetElementType()!);
            harmony.Patch(enemy, postfix: new HarmonyMethod(typedEnemy));
        }
        CoopHooks.Install(harmony);
        Observations.Install(harmony);
        Saves.Install(harmony);
        harmony.Patch(parse, prefix: PatchMethod(nameof(RepositoryPrefix)), postfix: PatchMethod(nameof(RepositoryPostfix)),
            finalizer: PatchMethod(nameof(RepositoryFinalizer)));
        Log.LogInfo("Required native methods resolved and Harmony patches requested. Invocation still requires in-game verification.");
    }
    private static void RepositoryPrefix(object __0, ref Il2CppStructArray<byte> __1, out RepositoryLoadState? __state)
    {
        __state = null;
        var p = Current;
        try
        {
            if (__0 is null || __1 is null) throw new InvalidDataException("Null data-loader argument");
            string path = Reflect.Read(__0, "FilePath")?.ToString() ?? throw new InvalidDataException("Repository FilePath is null");
            lock (p.reportLock) if (!p.ObservedPaths.Contains(path)) p.ObservedPaths.Add(path);
            string? name = p.Engine.ResolveRepository(path);
            if (name is null) return;
            var timer = Stopwatch.StartNew();
            // One bulk copy instead of millions of interop array-index accesses.
            byte[] original = __1.AsSpan().ToArray();
            byte[] transformed = p.Engine.Transform(name, original);
            bool applied = p.Active;
            if (applied) __1 = new Il2CppStructArray<byte>(transformed);
            JsonNode expected = JsonNode.Parse(PatchEngine.NormalizedText(applied ? transformed : original))!;
            __state = new(name, PatchEngine.Hash(applied ? transformed : original), applied, __0, expected, timer);
            p.Audit.Invalidate(name);
            lock (p.reportLock) { p.loadedCatalogHashes.Remove(name); p.Repositories[name] = "PENDING_NATIVE_READBACK"; }
            p.Log.LogInfo($"{name}: prepared {original.Length} -> {transformed.Length} bytes in {timer.ElapsedMilliseconds} ms; applied argument={applied}; native objects still unverified");
        }
        catch (Exception e) { p.Fail("Repository prefix", e); }
    }
    private static void RepositoryPostfix(RepositoryLoadState? __state)
    {
        if (__state is null) return;
        var p = Current;
        try
        {
            p.Audit.Verify(__state.Name, __state.Repository, __state.Expected, __state.Applied);
            lock (p.reportLock)
            {
                p.Repositories[__state.Name] = __state.Applied ? "objects-verified" : "baseline-verified-not-applied";
                if (__state.Applied) p.loadedCatalogHashes[__state.Name] = __state.Hash;
            }
            p.Log.LogInfo($"{__state.Name}: {p.Repositories[__state.Name]}; read-back finished in {__state.Timer.ElapsedMilliseconds} ms");
            p.WriteReport();
        }
        catch (Exception e)
        {
            p.Audit.Broken(__state.Name, e);
            lock (p.reportLock) { p.Repositories[__state.Name] = "BROKEN"; p.loadedCatalogHashes.Remove(__state.Name); }
            p.Fail("Native read-back: " + __state.Name, e);
        }
    }
    private static Exception? RepositoryFinalizer(Exception? __exception, RepositoryLoadState? __state)
    {
        if (__exception is not null && __state is not null)
        {
            Current.Audit.Broken(__state.Name, __exception);
            lock (Current.reportLock) { Current.Repositories[__state.Name] = "BROKEN"; Current.loadedCatalogHashes.Remove(__state.Name); }
            Current.Fail("Native repository deserialization: " + __state.Name, __exception);
        }
        return __exception;
    }
    internal void ResetEnemySession() => enemies?.BeginAuthoritativeSession();
    private static bool LocalizationPrefix(object[] __args, ref string __result)
    {
        var p = Current;
        if (!p.Active) return true;
        try
        {
            long id = Convert.ToInt64(Reflect.Read(__args[3], "KeyId"));
            return !p.TryText(id, __args[4], ref __result);
        }
        catch (Exception e) { p.Fail("Localization", e); return true; }
    }
    private static bool MissingTextPrefix(object[] __args, ref string __result)
    {
        if (!Current.Active) return true;
        try { return !Current.TryText(Convert.ToInt64(__args[1]), __args[4], ref __result); }
        catch (Exception e) { Current.Fail("Missing localization", e); return true; }
    }
    private bool TryText(long id, object? locale, ref string result)
    {
        if (strings[id.ToString(System.Globalization.CultureInfo.InvariantCulture)] is not JsonObject text) return false;
        string code = locale is null ? "en" : Reflect.Read(Reflect.Read(locale, "Identifier"), "Code")?.ToString() ?? "en";
        result = text[code.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en"]!.GetValue<string>();
        LocalizationHits++;
        return true;
    }
    private static void EnemyPostfix<T>(bool __result, object __instance, object __0, ref T __2) where T : Il2CppObjectBase
    {
        if (!__result || !Current.Active || Current.enemies is null) return;
        try
        {
            // The game's successful result must carry a real preset before any variant is considered.
            if (__2 is null) throw new InvalidDataException("Native TryGetBotPreset returned true with a null preset");
            object? replacement = Current.enemies.Replace(__instance, __0, __2, out string? tier);
            if (replacement is not null)
            {
                T typed = (T)replacement;
                __2 = typed;
                Current.EnemyChanges++; Current.LastEnemy = tier!;
                Current.Log.LogInfo("Elite selected: " + tier);
                Current.WriteReport();
            }
        }
        catch (Exception e) { Current.Fail("Enemy variant selection", e); }
    }
    internal void Fail(string stage, Exception e)
    {
        if (fatal) return;
        bool terminate = Active;
        fatal = true; Mode = "FAILED: " + stage + ": " + e.GetBaseException().Message;
        Log.LogError(Mode + "\n" + e);
        WriteReport(e.ToString());
        if (terminate)
        {
            // A partially modified repository set must not continue to the save code.
            // Startup/integration errors terminate the process; no attempt to save on quit.
            Log.LogFatal("Frontier is terminating the process (exit 78) to avoid continuing with a mixed catalog. Read BepInEx/Frontier-report.json.");
            Environment.Exit(78);
        }
        harmony?.UnpatchSelf(); // Diagnostics never replaced repository bytes.
    }
    internal void WriteReport(string? error = null)
    {
        if (string.IsNullOrEmpty(reportPath)) return;
        lock (reportLock)
        {
            try
            {
                var payload = new { pack = CoopProtocol.PackVersion, mode = Mode, utc = DateTime.UtcNow.ToString("O"), enabledModules = Settings?.EnabledModules,
                    repositories = Repositories, observedRepositoryPaths = ObservedPaths,
                    runtimeAudit = Audit?.Snapshot, runtimeObservations = Observations?.Snapshot, saveSnapshots = Saves?.Snapshot,
                    runtimeTestMode = Settings?.RuntimeTestMode,
                    expectedRepositories = Engine?.RepositoryNames, eliteChanges = EnemyChanges, lastElite = LastEnemy, localizationHits = LocalizationHits,
                    catalogDigest = CatalogDigest, loadedCatalogHashes, coop = Coop?.Snapshot,
                    resolvedCoopContracts = CoopHooks.Resolved,
                    authoritativeEnemyCalls = enemies?.AuthoritativeCalls ?? 0,
                    skippedClientEnemyCalls = enemies?.ClientCallsSkipped ?? 0,
                    runtimePlaytestClaim = false, error = error ?? (fatal ? Mode : null) };
                string temp = reportPath + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
                File.Move(temp, reportPath, true);
            }
            catch (Exception e) { Log.LogWarning("Could not write diagnostic report: " + e.Message); }
        }
    }
    public override bool Unload() => false; // Unloading would invalidate saved item IDs.
}
