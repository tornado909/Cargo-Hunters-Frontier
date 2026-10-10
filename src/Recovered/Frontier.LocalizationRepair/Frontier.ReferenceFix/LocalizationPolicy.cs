using System;
using System.Collections.Generic;

namespace Frontier.ReferenceFix;

internal static class LocalizationPolicy
{
	internal const long AttributeBase = 3468000000000000000L;

	internal static readonly Dictionary<int, (string Ru, string En)> Attributes = new Dictionary<int, (string, string)>
	{
		[1] = ("Время обыска", "Container search time"),
		[2] = ("Расход энергии на спринт", "Sprint energy consumption"),
		[4] = ("Скорость ходьбы", "Walking speed"),
		[5] = ("Скорость спринта", "Sprint speed"),
		[8] = ("Время применения ремкомплекта", "Repair kit use time"),
		[9] = ("Перерыв между ударами", "Melee attack cooldown"),
		[10] = ("Стабилизация вертикальной отдачи", "Vertical recoil stabilization"),
		[11] = ("Стабилизация горизонтальной отдачи", "Horizontal recoil stabilization"),
		[13] = ("Время смены оружия", "Weapon switch time"),
		[12] = ("Время перезарядки", "Reload time"),
		[14] = ("Дальность подсветки предметов", "Object highlight distance"),
		[16] = ("Дальность пристрелки", "Zeroing distance"),
		[17] = ("Время перехода к прицеливанию", "Aim transition time"),
		[18] = ("Расход энергии при прицеливании", "Aiming energy consumption"),
		[20] = ("Грузоподъёмность", "Carrying capacity"),
		[29] = ("Запас энергии ног", "Leg energy capacity"),
		[30] = ("Запас энергии рук", "Arm energy capacity"),
		[43] = ("Устойчивость к радиации", "Radiation resistance"),
		[65] = ("Скорость бега", "Running speed"),
		[71] = ("Дальность слышимости выстрела", "Gunshot audibility distance"),
		[73] = ("Дальность эффективного урона", "Damage falloff distance"),
		[74] = ("Громкость движения", "Movement sound volume"),
		[75] = ("Дальность слышимости движения", "Movement audibility distance"),
		[79] = ("Урон от падения", "Fall damage"),
		[87] = ("Скорость распила", "Dissection speed"),
		[92] = ("Устойчивость к оглушению", "Stun resistance")
	};

	internal static bool Plain(string? text)
	{
		if (text != null && !text.Contains('{'))
		{
			return !text.Contains('}');
		}
		return false;
	}

	internal static bool ReplaceAttribute(int id, long existingKey, string? existingText)
	{
		if (Attributes.ContainsKey(id))
		{
			if (id != 79 && id != 92 && existingKey != 0L)
			{
				return Broken(existingText);
			}
			return true;
		}
		return false;
	}

	internal static (string Ru, string En) Name(int id, bool convertedToSpeed)
	{
		(string, string) tuple = Attributes[id];
		if (!convertedToSpeed)
		{
			return tuple;
		}
		return id switch
		{
			1 => (Ru: "Скорость обыска", En: "Search speed"), 
			8 => (Ru: "Скорость лечения", En: "Repair speed"), 
			9 => (Ru: "Скорость атак ближнего боя", En: "Melee attack speed"), 
			12 => (Ru: "Скорость перезарядки", En: "Reload speed"), 
			17 => (Ru: "Скорость прицеливания", En: "Aiming speed"), 
			_ => tuple, 
		};
	}

	internal static bool Broken(string? text)
	{
		if (!string.IsNullOrWhiteSpace(text) && !text.Contains("Missing Key", StringComparison.OrdinalIgnoreCase) && !text.Contains("Missing Entry", StringComparison.OrdinalIgnoreCase) && !text.Contains("No translation found", StringComparison.OrdinalIgnoreCase))
		{
			return text.Equals("Missing", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	internal static bool Russian(string? code)
	{
		return code?.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ?? false;
	}
}
