using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier.ReferenceFix;

internal static class RankColors
{
	private static readonly Dictionary<string, object> Cache = new Dictionary<string, object>(StringComparer.Ordinal);

	private static Type? presentation;

	private static Type? protocol;

	internal static (string Rank, object? Color) Read(object player, Evidence report)
	{
		try
		{
			if ((object)presentation == null)
			{
				presentation = R.Type("Frontier.Core", "Frontier.Core.ElitePresentation");
			}
			if ((object)protocol == null)
			{
				protocol = R.Type("Frontier.Engine", "Frontier.Engine.EliteVisualProtocol");
			}
			object obj = R.Get(player, "Equipment");
			if (obj == null)
			{
				return (Rank: "normal", Color: null);
			}
			string text = R.Key(R.Get(obj, "Id"));
			string text2 = R.Key(R.Call(protocol, "EquipmentId", text));
			object obj2 = R.Call(presentation, "FindRank", text2);
			if (obj2 == null)
			{
				return (Rank: "normal", Color: null);
			}
			string item = R.Key(R.Get(obj2, "Id"));
			string text3 = R.Key(R.Get(obj2, "Hex"));
			if (!Cache.TryGetValue(text3, out object value))
			{
				if (!ColorUtility.TryParseHtmlString(text3, out var color))
				{
					return (Rank: item, Color: null);
				}
				value = color;
				Cache[text3] = value;
			}
			return (Rank: item, Color: value);
		}
		catch (Exception e)
		{
			report.Error("rank lookup", e);
			return (Rank: "unknown", Color: null);
		}
	}
}
