using System;
using System.Collections.Generic;
using System.Globalization;

namespace Frontier.ReferenceFix;

internal sealed class KnownLocalizationKeys
{
	private readonly HashSet<long> ids = new HashSet<long>();

	private readonly Dictionary<string, long> names = new Dictionary<string, long>(StringComparer.Ordinal);

	private readonly HashSet<string> ambiguous = new HashSet<string>(StringComparer.Ordinal);

	internal int Count => ids.Count;

	internal void Add(long id, string key)
	{
		if (id <= 0)
		{
			throw new ArgumentOutOfRangeException("id");
		}
		ids.Add(id);
		Bind(id.ToString(CultureInfo.InvariantCulture), id);
		if (!string.IsNullOrWhiteSpace(key))
		{
			Bind(key, id);
			Bind("Frontier." + key, id);
		}
	}

	private void Bind(string name, long id)
	{
		if (!ambiguous.Contains(name))
		{
			if (names.TryGetValue(name, out var value) && value != id)
			{
				names.Remove(name);
				ambiguous.Add(name);
			}
			else
			{
				names[name] = id;
			}
		}
	}

	internal long Resolve(long id, string? name)
	{
		if (id > 0 && ids.Contains(id))
		{
			return id;
		}
		if (string.IsNullOrEmpty(name))
		{
			return 0L;
		}
		if (names.TryGetValue(name, out var value))
		{
			return value;
		}
		if (!long.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out value) || !ids.Contains(value))
		{
			return 0L;
		}
		return value;
	}
}
