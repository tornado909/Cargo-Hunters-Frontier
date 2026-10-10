using System.Reflection;

namespace Frontier.Core;

internal static class RigOSLocalizationFix
{
    private static readonly Dictionary<int, (string Name, string Description)> Ru = new()
    {
        [9001] = ("Оператор", "За уровень: перезарядка −1%, вход в прицел −0,5%. До 6 уровней."),
        [9002] = ("Марш-бросок", "За уровень: расход выносливости при спринте −2%, скорость спринта +0,5%. До 6 уровней."),
        [9003] = ("Техноархеолог", "За уровень: осмотр контейнеров −1%, скорость разборки роботов +1,5%. До 6 уровней."),
        [9004] = ("Изоляция", "За уровень: устойчивость к радиации +3 ед., к оглушению +2 ед. До 6 уровней.")
    };

    internal static int Repaired { get; private set; }

    internal static void Apply(object view, int id)
    {
        if (!Ru.TryGetValue(id, out var text)) return;
        int changed = RepairObject(view, text, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));
        if (changed > 0)
        {
            Repaired += changed;
            Plugin.Current.Log.LogInfo($"RigOS localization repaired for skill {id}: {changed} field(s)");
        }
    }

    private static int RepairObject(object? instance, (string Name, string Description) text, int depth, HashSet<object> visited)
    {
        if (instance is null || depth > 1 || !visited.Add(instance)) return 0;
        Type type = instance.GetType();
        int changed = 0;
        foreach (var field in type.GetFields(Reflect.Flags))
        {
            object? value;
            try { value = field.GetValue(instance); } catch { continue; }
            changed += RepairMember(instance, field.Name, field.FieldType, value, v => field.SetValue(instance, v), text, depth, visited);
        }
        foreach (var prop in type.GetProperties(Reflect.Flags))
        {
            if (prop.GetIndexParameters().Length != 0 || prop.GetMethod is null) continue;
            object? value;
            try { value = prop.GetValue(instance); } catch { continue; }
            Action<object?>? setter = prop.SetMethod is null ? null : v => prop.SetValue(instance, v);
            changed += RepairMember(instance, prop.Name, prop.PropertyType, value, setter, text, depth, visited);
        }
        return changed;
    }

    private static int RepairMember(object owner, string name, Type memberType, object? value, Action<object?>? setter,
        (string Name, string Description) text, int depth, HashSet<object> visited)
    {
        string replacement = name.Contains("desc", StringComparison.OrdinalIgnoreCase) ? text.Description : text.Name;
        if (memberType == typeof(string) && value is string s && IsMissing(s) && setter is not null)
        {
            try { setter(replacement); return 1; } catch { return 0; }
        }
        if (value is null || memberType.IsPrimitive || memberType.IsEnum || memberType == typeof(string)) return 0;
        // TMP/Text-like controls expose a writable string property named text/Text.
        foreach (string pName in new[] { "text", "Text" })
        {
            var p = value.GetType().GetProperty(pName, Reflect.Flags);
            if (p?.PropertyType != typeof(string) || p.GetMethod is null || p.SetMethod is null) continue;
            try
            {
                if (p.GetValue(value) is string current && IsMissing(current))
                { p.SetValue(value, replacement); return 1; }
            }
            catch { }
        }
        string tn = memberType.FullName ?? memberType.Name;
        if (depth == 0 && (tn.Contains("View", StringComparison.OrdinalIgnoreCase) || tn.Contains("Text", StringComparison.OrdinalIgnoreCase) || tn.Contains("Label", StringComparison.OrdinalIgnoreCase)))
            return RepairObject(value, text, depth + 1, visited);
        return 0;
    }

    private static bool IsMissing(string value) => value.Contains("Missing Entry", StringComparison.OrdinalIgnoreCase) || value.Contains("<Missing", StringComparison.OrdinalIgnoreCase);
}
