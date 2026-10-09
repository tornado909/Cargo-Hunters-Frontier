using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Frontier.Engine;
using Frontier.Modular;
using HarmonyLib;

namespace Frontier.Core;

internal static class ElitePresentation
{
	internal sealed record RankStyle(string Id, string Name, string Hex)
	{
		[CompilerGenerated]
		private System.Type EqualityContract
		{
			[CompilerGenerated]
			get
			{
				return typeof(RankStyle);
			}
		}

		[CompilerGenerated]
		public override string ToString()
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Expected O, but got Unknown
			StringBuilder val = new StringBuilder();
			val.Append("RankStyle");
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
			builder.Append("Id = ");
			builder.Append((object)Id);
			builder.Append(", Name = ");
			builder.Append((object)Name);
			builder.Append(", Hex = ");
			builder.Append((object)Hex);
			return true;
		}

		[CompilerGenerated]
		public bool Equals(RankStyle? other)
		{
			if ((object)this != other)
			{
				if ((object)other != null && EqualityContract == other.EqualityContract && EqualityComparer<string>.Default.Equals(Id, other.Id) && EqualityComparer<string>.Default.Equals(Name, other.Name))
				{
					return EqualityComparer<string>.Default.Equals(Hex, other.Hex);
				}
				return false;
			}
			return true;
		}
	}

	internal const string Adapter = "rank-visuals-v3.1-rank-bound";

	private static readonly Dictionary<string, RankStyle> Styles = new Dictionary<string, RankStyle>((IEqualityComparer<string>)(object)StringComparer.Ordinal);

	private static readonly Dictionary<string, string> ByPreset = new Dictionary<string, string>((IEqualityComparer<string>)(object)StringComparer.OrdinalIgnoreCase);

	private static readonly Dictionary<string, string> LocalRanks = new Dictionary<string, string>((IEqualityComparer<string>)(object)StringComparer.Ordinal);

	private static readonly Queue<string> RankOrder = new Queue<string>();

	private static Dictionary<string, string> remoteRanks = new Dictionary<string, string>((IEqualityComparer<string>)(object)StringComparer.Ordinal);

	private static readonly HashSet<string> warnings = new HashSet<string>((IEqualityComparer<string>)(object)StringComparer.Ordinal);

	private static object? authoritativeSession;

	private static int mainThread;

	private static int revision;

	private static int sentRevision = -1;

	private static int registeredEquipment;

	private static int overflow;

	private static ulong lastLobby;

	private static ulong lastOwner;

	private static string lastEpoch = "";

	private static string lastHeader = "";

	private static string transport = "LOCAL_ONLY";

	private static string lastError = "";

	private static bool installed;

	private static float nextNetwork;

	private static readonly List<string> contracts = new List<string>();

	internal static int RankedEntities => registeredEquipment;

	internal static int VisualsTinted => LabelsSnapshot?.MaterialsConfigured ?? 0;

	private static EliteWorldDiagnostic? LabelsSnapshot => EliteWorldEffects.Stats;

	internal static object Snapshot => TrackerRepair.DecorateSnapshot(new
	{
		adapter = "rank-visuals-v3.1-rank-bound",
		hookInstalled = installed,
		authoritativeEquipment = LocalRanks.Count,
		remoteEquipment = remoteRanks.Count,
		rankedEntities = RankedEntities,
		visualsTinted = VisualsTinted,
		overflow = overflow,
		overlay = EliteWorldEffects.Stats,
		transport = transport,
		lastError = lastError,
		contracts = contracts.ToArray(),
		nativePlaytestVerified = false
	});

	internal static void Install(Harmony harmony, string packPath)
	{
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		if (!ModuleRuntime.Feature("elite-visuals"))
		{
			return;
		}
		mainThread = Environment.CurrentManagedThreadId;
		if (File.Exists(Path.Combine(Paths.BepInExRootPath, "Frontier-disable-visuals.flag")))
		{
			lastError = "Disabled by local Frontier-disable-visuals.flag";
			((BasePlugin)Plugin.Current).Log.LogWarning((object)"Elite presentation adapter: rank-visuals-v3.1-rank-bound DISABLED by local recovery flag");
			return;
		}
		try
		{
			System.Collections.Generic.IEnumerator<KeyValuePair<string, JsonNode>> enumerator = ((JsonNode)PatchEngine.ReadObject(Path.Combine(packPath, "elite-presentation.json")))["ranks"].AsObject().GetEnumerator();
			try
			{
				while (((System.Collections.IEnumerator)enumerator).MoveNext())
				{
					KeyValuePair<string, JsonNode> current = enumerator.Current;
					if (!EliteVisualProtocol.ValidRank(current.Key))
					{
						throw new InvalidDataException("Unknown elite presentation rank");
					}
					JsonObject val = current.Value.AsObject();
					Styles.Add(current.Key, new RankStyle(current.Key, ((JsonNode)val)["nameRu"].GetValue<string>(), ((JsonNode)val)["color"].GetValue<string>()));
				}
			}
			finally
			{
				((System.IDisposable)enumerator)?.Dispose();
			}
			enumerator = ((JsonNode)PatchEngine.ReadObject(Path.Combine(packPath, "elites.json")))["variants"].AsObject().GetEnumerator();
			try
			{
				while (((System.Collections.IEnumerator)enumerator).MoveNext())
				{
					System.Collections.Generic.IEnumerator<KeyValuePair<string, JsonNode>> enumerator2 = enumerator.Current.Value.AsObject().GetEnumerator();
					try
					{
						while (((System.Collections.IEnumerator)enumerator2).MoveNext())
						{
							KeyValuePair<string, JsonNode> current2 = enumerator2.Current;
							if (!Styles.ContainsKey(current2.Key))
							{
								throw new InvalidDataException("Rank style absent");
							}
							ByPreset.Add(current2.Value.GetValue<string>(), current2.Key);
						}
					}
					finally
					{
						((System.IDisposable)enumerator2)?.Dispose();
					}
				}
			}
			finally
			{
				((System.IDisposable)enumerator)?.Dispose();
			}
			EliteClientTracker.Install(harmony);
			contracts.Add("Client CharacterSpawnHandlerSystem.InitializeDependency/OnDestroy reference-only observers");
			installed = true;
			((BasePlugin)Plugin.Current).Log.LogInfo((object)"Elite presentation adapter: rank-visuals-v3.1-rank-bound | client-only world-space text/aura; no body material changes");
		}
		catch (System.Exception e)
		{
			Warn("Initialization", e);
		}
	}

	internal static void ObserveEquipment(object spawner, string presetId, object equipment)
	{
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		if (!installed || !Plugin.Current.Active || Environment.CurrentManagedThreadId != mainThread)
		{
			return;
		}
		try
		{
			string text = default(string);
			if (!ByPreset.TryGetValue(presetId, ref text))
			{
				return;
			}
			object obj = Reflect.Read(spawner, "_battleSession");
			if (obj == null)
			{
				return;
			}
			object obj2 = Reflect.Read(obj, "IsAuthoritative");
			if (!(obj2 is bool) || !(bool)obj2)
			{
				return;
			}
			if (!Reflect.SameNative(obj, authoritativeSession))
			{
				authoritativeSession = obj;
				LocalRanks.Clear();
				RankOrder.Clear();
				registeredEquipment = 0;
				revision++;
				sentRevision = -1;
			}
			string text2 = EliteVisualProtocol.EquipmentId(Reflect.Id(equipment));
			if (text2.Length == 0)
			{
				throw new InvalidDataException("Elite equipment has no valid ID");
			}
			string text3 = default(string);
			if (LocalRanks.TryGetValue(text2, ref text3) && text3 == text)
			{
				return;
			}
			if (!LocalRanks.ContainsKey(text2))
			{
				if (LocalRanks.Count >= 192 && RankOrder.Count > 0)
				{
					LocalRanks.Remove(RankOrder.Dequeue());
					overflow++;
				}
				RankOrder.Enqueue(text2);
			}
			LocalRanks[text2] = text;
			revision++;
			registeredEquipment++;
			((BasePlugin)Plugin.Current).Log.LogInfo((object)("Elite rank bound: " + text + " equipment=" + text2));
		}
		catch (System.Exception e)
		{
			Warn("Rank binding", e);
		}
	}

	internal static RankStyle? FindRank(string equipmentId)
	{
		string text = default(string);
		RankStyle result = default(RankStyle);
		if (!((Plugin.Current.Coop?.Role == "Client") ? remoteRanks : LocalRanks).TryGetValue(equipmentId, ref text) || !Styles.TryGetValue(text, ref result))
		{
			return null;
		}
		return result;
	}

	internal static void TickCosmetics(float now)
	{
		//IL_0202: Unknown result type (might be due to invalid IL or missing references)
		//IL_0207: Unknown result type (might be due to invalid IL or missing references)
		if (!installed || !Plugin.Current.Active || now < nextNetwork || Environment.CurrentManagedThreadId != mainThread)
		{
			return;
		}
		nextNetwork = now + 1f;
		try
		{
			CoopController coop = Plugin.Current.Coop;
			ulong lobby = Convert.ToUInt64(Reflect.Read(coop, "lobby"));
			ulong num = Convert.ToUInt64(Reflect.Read(coop, "local"));
			ulong num2 = Convert.ToUInt64(Reflect.Read(coop, "owner"));
			string text = (Reflect.Read(coop, "epoch") as string) ?? "";
			if (lobby != lastLobby || num2 != lastOwner || text != lastEpoch)
			{
				remoteRanks.Clear();
				lastHeader = "";
				sentRevision = -1;
				lastLobby = lobby;
				lastOwner = num2;
				lastEpoch = text;
			}
			if (lobby == 0L || num2 == 0L || num == 0L)
			{
				transport = "LOCAL_ONLY";
				return;
			}
			if (num2 == Convert.ToUInt64(Reflect.Read(coop, "pinnedOwner")))
			{
				object obj = Reflect.Read(coop, "steam");
				SteamLobbyApi api = obj as SteamLobbyApi;
				if (api != null && CoopProtocol.IsHex(text, 32) && coop.Catalog.Length == 64 && api.Owner(lobby) == num2 && !(api.Get(lobby, "frontier_signature") != coop.Signature) && !(api.Get(lobby, "frontier_epoch") != text) && !(api.Get(lobby, "frontier_catalog") != coop.Catalog))
				{
					if (num == num2)
					{
						if (sentRevision != revision)
						{
							Guid val = Guid.NewGuid();
							EliteVisualProtocol.Packet packet = EliteVisualProtocol.Encode(text, ((Guid)(ref val)).ToString("N"), (IReadOnlyDictionary<string, string>)(object)LocalRanks);
							for (int i = 0; i < packet.Pages.Length; i++)
							{
								api.Set(lobby, "frontier_visual_v2_p" + i, packet.Pages[i]);
							}
							api.Set(lobby, "frontier_visual_v2", packet.Header);
							sentRevision = revision;
						}
						transport = "HOST_MAP_PUBLISHED";
						return;
					}
					string text2 = api.Get(lobby, "frontier_visual_v2");
					if (!EliteVisualProtocol.TryReadHeader(text2, text, out string _, out int count))
					{
						remoteRanks.Clear();
						lastHeader = "";
						transport = "WAIT_HOST_RANKS";
						return;
					}
					if (text2 == lastHeader)
					{
						transport = "CLIENT_MAP_VERIFIED";
						return;
					}
					string[] pages = Enumerable.ToArray<string>(Enumerable.Select<int, string>(Enumerable.Range(0, count), (Func<int, string>)delegate(int num4)
					{
						SteamLobbyApi steamLobbyApi = api;
						ulong lobby2 = lobby;
						int num3 = num4;
						return steamLobbyApi.Get(lobby2, "frontier_visual_v2_p" + num3);
					}));
					if (api.Owner(lobby) != num2 || api.Get(lobby, "frontier_visual_v2") != text2 || !EliteVisualProtocol.TryDecode(text2, text, pages, out Dictionary<string, string> entries))
					{
						transport = "WAIT_COMPLETE_COSMETIC_MAP";
						return;
					}
					remoteRanks = entries;
					lastHeader = text2;
					transport = "CLIENT_MAP_VERIFIED";
					((BasePlugin)Plugin.Current).Log.LogInfo((object)("Elite cosmetic map received: " + entries.Count + " equipment IDs"));
					return;
				}
			}
			remoteRanks.Clear();
			transport = "WAIT_COMPATIBLE_OWNER";
		}
		catch (System.Exception e)
		{
			remoteRanks.Clear();
			lastHeader = "";
			transport = "COSMETIC_TRANSPORT_ERROR";
			Warn("Cosmetic metadata", e);
		}
	}

	internal static void Warn(string stage, System.Exception e)
	{
		string text = (lastError = stage + ": " + e.GetBaseException().Message);
		if (warnings.Count < 12 && warnings.Add(text))
		{
			((BasePlugin)Plugin.Current).Log.LogWarning((object)("Elite visuals only: " + text));
		}
	}
}
