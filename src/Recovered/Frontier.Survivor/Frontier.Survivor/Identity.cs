using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Core.Utils;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Collections.Generic;

namespace Frontier.Survivor;

internal static class Identity
{
	internal const string Map = "Frontier_Survivor";

	internal const int MapId = 3466001;

	internal const string Version = "0.2.0";

	internal const string Revision = "v10.10.2-survivor-coop.2";

	internal static string Folder = "";

	internal static string Hash = "";

	internal static UniqueId Id(string name)
	{
		byte[] array = SHA256.HashData(Encoding.UTF8.GetBytes("frontier.survivor.v1:" + name)).Take(16).ToArray();
		array[7] = (byte)((array[7] & 0xF) | 0x50);
		array[8] = (byte)((array[8] & 0x3F) | 0x80);
		return UniqueId.Parse(new Guid(array).ToString());
	}

	internal static bool Own(string? name)
	{
		return string.Equals(name, "Frontier_Survivor", StringComparison.Ordinal);
	}

	internal static int Ordinal(Anchor a, LocationDefinition def)
	{
		return Array.IndexOf(def.Anchors, a) + 3466100;
	}

	internal static int Count<T>(Il2CppSystem.Collections.Generic.IReadOnlyList<T>? list, int limit = 50000)
	{
		if (list == null)
		{
			return 0;
		}
		int num = ((Il2CppObjectBase)list).TryCast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<T>>()?.Count ?? 0;
		if (num < 0 || num > limit)
		{
			throw new InvalidDataException("Native repository exceeds limit");
		}
		return num;
	}

	internal static Il2CppStructArray<byte> Salt(Il2CppStructArray<byte>? hash, string key)
	{
		return (Il2CppStructArray<byte>)(SHA256.HashData(((hash == null) ? ((System.Collections.Generic.IEnumerable<byte>)Array.Empty<byte>()) : ((System.Collections.Generic.IEnumerable<byte>)(byte[])((Il2CppArrayBase<byte>)(object)hash))).Concat(Encoding.UTF8.GetBytes("v10.10.2-survivor-coop.2|" + Hash + "|" + key)).ToArray()));
	}
}
