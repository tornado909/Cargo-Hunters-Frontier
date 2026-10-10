using System.Text.Json;
using System.Text.Json.Nodes;
using Frontier.Engine;

internal static class CoopTests
{
    public static void Run()
    {
        int count = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Co-op test failed: " + name);
            count++;
        }
        void Throws(Action action, string name)
        {
            try { action(); }
            catch { count++; return; }
            throw new InvalidOperationException("Co-op test should have thrown: " + name);
        }
        var f = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "coop-vectors.json")))!.AsObject();
        string Str(string name) => f[name]!.GetValue<string>();
        var settings = f["settings"]!.Deserialize<PackSettings>()!;
        var hashes = f["hashes"]!.Deserialize<Dictionary<string, string>>()!;
        string sig = Str("fingerprint"), epoch = Str("epoch"), catalog = Str("catalog"), token = Str("readyToken");
        Check(CoopProtocol.PackVersion == Str("version"), "fixture release version");
        Check(CoopProtocol.Fingerprint(Str("game"), settings, hashes) == sig, "canonical signature golden vector");
        settings.EnabledModules = settings.EnabledModules.Reverse().ToArray();
        Check(CoopProtocol.Fingerprint(Str("game"), settings, hashes.Reverse().ToDictionary(p => p.Key, p => p.Value)) == sig, "enumeration order");
        settings.ShowOverlay = false; settings.TestProfileAcknowledged = true;
        Check(CoopProtocol.Fingerprint(Str("game"), settings, hashes) == sig, "local display/ack excluded");
        int seed = settings.EnemySeed; settings.EnemySeed++;
        Check(CoopProtocol.Fingerprint(Str("game"), settings, hashes) != sig, "seed mismatch"); settings.EnemySeed = seed;
        string[] modules = settings.EnabledModules; settings.EnabledModules = new[] { "elites" };
        Check(CoopProtocol.Fingerprint(Str("game"), settings, hashes) != sig, "module mismatch"); settings.EnabledModules = modules;
        int[] weights = settings.EnemyWeights; settings.EnemyWeights = new[] { 69, 18, 8, 3, 2 };
        Check(CoopProtocol.Fingerprint(Str("game"), settings, hashes) != sig, "weights mismatch"); settings.EnemyWeights = weights;
        var different = new Dictionary<string, string>(hashes) { ["Frontier.Core.dll"] = new('d', 64) };
        Check(CoopProtocol.Fingerprint(Str("game"), settings, different) != sig, "binary mismatch");
        foreach (var bad in new[] { Array.Empty<int>(), new[] { 0, 0, 0, 0, 0 }, new[] { -1, 1, 1, 1, 1 }, new[] { int.MaxValue, 1, 0, 0, 0 } })
        {
            settings.EnemyWeights = bad;
            Throws(settings.Validate, "bad weight vector");
        }
        settings.EnemyWeights = weights;
        settings.EnabledModules = new[] { "elites", "elites" }; Throws(settings.Validate, "duplicate module");
        settings.EnabledModules = new[] { "unknown" }; Throws(settings.Validate, "unknown module"); settings.EnabledModules = modules;
        settings.DiagnosticsOnly = false; settings.TestProfileAcknowledged = false; settings.SoloOnlyAcknowledged = true;
        Throws(settings.Validate, "old solo flag cannot authorize new active mode");
        settings.DiagnosticsOnly = true;
        Throws(() => CoopProtocol.Fingerprint(Str("game"), settings, new Dictionary<string, string> { ["file"] = "invalid" }), "malformed hash");
        var loaded = f["loaded"]!.Deserialize<Dictionary<string, string>>()!;
        string[] required = f["required"]!.Deserialize<string[]>()!;
        Check(CoopProtocol.CatalogDigest(loaded, required.Reverse()) == catalog, "catalog golden vector/load order");
        Check(CoopProtocol.CatalogDigest(new Dictionary<string, string>(), required) == "", "empty load is not ready");
        var partial = new Dictionary<string, string>(loaded); partial.Remove(required[0]);
        Check(CoopProtocol.CatalogDigest(partial, required) == "", "partial load is not ready");
        var changed = new Dictionary<string, string>(loaded) { [required[0]] = new('d', 64) };
        Check(CoopProtocol.CatalogDigest(changed, required) != catalog, "actual output mismatch");
        Check(CoopProtocol.ReadyToken(sig, epoch, catalog) == token, "token golden vector");
        Check(CoopProtocol.IsReadyToken(token, epoch), "token valid");
        foreach (string bad in new[] { "", token.ToUpperInvariant(), token + "|extra", "1|x|y|z", "2" + token[1..] })
            Check(!CoopProtocol.IsReadyToken(bad, epoch), "malformed token");
        Throws(() => CoopProtocol.ReadyToken(sig, "invalid", catalog), "epoch validation");
        Check(CoopProtocol.HeaderError("1", sig, epoch, sig) is null, "matching lobby header");
        Check(CoopProtocol.HeaderError("", "", "", sig) is not null, "vanilla lobby blocked");
        Check(CoopProtocol.HeaderError("2", sig, epoch, sig) is not null, "protocol mismatch");
        Check(CoopProtocol.HeaderError("1", new('d', 64), epoch, sig) is not null, "pack mismatch");
        Check(CoopProtocol.HeaderError("1", sig, "", sig) is not null, "header awaiting epoch");
        var peers = new PeerReadiness[] { new(1, token), new(2, token), new(3, token) };
        var roster = new CoopRoster();
        Check(roster.Check(1, 1, epoch, token, peers).Ready, "host preflight");
        Check(roster.Check(1, 2, epoch, token, peers).Ready, "client preflight");
        Check(!roster.Freeze(1, 2, epoch, token, peers).Ready && !roster.IsFrozen, "only owner can freeze");
        Check(!roster.Check(1, 1, epoch, token, Array.Empty<PeerReadiness>()).Ready, "empty roster");
        Check(!roster.Check(1, 2, epoch, token, new[] { peers[0] }).Ready, "local absent");
        Check(!roster.Check(1, 2, epoch, token, new[] { peers[1] }).Ready, "owner absent");
        Check(!roster.Check(1, 2, epoch, token, new[] { peers[0], peers[1], peers[1] }).Ready, "duplicate member");
        Check(!roster.Check(1, 2, epoch, token, new PeerReadiness[] { new(1, token), new(0, token), new(2, token) }).Ready, "zero member");
        foreach (string bad in new[] { "", CoopProtocol.ReadyToken(new('d', 64), epoch, catalog),
            CoopProtocol.ReadyToken(sig, new('f', 32), catalog), CoopProtocol.ReadyToken(sig, epoch, new('d', 64)) })
            Check(!roster.Check(1, 2, epoch, token, new PeerReadiness[] { new(1, token), new(2, bad) }).Ready, "mismatching member readiness");
        Check(!roster.Check(1, 1, epoch, "bad", new PeerReadiness[] { new(1, "bad") }).Ready, "matching malformed token rejected");
        Check(!roster.Freeze(1, 1, epoch, token, peers.Append(new PeerReadiness(4, "")).ToArray()).Ready, "join race at freeze");
        Check(roster.Freeze(1, 1, epoch, token, peers).Ready && roster.IsFrozen, "host freeze");
        Check(roster.FrozenMembers.SequenceEqual(new ulong[] { 1, 2, 3 }), "stable frozen membership");
        Check(roster.Check(1, 2, epoch, token, peers.Reverse().ToArray()).Ready, "roster order does not matter");
        Check(!roster.Check(1, 1, epoch, token, peers.Append(new PeerReadiness(4, token)).ToArray()).Ready, "late join blocked even with matching pack");
        Check(roster.Check(1, 1, epoch, token, new[] { peers[0], peers[2] }).Ready, "non-owner departure allowed");
        Check(!roster.Check(2, 2, epoch, token, peers).Ready, "host migration blocked");
        string epoch2 = new('f', 32), token2 = CoopProtocol.ReadyToken(sig, epoch2, catalog);
        Check(!roster.Check(1, 1, epoch2, token2, peers.Select(p => p with { Token = token2 }).ToArray()).Ready, "epoch pinned during raid");
        roster.Clear();
        Check(!roster.IsFrozen && roster.Freeze(1, 1, epoch2, token2, new PeerReadiness[] { new(1, token2), new(4, token2) }).Ready, "fresh raid resets roster");
        foreach (var node in f["enemyCases"]!.AsArray())
        {
            var v = node!.AsObject();
            Check(CoopProtocol.EnemyTier(v["salt"]!.GetValue<string>(), v["spawn"]!.GetValue<string>(), v["base"]!.GetValue<string>(),
                v["weights"]!.Deserialize<int[]>()!) == v["tier"]!.GetValue<int>(), "SHA enemy golden vector");
        }
        Check(CoopProtocol.EnemyTier("salt", "ABCD", "EFGH", weights) == CoopProtocol.EnemyTier("salt", "abcd", "efgh", weights), "GUID casing");
        var draws = Enumerable.Range(0, 1000).ToDictionary(i => i, i => CoopProtocol.EnemyTier("session", i.ToString(), "base", weights));
        Check(Enumerable.Range(0, 1000).Reverse().All(i => draws[i] == CoopProtocol.EnemyTier("session", i.ToString(), "base", weights)), "spawn order/repeated calls do not reroll");
        Check(Enumerable.Range(0, 1000).Any(i => draws[i] != CoopProtocol.EnemyTier("different-session", i.ToString(), "base", weights)), "new salt changes sample");
        for (int index = 0; index < 5; index++)
        {
            var forced = new int[5]; forced[index] = 100;
            Check(Enumerable.Range(0, 100).All(i => CoopProtocol.EnemyTier("force", i.ToString(), "base", forced) == index), "forced tier");
        }
        Console.WriteLine($"PASS: {count} C# co-op policy/golden-vector assertions. Steam/native/network gameplay NOT exercised.");
    }
}
