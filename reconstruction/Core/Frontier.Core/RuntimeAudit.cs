using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Frontier.Engine;
using Frontier.Modular;

namespace Frontier.Core;

internal sealed class RuntimeAudit
{
	private readonly Plugin plugin;

	private readonly object sync = new object();

	private readonly Dictionary<string, object> repositories = new Dictionary<string, object>((IEqualityComparer<string>)(object)StringComparer.Ordinal);

	private readonly Dictionary<string, Dictionary<string, object>> rows = new Dictionary<string, Dictionary<string, object>>((IEqualityComparer<string>)(object)StringComparer.Ordinal);

	private readonly Dictionary<string, object> checks = new Dictionary<string, object>((IEqualityComparer<string>)(object)StringComparer.Ordinal);

	private readonly Dictionary<string, JsonNode> expected = new Dictionary<string, JsonNode>((IEqualityComparer<string>)(object)StringComparer.Ordinal);

	[field: CompilerGenerated]
	internal bool CatalogReady
	{
		[CompilerGenerated]
		get;
		[CompilerGenerated]
		private set;
	}

	internal object Snapshot
	{
		get
		{
			lock (sync)
			{
				return new
				{
					catalogReady = CatalogReady,
					checks = new Dictionary<string, object>((IDictionary<string, object>)(object)checks)
				};
			}
		}
	}

	internal RuntimeAudit(Plugin plugin)
	{
		this.plugin = plugin;
	}

	internal object? Get(string repository, string id)
	{
		lock (sync)
		{
			Dictionary<string, object> val = default(Dictionary<string, object>);
			object obj = default(object);
			return (rows.TryGetValue(repository, ref val) && val.TryGetValue(id, ref obj)) ? obj : null;
		}
	}

	internal void Invalidate(string name)
	{
		lock (sync)
		{
			CatalogReady = false;
			repositories.Remove(name);
			rows.Remove(name);
			expected.Remove(name);
			checks[name] = new
			{
				status = "PENDING"
			};
		}
	}

	internal void Verify(string name, object nativeRepository, JsonNode expectedRoot, bool applied)
	{
		//IL_0332: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0193: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b9: Unknown result type (might be due to invalid IL or missing references)
		lock (sync)
		{
			System.Type type = Reflect.FindType(PatchEngine.Str(((JsonNode)plugin.Engine.RuntimeContracts)["repositoryTypes"], name));
			object obj = Reflect.AsConcrete(nativeRepository, type);
			Dictionary<string, object> val = new Dictionary<string, object>((IEqualityComparer<string>)(object)StringComparer.OrdinalIgnoreCase);
			JsonArray val2 = (JsonArray)(object)((expectedRoot is JsonArray) ? expectedRoot : null);
			if (val2 != null)
			{
				int num = Reflect.Count(Reflect.Call(obj, "GetAllData") ?? throw new InvalidDataException(name + ": GetAllData returned null"));
				if (num != val2.Count)
				{
					throw new InvalidDataException($"{name}: native count {num}, expected {val2.Count}. The byte replacement/load did not produce the expected catalog.");
				}
				MethodInfo val3 = Enumerable.Single<MethodInfo>((System.Collections.Generic.IEnumerable<MethodInfo>)type.GetMethods((BindingFlags)60), (Func<MethodInfo, bool>)((MethodInfo m) => ((MemberInfo)m).Name == "GetData" && ((MethodBase)m).GetParameters().Length == 1 && !((MethodBase)m).ContainsGenericParameters));
				System.Type parameterType = ((MethodBase)val3).GetParameters()[0].ParameterType;
				System.Collections.Generic.IEnumerator<JsonNode> enumerator = val2.GetEnumerator();
				try
				{
					while (((System.Collections.IEnumerator)enumerator).MoveNext())
					{
						JsonNode current = enumerator.Current;
						string text = PatchEngine.Scalar(current["Id"] ?? current["_id"]);
						object obj2 = ((MethodBase)val3).Invoke(obj, new object[1] { Reflect.ParseId(parameterType, text) }) ?? throw new InvalidDataException(name + ": GetData(" + text + ") returned null");
						if (!string.Equals(Reflect.Id(obj2), text, (StringComparison)5))
						{
							throw new InvalidDataException(name + ": wrong object ID " + text);
						}
						val.Add(text, obj2);
						string[] array = ((name == "skills") ? new string[4] { "IsDisabled", "MaxVersion", "Order", "IconName" } : ((name == "bot_presets") ? new string[3] { "AISettingId", "EquipmentPresetId", "PlatformPresetId" } : ((!(name == "ai_settings")) ? System.Array.Empty<string>() : new string[1] { "ExperienceForKill" })));
						array = array;
						foreach (string text2 in array)
						{
							JsonNode obj3 = current[text2];
							JsonValue val4 = (JsonValue)(object)((obj3 is JsonValue) ? obj3 : null);
							if (val4 != null)
							{
								Match(string.Concat(new string[5] { name, ".", text, ".", text2 }), Reflect.Read(obj2, text2), val4);
							}
						}
					}
				}
				finally
				{
					((System.IDisposable)enumerator)?.Dispose();
				}
				checks[name] = new
				{
					status = (applied ? "OBJECTS_VERIFIED" : "BASELINE_OBJECTS_VERIFIED"),
					nativeCount = num,
					expectedCount = val2.Count,
					lookedUpIds = val.Count
				};
			}
			else
			{
				object instance = Reflect.Call(obj, "GetData") ?? throw new InvalidDataException(name + ": empty native singleton");
				Match(name + ".MaxLevel", Reflect.Read(instance, "MaxLevel"), expectedRoot["MaxLevel"].AsValue());
				checks[name] = new
				{
					status = (applied ? "OBJECTS_VERIFIED" : "BASELINE_OBJECTS_VERIFIED"),
					maxLevel = Convert.ToInt32(Reflect.Read(instance, "MaxLevel"))
				};
			}
			repositories[name] = obj;
			rows[name] = val;
			expected[name] = expectedRoot;
			if (applied)
			{
				System.Collections.Generic.IReadOnlyCollection<string> repositoryNames = plugin.Engine.RepositoryNames;
				Dictionary<string, object> obj4 = repositories;
				if (Enumerable.All<string>((System.Collections.Generic.IEnumerable<string>)repositoryNames, (Func<string, bool>)obj4.ContainsKey))
				{
					CheckCrossReferences();
					CatalogReady = true;
				}
			}
		}
	}

	private static void Match(string where, object? native, JsonValue value)
	{
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		string text2 = default(string);
		string text = (value.TryGetValue<string>(ref text2) ? text2 : ((JsonNode)value).ToJsonString((JsonSerializerOptions)null));
		string text3 = ((!(native is bool)) ? (Convert.ToString(native, (IFormatProvider)(object)CultureInfo.InvariantCulture) ?? "null") : (((bool)native) ? "true" : "false"));
		if (!string.Equals(text3, text, (StringComparison)5))
		{
			throw new InvalidDataException($"{where}: native {text3}, expected {text}");
		}
	}

	private void CheckCrossReferences()
	{
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_036f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0218: Unknown result type (might be due to invalid IL or missing references)
		int num = 0;
		System.Collections.Generic.IEnumerator<JsonNode> enumerator = ((JsonNode)plugin.Engine.RuntimeContracts)["addedIds"]["bot_presets"].AsArray().GetEnumerator();
		try
		{
			while (((System.Collections.IEnumerator)enumerator).MoveNext())
			{
				JsonNode current = enumerator.Current;
				object instance = rows["bot_presets"][PatchEngine.Scalar(current)];
				ValueTuple<string, string>[] array = new ValueTuple<string, string>[3]
				{
					new ValueTuple<string, string>("AISettingId", "ai_settings"),
					new ValueTuple<string, string>("EquipmentPresetId", "bot_equipment_presets"),
					new ValueTuple<string, string>("PlatformPresetId", "bot_equipment_presets")
				};
				for (int i = 0; i < array.Length; i++)
				{
					ValueTuple<string, string> val = array[i];
					string text = Reflect.Read(instance, val.Item1)?.ToString() ?? "";
					if (!rows[val.Item2].ContainsKey(text))
					{
						throw new InvalidDataException("Broken elite reference: " + val.Item1 + " -> " + text);
					}
					num++;
				}
			}
		}
		finally
		{
			((System.IDisposable)enumerator)?.Dispose();
		}
		checks["eliteReferences"] = new
		{
			status = "OBJECTS_VERIFIED",
			references = num
		};
		if (ModuleRuntime.ContentGate(plugin.Engine, "progression"))
		{
			enumerator = ((JsonNode)plugin.Engine.RuntimeContracts)["skillIds"].AsArray().GetEnumerator();
			try
			{
				while (((System.Collections.IEnumerator)enumerator).MoveNext())
				{
					JsonNode current2 = enumerator.Current;
					object instance2 = rows["skills"][PatchEngine.Scalar(current2)];
					object obj = Reflect.Read(instance2, "IsDisabled");
					if (!(obj is bool) || (bool)obj || Reflect.Count(Reflect.Read(instance2, "PerkList")) < 1)
					{
						throw new InvalidDataException("Skill disabled or no native perks: " + (object)current2);
					}
				}
			}
			finally
			{
				((System.IDisposable)enumerator)?.Dispose();
			}
		}
		string shopId = PatchEngine.Str((JsonNode)(object)plugin.Engine.RuntimeContracts, "testShopId");
		Dictionary<string, object> val2 = Enumerable.ToDictionary<object, string, object>(Reflect.Values(Reflect.Read(Reflect.Read(rows["shop_templates"][shopId], "PricelistTemplate"), "Commodities")), (Func<object, string>)Reflect.Id, (Func<object, object>)((object x) => x), (IEqualityComparer<string>)(object)StringComparer.Ordinal);
		JsonNode[] array2 = Enumerable.ToArray<JsonNode>(Enumerable.Where<JsonNode>((System.Collections.Generic.IEnumerable<JsonNode>)Enumerable.Single<JsonNode>((System.Collections.Generic.IEnumerable<JsonNode>)expected["shop_templates"].AsArray(), (Func<JsonNode, bool>)((JsonNode x) => PatchEngine.Scalar(x["Id"]) == shopId))["PricelistTemplate"]["Commodities"].AsArray(), (Func<JsonNode, bool>)((JsonNode x) => x["Id"].GetValue<string>().StartsWith("Frontier_", (StringComparison)4))));
		JsonNode[] array3 = array2;
		object instance3 = default(object);
		for (int num2 = 0; num2 < array3.Length; num2++)
		{
			string value = array3[num2]["Id"].GetValue<string>();
			if (!val2.TryGetValue(value, ref instance3) || Reflect.Read(instance3, "Item") == null)
			{
				throw new InvalidDataException("Shop route not loaded: " + value);
			}
		}
		checks["shopRoutes"] = new
		{
			status = "OBJECTS_VERIFIED",
			offers = array2.Length,
			liveInventory = "NOT_OBSERVED"
		};
	}

	internal void Broken(string stage, System.Exception error)
	{
		lock (sync)
		{
			CatalogReady = false;
			checks[stage] = new
			{
				status = "BROKEN",
				error = error.GetBaseException().Message
			};
		}
	}
}
