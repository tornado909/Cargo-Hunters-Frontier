using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Frontier.Engine;

/// <summary>Pure managed protocol; Steam is a transport for these values, never an authority on game data.</summary>
public static class CoopProtocol
{
    public const string PackVersion = "0.2.0-coop-rc1";
    public const string Version = "1";
    public const string SignatureKey = "frontier_signature";
    public const string ProtocolKey = "frontier_protocol";
    public const string EpochKey = "frontier_epoch";
    public const string CatalogKey = "frontier_catalog";
    public const string ReadyKey = "frontier_ready";
    public const string PhaseKey = "frontier_phase";

    public static bool IsHex(string? text, int length) => text is not null && text.Length == length &&
        text.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static string Hash(string text) => PatchEngine.Hash(Encoding.UTF8.GetBytes(text));

    public static string Fingerprint(string gameVersion, PackSettings settings, IReadOnlyDictionary<string, string> hashes)
    {
        settings.Validate();
        if (string.IsNullOrWhiteSpace(gameVersion) || gameVersion.Any(char.IsControl)) throw new InvalidDataException("Invalid game version");
        var lines = new List<string> { "frontier-net/" + Version, "pack=" + PackVersion, "game=" + gameVersion,
            "modules=" + string.Join(",", settings.EnabledModules.OrderBy(s => s, StringComparer.Ordinal)),
            "enemySeed=" + settings.EnemySeed.ToString(CultureInfo.InvariantCulture),
            "enemyWeights=" + string.Join(",", settings.EnemyWeights.Select(n => n.ToString(CultureInfo.InvariantCulture))) };
        foreach (var entry in hashes.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            if (entry.Key.Any(char.IsControl) || entry.Key.Contains('=') || !IsHex(entry.Value, 64))
                throw new InvalidDataException("Invalid fingerprint entry: " + entry.Key);
            lines.Add("file:" + entry.Key + "=" + entry.Value);
        }
        return Hash(string.Join("\n", lines) + "\n");
    }

    public static string CatalogDigest(IReadOnlyDictionary<string, string> loaded, IEnumerable<string> required)
    {
        var names = required.OrderBy(s => s, StringComparer.Ordinal).ToArray();
        if (names.Length == 0 || names.Any(n => !loaded.TryGetValue(n, out string? h) || !IsHex(h, 64))) return "";
        return Hash(string.Join("\n", names.Select(n => n + "=" + loaded[n])) + "\n");
    }

    public static string ReadyToken(string signature, string epoch, string catalog)
    {
        if (!IsHex(signature, 64) || !IsHex(epoch, 32) || !IsHex(catalog, 64)) throw new InvalidDataException("Invalid readiness input");
        return Version + "|" + signature + "|" + epoch + "|" + catalog;
    }

    public static bool IsReadyToken(string? token, string epoch)
    {
        string[] parts = (token ?? "").Split('|');
        return parts.Length == 4 && parts[0] == Version && IsHex(parts[1], 64) &&
            parts[2] == epoch && IsHex(parts[2], 32) && IsHex(parts[3], 64);
    }

    public static string? HeaderError(string protocol, string signature, string epoch, string localSignature)
    {
        if (protocol != Version) return "Frontier protocol is absent or different";
        if (!IsHex(signature, 64) || signature != localSignature) return "Frontier files, modules, settings or loader differ";
        if (!IsHex(epoch, 32)) return "Lobby handshake is not initialized";
        return null;
    }

    // No sequence-based Random, culture-dependent GetHashCode, clock, frame number, or client-side RNG.
    // Domain-separated SHA-256 gives the same choice on repeat calls for a spawn in the same session.
    public static int EnemyTier(string sessionSalt, string spawnId, string baseId, IReadOnlyList<int> weights)
    {
        long sum = weights.Sum(x => (long)x);
        if (weights.Count != 5 || weights.Any(x => x < 0) || sum is <= 0 or > int.MaxValue) throw new ArgumentException("Invalid weights");
        string message = "frontier-enemy/v1\n" + sessionSalt + "\n" + spawnId.ToLowerInvariant() + "\n" + baseId.ToLowerInvariant() + "\n";
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(message));
        // 32-bit multiply-high mapping; bias is at most one SHA outcome per bucket boundary.
        uint value = BinaryPrimitives.ReadUInt32LittleEndian(digest);
        int roll = (int)(((ulong)value * (ulong)sum) >> 32);
        return WeightedRoll.Pick(roll, weights);
    }
}

public sealed record PeerReadiness(ulong Id, string Token);
public sealed record ReadinessResult(bool Ready, string Reason, int ReadyCount, int MemberCount);

/// <summary>Fail-closed preflight and roster pinning. Not authentication and not an anti-cheat protocol.</summary>
public sealed class CoopRoster
{
    private HashSet<ulong>? frozen;
    private ulong owner;
    private string epoch = "";
    public bool IsFrozen => frozen is not null;
    public ulong[] FrozenMembers => frozen?.OrderBy(x => x).ToArray() ?? Array.Empty<ulong>();
    public void Clear() { frozen = null; owner = 0; epoch = ""; }

    public ReadinessResult Check(ulong currentOwner, ulong localMember, string currentEpoch, string expectedToken,
        IReadOnlyList<PeerReadiness> members)
    {
        if (currentOwner == 0 || localMember == 0 || !CoopProtocol.IsHex(currentEpoch, 32) || !CoopProtocol.IsReadyToken(expectedToken, currentEpoch))
            return new(false, "Handshake not initialized", 0, members.Count);
        if (members.Count is < 1 or > 250 || members.Any(m => m.Id == 0) || members.Select(m => m.Id).Distinct().Count() != members.Count)
            return new(false, "Invalid or changing lobby membership", 0, members.Count);
        if (!members.Any(m => m.Id == currentOwner) || !members.Any(m => m.Id == localMember))
            return new(false, "Owner or local member is not present", 0, members.Count);
        if (frozen is not null && (owner != currentOwner || epoch != currentEpoch))
            return new(false, "Host or lobby epoch changed; recreate the lobby", 0, members.Count);
        if (frozen is not null && members.Any(m => !frozen.Contains(m.Id)))
            return new(false, "New member after deployment lock; return to lobby", 0, members.Count);
        int ready = members.Count(m => m.Token == expectedToken);
        return ready == members.Count
            ? new(true, "READY", ready, members.Count)
            : new(false, "Waiting for identical loaded catalogs: " + ready + "/" + members.Count, ready, members.Count);
    }

    public ReadinessResult Freeze(ulong currentOwner, ulong localMember, string currentEpoch, string expectedToken,
        IReadOnlyList<PeerReadiness> members)
    {
        if (currentOwner != localMember) return new(false, "Only the lobby owner can deploy", 0, members.Count);
        var result = Check(currentOwner, localMember, currentEpoch, expectedToken, members);
        if (result.Ready && frozen is null) { frozen = members.Select(m => m.Id).ToHashSet(); owner = currentOwner; epoch = currentEpoch; }
        return result;
    }
}
