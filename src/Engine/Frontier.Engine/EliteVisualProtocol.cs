using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Frontier.Engine;

public static class EliteVisualProtocol
{
	public sealed record Packet(string Header, string[] Pages);

	public const string HeaderKey = "frontier_visual_v2";

	public const string PagePrefix = "frontier_visual_v2_p";

	public const int EntriesPerPage = 24;

	public const int MaxPages = 8;

	public const int MaxEntries = 192;

	public static bool ValidRank(string? rank)
	{
		switch (rank)
		{
		case "veteran":
		case "tracker":
		case "bulwark":
		case "nemesis":
			return true;
		default:
			return false;
		}
	}

	public static string EquipmentId(string id)
	{
		if (!Guid.TryParse(id, out var result) || !(result != Guid.Empty))
		{
			return "";
		}
		return result.ToString("N");
	}

	private static char Code(string rank)
	{
		return rank switch
		{
			"veteran" => '1', 
			"tracker" => '2', 
			"bulwark" => '3', 
			"nemesis" => '4', 
			_ => throw new ArgumentException("Unknown rank"), 
		};
	}

	private static string Rank(char code)
	{
		return code switch
		{
			'1' => "veteran", 
			'2' => "tracker", 
			'3' => "bulwark", 
			'4' => "nemesis", 
			_ => "", 
		};
	}

	private static string Hash(IEnumerable<string> pages)
	{
		return PatchEngine.Hash(Encoding.UTF8.GetBytes(string.Join("\n", pages)));
	}

	public static Packet Encode(string epoch, string generation, IReadOnlyDictionary<string, string> entries)
	{
		if (!CoopProtocol.IsHex(epoch, 32) || !CoopProtocol.IsHex(generation, 32) || entries.Count > 192)
		{
			throw new ArgumentException("Invalid visual envelope");
		}
		SortedDictionary<string, string> sortedDictionary = new SortedDictionary<string, string>(StringComparer.Ordinal);
		foreach (KeyValuePair<string, string> entry in entries)
		{
			string text = EquipmentId(entry.Key);
			if (text.Length == 0 || !ValidRank(entry.Value) || sortedDictionary.ContainsKey(text))
			{
				throw new ArgumentException("Invalid/duplicate visual identity");
			}
			sortedDictionary.Add(text, text + ":" + Code(entry.Value));
		}
		string[] array = (from a in sortedDictionary.Values.Chunk(24)
			select generation + "|" + string.Join("\n", a)).ToArray();
		if (array.Any((string p) => p.Length > 1024))
		{
			throw new InvalidDataException("Cosmetic page exceeds existing Steam adapter limit");
		}
		return new Packet("2|" + epoch + "|" + generation + "|" + array.Length.ToString(CultureInfo.InvariantCulture) + "|" + Hash(array), array);
	}

	public static bool TryReadHeader(string header, string epoch, out string generation, out int count)
	{
		generation = "";
		count = 0;
		if (header == null || header.Length > 256 || !CoopProtocol.IsHex(epoch, 32))
		{
			return false;
		}
		string[] array = header.Split('|');
		if (array.Length != 5 || array[0] != "2" || array[1] != epoch || !CoopProtocol.IsHex(array[2], 32) || !CoopProtocol.IsHex(array[4], 64) || !int.TryParse(array[3], NumberStyles.None, CultureInfo.InvariantCulture, out count) || count < 0 || count > 8)
		{
			return false;
		}
		generation = array[2];
		return true;
	}

	public static bool TryDecode(string header, string epoch, IReadOnlyList<string> pages, out Dictionary<string, string> entries)
	{
		entries = new Dictionary<string, string>(StringComparer.Ordinal);
		if (!TryReadHeader(header, epoch, out string generation, out int count) || count != pages.Count || pages.Any((string p) => p == null || p.Length > 1024) || header.Split('|')[4] != Hash(pages))
		{
			return false;
		}
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (string page in pages)
		{
			if (!page.StartsWith(generation + "|", StringComparison.Ordinal))
			{
				return false;
			}
			string[] array = page.Substring(33).Split('\n');
			int num = array.Length;
			if ((num < 1 || num > 24) ? true : false)
			{
				return false;
			}
			string[] array2 = array;
			foreach (string text in array2)
			{
				if (text.Length != 34 || text[32] != ':' || !CoopProtocol.IsHex(text.Substring(0, 32), 32) || EquipmentId(text.Substring(0, 32)).Length == 0 || !ValidRank(Rank(text[33])) || !dictionary.TryAdd(text.Substring(0, 32), Rank(text[33])))
				{
					return false;
				}
			}
		}
		if (dictionary.Count > 192)
		{
			return false;
		}
		entries = dictionary;
		return true;
	}
}
