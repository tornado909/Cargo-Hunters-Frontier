using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Frontier.Modular;
using Game.ItemsSystem;
using Game.UI.Views;
using Game.WeaponSystem.ItemComponents;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine.Localization.Components;

namespace Frontier.Loot;

internal static class WeaponSummary
{
	internal sealed class Breakdown
	{
		internal readonly Bonuses Own = new Bonuses();

		internal readonly Bonuses Components = new Bonuses();

		internal readonly Bonuses Total = new Bonuses();

		internal readonly Dictionary<int, double> OwnNative = new Dictionary<int, double>();

		internal readonly Dictionary<int, double> ComponentsNative = new Dictionary<int, double>();

		internal readonly Dictionary<int, double> TotalNative = new Dictionary<int, double>();

		internal int AttachedCount;

		internal int BonusParts;
	}

	private sealed class Binding
	{
		internal IItem Item;

		internal ItemView View;
	}

	private static readonly Dictionary<IntPtr, Binding> Bindings = new Dictionary<IntPtr, Binding>();

	private static readonly Dictionary<string, Dictionary<int, double>> NativeModifiers = new Dictionary<string, Dictionary<int, double>>(StringComparer.Ordinal);

	private static readonly Regex RarityBlock = new Regex("(?m)^(Улучшенное|Редкое|Эпическое|Легендарное); качество [123]/3\\.", RegexOptions.Compiled);

	private static bool installed;

	private static string error = "";

	private static long updates;

	private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

	internal static object Status()
	{
		return new
		{
			adapter = "weapon-compact-cards-v2",
			installed = installed,
			bindings = Bindings.Count,
			updates = updates,
			lastError = error,
			scope = "Frontier rarity bonuses; equipped weapon attachments only; no robot/backpack/ammo traits"
		};
	}

	internal static void Install(Harmony h, string root)
	{
		if (!ModuleRuntime.Feature("weapon-summary"))
		{
			return;
		}
		foreach (KeyValuePair<string, Dictionary<int, double>> item in JsonSerializer.Deserialize<Dictionary<string, Dictionary<int, double>>>(File.ReadAllText(Path.Combine(root, "compiled", "weapon-summary.json"))) ?? throw new InvalidDataException("Missing weapon summary metadata"))
		{
			NativeModifiers.Add(item.Key, item.Value);
		}
		Runtime.Patch(h, typeof(ItemView), "InitializeFrom", 3, "BeforeItem", "AfterItem", null, typeof(WeaponSummary));
		Runtime.Patch(h, typeof(ItemView), "Clear", 0, "ClearItem", null, null, typeof(WeaponSummary));
		Runtime.Patch(h, typeof(LocalizeStringEvent), "UpdateString", 1, "UpdateText", null, null, typeof(WeaponSummary));
		Runtime.Patch(h, typeof(LocalizeStringEvent), "OnDestroy", 0, "DestroyText", null, null, typeof(WeaponSummary));
		installed = true;
		Runtime.Log.LogInfo((object)"Weapon summary adapter: weapon-compact-cards-v2 (CompactCards1)");
	}

	private static bool IsWeapon(IItem item)
	{
		string key = Runtime.Id(item.TemplateId);
		if (Runtime.Db.Variants.TryGetValue(key, out Trait value))
		{
			return value.Category == 1;
		}
		if (Runtime.Db.Families.TryGetValue(key, out Family value2))
		{
			return value2.Category == 1;
		}
		return item.HasComponent(Il2CppType.Of<FirearmComponent>());
	}

	private static bool IsPart(IItem item)
	{
		string key = Runtime.Id(item.TemplateId);
		if (Runtime.Db.Variants.TryGetValue(key, out Trait value))
		{
			return value.Category == 20;
		}
		if (Runtime.Db.Families.TryGetValue(key, out Family value2))
		{
			return value2.Category == 20;
		}
		return false;
	}

	internal static Breakdown Collect(IItem? weapon)
	{
		Breakdown r = new Breakdown();
		if (weapon == null)
		{
			return r;
		}
		HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal) { Runtime.Id(weapon.Id) };
		Add(weapon, r.Own, r.OwnNative);
		Add(weapon, r.Total, r.TotalNative);
		if (!weapon.IsBroken)
		{
			Children(weapon, 0);
		}
		r.Total.ClampLadder();
		return r;
		void Children(IItem parent, int depth)
		{
			if (depth < 8 && parent.IsContainer)
			{
				IItemsContainer container = parent.Container;
				if (container != null)
				{
					int num = Math.Min(container.ItemsCount, 128);
					for (int i = 0; i < num; i++)
					{
						IItem itemAtIndex = container.GetItemAtIndex(i);
						if (itemAtIndex != null && IsPart(itemAtIndex) && visited.Add(Runtime.Id(itemAtIndex.Id)))
						{
							r.AttachedCount++;
							int count = r.Components.Count;
							Add(itemAtIndex, r.Components, r.ComponentsNative);
							Add(itemAtIndex, r.Total, r.TotalNative);
							if (r.Components.Count > count)
							{
								r.BonusParts++;
							}
							Children(itemAtIndex, depth + 1);
						}
					}
				}
			}
		}
	}

	private static void Add(IItem item, Bonuses b, Dictionary<int, double> attrs)
	{
		if (item.IsBroken)
		{
			return;
		}
		string key = Runtime.Id(item.TemplateId);
		if (Runtime.Db.Variants.TryGetValue(key, out Trait value))
		{
			b.Add(RankLadder.Resolve(item, value));
		}
		foreach (KeyValuePair<int, double> item2 in RankLadder.Attributes(item))
		{
			attrs[item2.Key] = attrs.GetValueOrDefault(item2.Key) + item2.Value;
		}
		if (!NativeModifiers.TryGetValue(key, out Dictionary<int, double> value2))
		{
			return;
		}
		foreach (KeyValuePair<int, double> item3 in value2)
		{
			attrs[item3.Key] = attrs.GetValueOrDefault(item3.Key) + item3.Value;
		}
	}

	private static void BeforeItem(ItemView __instance, IItem __0)
	{
		try
		{
			LocalizeStringEvent descriptionStringEvent = __instance._descriptionStringEvent;
			if (descriptionStringEvent == null)
			{
				return;
			}
			Bindings.Remove(((Il2CppObjectBase)descriptionStringEvent).Pointer);
			if (__0 != null && (IsWeapon(__0) || RankLadder.NewItem(__0)))
			{
				if (Bindings.Count >= 256)
				{
					Bindings.Clear();
				}
				Bindings[((Il2CppObjectBase)descriptionStringEvent).Pointer] = new Binding
				{
					Item = __0,
					View = __instance
				};
				Runtime.Inc("weaponSummaryBindings", 1L);
			}
		}
		catch (Exception e)
		{
			RecordError(e);
		}
	}

	private static void AfterItem(ItemView __instance, IItem __0)
	{
		try
		{
			BeforeItem(__instance, __0);
			LocalizeStringEvent descriptionStringEvent = __instance._descriptionStringEvent;
			if (descriptionStringEvent != null && Bindings.ContainsKey(((Il2CppObjectBase)descriptionStringEvent).Pointer) && __instance._inspected && !__instance._hideSpecialInformation)
			{
				descriptionStringEvent.RefreshString();
			}
		}
		catch (Exception e)
		{
			RecordError(e);
		}
	}

	private static void ClearItem(ItemView __instance)
	{
		try
		{
			LocalizeStringEvent descriptionStringEvent = __instance._descriptionStringEvent;
			if (descriptionStringEvent != null)
			{
				Bindings.Remove(((Il2CppObjectBase)descriptionStringEvent).Pointer);
			}
		}
		catch (Exception e)
		{
			RecordError(e);
		}
	}

	private static void DestroyText(LocalizeStringEvent __instance)
	{
		Bindings.Remove(((Il2CppObjectBase)__instance).Pointer);
	}

	private static void UpdateText(LocalizeStringEvent __instance, ref string __0)
	{
		if (!Bindings.TryGetValue(((Il2CppObjectBase)__instance).Pointer, out Binding value))
		{
			__0 = CompactCards.CleanDescription(__0);
			return;
		}
		try
		{
			if (!(value.View == null) && value.Item != null && value.View._inspected && !value.View._hideSpecialInformation && !(Runtime.Id(value.View.ItemId) != Runtime.Id(value.Item.Id)))
			{
				if (RankLadder.NewItem(value.Item) && !IsWeapon(value.Item))
				{
					__0 = RankLadder.Describe(value.Item, __0);
					updates++;
					return;
				}
				Breakdown r = Collect(value.Item);
				string text = RankCardText.CleanDescription(__0);
				Match match = RarityBlock.Match(text);
				if (match.Success)
				{
					text = text.Substring(0, match.Index).Trim();
				}
				__0 = Render(r, text, Runtime.CombatEnabled, Combat.OpenCircuits());
				updates++;
				Runtime.Inc("weaponSummaryUpdates", 1L);
			}
		}
		catch (Exception e)
		{
			RecordError(e);
		}
		__0 = CompactCards.CleanDescription(__0);
	}

	private static void RecordError(Exception e)
	{
		error = e.ToString();
		Runtime.Error("ui.summary", e);
	}

	internal static string Render(Breakdown r, string original, bool combat, string[] circuits)
	{
		StringBuilder stringBuilder = new StringBuilder(640);
		stringBuilder.AppendLine("<b>ОРУЖИЕ — СОБСТВЕННЫЕ БОНУСЫ</b>");
		Rows(stringBuilder, r.Own, r.OwnNative, total: false);
		stringBuilder.AppendLine().Append("<b>КОМПОНЕНТЫ — СУММА (").Append(r.AttachedCount)
			.AppendLine(")</b>");
		Rows(stringBuilder, r.Components, r.ComponentsNative, total: false);
		stringBuilder.AppendLine().AppendLine("<b>ИТОГО: ОРУЖИЕ + КОМПОНЕНТЫ</b>");
		Rows(stringBuilder, r.Total, r.TotalNative, total: true);
		if (!combat || circuits.Length != 0)
		{
			stringBuilder.AppendLine().Append("<color=#FFAD63>Боевые эффекты ");
			stringBuilder.Append((!combat) ? "выключены настройкой." : "временно недоступны.");
			stringBuilder.AppendLine("</color>");
		}
		stringBuilder.AppendLine().Append("Бонусы редкости Frontier. Части робота не включены. Штатные параметры — выше.");
		if (!string.IsNullOrWhiteSpace(original))
		{
			stringBuilder.AppendLine().AppendLine().Append(original);
		}
		return CompactCards.FormatSummary(stringBuilder.ToString().TrimEnd());
	}

	private static string N(double x, int decimals = 2)
	{
		return Math.Round(decimal.Parse(x.ToString("F5", CI), CI), decimals, MidpointRounding.AwayFromZero).ToString(decimals switch
		{
			1 => "0.#", 
			3 => "0.###", 
			_ => "0.##", 
		}, CI);
	}

	private static void Rows(StringBuilder s, Bonuses b, Dictionary<int, double> a, bool total)
	{
		int length = s.Length;
		if ((double)Math.Abs(b.Damage) > 1E-06)
		{
			Row("Урон", "+" + N(b.Damage * 100f) + "%");
		}
		if (b.Crit > 0f)
		{
			Row("Шанс крита", "+" + N((total ? Math.Clamp(b.Crit, 0f, 5f) : b.Crit) * 100f) + " п.п.");
		}
		if (b.CritPower > 0f)
		{
			Row("Критический множитель", total ? ("×" + N(1.5 + (double)b.CritPower, 3)) : ("+" + N(b.CritPower, 3)));
		}
		if (b.Penetration > 0)
		{
			Row("Пробитие", "+" + b.Penetration + " кл.");
		}
		if (b.SaveAmmo > 0f)
		{
			Row("Не расходовать патрон", N((double)(Math.Clamp(total ? Math.Min((decimal)b.SaveAmmo, 0.95m) : ((decimal)b.SaveAmmo), 0m, 10m) * 100m), 1) + "%");
		}
		if (b.FireRate > 0f)
		{
			Row("Темп огня", "+" + N(b.FireRate * 100f) + "%");
		}
		int[] array = new int[6] { 12, 73, 10, 11, 17, 13 };
		foreach (int num in array)
		{
			if (a.TryGetValue(num, out var value) && !(Math.Abs(value) < 1E-08))
			{
				Row(num switch
				{
					12 => "Время перезарядки", 
					73 => "Дистанция сохранения урона", 
					10 => "Вертикальная стабилизация", 
					11 => "Горизонтальная стабилизация", 
					17 => "Время прицеливания", 
					13 => "Время смены оружия", 
					_ => "Модификатор", 
				}, ((value >= 0.0) ? "+" : "") + N(value * 100.0) + "%");
			}
		}
		if (b.Executor)
		{
			Row("Экзекутор", "попадания по роботу — в голову");
		}
		if (s.Length == length)
		{
			s.AppendLine("Дополнительных бонусов нет.");
		}
		void Row(string label, string v)
		{
			s.Append(label).Append(": ").AppendLine(v);
		}
	}
}
