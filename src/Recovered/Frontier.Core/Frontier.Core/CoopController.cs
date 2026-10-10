using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Frontier.Engine;
using Il2CppInterop.Runtime.InteropTypes;

namespace Frontier.Core;

internal sealed class CoopController
{
	private sealed record PendingCall(object Target, string Method, object[] Arguments, ulong Lobby, string Epoch, long Expires, int Generation)
	{
		[CompilerGenerated]
		private System.Type EqualityContract
		{
			[CompilerGenerated]
			get
			{
				return typeof(PendingCall);
			}
		}

		[CompilerGenerated]
		public override string ToString()
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Expected O, but got Unknown
			StringBuilder val = new StringBuilder();
			val.Append("PendingCall");
			val.Append(" { ");
			if (PrintMembers(val))
			{
				val.Append(' ');
			}
			val.Append('}');
			return ((object)val).ToString();
		}

		[CompilerGenerated]
		private bool PrintMembers(StringBuilder builder)
		{
			RuntimeHelpers.EnsureSufficientExecutionStack();
			builder.Append("Target = ");
			builder.Append(Target);
			builder.Append(", Method = ");
			builder.Append((object)Method);
			builder.Append(", Arguments = ");
			builder.Append((object)Arguments);
			builder.Append(", Lobby = ");
			builder.Append(((object)Lobby/*cast due to .constrained prefix*/).ToString());
			builder.Append(", Epoch = ");
			builder.Append((object)Epoch);
			builder.Append(", Expires = ");
			builder.Append(((object)Expires/*cast due to .constrained prefix*/).ToString());
			builder.Append(", Generation = ");
			builder.Append(((object)Generation/*cast due to .constrained prefix*/).ToString());
			return true;
		}

		[CompilerGenerated]
		public bool Equals(PendingCall? other)
		{
			if ((object)this != other)
			{
				if ((object)other != null && EqualityContract == other.EqualityContract && EqualityComparer<object>.Default.Equals(Target, other.Target) && EqualityComparer<string>.Default.Equals(Method, other.Method) && EqualityComparer<object[]>.Default.Equals(Arguments, other.Arguments) && EqualityComparer<ulong>.Default.Equals(Lobby, other.Lobby) && EqualityComparer<string>.Default.Equals(Epoch, other.Epoch) && EqualityComparer<long>.Default.Equals(Expires, other.Expires))
				{
					return EqualityComparer<int>.Default.Equals(Generation, other.Generation);
				}
				return false;
			}
			return true;
		}
	}

	private readonly Plugin plugin;

	private object? service;

	private object? retiredService;

	private SteamLobbyApi? steam;

	private readonly CoopRoster roster = new CoopRoster();

	private ulong lobby;

	private ulong owner;

	private ulong local;

	private string epoch = "";

	private string advertisedCatalog = "";

	private string expectedToken = "";

	private string sentToken = "";

	private PeerReadiness[] members = System.Array.Empty<PeerReadiness>();

	private long lastTick;

	private long lastProbe;

	private long lastReport;

	private readonly Dictionary<ulong, long> dataRequests = new Dictionary<ulong, long>();

	private bool refreshing;

	private bool locked;

	private bool coopRaidEntered;

	private bool replaying;

	private bool nativeRaidActive;

	private bool nativeRaidConnecting;

	private bool safeLobbyScene;

	private bool rotateEpochPending;

	private bool synchronizingRole;

	private int lifecycleGeneration;

	private int resetCount;

	private int lobbyHandovers;

	private int canceledDeferredCalls;

	private string lastLifecycle = "STARTUP";

	private string nativeRole = "Unknown";

	private PendingCall? pendingStart;

	private PendingCall? pendingConnect;

	private ulong pinnedOwner;

	private string sessionIdBeforeStart = "";

	internal const string ConnectionAdapterRevision = "session-lifecycle-v10.6";

	private readonly ShelterConnectionBinding shelterBinding = new ShelterConnectionBinding();

	[field: CompilerGenerated]
	public int ShelterConnectionsAllowed
	{
		[CompilerGenerated]
		get;
		[CompilerGenerated]
		private set;
	}

	[field: CompilerGenerated]
	public int ShelterNativeCallsObserved
	{
		[CompilerGenerated]
		get;
		[CompilerGenerated]
		private set;
	}

	[field: CompilerGenerated]
	public int ShelterConnectionBindingMisses
	{
		[CompilerGenerated]
		get;
		[CompilerGenerated]
		private set;
	}

	[field: CompilerGenerated]
	public string LastConnectionDecision
	{
		[CompilerGenerated]
		get;
		[CompilerGenerated]
		private set;
	} = "NOT_OBSERVED";

	[field: CompilerGenerated]
	public string Signature
	{
		[CompilerGenerated]
		get;
	}

	[field: CompilerGenerated]
	public bool HooksResolved
	{
		[CompilerGenerated]
		get;
		[CompilerGenerated]
		internal set;
	}

	public bool SteamExportsResolved => steam != null;

	[field: CompilerGenerated]
	public string Status
	{
		[CompilerGenerated]
		get;
		[CompilerGenerated]
		private set;
	} = "Waiting for co-op service";

	[field: CompilerGenerated]
	public string LastError
	{
		[CompilerGenerated]
		get;
		[CompilerGenerated]
		private set;
	} = "";

	[field: CompilerGenerated]
	public string Role
	{
		[CompilerGenerated]
		get;
		[CompilerGenerated]
		private set;
	} = "None";

	[field: CompilerGenerated]
	public int ApprovedMembers
	{
		[CompilerGenerated]
		get;
		[CompilerGenerated]
		private set;
	}

	public int MemberCount => members.Length;

	public string Catalog => plugin.CatalogDigest;

	[field: CompilerGenerated]
	public int DeploymentChecks
	{
		[CompilerGenerated]
		get;
		[CompilerGenerated]
		private set;
	}

	[field: CompilerGenerated]
	public int AdmissionChecks
	{
		[CompilerGenerated]
		get;
		[CompilerGenerated]
		private set;
	}

	[field: CompilerGenerated]
	public int ConnectionChecks
	{
		[CompilerGenerated]
		get;
		[CompilerGenerated]
		private set;
	}

	public bool InLobby => ReadLobby() != 0;

	public object Snapshot => new
	{
		protocol = "1",
		Signature = Signature,
		Catalog = Catalog,
		HooksResolved = HooksResolved,
		SteamExportsResolved = SteamExportsResolved,
		Status = Status,
		LastError = LastError,
		Role = Role,
		ApprovedMembers = ApprovedMembers,
		MemberCount = MemberCount,
		deploymentLocked = locked,
		frozenMemberCount = roster.FrozenMembers.Length,
		DeploymentChecks = DeploymentChecks,
		AdmissionChecks = AdmissionChecks,
		ConnectionChecks = ConnectionChecks,
		ShelterConnectionsAllowed = ShelterConnectionsAllowed,
		LastConnectionDecision = LastConnectionDecision,
		connectionAdapter = "session-lifecycle-v10.6",
		ShelterNativeCallsObserved = ShelterNativeCallsObserved,
		ShelterConnectionBindingMisses = ShelterConnectionBindingMisses,
		connectionPurpose = (shelterBinding.IntentObserved ? "LOCAL_SHELTER" : "RAID_OR_UNKNOWN"),
		shelterNativeStateObserved = shelterBinding.HasNativeState,
		deferredStart = ((object)pendingStart != null),
		deferredConnection = ((object)pendingConnect != null),
		coopRaidEntered = coopRaidEntered,
		lifecycleRevision = "v10.6",
		lobbyId = lobby.ToString((IFormatProvider)(object)CultureInfo.InvariantCulture),
		ownerSteamId = owner.ToString((IFormatProvider)(object)CultureInfo.InvariantCulture),
		pinnedOwnerSteamId = pinnedOwner.ToString((IFormatProvider)(object)CultureInfo.InvariantCulture),
		localSteamId = local.ToString((IFormatProvider)(object)CultureInfo.InvariantCulture),
		nativeRole = nativeRole,
		nativeRaidActive = nativeRaidActive,
		nativeRaidConnecting = nativeRaidConnecting,
		safeLobbyScene = safeLobbyScene,
		lifecycleGeneration = lifecycleGeneration,
		lifecycleResets = resetCount,
		lastLifecycle = lastLifecycle,
		lobbyOnlyHandovers = lobbyHandovers,
		canceledDeferredCalls = canceledDeferredCalls,
		liveRaidMigrationSupported = false
	};

	private static bool NativeAlive(object? value)
	{
		if (value != null)
		{
			Il2CppObjectBase val = (Il2CppObjectBase)((value is Il2CppObjectBase) ? value : null);
			if (val != null)
			{
				return !val.WasCollected;
			}
			return true;
		}
		return false;
	}

	public void LobbyChanged(object instance)
	{
		Capture(instance);
		lastTick = 0L;
	}

	private void CancelDeferred(string reason)
	{
		if (pendingStart != null)
		{
			canceledDeferredCalls++;
		}
		if (pendingConnect != null)
		{
			canceledDeferredCalls++;
		}
		pendingStart = (pendingConnect = null);
		lifecycleGeneration++;
		lastLifecycle = reason;
	}

	private void EndRaidLocal(string reason)
	{
		bool num = locked || roster.IsFrozen || coopRaidEntered;
		CancelDeferred(reason);
		locked = false;
		roster.Clear();
		coopRaidEntered = false;
		sessionIdBeforeStart = "";
		expectedToken = (sentToken = "");
		ApprovedMembers = 0;
		if (num && lobby != 0L && owner == local && owner == pinnedOwner)
		{
			rotateEpochPending = true;
		}
		resetCount++;
		lastTick = 0L;
	}

	public void EnterMenu()
	{
		safeLobbyScene = true;
		nativeRaidActive = (nativeRaidConnecting = false);
		shelterBinding.Leave();
		EndRaidLocal("ENTER_MENU");
	}

	public void BeginRaidConnection()
	{
		safeLobbyScene = false;
		nativeRaidConnecting = true;
		shelterBinding.Leave();
		lastLifecycle = "ENTER_RAID_CONNECTION";
	}

	public void EnterRaid()
	{
		safeLobbyScene = false;
		nativeRaidActive = true;
		nativeRaidConnecting = false;
		lastLifecycle = "ENTER_RAID";
	}

	public void ExitRaid()
	{
		nativeRaidActive = (nativeRaidConnecting = false);
		safeLobbyScene = false;
		CancelDeferred("EXIT_RAID_OR_CONNECTION_LOST");
	}

	public void LeaveLobbyIntent(object instance)
	{
		if (service == null || Reflect.SameNative(service, instance))
		{
			CancelDeferred("NATIVE_LEAVE_LOBBY");
			EndRaidLocal("NATIVE_LEAVE_LOBBY");
			rotateEpochPending = false;
		}
	}

	public void ConnectionDisconnected(object connection)
	{
		ClearDeferredConnection(connection);
		CancelDeferred("CONNECTION_DISCONNECT");
	}

	private bool SynchronizeLobbyOwner(ulong newOwner, ulong localId)
	{
		//IL_0226: Unknown result type (might be due to invalid IL or missing references)
		//IL_0256: Unknown result type (might be due to invalid IL or missing references)
		//IL_025b: Unknown result type (might be due to invalid IL or missing references)
		//IL_028d: Unknown result type (might be due to invalid IL or missing references)
		if (nativeRaidActive || nativeRaidConnecting || !safeLobbyScene || locked || roster.IsFrozen || coopRaidEntered)
		{
			CancelDeferred("OWNER_LOST_DURING_RAID");
			WithdrawReadiness();
			ApprovedMembers = 0;
			SetStatus("HOST_LOST: active raid cannot migrate; return to lobby/shelter");
			return false;
		}
		if (!NativeAlive(service) || steam == null)
		{
			return false;
		}
		if (steam.Owner(lobby) != newOwner)
		{
			return false;
		}
		PeerReadiness[] array = steam.Members(lobby);
		if (!Enumerable.Any<PeerReadiness>((System.Collections.Generic.IEnumerable<PeerReadiness>)array, (Func<PeerReadiness, bool>)((PeerReadiness m) => m.Id == newOwner)) || !Enumerable.Any<PeerReadiness>((System.Collections.Generic.IEnumerable<PeerReadiness>)array, (Func<PeerReadiness, bool>)((PeerReadiness m) => m.Id == localId)))
		{
			return false;
		}
		EndRaidLocal("LOBBY_OWNER_HANDOVER");
		pinnedOwner = (owner = newOwner);
		local = localId;
		epoch = (advertisedCatalog = (expectedToken = (sentToken = "")));
		rotateEpochPending = false;
		MethodInfo val = Reflect.UniqueMethod(service.GetType(), "SetCurrentLobby", 2);
		object obj = System.Enum.Parse(((MethodBase)val).GetParameters()[1].ParameterType, (newOwner == localId) ? "Host" : "Client");
		object obj2 = Reflect.Read(service, "_currentLobby");
		synchronizingRole = true;
		try
		{
			((MethodBase)val).Invoke(service, new object[2] { obj2, obj });
			((MethodBase)Reflect.UniqueMethod(service.GetType(), "ClearSession", 0)).Invoke(service, (object[])null);
		}
		finally
		{
			synchronizingRole = false;
		}
		nativeRole = Reflect.Read(service, "Role")?.ToString() ?? "Unknown";
		if (nativeRole != obj.ToString())
		{
			throw new InvalidOperationException("Native lobby role did not confirm handover");
		}
		if (steam.Owner(lobby) != newOwner)
		{
			return false;
		}
		if (newOwner == localId)
		{
			Guid val2 = Guid.NewGuid();
			epoch = val2.ToString("N");
			PublishHeader();
			if (!steam.SetJoinable(lobby, value: true))
			{
				throw new InvalidOperationException("Cannot reopen recovered lobby");
			}
		}
		lobbyHandovers++;
		return true;
	}

	public void EnterShelter()
	{
		nativeRaidActive = (nativeRaidConnecting = false);
		safeLobbyScene = true;
		EndRaidLocal("ENTER_SHELTER");
		shelterBinding.Enter();
	}

	public void LeaveShelter()
	{
		shelterBinding.Leave();
	}

	public void CaptureShelterConnection(object state)
	{
		shelterBinding.ObserveNativeCall(state);
		ShelterNativeCallsObserved++;
		LastConnectionDecision = "SHELTER_NATIVE_CALL_OBSERVED: waiting for in-method assignment";
		((BasePlugin)plugin).Log.LogInfo((object)("Co-op connection [session-lifecycle-v10.6]: " + LastConnectionDecision));
	}

	private bool IsObservedShelterConnection(object connection)
	{
		bool num = shelterBinding.MatchesCurrent(connection, (object state) => Reflect.Read(state, "_connection"), Reflect.SameNative);
		if (!num && shelterBinding.HasNativeState)
		{
			ShelterConnectionBindingMisses++;
		}
		return num;
	}

	public void ShelterNativeCallFailed(object state, System.Exception error)
	{
		shelterBinding.ClearIfOwnedBy(state, Reflect.SameNative);
		LastConnectionDecision = "NATIVE_SHELTER_CALL_FAILED";
		Error(error);
	}

	public CoopController(Plugin plugin, string root, string packPath)
	{
		this.plugin = plugin;
		Dictionary<string, string> val = new Dictionary<string, string>((IEqualityComparer<string>)(object)StringComparer.Ordinal);
		System.Collections.Generic.IEnumerator<string> enumerator = Directory.EnumerateFiles(packPath, "*.json").GetEnumerator();
		try
		{
			while (((System.Collections.IEnumerator)enumerator).MoveNext())
			{
				string current = enumerator.Current;
				if (!(Path.GetFileName(current) == "pack-settings.example.json"))
				{
					val.Add("packs/" + Path.GetFileName(current), PatchEngine.Hash(File.ReadAllBytes(current)));
				}
			}
		}
		finally
		{
			((System.IDisposable)enumerator)?.Dispose();
		}
		string[] array = new string[2] { "Frontier.Core.dll", "Frontier.Engine.dll" };
		foreach (string text in array)
		{
			val.Add(text, PatchEngine.Hash(File.ReadAllBytes(Path.Combine(root, text))));
		}
		array = new string[4] { "BepInEx.Core.dll", "BepInEx.Unity.IL2CPP.dll", "0Harmony.dll", "Il2CppInterop.Runtime.dll" };
		foreach (string text2 in array)
		{
			val.Add("loader/" + text2, PatchEngine.Hash(File.ReadAllBytes(Path.Combine(Paths.BepInExRootPath, "core", text2))));
		}
		Signature = CoopProtocol.Fingerprint(plugin.Engine.GameVersion, plugin.Settings, (IReadOnlyDictionary<string, string>)(object)val);
	}

	public void Capture(object instance)
	{
		if (NativeAlive(instance) && instance != retiredService && (!NativeAlive(retiredService) || !Reflect.SameNative(retiredService, instance)))
		{
			if (service != null && (!NativeAlive(service) || !Reflect.SameNative(service, instance)))
			{
				ResetLocalState();
			}
			service = instance;
		}
	}

	public void Release(object instance)
	{
		if (service == instance || (NativeAlive(service) && Reflect.SameNative(service, instance)))
		{
			retiredService = instance;
			service = null;
			ResetLocalState();
			shelterBinding.Leave();
			steam = null;
			nativeRaidActive = (nativeRaidConnecting = (safeLobbyScene = false));
			SetStatus("Co-op service disposed before native teardown");
		}
	}

	private ulong ReadLobby()
	{
		if (NativeAlive(service))
		{
			return Convert.ToUInt64(Reflect.Read(Reflect.Read(service, "_currentLobby"), "Value"), (IFormatProvider)(object)CultureInfo.InvariantCulture);
		}
		return 0uL;
	}

	private static ulong LobbyId(object value)
	{
		return Convert.ToUInt64(Reflect.Read(value, "Value"), (IFormatProvider)(object)CultureInfo.InvariantCulture);
	}

	public void Error(System.Exception error)
	{
		string message = error.GetBaseException().Message;
		if (LastError != message)
		{
			((BasePlugin)plugin).Log.LogWarning((object)("Co-op BLOCKED: " + message));
		}
		LastError = message;
		ApprovedMembers = 0;
		WithdrawReadiness();
		SetStatus("BLOCKED: " + message);
	}

	private void WithdrawReadiness()
	{
		expectedToken = "";
		if (sentToken.Length == 0)
		{
			return;
		}
		try
		{
			if (steam != null && lobby != 0L)
			{
				steam.Ready(lobby, "");
			}
		}
		catch
		{
		}
		sentToken = "";
	}

	private void SetStatus(string message)
	{
		if (!(Status == message))
		{
			Status = message;
			((BasePlugin)plugin).Log.LogInfo((object)("Co-op: " + message));
			plugin.WriteReport();
		}
	}

	private void ResetLocalState()
	{
		CancelDeferred("RESET_LOBBY");
		coopRaidEntered = false;
		sessionIdBeforeStart = "";
		rotateEpochPending = false;
		dataRequests.Clear();
		nativeRole = "Unknown";
		resetCount++;
		lobby = (owner = (local = (pinnedOwner = 0uL)));
		epoch = (advertisedCatalog = (expectedToken = (sentToken = "")));
		members = System.Array.Empty<PeerReadiness>();
		roster.Clear();
		locked = false;
		Role = "None";
		ApprovedMembers = 0;
		pendingStart = (pendingConnect = null);
		LastError = "";
	}

	private void ProbeSteam(bool force = false)
	{
		if (steam == null)
		{
			long timestamp = Stopwatch.GetTimestamp();
			if (force || timestamp - lastProbe >= Stopwatch.Frequency * 2)
			{
				lastProbe = timestamp;
				steam = new SteamLobbyApi();
				plugin.WriteReport();
			}
		}
	}

	public void Tick(bool force = false)
	{
		long timestamp = Stopwatch.GetTimestamp();
		if (!force && timestamp - lastTick < Stopwatch.Frequency / 2)
		{
			return;
		}
		lastTick = timestamp;
		try
		{
			if (!NativeAlive(service))
			{
				return;
			}
			ProbeSteam(force);
			if (!plugin.Active)
			{
				SetStatus("DIAGNOSTIC: network hooks resolved; lobby data not changed");
				return;
			}
			Refresh();
			RetryDeferredClientCalls();
			if (timestamp - lastReport >= Stopwatch.Frequency * 10)
			{
				lastReport = timestamp;
				plugin.WriteReport();
			}
		}
		catch (System.Exception error)
		{
			Error(error);
		}
	}

	public void Refresh()
	{
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_021f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0224: Unknown result type (might be due to invalid IL or missing references)
		//IL_0256: Unknown result type (might be due to invalid IL or missing references)
		if (refreshing)
		{
			return;
		}
		refreshing = true;
		try
		{
			ulong num = ReadLobby();
			if (num == 0L)
			{
				bool flag = locked || coopRaidEntered;
				ResetLocalState();
				SetStatus(flag ? "LOBBY_LEFT: stale raid references cleared" : "SOLO / not in a lobby");
				return;
			}
			if (!plugin.Settings.CoopEnabled)
			{
				WithdrawReadiness();
				SetStatus("BLOCKED: CoopEnabled=false");
				return;
			}
			ProbeSteam(force: true);
			ulong num2 = Convert.ToUInt64(Reflect.Read(Reflect.Read(Reflect.Read(service, "_steamworks"), "LocalUser") ?? throw new InvalidDataException("Steam user unavailable"), "SteamId"), (IFormatProvider)(object)CultureInfo.InvariantCulture);
			ulong num3 = steam.Owner(num);
			if (num2 == 0L || num3 == 0L)
			{
				throw new InvalidDataException("Lobby owner/member is unavailable");
			}
			Guid val;
			if (lobby != num)
			{
				ResetLocalState();
				lobby = num;
				local = num2;
				owner = (pinnedOwner = num3);
				if (local == owner)
				{
					val = Guid.NewGuid();
					epoch = val.ToString("N");
					PublishHeader();
				}
			}
			owner = num3;
			local = num2;
			Role = ((owner == local) ? "Host" : "Client");
			nativeRole = Reflect.Read(service, "Role")?.ToString() ?? "Unknown";
			bool flag2 = safeLobbyScene && !nativeRaidActive && !nativeRaidConnecting && nativeRole != Role;
			if ((owner != pinnedOwner || flag2) && !SynchronizeLobbyOwner(owner, local))
			{
				return;
			}
			if (rotateEpochPending && !nativeRaidActive && !nativeRaidConnecting && safeLobbyScene && owner == local)
			{
				rotateEpochPending = false;
				val = Guid.NewGuid();
				epoch = val.ToString("N");
				PublishHeader();
				if (!steam.SetJoinable(lobby, value: true))
				{
					throw new InvalidOperationException("Cannot unlock completed-session lobby");
				}
			}
			nativeRole = Reflect.Read(service, "Role")?.ToString() ?? "Unknown";
			if (steam.Get(lobby, "frontier_owner") != owner.ToString((IFormatProvider)(object)CultureInfo.InvariantCulture))
			{
				WithdrawReadiness();
				ApprovedMembers = 0;
				SetStatus("WAIT: owner-bound lobby header is not ready");
				return;
			}
			string text = steam.Get(lobby, "frontier_epoch");
			string text2 = CoopProtocol.HeaderError(steam.Get(lobby, "frontier_protocol"), steam.Get(lobby, "frontier_signature"), text, Signature);
			if (text2 != null)
			{
				WithdrawReadiness();
				ApprovedMembers = 0;
				SetStatus("BLOCKED: " + text2);
				return;
			}
			if ((roster.IsFrozen || coopRaidEntered) && epoch != text)
			{
				WithdrawReadiness();
				SetStatus("BLOCKED: epoch changed during deployment");
				return;
			}
			if (owner == local && epoch != text)
			{
				WithdrawReadiness();
				SetStatus("WAIT: new host epoch has not become visible yet");
				return;
			}
			if (owner != local)
			{
				epoch = text;
			}
			if (owner == local && advertisedCatalog != Catalog)
			{
				steam.Set(lobby, "frontier_catalog", Catalog);
				advertisedCatalog = Catalog;
			}
			string text3 = steam.Get(lobby, "frontier_catalog");
			if (!CoopProtocol.IsHex(Catalog, 64) || text3 != Catalog)
			{
				WithdrawReadiness();
				ApprovedMembers = 0;
				SetStatus("WAIT: all 9 matching catalogs must finish loading on host and client");
				return;
			}
			expectedToken = CoopProtocol.ReadyToken(Signature, epoch, Catalog);
			if (sentToken != expectedToken)
			{
				steam.Ready(lobby, expectedToken);
				sentToken = expectedToken;
			}
			members = steam.Members(lobby);
			ReadinessResult readinessResult = roster.Check(owner, local, epoch, expectedToken, members);
			ApprovedMembers = readinessResult.ReadyCount;
			if (readinessResult.Ready)
			{
				LastError = "";
			}
			SetStatus((locked ? "DEPLOYMENT LOCKED | " : "") + readinessResult.Reason);
		}
		finally
		{
			refreshing = false;
		}
	}

	private void PublishHeader()
	{
		steam.Set(lobby, "frontier_owner", owner.ToString((IFormatProvider)(object)CultureInfo.InvariantCulture));
		steam.Set(lobby, "frontier_protocol", "1");
		steam.Set(lobby, "frontier_signature", Signature);
		steam.Set(lobby, "frontier_epoch", epoch);
		steam.Set(lobby, "frontier_catalog", Catalog);
		advertisedCatalog = Catalog;
		steam.Set(lobby, "frontier_phase", "lobby");
	}

	public bool IsCompatible(object lobbyOrInfo)
	{
		if (!plugin.Active)
		{
			return true;
		}
		if (!plugin.Settings.CoopEnabled)
		{
			return false;
		}
		object obj = ((lobbyOrInfo.GetType().FullName == "Game.CoopSystem.Data.LobbyInfo") ? Reflect.Read(lobbyOrInfo, "Id") : lobbyOrInfo);
		ulong num = ((obj == null) ? 0 : LobbyId(obj));
		if (num == 0L)
		{
			return false;
		}
		ProbeSteam(force: true);
		string text = CoopProtocol.HeaderError(steam.Get(num, "frontier_protocol"), steam.Get(num, "frontier_signature"), steam.Get(num, "frontier_epoch"), Signature);
		if (text != null)
		{
			long timestamp = Stopwatch.GetTimestamp();
			long num2 = default(long);
			if (!dataRequests.TryGetValue(num, out num2) || timestamp - num2 > Stopwatch.Frequency * 2)
			{
				if (dataRequests.Count > 128)
				{
					dataRequests.Clear();
				}
				dataRequests[num] = timestamp;
				steam.Request(num);
			}
			LastError = text;
			return false;
		}
		return true;
	}

	public void RejectJoin(object callback, string reason)
	{
		object obj = ((MethodBase)Enumerable.Single<MethodInfo>((System.Collections.Generic.IEnumerable<MethodInfo>)Reflect.FindType("Game.CoopSystem.Data.LobbyJoinResult").GetMethods((BindingFlags)60), (Func<MethodInfo, bool>)((MethodInfo m) => ((MemberInfo)m).Name == "Failed" && ((MethodBase)m).IsStatic && ((MethodBase)m).GetParameters().Length == 1 && ((MethodBase)m).GetParameters()[0].ParameterType == typeof(string)))).Invoke((object)null, new object[1] { "Frontier: " + reason });
		((MethodBase)Reflect.UniqueMethod(callback.GetType(), "Invoke", 1)).Invoke(callback, new object[1] { obj });
		SetStatus("JOIN BLOCKED: " + reason);
	}

	private bool ReadyNow()
	{
		Refresh();
		if (lobby == 0L || owner != pinnedOwner || !plugin.Settings.CoopEnabled || expectedToken.Length == 0)
		{
			return false;
		}
		return roster.Check(owner, local, epoch, expectedToken, members).Ready;
	}

	public bool CanPrepare()
	{
		if (!InLobby)
		{
			return !coopRaidEntered;
		}
		return ReadyNow();
	}

	public bool BeginDeployment(out bool createdHostLock)
	{
		createdHostLock = false;
		DeploymentChecks++;
		if (!InLobby)
		{
			return !coopRaidEntered;
		}
		if (!ReadyNow())
		{
			return false;
		}
		object obj = ((MethodBase)Reflect.UniqueMethod(service.GetType(), "CanStartSession", 0)).Invoke(service, (object[])null);
		if (!(obj is bool) || !(bool)obj)
		{
			return false;
		}
		if (owner != local)
		{
			return steam.Get(lobby, "frontier_phase") == "locked";
		}
		if (locked)
		{
			return roster.IsFrozen;
		}
		sessionIdBeforeStart = Reflect.Read(service, "_currentSessionId")?.ToString() ?? "";
		if (!steam.SetJoinable(lobby, value: false))
		{
			SetStatus("BLOCKED: Steam could not lock the lobby");
			return false;
		}
		locked = true;
		try
		{
			Refresh();
			if (expectedToken.Length == 0 || owner != pinnedOwner)
			{
				RollbackDeployment();
				return false;
			}
			ReadinessResult readinessResult = roster.Freeze(owner, local, epoch, expectedToken, members);
			if (!readinessResult.Ready)
			{
				RollbackDeployment();
				SetStatus(readinessResult.Reason);
				return false;
			}
			steam.Set(lobby, "frontier_phase", "locked");
			SetStatus("DEPLOYMENT LOCKED | READY " + members.Length + "/" + members.Length);
			createdHostLock = true;
			return true;
		}
		catch
		{
			RollbackDeployment();
			throw;
		}
	}

	public void NativeStartCompleted(bool createdHostLock)
	{
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		if (createdHostLock)
		{
			string text = Reflect.Read(service, "_currentSessionId")?.ToString() ?? "";
			Guid val = default(Guid);
			if (!Guid.TryParse(text, out val) || val == Guid.Empty || text == sessionIdBeforeStart)
			{
				RollbackDeployment();
				SetStatus("Native start did not create a session; lobby unlocked");
			}
			else
			{
				coopRaidEntered = true;
			}
		}
	}

	public bool CanPublishSessionReady()
	{
		if (InLobby || coopRaidEntered)
		{
			if (ReadyNow())
			{
				if (owner == local)
				{
					if (locked)
					{
						return roster.IsFrozen;
					}
					return false;
				}
				return true;
			}
			return false;
		}
		return true;
	}

	public void RollbackDeployment()
	{
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		if (lobby != 0L && locked && steam != null && steam.Owner(lobby) == local)
		{
			if (!steam.SetJoinable(lobby, value: true))
			{
				throw new InvalidOperationException("Could not unlock lobby; leave and recreate it");
			}
			steam.Set(lobby, "frontier_phase", "lobby");
		}
		locked = false;
		roster.Clear();
	}

	public void SessionCleared()
	{
		if (plugin.Active && !synchronizingRole)
		{
			EndRaidLocal("NATIVE_SESSION_CLEARED");
		}
	}

	public bool CanAdmitPlayer(object manager)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		AdmissionChecks++;
		object obj = Reflect.Read(Reflect.Read(manager, "_battleSession") ?? throw new InvalidDataException("Missing battle session"), "IsAuthoritative");
		if (!(obj is bool) || !(bool)obj)
		{
			return false;
		}
		if (!InLobby)
		{
			return !coopRaidEntered;
		}
		if (ReadyNow() && local == owner && locked)
		{
			return roster.IsFrozen;
		}
		return false;
	}

	private static bool IsLoopback(object address)
	{
		string text = Reflect.Read(address, "IpAddress")?.ToString() ?? "";
		if (!string.Equals(text, "localhost", (StringComparison)5))
		{
			IPAddress val = default(IPAddress);
			if (IPAddress.TryParse(text, out val))
			{
				return IPAddress.IsLoopback(val);
			}
			return false;
		}
		return true;
	}

	public bool CanConnect(object connection, object address)
	{
		ConnectionChecks++;
		object obj = Reflect.Read(address, "IsSteam");
		bool flag = obj is bool && (bool)obj;
		bool loopback = !flag && IsLoopback(address);
		if (ConnectionPolicy.AllowLocalShelter(IsObservedShelterConnection(connection), flag, loopback, CoopProtocol.IsHex(Catalog, 64)))
		{
			ShelterConnectionsAllowed++;
			LastConnectionDecision = "ALLOW_LOCAL_SHELTER: native path + loopback + verified catalog";
			((BasePlugin)plugin).Log.LogInfo((object)("Co-op connection: " + LastConnectionDecision));
			plugin.WriteReport();
			return true;
		}
		LastConnectionDecision = "EVALUATING_RAID";
		if (InLobby)
		{
			if (!ReadyNow())
			{
				return false;
			}
			if (!ConnectionPolicy.DeploymentReady(owner == local, locked, roster.IsFrozen, steam.Get(lobby, "frontier_phase")))
			{
				LastConnectionDecision = "WAIT_RAID_DEPLOYMENT_LOCK";
				SetStatus("WAIT: host has not locked deployment");
				return false;
			}
			obj = Reflect.Read(address, "IsSteam");
			if (obj is bool && (bool)obj)
			{
				if (Convert.ToUInt64(Reflect.Read(address, "SteamId"), (IFormatProvider)(object)CultureInfo.InvariantCulture) != owner)
				{
					SetStatus("BLOCKED: Steam destination is not the verified lobby owner");
					return false;
				}
			}
			else if (local != owner || !IsLoopback(address))
			{
				SetStatus("BLOCKED: only the host's built-in loopback connection is allowed without Steam");
				return false;
			}
			coopRaidEntered = true;
			LastConnectionDecision = "ALLOW_VERIFIED_RAID";
			return true;
		}
		if (!coopRaidEntered)
		{
			obj = Reflect.Read(address, "IsSteam");
			if (!(obj is bool) || !(bool)obj)
			{
				bool num = IsLoopback(address);
				if (!num)
				{
					SetStatus("BLOCKED: external IP/dedicated servers are not supported by this co-op adapter");
				}
				return num;
			}
		}
		SetStatus("BLOCKED: a verified Frontier lobby is required");
		return false;
	}

	public void ClearDeferredStart()
	{
		pendingStart = null;
	}

	public void ClearDeferredConnection(object connection)
	{
		if ((object)pendingConnect != null && (pendingConnect.Target == connection || Reflect.SameNative(pendingConnect.Target, connection)))
		{
			pendingConnect = null;
		}
	}

	public void DeferClientStart(object target, string map)
	{
		Defer(ref pendingStart, target, "StartSession", new object[1] { map });
	}

	public void DeferClientConnection(object target, object address, bool reconnect)
	{
		object obj = Reflect.Read(address, "IsSteam");
		if (obj is bool && (bool)obj && Convert.ToUInt64(Reflect.Read(address, "SteamId"), (IFormatProvider)(object)CultureInfo.InvariantCulture) == owner)
		{
			Defer(ref pendingConnect, target, reconnect ? "Reconnect" : "Connect", reconnect ? System.Array.Empty<object>() : new object[1] { address });
		}
	}

	private void Defer(ref PendingCall? slot, object target, string method, object[] arguments)
	{
		if (!replaying && plugin.Active && plugin.Settings.CoopEnabled && lobby != 0L && owner != 0L && owner != local && owner == pinnedOwner && CoopProtocol.IsHex(epoch, 32) && NativeAlive(target))
		{
			long expires = ((slot != null && slot.Generation == lifecycleGeneration) ? slot.Expires : (Stopwatch.GetTimestamp() + 60 * Stopwatch.Frequency));
			slot = new PendingCall(target, method, arguments, lobby, epoch, expires, lifecycleGeneration);
			SetStatus("WAIT: deferred client " + method + " until matching lobby data arrives (60 s maximum)");
		}
	}

	private void RetryDeferredClientCalls()
	{
		if (replaying || ((object)pendingStart == null && (object)pendingConnect == null))
		{
			return;
		}
		Expire(ref pendingStart);
		Expire(ref pendingConnect);
		if (((object)pendingStart == null && (object)pendingConnect == null) || !ReadyNow() || owner == local || steam.Get(lobby, "frontier_phase") != "locked")
		{
			return;
		}
		replaying = true;
		try
		{
			if ((object)pendingStart != null)
			{
				object obj = ((MethodBase)Reflect.UniqueMethod(service.GetType(), "CanStartSession", 0)).Invoke(service, (object[])null);
				if (obj is bool && (bool)obj)
				{
					PendingCall pendingCall = pendingStart;
					pendingStart = null;
					((MethodBase)Reflect.UniqueMethod(pendingCall.Target.GetType(), pendingCall.Method, pendingCall.Arguments.Length)).Invoke(pendingCall.Target, pendingCall.Arguments);
				}
			}
			if ((object)pendingConnect != null)
			{
				PendingCall pendingCall2 = pendingConnect;
				pendingConnect = null;
				((MethodBase)Reflect.UniqueMethod(pendingCall2.Target.GetType(), pendingCall2.Method, pendingCall2.Arguments.Length)).Invoke(pendingCall2.Target, pendingCall2.Arguments);
			}
		}
		finally
		{
			replaying = false;
		}
		[CompilerGenerated]
		void Expire(ref PendingCall? pending)
		{
			if ((object)pending != null && (pending.Generation != lifecycleGeneration || !NativeAlive(pending.Target) || pending.Lobby != lobby || pending.Epoch != epoch || Stopwatch.GetTimestamp() >= pending.Expires))
			{
				SetStatus("Deferred " + pending.Method + " canceled or timed out; return to the lobby and retry");
				pending = null;
				canceledDeferredCalls++;
			}
		}
	}
}
