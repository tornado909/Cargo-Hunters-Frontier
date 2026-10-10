using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;
namespace Frontier.Core;
internal static class Reflect
{
    internal const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    internal static object? Read(object? instance, string member)
    {
        if (instance is null) return null;
        for (Type? t = instance.GetType(); t is not null; t = t.BaseType)
        {
            var p = t.GetProperty(member, Flags | BindingFlags.DeclaredOnly);
            if (p is not null) return p.GetValue(instance);
            var f = t.GetField(member, Flags | BindingFlags.DeclaredOnly);
            if (f is not null) return f.GetValue(instance);
        }
        throw new MissingMemberException(instance.GetType().FullName, member);
    }
    internal static void Write(object instance, string member, object? value)
    {
        for (Type? t = instance.GetType(); t is not null; t = t.BaseType)
        {
            var p = t.GetProperty(member, Flags | BindingFlags.DeclaredOnly);
            if (p?.SetMethod is not null) { p.SetValue(instance, value); return; }
            var f = t.GetField(member, Flags | BindingFlags.DeclaredOnly);
            if (f is not null) { f.SetValue(instance, value); return; }
        }
        throw new MissingMemberException(instance.GetType().FullName, member);
    }
    internal static object? Call(object instance, string name, params object?[] args)
    {
        var method = instance.GetType().GetMethods(Flags).Where(m => m.Name == name && !m.ContainsGenericParameters && m.GetParameters().Length == args.Length)
            .Single(m => m.GetParameters().Select((p, i) => args[i] is null || p.ParameterType.IsInstanceOfType(args[i])).All(x => x));
        return method.Invoke(instance, args);
    }
    internal static int Count(object list)
    {
        try { return Convert.ToInt32(Read(list, "Count")); }
        catch (MissingMemberException)
        {
            Type type = list.GetType();
            if (type.IsGenericType && type.GetGenericTypeDefinition().FullName == "Il2CppSystem.Collections.Generic.IReadOnlyList`1")
            {
                Type collection = FindType("Il2CppSystem.Collections.Generic.IReadOnlyCollection`1").MakeGenericType(type.GetGenericArguments());
                return Convert.ToInt32(Read(AsConcrete(list, collection), "Count"));
            }
            return Convert.ToInt32(Read(list, "Length"));
        }
    }
    internal static IEnumerable<object> Values(object list)
    {
        int count = Count(list);
        if (count < 0 || count > 100000) throw new InvalidDataException("Unexpected collection size");
        var indexer = list.GetType().GetMethods(Flags).Single(m => m.Name == "get_Item" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(int));
        for (int i = 0; i < count; i++) if (indexer.Invoke(list, new object[] { i }) is object value) yield return value;
    }
    internal static string Id(object instance)
    {
        try { return Read(instance, "Id")?.ToString() ?? ""; }
        catch (MissingMemberException) { return Call(instance, "GetId")?.ToString() ?? ""; }
    }
    internal static object ParseId(Type type, string id)
    {
        if (type == typeof(int)) return int.Parse(id, System.Globalization.CultureInfo.InvariantCulture);
        if (type == typeof(string)) return id;
        var parse = type.GetMethods(Flags).Single(m => m.Name == "Parse" && m.IsStatic && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));
        return parse.Invoke(null, new object[] { id })!;
    }
    internal static Type FindType(string fullName)
    {
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            if (a.GetType(fullName, false) is Type t) return t;
        throw new TypeLoadException($"Required interop type missing: {fullName}");
    }
    internal static void LoadAssembly(string name, string directory)
    {
        if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == name)) return;
        string path = Path.Combine(directory, name + ".dll");
        if (!File.Exists(path)) throw new FileNotFoundException("Missing generated interop assembly", path);
        Assembly.LoadFrom(path);
    }
    internal static object AsConcrete(object proxy, Type concrete)
    {
        if (concrete.IsInstanceOfType(proxy)) return proxy;
        if (proxy is not Il2CppObjectBase native) throw new InvalidCastException("Expected IL2CPP object proxy");
        // Generated interface proxies expose no generic base GetData; use the concrete native wrapper.
        return Activator.CreateInstance(concrete, native.Pointer) ?? throw new InvalidOperationException("Could not wrap repository");
    }
    internal static bool SameNative(object? first, object? second) => first is Il2CppObjectBase a && second is Il2CppObjectBase b && a.Pointer == b.Pointer;
    internal static MethodInfo UniqueMethod(Type type, string name, int count) => type.GetMethods(Flags).Single(m => m.Name == name && m.GetParameters().Length == count);
}
