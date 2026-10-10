using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Game.Application.Client;
using Game.CoopSystem;
using Game.GameModes;
using Il2CppInterop.Runtime.InteropTypes;
using SteamNetworkSocket;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;

namespace Frontier.Survivor;

internal static class SurvivorCoopTransport
{
	private sealed record PartyIdentity(int Version, string Session, string Map, int PlayerId, string Equipment, string Character, string Name);

	private const string ChoiceKey = "frontier_survivor_choice_v1";

	private const string StatePrefix = "frontier_survivor_state_v1_";

	private const string IdentityKey = "frontier_party_identity_v107";

	private static ClientBattleState? battle;

	private static CoopService? service;

	private static NativeRoot? serviceRoot;

	private static object? steam;

	private static Type? api;

	private static ulong lobby;

	private static ulong local;

	private static ulong owner;

	private static string session = "";

	private static double next;

	private static double receivedAt;

	private static long sequence;

	private static long choiceSequence;

	private static long receivedSequence;

	private static BonusChoice? pending;

	private static readonly Dictionary<ulong, long> accepted = new Dictionary<ulong, long>();

	private static readonly HashSet<ulong> published = new HashSet<ulong>();

	internal static bool IsCoop => lobby != 0;

	internal static bool Fresh
	{
		get
		{
			if (receivedSequence > 0)
			{
				return Time.realtimeSinceStartupAsDouble - receivedAt < 10.0;
			}
			return false;
		}
	}

	internal static bool Pending => pending != null;

	internal static void Capture(ClientBattleState state)
	{
		ClientBattleState? clientBattleState = battle;
		if (!(((clientBattleState != null) ? new IntPtr?(((Il2CppObjectBase)clientBattleState).Pointer) : ((IntPtr?)null)) == ((Il2CppObjectBase)state).Pointer))
		{
			battle = state;
		}
	}

	internal static void Release(ClientBattleState state)
	{
		ClientBattleState? clientBattleState = battle;
		if (!(((clientBattleState != null) ? new IntPtr?(((Il2CppObjectBase)clientBattleState).Pointer) : ((IntPtr?)null)) != ((Il2CppObjectBase)state).Pointer))
		{
			battle = null;
			if (!SurvivorRun.ServerWorldBound)
			{
				Clear();
			}
		}
	}

	internal static void Clear()
	{
		try
		{
			if (steam != null && lobby != 0L)
			{
				Write("frontier_survivor_choice_v1", "");
				foreach (ulong item in published)
				{
					Write("frontier_survivor_state_v1_" + item.ToString(CultureInfo.InvariantCulture), "");
				}
			}
		}
		catch (Exception e)
		{
			Trace.Error("survivor metadata teardown", e);
		}
		serviceRoot?.Dispose();
		serviceRoot = null;
		service = null;
		lobby = (local = (owner = 0uL));
		session = "";
		next = (receivedAt = 0.0);
		sequence = (choiceSequence = (receivedSequence = 0L));
		pending = null;
		accepted.Clear();
		published.Clear();
	}

	private static object? Call(string name, params object[] args)
	{
		return api.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(steam, args);
	}

	private static object? Delegate(string name, params object[] args)
	{
		return ((Delegate)api.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(steam)).DynamicInvoke(args);
	}

	private static object Self()
	{
		return Call("Interface");
	}

	private static string Read(ulong member, string key)
	{
		IntPtr intPtr = (IntPtr)Delegate("getMemberData", Self(), lobby, member, key);
		object obj;
		if (!(intPtr == IntPtr.Zero))
		{
			obj = Marshal.PtrToStringUTF8(intPtr);
			if (obj == null)
			{
				return "";
			}
		}
		else
		{
			obj = "";
		}
		return (string)obj;
	}

	private static void Write(string key, string text)
	{
		if (Encoding.UTF8.GetByteCount(text) > 1000)
		{
			throw new ArgumentException("Survivor member data bound");
		}
		Delegate("setMemberData", Self(), lobby, key, text);
	}

	private static ulong[] Members()
	{
		object obj = Self();
		int num = (int)Delegate("count", obj, lobby);
		if (num < 1 || num > 16)
		{
			return Array.Empty<ulong>();
		}
		ulong[] array = new ulong[num];
		for (int i = 0; i < num; i++)
		{
			array[i] = (ulong)Delegate("memberAt", obj, lobby, i);
		}
		if (num != (int)Delegate("count", obj, lobby) || array.Distinct().Count() != num)
		{
			return Array.Empty<ulong>();
		}
		return array;
	}

	private static bool Context()
	{
		if (battle == null && !SurvivorRun.ServerWorldBound)
		{
			return false;
		}
		if (battle != null && !Identity.Own(battle._mapName))
		{
			return false;
		}
		string text = ((battle != null) ? battle._sessionId.ToString() : session);
		if (!SurvivorCoopProtocol.Id(text))
		{
			return false;
		}
		if (service == null)
		{
			ClientBattleState? clientBattleState = battle;
			object obj;
			if (clientBattleState == null)
			{
				obj = null;
			}
			else
			{
				ICoopService coopService = clientBattleState._coopService;
				obj = ((coopService != null) ? ((Il2CppObjectBase)coopService).TryCast<CoopService>() : null);
			}
			service = (CoopService?)obj;
			if (service != null)
			{
				serviceRoot = new NativeRoot((Il2CppObjectBase)(object)service);
			}
		}
		if (service == null || !service.IsInLobby)
		{
			if (lobby != 0L)
			{
				SurvivorRun.Fail("Steam lobby was lost during Survivor; live migration is unsupported");
				return false;
			}
			return false;
		}
		ulong value = service._currentLobby.Value;
		ulong id = service.GetLocalMember().Id;
		if (value == 0L || id == 0L)
		{
			return false;
		}
		if (steam == null)
		{
			api = (from a in AppDomain.CurrentDomain.GetAssemblies()
				select a.GetType("Frontier.Core.SteamLobbyApi")).FirstOrDefault((Type t) => t != null) ?? throw new MissingMemberException("Existing Frontier SteamLobbyApi unavailable");
			steam = Activator.CreateInstance(api, nonPublic: true);
		}
		ulong num = (ulong)Call("Owner", value);
		if (lobby == 0L)
		{
			lobby = value;
			local = id;
			owner = num;
			session = text;
		}
		if (value != lobby || id != local || num != owner || text != session)
		{
			SurvivorRun.Fail("Survivor lobby/session/owner changed; start a new raid through Frontier");
			return false;
		}
		return num != 0;
	}

	private static RunHeader Header()
	{
		return new RunHeader(1, session, "Frontier_Survivor", Identity.Hash, SurvivorRun.RunId, sequence);
	}

	private static PartyIdentity? Resolve(ulong member, BattleSession authority)
	{
		PartyIdentity partyIdentity = SurvivorCoopProtocol.Decode<PartyIdentity>(Read(member, "frontier_party_identity_v107"));
		if (partyIdentity == null || partyIdentity.Version != 1 || partyIdentity.Session != session || partyIdentity.Map != "Frontier_Survivor" || partyIdentity.PlayerId < 0 || authority._players == null || !authority._players.ContainsKey(partyIdentity.PlayerId) || authority.IsBotPlayer(partyIdentity.PlayerId) || !authority.GetPlayerAccountId(partyIdentity.PlayerId).HasValue)
		{
			return null;
		}
		BattleSession.Player player = authority.GetPlayer(partyIdentity.PlayerId);
		if (player == null || player.Equipment == null || player.Equipment.Id.ToString() != partyIdentity.Equipment || player.CharacterItemId.ToString() != partyIdentity.Character || !player.AccountId.HasValue)
		{
			return null;
		}
		ulong nativeSteamId = 0uL;
		if (member != local)
		{
			World authorityWorld = SurvivorRun.AuthorityWorld;
			EntityManager entityManager = authority.EntityManager;
			if (authorityWorld == null || player.Connection == Entity.Null || !entityManager.Exists(player.Connection) || !entityManager.HasComponent<NetworkStreamConnection>(player.Connection))
			{
				return null;
			}
			NetworkStreamReceiveSystem existingSystem = authorityWorld.GetExistingSystem<NetworkStreamReceiveSystem>();
			if (existingSystem == null || !existingSystem.Driver.IsCreated)
			{
				return null;
			}
			existingSystem.LastDriverWriter.Complete();
			NetworkConnection value = entityManager.GetComponentData<NetworkStreamConnection>(player.Connection).Value;
			if (!value.IsCreated || existingSystem.Driver.GetConnectionState(value) != NetworkConnection.State.Connected)
			{
				return null;
			}
			nativeSteamId = SteamNetworkEndpointCodec.InterfaceEndpointToSteamId(existingSystem.Driver.GetConnection(value).Address);
		}
		if (!SurvivorIdentityPolicy.Bound(member, local, partyIdentity.PlayerId, SurvivorRun.LocalPlayerId, nativeSteamId))
		{
			Trace.Data["survivorIdentityBindingWaiting"] = new
			{
				member = member,
				player = partyIdentity.PlayerId,
				nativeSteamId = nativeSteamId
			};
			return null;
		}
		return partyIdentity;
	}

	internal static string[] Preflight()
	{
		MethodInfo method = typeof(SteamNetworkEndpointCodec).GetMethod("InterfaceEndpointToSteamId", new Type[1] { typeof(NetworkInterfaceEndPoint) });
		MethodInfo method2 = typeof(NetworkDriver).GetMethod("GetConnection", new Type[1] { typeof(NetworkConnection) });
		if (method == null || method.ReturnType != typeof(ulong) || method2 == null || typeof(NetworkStreamReceiveSystem).GetProperty("Driver") == null || typeof(NetworkStreamReceiveSystem).GetProperty("LastDriverWriter") == null || typeof(NetworkDriver.Connection).GetField("Address")?.FieldType != typeof(NetworkInterfaceEndPoint))
		{
			throw new MissingMemberException("Native Steam endpoint binding contract unavailable");
		}
		return new string[2]
		{
			method.ToString(),
			method2.ToString()
		};
	}

	internal static void Tick(BattleSession? authority, BattleSession? client)
	{
		double realtimeSinceStartupAsDouble = Time.realtimeSinceStartupAsDouble;
		if (realtimeSinceStartupAsDouble < next)
		{
			return;
		}
		next = realtimeSinceStartupAsDouble + 0.5;
		if (!Context() || (authority == null && client == null))
		{
			return;
		}
		ulong[] members = Members();
		if (!members.Contains(owner) || !members.Contains(local))
		{
			return;
		}
		if (authority != null)
		{
			if (owner != local)
			{
				SurvivorRun.Fail("Authoritative Survivor world does not belong to lobby owner");
				return;
			}
			if (++sequence > 1000000000)
			{
				SurvivorRun.Fail("Survivor sequence budget exhausted");
				return;
			}
			ulong[] array = members;
			for (int i = 0; i < array.Length; i++)
			{
				ulong num = array[i];
				PartyIdentity partyIdentity = Resolve(num, authority);
				if (!(partyIdentity == null))
				{
					RunProgress runProgress = SurvivorRun.ForPlayer(partyIdentity.PlayerId);
					BonusChoice c = SurvivorCoopProtocol.Decode<BonusChoice>(Read(num, "frontier_survivor_choice_v1"));
					long acceptedSequence = accepted.GetValueOrDefault(num);
					if (!authority.IsPlayerIsDead(partyIdentity.PlayerId) && SurvivorCoopProtocol.Apply(c, Header(), num, partyIdentity.PlayerId, runProgress, ref acceptedSequence))
					{
						accepted[num] = acceptedSequence;
						Trace.Add("survivorRemoteChoicesAccepted");
					}
					PersonalSnapshot value = new PersonalSnapshot(Header(), num, partyIdentity.PlayerId, partyIdentity.Equipment, partyIdentity.Character, SurvivorRun.Seconds, SurvivorRun.Alive, SurvivorRun.Ended, runProgress.Export());
					Write("frontier_survivor_state_v1_" + num.ToString(CultureInfo.InvariantCulture), SurvivorCoopProtocol.Encode(value));
					published.Add(num);
					Trace.Add("survivorSnapshotsPublished");
				}
			}
			array = published.Where((ulong m) => !members.Contains(m)).ToArray();
			for (int i = 0; i < array.Length; i++)
			{
				ulong num2 = array[i];
				Write("frontier_survivor_state_v1_" + num2.ToString(CultureInfo.InvariantCulture), "");
				published.Remove(num2);
				accepted.Remove(num2);
			}
			return;
		}
		PersonalSnapshot personalSnapshot = SurvivorCoopProtocol.Decode<PersonalSnapshot>(Read(owner, "frontier_survivor_state_v1_" + local.ToString(CultureInfo.InvariantCulture)));
		int localPlayerId = client.GetLocalPlayerId();
		BattleSession.Player player = client.GetPlayer(localPlayerId);
		if (!SurvivorCoopProtocol.Valid(personalSnapshot) || personalSnapshot.Header.Session != session || personalSnapshot.Header.Geometry != Identity.Hash || personalSnapshot.Member != local || personalSnapshot.PlayerId != localPlayerId || player?.Equipment == null || player.Equipment.Id.ToString() != personalSnapshot.Equipment || player.CharacterItemId.ToString() != personalSnapshot.Character)
		{
			return;
		}
		if (SurvivorRun.RunId.Length > 0 && personalSnapshot.Header.Run != SurvivorRun.RunId)
		{
			SurvivorRun.Fail("Host changed Survivor run identity during raid");
		}
		else if (personalSnapshot.Header.Sequence > receivedSequence)
		{
			receivedSequence = personalSnapshot.Header.Sequence;
			receivedAt = realtimeSinceStartupAsDouble;
			sequence = receivedSequence;
			SurvivorRun.Receive(personalSnapshot);
			Trace.Add("survivorSnapshotsReceived");
			if (pending != null && (personalSnapshot.Progress.OfferSerial != pending.OfferSerial || personalSnapshot.Progress.Pending == 0))
			{
				pending = null;
				Write("frontier_survivor_choice_v1", "");
			}
			if (pending != null)
			{
				Write("frontier_survivor_choice_v1", SurvivorCoopProtocol.Encode(pending with
				{
					Header = personalSnapshot.Header
				}));
			}
		}
	}

	internal static void Choose(int index, RunProgress progress)
	{
		if (Fresh && !(pending != null) && index >= 0 && index < progress.Offer.Length && progress.PendingChoices >= 1 && choiceSequence < 1000000000)
		{
			pending = new BonusChoice(Header(), local, SurvivorRun.LocalPlayerId, ++choiceSequence, progress.OfferSerial, index);
			Write("frontier_survivor_choice_v1", SurvivorCoopProtocol.Encode(pending));
			Trace.Add("survivorChoicesSent");
		}
	}
}
