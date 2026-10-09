using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
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

	private readonly object sync = new object();

	private System.Type? repositoryType;

	private System.Type? idType;

	private MethodInfo? getData;

	private MethodInfo? parseId;

	[field: CompilerGenerated]
	public int ClientCallsSkipped
	{
		[CompilerGenerated]
		get;
		[CompilerGenerated]
		private set;
	}

	[field: CompilerGenerated]
	public int AuthoritativeCalls
	{
		[CompilerGenerated]
		get;
		[CompilerGenerated]
		private set;
	}

	public EnemyRoller(JsonObject catalog, PackSettings settings)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Expected O, but got Unknown
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Expected O, but got Unknown
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Expected O, but got Unknown
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		spawnPoints = Enumerable.ToHashSet<string>(Enumerable.Select<JsonNode, string>((System.Collections.Generic.IEnumerable<JsonNode>)(JsonArray)((JsonNode)catalog)["allowedSpawnPointIds"], (Func<JsonNode, string>)((JsonNode n) => n.GetValue<string>())), (IEqualityComparer<string>)(object)StringComparer.OrdinalIgnoreCase);
		variants = (JsonObject)((JsonNode)catalog)["variants"];
		tiers = Enumerable.ToArray<string>(Enumerable.Select<JsonNode, string>((System.Collections.Generic.IEnumerable<JsonNode>)(JsonArray)((JsonNode)catalog)["tiers"], (Func<JsonNode, string>)((JsonNode n) => PatchEngine.Str(n, "id"))));
		if (tiers.Length != 4)
		{
			throw new InvalidDataException("Expected four elite tiers");
		}
		testMode = settings.RuntimeTestMode;
		weights = Enumerable.ToArray<int>((System.Collections.Generic.IEnumerable<int>)settings.EnemyWeights);
		configuredSeed = settings.EnemySeed;
		sessionSalt = NewSalt();
	}

	private string NewSalt()
	{
		if (configuredSeed != 0)
		{
			return configuredSeed.ToString((IFormatProvider)(object)CultureInfo.InvariantCulture);
		}
		return Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
	}

	public void BeginAuthoritativeSession()
	{
		lock (sync)
		{
			sessionSalt = NewSalt();
		}
	}

	public object? Replace(object manager, object spawnPointId, object originalPreset, out string? description)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_020f: Unknown result type (might be due to invalid IL or missing references)
		//IL_023b: Unknown result type (might be due to invalid IL or missing references)
		description = null;
		object? obj = Reflect.Read(Reflect.Read(manager, "_itemsManager") ?? throw new InvalidDataException("Spawn items manager is null"), "IsAuthoritative");
		if (!(obj is bool))
		{
			throw new MissingMemberException("IItemsManager.IsAuthoritative must return bool");
		}
		if (!(bool)obj)
		{
			ClientCallsSkipped++;
			return null;
		}
		AuthoritativeCalls++;
		string text = spawnPointId.ToString();
		if (!spawnPoints.Contains(text))
		{
			return null;
		}
		string text2 = Reflect.Read(originalPreset, "Id")?.ToString() ?? "";
		JsonNode obj2 = ((JsonNode)variants)[text2];
		JsonObject val = (JsonObject)(object)((obj2 is JsonObject) ? obj2 : null);
		if (val == null)
		{
			return null;
		}
		int num;
		lock (sync)
		{
			num = CoopProtocol.EnemyTier(sessionSalt, text, text2, weights);
		}
		if (testMode)
		{
			num = 1;
		}
		if (num == 0)
		{
			return null;
		}
		string text3 = tiers[num - 1];
		string value = ((JsonNode)val)[text3].GetValue<string>();
		if (repositoryType == null)
		{
			repositoryType = Reflect.FindType("Game.SpawnPointSystem.BotSpawnPoints.Data.BotPresetDataRepository");
		}
		if (idType == null)
		{
			idType = Reflect.FindType("Core.Utils.UniqueId");
		}
		if (parseId == null)
		{
			parseId = Enumerable.Single<MethodInfo>((System.Collections.Generic.IEnumerable<MethodInfo>)idType.GetMethods((BindingFlags)60), (Func<MethodInfo, bool>)((MethodInfo m) => ((MethodBase)m).IsStatic && ((MemberInfo)m).Name == "Parse" && ((MethodBase)m).GetParameters().Length == 1 && ((MethodBase)m).GetParameters()[0].ParameterType == typeof(string)));
		}
		if (getData == null)
		{
			getData = Enumerable.Single<MethodInfo>((System.Collections.Generic.IEnumerable<MethodInfo>)repositoryType.GetMethods((BindingFlags)60), (Func<MethodInfo, bool>)([CompilerGenerated] (MethodInfo m) => ((MemberInfo)m).Name == "GetData" && ((MethodBase)m).GetParameters().Length == 1 && ((MethodBase)m).GetParameters()[0].ParameterType == idType));
		}
		object obj3 = Reflect.AsConcrete(Reflect.Read(manager, "_botPresetDataRepository") ?? throw new InvalidDataException("Bot repository is null"), repositoryType);
		object obj4 = ((MethodBase)getData).Invoke(obj3, new object[1] { ((MethodBase)parseId).Invoke((object)null, new object[1] { value }) }) ?? throw new InvalidDataException("Elite preset was not registered");
		if (!string.Equals(Reflect.Read(obj4, "Id")?.ToString(), value, (StringComparison)5))
		{
			throw new InvalidDataException("Wrong elite preset returned");
		}
		description = $"{text3} | authoritative spawn {text} | {value}";
		return obj4;
	}
}
