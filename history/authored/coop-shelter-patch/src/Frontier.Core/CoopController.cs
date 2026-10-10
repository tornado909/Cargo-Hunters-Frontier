using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Frontier.Engine;

namespace Frontier.Core;

internal sealed class CoopController
{
    private readonly Plugin plugin;
    private object? service;
    private SteamLobbyApi? steam;
    private readonly CoopRoster roster = new();
    private ulong lobby, owner, local;
    private string epoch = "", advertisedCatalog = "", expectedToken = "", sentToken = "";
    private PeerReadiness[] members = Array.Empty<PeerReadiness>();
    private long lastTick, lastProbe, lastReport;
    private readonly Dictionary<ulong, long> dataRequests = new();
    private bool refreshing, locked, coopRaidEntered, replaying;
    private sealed record PendingCall(object Target, string Method, object[] Arguments, ulong Lobby, string Epoch, long Expires);
    private PendingCall? pendingStart, pendingConnect;
    private ulong pinnedOwner;
    private string sessionIdBeforeStart = "";
    // The native ConnectToShelter creates and assigns _connection INSIDE its body.
    // Its prefix records the owner state only. Resolve the field at Connect, after
    // that assignment, and compare the actual IL2CPP object identities.
    internal const string ConnectionAdapterRevision = "shelter-late-bind-v2";
    private readonly ShelterConnectionBinding shelterBinding = new();
    public int ShelterConnectionsAllowed { get; private set; }
    public int ShelterNativeCallsObserved { get; private set; }
    public int ShelterConnectionBindingMisses { get; private set; }
    public string LastConnectionDecision { get; private set; } = "NOT_OBSERVED";
    public void EnterShelter() => shelterBinding.Enter();
    public void LeaveShelter() => shelterBinding.Leave();
    public void CaptureShelterConnection(object state)
    {
        // A null _connection here is expected on first entry, NOT an error.
        // Do not read it until BattleServerConnection.Connect is entered.
        shelterBinding.ObserveNativeCall(state);
        ShelterNativeCallsObserved++;
        LastConnectionDecision = "SHELTER_NATIVE_CALL_OBSERVED: waiting for in-method assignment";
        plugin.Log.LogInfo("Co-op connection [" + ConnectionAdapterRevision + "]: " + LastConnectionDecision);
    }
    private bool IsObservedShelterConnection(object connection)
    {
        bool matched = shelterBinding.MatchesCurrent(connection,
            state => Reflect.Read(state, "_connection"), Reflect.SameNative);
        if (!matched && shelterBinding.HasNativeState) ShelterConnectionBindingMisses++;
        return matched;
    }
    public void ShelterNativeCallFailed(object state, Exception error)
    {
        shelterBinding.ClearIfOwnedBy(state, Reflect.SameNative);
        LastConnectionDecision = "NATIVE_SHELTER_CALL_FAILED";
        Error(error);
    }
    public string Signature { get; }
    public bool HooksResolved { get; internal set; }
    public bool SteamExportsResolved => steam is not null;
    public string Status { get; private set; } = "Waiting for co-op service";
    public string LastError { get; private set; } = "";
    public string Role { get; private set; } = "None";
    public int ApprovedMembers { get; private set; }
    public int MemberCount => members.Length;
    public string Catalog => plugin.CatalogDigest;
    public int DeploymentChecks { get; private set; }
    public int AdmissionChecks { get; private set; }
    public int ConnectionChecks { get; private set; }
    public bool InLobby => ReadLobby() != 0;
    public object Snapshot => new { protocol = CoopProtocol.Version, Signature, Catalog, HooksResolved,
        SteamExportsResolved, Status, LastError, Role, ApprovedMembers, MemberCount,
        deploymentLocked = locked, frozenMemberCount = roster.FrozenMembers.Length,
        DeploymentChecks, AdmissionChecks, ConnectionChecks, ShelterConnectionsAllowed, LastConnectionDecision,
        connectionAdapter = ConnectionAdapterRevision, ShelterNativeCallsObserved, ShelterConnectionBindingMisses,
        connectionPurpose = shelterBinding.IntentObserved ? "LOCAL_SHELTER" : "RAID_OR_UNKNOWN",
        shelterNativeStateObserved = shelterBinding.HasNativeState,
        deferredStart = pendingStart is not null, deferredConnection = pendingConnect is not null, coopRaidEntered };

    public CoopController(Plugin plugin, string root, string packPath)
    {
        this.plugin = plugin;
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string file in Directory.EnumerateFiles(packPath, "*.json"))
        {
            // This file contains local installation defaults, not runtime game data.
            if (Path.GetFileName(file) == "pack-settings.example.json") continue;
            hashes.Add("packs/" + Path.GetFileName(file), PatchEngine.Hash(File.ReadAllBytes(file)));
        }
        foreach (string name in new[] { "Frontier.Core.dll", "Frontier.Engine.dll" })
            hashes.Add(name, PatchEngine.Hash(File.ReadAllBytes(Path.Combine(root, name))));
        foreach (string name in new[] { "BepInEx.Core.dll", "BepInEx.Unity.IL2CPP.dll", "0Harmony.dll", "Il2CppInterop.Runtime.dll" })
            hashes.Add("loader/" + name, PatchEngine.Hash(File.ReadAllBytes(Path.Combine(BepInEx.Paths.BepInExRootPath, "core", name))));
        Signature = CoopProtocol.Fingerprint(plugin.Engine.GameVersion, plugin.Settings, hashes);
    }

    public void Capture(object instance) { service = instance; }
    public void Release(object instance)
    {
        if (ReferenceEquals(service, instance) || Reflect.SameNative(service, instance))
        { service = null; coopRaidEntered = false; ResetLocalState(); SetStatus("Co-op service disposed"); }
    }
    private ulong ReadLobby() => service is null ? 0 : Convert.ToUInt64(Reflect.Read(Reflect.Read(service, "_currentLobby"), "Value"), CultureInfo.InvariantCulture);
    private static ulong LobbyId(object value) => Convert.ToUInt64(Reflect.Read(value, "Value"), CultureInfo.InvariantCulture);
    public void Error(Exception error)
    {
        string message = error.GetBaseException().Message;
        if (LastError != message) plugin.Log.LogWarning("Co-op BLOCKED: " + message);
        LastError = message; ApprovedMembers = 0; WithdrawReadiness();
        SetStatus("BLOCKED: " + message);
    }
    private void WithdrawReadiness()
    {
        expectedToken = "";
        if (sentToken.Length == 0) return;
        // Best-effort revocation; a transport failure itself prevents local start/connection.
        try { if (steam is not null && lobby != 0) steam.Ready(lobby, ""); } catch { }
        sentToken = "";
    }
    private void SetStatus(string message)
    {
        if (Status == message) return;
        Status = message; plugin.Log.LogInfo("Co-op: " + message); plugin.WriteReport();
    }
    private void ResetLocalState()
    {
        lobby = owner = local = pinnedOwner = 0; epoch = advertisedCatalog = expectedToken = sentToken = "";
        members = Array.Empty<PeerReadiness>(); roster.Clear(); locked = false; Role = "None"; ApprovedMembers = 0;
        pendingStart = pendingConnect = null; LastError = "";
    }
    private void ProbeSteam(bool force = false)
    {
        if (steam is not null) return;
        long now = Stopwatch.GetTimestamp();
        if (!force && now - lastProbe < Stopwatch.Frequency * 2) return;
        lastProbe = now; steam = new SteamLobbyApi();
        plugin.WriteReport();
    }
    public void Tick(bool force = false)
    {
        long now = Stopwatch.GetTimestamp();
        if (!force && now - lastTick < Stopwatch.Frequency / 2) return;
        lastTick = now;
        // Diagnostic mode resolves exports only. It never publishes data or changes joinability.
        try
        {
            ProbeSteam(force);
            if (!plugin.Active) { SetStatus("DIAGNOSTIC: network hooks resolved; lobby data not changed"); return; }
            Refresh();
            RetryDeferredClientCalls();
            if (now - lastReport >= Stopwatch.Frequency * 10) { lastReport = now; plugin.WriteReport(); }
        }
        catch (Exception e) { Error(e); }
    }
    public void Refresh()
    {
        if (refreshing) return;
        refreshing = true;
        try
        {
            ulong id = ReadLobby();
            if (id == 0)
            {
                bool wasLocked = locked;
                ResetLocalState();
                SetStatus(wasLocked || coopRaidEntered ? "Lobby closed during a co-op raid; reconnect is blocked" : "SOLO / not in a lobby");
                return;
            }
            if (!plugin.Settings.CoopEnabled) { WithdrawReadiness(); SetStatus("BLOCKED: CoopEnabled=false"); return; }
            ProbeSteam(true);
            object user = Reflect.Read(Reflect.Read(service, "_steamworks"), "LocalUser") ?? throw new InvalidDataException("Steam user unavailable");
            ulong localId = Convert.ToUInt64(Reflect.Read(user, "SteamId"), CultureInfo.InvariantCulture);
            ulong ownerId = steam!.Owner(id);
            if (localId == 0 || ownerId == 0) throw new InvalidDataException("Lobby owner/member is unavailable");
            if (lobby != id)
            {
                ResetLocalState(); lobby = id; local = localId; owner = pinnedOwner = ownerId;
                if (local == owner)
                {
                    epoch = Guid.NewGuid().ToString("N");
                    PublishHeader();
                }
            }
            owner = ownerId; local = localId; Role = owner == local ? "Host" : "Client";
            if (owner != pinnedOwner)
            { WithdrawReadiness(); SetStatus("BLOCKED: lobby owner changed; recreate the lobby (no host migration)"); return; }
            string remoteEpoch = steam.Get(lobby, CoopProtocol.EpochKey);
            string? headerError = CoopProtocol.HeaderError(steam.Get(lobby, CoopProtocol.ProtocolKey),
                steam.Get(lobby, CoopProtocol.SignatureKey), remoteEpoch, Signature);
            if (headerError is not null)
            { WithdrawReadiness(); ApprovedMembers = 0; SetStatus("BLOCKED: " + headerError); return; }
            if ((roster.IsFrozen || coopRaidEntered) && epoch != remoteEpoch)
            { WithdrawReadiness(); SetStatus("BLOCKED: epoch changed during deployment"); return; }
            if (owner == local && epoch != remoteEpoch)
            { WithdrawReadiness(); SetStatus("WAIT: new host epoch has not become visible yet"); return; }
            if (owner != local) epoch = remoteEpoch;
            if (owner == local && advertisedCatalog != Catalog)
            {
                steam.Set(lobby, CoopProtocol.CatalogKey, Catalog); advertisedCatalog = Catalog;
            }
            string hostCatalog = steam.Get(lobby, CoopProtocol.CatalogKey);
            if (!CoopProtocol.IsHex(Catalog, 64) || hostCatalog != Catalog)
            { WithdrawReadiness(); ApprovedMembers = 0; SetStatus("WAIT: all 9 matching catalogs must finish loading on host and client"); return; }
            expectedToken = CoopProtocol.ReadyToken(Signature, epoch, Catalog);
            if (sentToken != expectedToken) { steam.Ready(lobby, expectedToken); sentToken = expectedToken; }
            members = steam.Members(lobby);
            var ready = roster.Check(owner, local, epoch, expectedToken, members);
            ApprovedMembers = ready.ReadyCount;
            if (ready.Ready) LastError = "";
            SetStatus((locked ? "DEPLOYMENT LOCKED | " : "") + ready.Reason);
        }
        finally { refreshing = false; }
    }
    private void PublishHeader()
    {
        steam!.Set(lobby, CoopProtocol.ProtocolKey, CoopProtocol.Version);
        steam.Set(lobby, CoopProtocol.SignatureKey, Signature);
        steam.Set(lobby, CoopProtocol.EpochKey, epoch);
        steam.Set(lobby, CoopProtocol.CatalogKey, Catalog); advertisedCatalog = Catalog;
        steam.Set(lobby, CoopProtocol.PhaseKey, "lobby");
        // Deliberately do NOT change the game's client_version, authentication or saved version.
    }
    public bool IsCompatible(object lobbyOrInfo)
    {
        if (!plugin.Active) return true;
        if (!plugin.Settings.CoopEnabled) return false;
        object? value = lobbyOrInfo.GetType().FullName == "Game.CoopSystem.Data.LobbyInfo" ? Reflect.Read(lobbyOrInfo, "Id") : lobbyOrInfo;
        ulong id = value is null ? 0 : LobbyId(value);
        if (id == 0) return false;
        ProbeSteam(true);
        string? error = CoopProtocol.HeaderError(steam!.Get(id, CoopProtocol.ProtocolKey),
            steam.Get(id, CoopProtocol.SignatureKey), steam.Get(id, CoopProtocol.EpochKey), Signature);
        if (error is not null)
        {
            long now = Stopwatch.GetTimestamp();
            if (!dataRequests.TryGetValue(id, out long prev) || now - prev > Stopwatch.Frequency * 2)
            {
                // Keep requests bounded, including a long browsing session with many different lobbies.
                if (dataRequests.Count > 128) dataRequests.Clear();
                dataRequests[id] = now; steam.Request(id);
            }
            LastError = error;
            return false;
        }
        return true;
    }
    public void RejectJoin(object callback, string reason)
    {
        Type result = Reflect.FindType("Game.CoopSystem.Data.LobbyJoinResult");
        MethodInfo failed = result.GetMethods(Reflect.Flags).Single(m => m.Name == "Failed" && m.IsStatic &&
            m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));
        object value = failed.Invoke(null, new object[] { "Frontier: " + reason })!;
        Reflect.UniqueMethod(callback.GetType(), "Invoke", 1).Invoke(callback, new[] { value });
        SetStatus("JOIN BLOCKED: " + reason);
    }
    private bool ReadyNow()
    {
        Refresh();
        if (lobby == 0 || owner != pinnedOwner || !plugin.Settings.CoopEnabled || expectedToken.Length == 0) return false;
        return roster.Check(owner, local, epoch, expectedToken, members).Ready;
    }
    public bool CanPrepare()
    {
        if (!InLobby) return !coopRaidEntered;
        return ReadyNow();
    }
    public bool BeginDeployment(out bool createdHostLock)
    {
        createdHostLock = false;
        DeploymentChecks++;
        if (!InLobby) return !coopRaidEntered;
        if (!ReadyNow()) return false;
        // The native method also runs on clients to record participation in the host's session.
        // Do not suppress that client branch; only the owner creates/locks a new session.
        if (Reflect.UniqueMethod(service!.GetType(), "CanStartSession", 0).Invoke(service, null) is not true) return false;
        if (owner != local) return steam!.Get(lobby, CoopProtocol.PhaseKey) == "locked";
        if (locked) return roster.IsFrozen;
        // Lock first, then take a fresh snapshot: no approval from a stale UI readiness result.
        sessionIdBeforeStart = Reflect.Read(service, "_currentSessionId")?.ToString() ?? "";
        if (!steam!.SetJoinable(lobby, false)) { SetStatus("BLOCKED: Steam could not lock the lobby"); return false; }
        locked = true;
        try
        {
            Refresh();
            if (expectedToken.Length == 0 || owner != pinnedOwner) { RollbackDeployment(); return false; }
            var result = roster.Freeze(owner, local, epoch, expectedToken, members);
            if (!result.Ready) { RollbackDeployment(); SetStatus(result.Reason); return false; }
            steam.Set(lobby, CoopProtocol.PhaseKey, "locked");
            SetStatus("DEPLOYMENT LOCKED | READY " + members.Length + "/" + members.Length);
            createdHostLock = true;
            return true;
        }
        catch { RollbackDeployment(); throw; }
    }
    public void NativeStartCompleted(bool createdHostLock)
    {
        if (!createdHostLock) return;
        string id = Reflect.Read(service, "_currentSessionId")?.ToString() ?? "";
        if (!Guid.TryParse(id, out Guid parsed) || parsed == Guid.Empty || id == sessionIdBeforeStart)
        { RollbackDeployment(); SetStatus("Native start did not create a session; lobby unlocked"); }
        else coopRaidEntered = true;
    }
    public bool CanPublishSessionReady() => (!InLobby && !coopRaidEntered) || (ReadyNow() && (owner != local || (locked && roster.IsFrozen)));
    public void RollbackDeployment()
    {
        if (lobby != 0 && locked && steam is not null && steam.Owner(lobby) == local)
        {
            if (!steam.SetJoinable(lobby, true)) throw new InvalidOperationException("Could not unlock lobby; leave and recreate it");
            steam.Set(lobby, CoopProtocol.PhaseKey, "lobby");
        }
        locked = false; roster.Clear();
    }
    public void SessionCleared()
    {
        if (!plugin.Active) return;
        coopRaidEntered = false; pendingStart = pendingConnect = null;
        if (!locked) return;
        // A client must never unlock its host's lobby or mint an epoch.
        Refresh();
        if (lobby != 0 && owner == local && owner == pinnedOwner)
        {
            RollbackDeployment(); epoch = Guid.NewGuid().ToString("N"); sentToken = ""; PublishHeader();
        }
        else { roster.Clear(); locked = false; }
        Refresh();
    }
    public bool CanAdmitPlayer(object manager)
    {
        AdmissionChecks++;
        object session = Reflect.Read(manager, "_battleSession") ?? throw new InvalidDataException("Missing battle session");
        if (Reflect.Read(session, "IsAuthoritative") is not true) return false;
        if (!InLobby) return !coopRaidEntered; // Never treat a lost co-op lobby as a solo server.
        return ReadyNow() && local == owner && locked && roster.IsFrozen;
    }
    private static bool IsLoopback(object address)
    {
        string ip = Reflect.Read(address, "IpAddress")?.ToString() ?? "";
        return string.Equals(ip, "localhost", StringComparison.OrdinalIgnoreCase) ||
            (System.Net.IPAddress.TryParse(ip, out var parsed) && System.Net.IPAddress.IsLoopback(parsed));
    }
    public bool CanConnect(object connection, object address)
    {
        ConnectionChecks++;
        bool isSteam = Reflect.Read(address, "IsSteam") is true;
        bool loopbackAddress = !isSteam && IsLoopback(address);
        if (ConnectionPolicy.AllowLocalShelter(IsObservedShelterConnection(connection), isSteam,
                loopbackAddress, CoopProtocol.IsHex(Catalog, 64)))
        {
            // A player's OWN shelter is not the host's raid. Do not require a
            // deployment lock or all other peers to acknowledge before opening it.
            // No Steam destination, public IP, admission, authentication or raid gate is bypassed.
            ShelterConnectionsAllowed++;
            LastConnectionDecision = "ALLOW_LOCAL_SHELTER: native path + loopback + verified catalog";
            plugin.Log.LogInfo("Co-op connection: " + LastConnectionDecision);
            plugin.WriteReport();
            return true;
        }
        LastConnectionDecision = "EVALUATING_RAID";
        if (InLobby)
        {
            if (!ReadyNow()) return false;
            if (!ConnectionPolicy.DeploymentReady(owner == local, locked, roster.IsFrozen, steam!.Get(lobby, CoopProtocol.PhaseKey)))
            { LastConnectionDecision = "WAIT_RAID_DEPLOYMENT_LOCK"; SetStatus("WAIT: host has not locked deployment"); return false; }
            if (Reflect.Read(address, "IsSteam") is true)
            {
                ulong target = Convert.ToUInt64(Reflect.Read(address, "SteamId"), CultureInfo.InvariantCulture);
                if (target != owner) { SetStatus("BLOCKED: Steam destination is not the verified lobby owner"); return false; }
            }
            else if (local != owner || !IsLoopback(address))
            { SetStatus("BLOCKED: only the host's built-in loopback connection is allowed without Steam"); return false; }
            coopRaidEntered = true;
            LastConnectionDecision = "ALLOW_VERIFIED_RAID";
            return true;
        }
        // This release supports Steam co-op and the built-in loopback solo server, not public/IP servers.
        if (coopRaidEntered || Reflect.Read(address, "IsSteam") is true)
        { SetStatus("BLOCKED: a verified Frontier lobby is required"); return false; }
        bool loopback = IsLoopback(address);
        if (!loopback) SetStatus("BLOCKED: external IP/dedicated servers are not supported by this co-op adapter");
        return loopback;
    }
    public void ClearDeferredStart() => pendingStart = null;
    public void ClearDeferredConnection(object connection)
    {
        if (pendingConnect is not null && (ReferenceEquals(pendingConnect.Target, connection) || Reflect.SameNative(pendingConnect.Target, connection)))
            pendingConnect = null;
    }
    public void DeferClientStart(object target, string map) => Defer(ref pendingStart, target, "StartSession", new object[] { map });
    public void DeferClientConnection(object target, object address, bool reconnect)
    {
        // Never retry an unverified destination or turn an explicit external-server request into a Steam connection.
        if (Reflect.Read(address, "IsSteam") is not true || Convert.ToUInt64(Reflect.Read(address, "SteamId"), CultureInfo.InvariantCulture) != owner) return;
        Defer(ref pendingConnect, target, reconnect ? "Reconnect" : "Connect", reconnect ? Array.Empty<object>() : new[] { address });
    }
    private void Defer(ref PendingCall? slot, object target, string method, object[] arguments)
    {
        if (replaying || !plugin.Active || !plugin.Settings.CoopEnabled || lobby == 0 || owner == 0 || owner == local || owner != pinnedOwner ||
            !CoopProtocol.IsHex(epoch, 32)) return;
        long until = slot?.Expires ?? (Stopwatch.GetTimestamp() + 60 * Stopwatch.Frequency);
        slot = new(target, method, arguments, lobby, epoch, until);
        SetStatus("WAIT: deferred client " + method + " until matching lobby data arrives (60 s maximum)");
    }
    private void RetryDeferredClientCalls()
    {
        if (replaying || (pendingStart is null && pendingConnect is null)) return;
        void Expire(ref PendingCall? pending)
        {
            if (pending is not null && (pending.Lobby != lobby || pending.Epoch != epoch || Stopwatch.GetTimestamp() >= pending.Expires))
            { SetStatus("Deferred " + pending.Method + " canceled or timed out; return to the lobby and retry"); pending = null; }
        }
        Expire(ref pendingStart); Expire(ref pendingConnect);
        if ((pendingStart is null && pendingConnect is null) || !ReadyNow() || owner == local || steam!.Get(lobby, CoopProtocol.PhaseKey) != "locked") return;
        // Steam metadata is asynchronous. Suppressing a one-shot client callback forever would
        // strand the player; replay that same native request once readiness becomes visible.
        replaying = true;
        try
        {
            if (pendingStart is not null && Reflect.UniqueMethod(service!.GetType(), "CanStartSession", 0).Invoke(service, null) is true)
            {
                PendingCall call = pendingStart; pendingStart = null;
                Reflect.UniqueMethod(call.Target.GetType(), call.Method, call.Arguments.Length).Invoke(call.Target, call.Arguments);
            }
            if (pendingConnect is not null)
            {
                PendingCall call = pendingConnect; pendingConnect = null;
                Reflect.UniqueMethod(call.Target.GetType(), call.Method, call.Arguments.Length).Invoke(call.Target, call.Arguments);
            }
        }
        finally { replaying = false; }
    }
}
