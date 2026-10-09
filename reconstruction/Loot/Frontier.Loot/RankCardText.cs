using System;
using System.Text.RegularExpressions;

namespace Frontier.Loot;

public static class RankCardText
{
	private static readonly Regex Markup = new Regex("<[^>]*>", RegexOptions.CultureInvariant);

	private static readonly Regex Suffix = new Regex("\\s*\\[(?:[УРЭЛ]\\s*[123]|(?:Uncommon|Rare|Epic|Legendary)\\s+[123]|(?:F|E|D|C|B|A|S|SS|SSS)[123]|Z)\\]\\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex Technical = new Regex("(?im)^\\s*(?:Свойства этого экземпляра сохранены по его ID\\.?|Индивидуальные бонусы закреплены за экземпляром\\.?|Individual bonuses are bound to this item\\.?|Roll is persistent\\.?|Player effects only\\.\\s*(?:Roll is persistent\\.|Only installed parts contribute\\.)?|Only installed parts contribute\\.?|Суммируются только установленные детали\\.?|Боевые эффекты Frontier действуют[^\\r\\n]*|Бонусы редкости Frontier\\. Части робота не включены\\. Штатные параметры — выше\\.|[^\\r\\n]*См\\. Frontier-loot-report\\.json\\.)\\s*(?:\\r?\\n|$)", RegexOptions.CultureInvariant);

	public static string BaseName(string? value)
	{
		string text = Markup.Replace(value ?? "", "").Trim();
		for (int i = 0; i < 8; i++)
		{
			string text2 = Suffix.Replace(text, "").TrimEnd();
			if (text2 == text)
			{
				break;
			}
			text = text2;
		}
		return text;
	}

	public static string Name(string value, int rank)
	{
		int num = RankPolicy.Grade(rank);
		if (num < 0)
		{
			throw new ArgumentOutOfRangeException("rank");
		}
		string text = BaseName(value);
		if (text.Length == 0)
		{
			throw new ArgumentException("Empty item name", "value");
		}
		return "<color=" + RankPolicy.Colors[num] + ">" + text + " [" + RankPolicy.Name(rank) + "]</color>";
	}

	public static string CleanDescription(string? value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return value ?? "";
		}
		string input = value.Replace("Свойства этого экземпляра сохранены по его ID.", "", StringComparison.Ordinal).Replace("Индивидуальные бонусы закреплены за экземпляром.", "", StringComparison.Ordinal).Replace("Individual bonuses are bound to this item.", "", StringComparison.Ordinal)
			.Replace("A fixed, persistent salvage roll; not a per-instance random affix.", "", StringComparison.Ordinal);
		return Technical.Replace(input, "").TrimEnd();
	}
}
