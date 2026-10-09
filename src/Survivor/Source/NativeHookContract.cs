#nullable enable
using System;
using System.Linq;
using System.Reflection;
namespace Frontier.Safety;

/// <summary>Pre-flight only: inspect managed metadata, never dereference a native object.</summary>
internal static class NativeHookContract
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    internal static MethodInfo Exact(Type owner, string name, bool isStatic, Type result, params Type[] args)
    {
        var method = owner.GetMethod(name, Flags, binder: null, types: args, modifiers: null)
            ?? throw new MissingMethodException(owner.FullName, name);
        if (method.IsStatic != isStatic || method.ReturnType != result || method.DeclaringType != owner)
            throw new InvalidOperationException("Native hook contract changed: " + method);
        RequireSafe(method);
        return method;
    }

    internal static void RequireSafe(MethodInfo method)
    {
        if (method.ContainsGenericParameters || method.IsAbstract || method.IsGenericMethod)
            throw new NotSupportedException("No abstract or generic native detours: " + method);
        CheckType(method.ReturnType, true);
        foreach (var arg in method.GetParameters()) CheckType(arg.ParameterType, false);

        // The shipped interop records identify the folded empty stub used by EnterExitState
        // with a huge, identical xref/ref interval. Do not detour that shared implementation.
        // This is a conservative metadata guard, not a claim that a compiler call-count
        // alone proves runtime address uniqueness.
        var cached = method.GetCustomAttributesData().SingleOrDefault(a =>
            a.AttributeType.FullName == "Il2CppInterop.Common.Attributes.CachedScanResultsAttribute");
        if (cached == null)
            throw new NotSupportedException("Native scan metadata missing: " + method);
        long Field(string name) => Convert.ToInt64(cached.NamedArguments
            .Single(a => a.MemberName == name).TypedValue.Value);
        long xs = Field("XrefRangeStart"), xe = Field("XrefRangeEnd"),
             rs = Field("RefRangeStart"), re = Field("RefRangeEnd");
        if (xs == 0 && xe == 0 || xs == rs && xe == re && xe - xs >= 1024)
            throw new NotSupportedException("Empty/shared native implementation is not a hook target: " + method);
    }

    private static void CheckType(Type type, bool result)
    {
        if (type == typeof(void) && result) return;
        // Il2CppSystem.ValueType is a managed reference wrapper for a NATIVE struct.
        // Checking System.Type.IsValueType alone is insufficient. In particular, ref
        // ItemDto would enter the installed trampoline's ldind.i -> ObjectPool path.
        if (type.IsByRef || type.IsPointer || type.ContainsGenericParameters)
            throw new NotSupportedException("No byref/pointer native hook arguments or results: " + type);
        for (Type? t = type; t != null; t = t.BaseType)
            if (t.FullName == "Il2CppSystem.ValueType")
                throw new NotSupportedException("No boxed native-struct detours: " + type);
        if (type.IsValueType && !type.IsPrimitive && !type.IsEnum &&
            !(type.FullName == "Core.Utils.UniqueId" && !result))
            throw new NotSupportedException("Value type not in the inspected hook allowlist: " + type);
    }
}
