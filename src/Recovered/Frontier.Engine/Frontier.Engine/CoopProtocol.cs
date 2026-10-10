using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Frontier.Engine;

public static class CoopProtocol
{
	public const string PackVersion = "0.4.0-production";

	public const string Version = "1";

	public const string SignatureKey = "frontier_signature";

	public const string ProtocolKey = "frontier_protocol";

	public const string EpochKey = "frontier_epoch";

	public const string CatalogKey = "frontier_catalog";

	public const string ReadyKey = "frontier_ready";

	public const string PhaseKey = "frontier_phase";

	public static bool IsHex(string? text, int length)
	{
		if (text != null && text.Length == length)
		{
			return text.All(delegate(char c)
			{
				switch (c)
				{
				case '0':
				case '1':
				case '2':
				case '3':
				case '4':
				case '5':
				case '6':
				case '7':
				case '8':
				case '9':
				case 'a':
				case 'b':
				case 'c':
				case 'd':
				case 'e':
				case 'f':
					return true;
				default:
					return false;
				}
			});
		}
		return false;
	}

	private static string Hash(string text)
	{
		return PatchEngine.Hash(Encoding.UTF8.GetBytes(text));
	}

	public static string Fingerprint(string gameVersion, PackSettings settings, IReadOnlyDictionary<string, string> hashes)
	{
		settings.Validate();
		if (string.IsNullOrWhiteSpace(gameVersion) || gameVersion.Any(char.IsControl))
		{
			throw new InvalidDataException("Invalid game version");
		}
		List<string> list = new List<string>
		{
			"frontier-net/1",
			"pack=0.4.0-production",
			"game=" + gameVersion,
			"modules=" + string.Join(",", settings.EnabledModules.OrderBy<string, string>((string s) => s, StringComparer.Ordinal)),
			"productionMode=" + (settings.ProductionMode ? "1" : "0"),
			"runtimeTestMode=" + (settings.RuntimeTestMode ? "1" : "0"),
			"enemySeed=" + settings.EnemySeed.ToString(CultureInfo.InvariantCulture),
			"enemyWeights=" + string.Join(",", settings.EnemyWeights.Select((int n) => n.ToString(CultureInfo.InvariantCulture)))
		};
		foreach (KeyValuePair<string, string> item in hashes.OrderBy<KeyValuePair<string, string>, string>((KeyValuePair<string, string> e) => e.Key, StringComparer.Ordinal))
		{
			if (item.Key.Any(char.IsControl) || item.Key.Contains('=') || !IsHex(item.Value, 64))
			{
				throw new InvalidDataException("Invalid fingerprint entry: " + item.Key);
			}
			list.Add("file:" + item.Key + "=" + item.Value);
		}
		return Hash(string.Join("\n", list) + "\n");
	}

	public static string CatalogDigest(IReadOnlyDictionary<string, string> loaded, IEnumerable<string> required)
	{
		string[] array = required.OrderBy<string, string>((string s) => s, StringComparer.Ordinal).ToArray();
		if (array.Length == 0 || array.Any((string n) => !loaded.TryGetValue(n, out string value) || !IsHex(value, 64)))
		{
			return "";
		}
		return Hash(string.Join("\n", array.Select((string n) => n + "=" + loaded[n])) + "\n");
	}

	public static string ReadyToken(string signature, string epoch, string catalog)
	{
		if (!IsHex(signature, 64) || !IsHex(epoch, 32) || !IsHex(catalog, 64))
		{
			throw new InvalidDataException("Invalid readiness input");
		}
		return "1|" + signature + "|" + epoch + "|" + catalog;
	}

	public static bool IsReadyToken(string? token, string epoch)
	{
		string[] array = (token ?? "").Split('|');
		if (array.Length == 4 && array[0] == "1" && IsHex(array[1], 64) && array[2] == epoch && IsHex(array[2], 32))
		{
			return IsHex(array[3], 64);
		}
		return false;
	}

	public static string? HeaderError(string protocol, string signature, string epoch, string localSignature)
	{
		if (protocol != "1")
		{
			return "Frontier protocol is absent or different";
		}
		if (!IsHex(signature, 64) || signature != localSignature)
		{
			return "Frontier files, modules, settings or loader differ";
		}
		if (!IsHex(epoch, 32))
		{
			return "Lobby handshake is not initialized";
		}
		return null;
	}

	public static int EnemyTier(string sessionSalt, string spawnId, string baseId, IReadOnlyList<int> weights)
	{
		long num = ((IEnumerable<int>)weights).Sum((Func<int, long>)((int x) => x));
		bool flag = weights.Count != 5 || weights.Any((int x) => x < 0);
		if (!flag)
		{
			flag = ((num <= 0 || num > int.MaxValue) ? true : false);
		}
		if (flag)
		{
			throw new ArgumentException("Invalid weights");
		}
		string s = "frontier-enemy/v1\n" + sessionSalt + "\n" + spawnId.ToLowerInvariant() + "\n" + baseId.ToLowerInvariant() + "\n";
		return WeightedRoll.Pick((int)(BinaryPrimitives.ReadUInt32LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes(s))) * num >>> 32), weights);
	}
}
