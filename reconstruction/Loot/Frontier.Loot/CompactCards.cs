using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Frontier.Loot;

public static class CompactCards
{
	private const string OwnHeader = "<b>ОРУЖИЕ — СОБСТВЕННЫЕ БОНУСЫ</b>";

	private const string PartsHeader = "<b>КОМПОНЕНТЫ — СУММА (";

	private const string TotalHeader = "<b>ИТОГО: ОРУЖИЕ + КОМПОНЕНТЫ</b>";

	private const string Footer = "Бонусы редкости Frontier. Части робота не включены. Штатные параметры — выше.";

	public static string FormatSummary(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return value;
		}
		try
		{
			string text = value.Replace("\r\n", "\n");
			int num = text.IndexOf("<b>ОРУЖИЕ — СОБСТВЕННЫЕ БОНУСЫ</b>", StringComparison.Ordinal);
			int num2 = text.IndexOf("<b>КОМПОНЕНТЫ — СУММА (", StringComparison.Ordinal);
			int num3 = text.IndexOf("<b>ИТОГО: ОРУЖИЕ + КОМПОНЕНТЫ</b>", StringComparison.Ordinal);
			int num4 = text.IndexOf("Бонусы редкости Frontier. Части робота не включены. Штатные параметры — выше.", StringComparison.Ordinal);
			if (num != 0 || num2 < 0 || num3 <= num2 || num4 <= num3)
			{
				return value;
			}
			int num5 = text.IndexOf(")</b>", num2, StringComparison.Ordinal);
			if (num5 < num2 || num5 >= num3)
			{
				return value;
			}
			string value2 = text.Substring(num2 + "<b>КОМПОНЕНТЫ — СУММА (".Length, num5 - num2 - "<b>КОМПОНЕНТЫ — СУММА (".Length);
			int num6 = text.IndexOf("<color=#FFAD63>Боевые эффекты ", num3, StringComparison.Ordinal);
			int num7 = ((num6 >= num3 && num6 < num4) ? num6 : num4);
			Dictionary<string, string> dictionary = ReadRows(text.Substring("<b>ОРУЖИЕ — СОБСТВЕННЫЕ БОНУСЫ</b>".Length, num2 - "<b>ОРУЖИЕ — СОБСТВЕННЫЕ БОНУСЫ</b>".Length));
			Dictionary<string, string> dictionary2 = ReadRows(text.Substring(num5 + 5, num3 - num5 - 5));
			Dictionary<string, string> dictionary3 = ReadRows(text.Substring(num3 + "<b>ИТОГО: ОРУЖИЕ + КОМПОНЕНТЫ</b>".Length, num7 - num3 - "<b>ИТОГО: ОРУЖИЕ + КОМПОНЕНТЫ</b>".Length));
			string value3 = text.Substring(num4 + "Бонусы редкости Frontier. Части робота не включены. Штатные параметры — выше.".Length).Trim();
			StringBuilder stringBuilder = new StringBuilder(512);
			if (dictionary3.Count != 0)
			{
				bool flag = dictionary.Count != 0 && dictionary2.Count != 0;
				if (flag)
				{
					stringBuilder.AppendLine("<b>ИТОГО</b> (оружие / модули)");
				}
				else if (dictionary2.Count != 0)
				{
					stringBuilder.Append("<b>МОДУЛИ (").Append(value2).AppendLine(") · ИТОГО</b>");
				}
				else
				{
					stringBuilder.AppendLine("<b>БОНУСЫ ОРУЖИЯ</b>");
				}
				List<string> list = new List<string>();
				foreach (KeyValuePair<string, string> item in dictionary3)
				{
					string text2 = ShortLabel(item.Key) + ": " + item.Value;
					if (flag)
					{
						dictionary.TryGetValue(item.Key, out var value4);
						dictionary2.TryGetValue(item.Key, out var value5);
						text2 = ((!(item.Key == "Экзекутор")) ? (text2 + " (" + Contribution(value4) + " / " + Contribution(value5) + ")") : (text2 + " (" + ((value4 == null) ? "модули" : ((value5 == null) ? "оружие" : "оружие и модули")) + ")"));
					}
					list.Add(text2);
				}
				AppendPacked(stringBuilder, list, (!flag) ? 43 : 0);
				if (num6 >= 0 && num6 < num4)
				{
					stringBuilder.AppendLine("<color=#FFAD63>Бонусы временно недоступны.</color>");
				}
			}
			if (!string.IsNullOrWhiteSpace(value3))
			{
				if (stringBuilder.Length != 0)
				{
					stringBuilder.AppendLine();
				}
				stringBuilder.Append(value3);
			}
			return stringBuilder.ToString().TrimEnd();
		}
		catch
		{
			return value;
		}
	}

	private static Dictionary<string, string> ReadRows(string block)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
		string[] array = block.Split('\n');
		for (int i = 0; i < array.Length; i++)
		{
			string text = array[i].Trim();
			if (text.Length == 0 || text == "Дополнительных бонусов нет.")
			{
				continue;
			}
			int num = text.IndexOf(": ", StringComparison.Ordinal);
			if (num >= 1 && text[0] != '<')
			{
				string key = text.Substring(0, num);
				if (dictionary.ContainsKey(key))
				{
					throw new FormatException("Duplicate summary row");
				}
				dictionary.Add(key, text.Substring(num + 2));
			}
		}
		return dictionary;
	}

	private static string Contribution(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return "—";
		}
		return value.Replace(" п.п.", "").Replace(" кл.", "").Replace("%", "");
	}

	private static string ShortLabel(string label)
	{
		return label switch
		{
			"Критический множитель" => "Урон крита", 
			"Не расходовать патрон" => "Экономия патронов", 
			"Время перезарядки" => "Перезарядка", 
			"Дистанция сохранения урона" => "Дальность урона", 
			"Время прицеливания" => "Прицеливание", 
			_ => label, 
		};
	}

	private static void AppendPacked(StringBuilder output, List<string> rows, int limit)
	{
		for (int i = 0; i < rows.Count; i++)
		{
			string text = rows[i];
			if (limit > 0 && i + 1 < rows.Count && text.Length + rows[i + 1].Length + 3 <= limit)
			{
				text = text + " · " + rows[++i];
			}
			output.AppendLine(text);
		}
	}

	public static string CleanDescription(string value)
	{
		value = RankCardText.CleanDescription(value);
		if (string.IsNullOrEmpty(value))
		{
			return value;
		}
		if (!value.Contains("; качество ", StringComparison.Ordinal) && !value.Contains("; quality ", StringComparison.Ordinal))
		{
			return value;
		}
		try
		{
			Match match = Regex.Match(value, "(?m)^(Улучшенное|Редкое|Эпическое|Легендарное); качество ([123])/3\\.|^(Uncommon|Rare|Epic|Legendary); quality ([123])/3\\.");
			if (!match.Success)
			{
				return value;
			}
			string value2 = (match.Groups[3].Success ? (match.Groups[3].Value + " · " + match.Groups[4].Value + "/3") : (match.Groups[1].Value + " · " + match.Groups[2].Value + "/3"));
			string text = value.Substring(0, match.Index).TrimEnd();
			string text2 = value.Substring(match.Index + match.Length).Replace("\r\n", "\n");
			List<string> list = new List<string>();
			string[] array = text2.Split('\n');
			for (int i = 0; i < array.Length; i++)
			{
				string text3 = array[i].Trim();
				if (text3.Length != 0 && !IsTechnical(text3))
				{
					text3 = text3.Replace("Не расходует патрон: шанс ", "Экономия патронов: ").Replace("Время перезарядки:", "Перезарядка:").Replace("Дистанция сохранения урона:", "Дальность урона:")
						.Replace("Критический множитель:", "Урон крита:")
						.Replace("Cartridge conservation chance:", "Ammo saving:")
						.Replace("Reload time:", "Reload:");
					if (text3.EndsWith("%.", StringComparison.Ordinal))
					{
						text3 = text3.Substring(0, text3.Length - 1);
					}
					list.Add(text3);
				}
			}
			StringBuilder stringBuilder = new StringBuilder(value.Length);
			if (text.Length != 0)
			{
				stringBuilder.Append(text).AppendLine().AppendLine();
			}
			stringBuilder.AppendLine(value2);
			AppendPacked(stringBuilder, list, 43);
			return stringBuilder.ToString().TrimEnd();
		}
		catch
		{
			return value;
		}
	}

	private static bool IsTechnical(string line)
	{
		if (!line.StartsWith("Боевые эффекты Frontier действуют ", StringComparison.Ordinal))
		{
			switch (line)
			{
			default:
				return line == "Roll is persistent.";
			case "Суммируются только установленные детали.":
			case "Player effects only. Roll is persistent.":
			case "Player effects only. Only installed parts contribute.":
			case "Only installed parts contribute.":
				break;
			}
		}
		return true;
	}
}
