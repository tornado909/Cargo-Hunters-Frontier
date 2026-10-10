using System.Reflection;
using System.Text.Json;
using BepInEx;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Frontier.Engine;
namespace Frontier.Core;

/// <summary>Copy actual bytes supplied to the offline-save deserializer, not an arbitrary preferences folder.</summary>
internal sealed class SaveSnapshots
{
    private readonly Plugin p;
    private readonly object sync = new();
    private readonly string folder;
    private string? latestPath, latestHash, lastError;
    private bool parsed;
    internal SaveSnapshots(Plugin p) { this.p = p; folder = Path.Combine(Paths.BepInExRootPath, "Frontier-save-snapshots"); }
    private sealed record Capture(string Path, string Hash, long Bytes);
    internal object Snapshot { get { lock (sync) return new { verified = parsed, path = latestPath, sha256 = latestHash, lastError,
        source = "OfflineAccountContextPersistenceService.DeserializeAccountContextSource input bytes",
        scope = "offline account only; not Steam settings; not a verified restore procedure" }; } }
    private static SaveSnapshots S => Plugin.Current.Saves;
    internal void Install(Harmony h)
    {
        var method = Reflect.UniqueMethod(Reflect.FindType("Game.Application.Offline.OfflineAccountContextPersistenceService"), "DeserializeAccountContextSource", 1);
        if (method.GetParameters()[0].ParameterType != typeof(Il2CppStructArray<byte>)) throw new MissingMethodException("Offline deserializer signature changed");
        HarmonyMethod H(string name) => new(typeof(SaveSnapshots).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!);
        h.Patch(method, prefix: H(nameof(Before)), postfix: H(nameof(After)));
    }
    private static void Before(Il2CppStructArray<byte> __0, out Capture? __state)
    {
        __state = null;
        try
        {
            if (__0 is null || __0.Length == 0 || __0.Length > 128 * 1024 * 1024) throw new InvalidDataException("Unexpected offline save length");
            byte[] bytes = __0.AsSpan().ToArray(); string hash = PatchEngine.Hash(bytes);
            Directory.CreateDirectory(S.folder);
            string path = Path.Combine(S.folder, hash + ".save");
            lock (S.sync)
            {
                if (!File.Exists(path)) { File.WriteAllBytes(path + ".tmp", bytes); File.Move(path + ".tmp", path); }
                if (PatchEngine.Hash(File.ReadAllBytes(path)) != hash) throw new IOException("Offline snapshot hash mismatch");
            }
            __state = new(path, hash, bytes.LongLength);
        }
        catch (Exception e)
        {
            lock (S.sync) S.lastError = e.GetBaseException().Message;
            // Do not alter the user's save input or suppress native deserialization.
            Plugin.Current.Log.LogError("Offline snapshot failed: " + S.lastError);
        }
    }
    private static void After(object? __result, Capture? __state)
    {
        if (__state is null || __result is null) return;
        try
        {
            lock (S.sync)
            {
                S.latestPath = __state.Path; S.latestHash = __state.Hash; S.parsed = true; S.lastError = null;
                var manifest = new { schema = 1, utc = DateTime.UtcNow.ToString("O"), path = __state.Path,
                    sha256 = __state.Hash, bytes = __state.Bytes, nativeDeserialized = true,
                    source = "offline deserializer input", activeAtCapture = Plugin.Current.Active, pack = CoopProtocol.PackVersion };
                File.WriteAllText(Path.Combine(S.folder, "latest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
            }
            Plugin.Current.Log.LogInfo("Actual offline save captured before loading: " + __state.Path);
            Plugin.Current.WriteReport();
        }
        catch (Exception e)
        {
            lock (S.sync) { S.parsed = false; S.lastError = e.GetBaseException().Message; }
            Plugin.Current.Log.LogError("Offline snapshot metadata failed: " + S.lastError);
        }
    }
}
