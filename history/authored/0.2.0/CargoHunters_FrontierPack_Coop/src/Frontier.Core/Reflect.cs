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
