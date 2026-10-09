using System;
using System.Linq;
using System.Reflection;

namespace Frontier.Safety;

internal static class NativeHookContract
{
	private const BindingFlags Flags = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

	internal static MethodInfo Exact(Type owner, string name, bool isStatic, Type result, params Type[] args)
	{
		MethodInfo methodInfo = owner.GetMethod(name, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, args, null) ?? throw new MissingMethodException(owner.FullName, name);
		if (methodInfo.IsStatic != isStatic || methodInfo.ReturnType != result || methodInfo.DeclaringType != owner)
		{
			throw new InvalidOperationException("Native hook contract changed: " + methodInfo);
		}
		RequireSafe(methodInfo);
		return methodInfo;
	}

	internal static void RequireSafe(MethodInfo method)
	{
		if (method.ContainsGenericParameters || method.IsAbstract || method.IsGenericMethod)
		{
			throw new NotSupportedException("No abstract or generic native detours: " + method);
		}
		CheckType(method.ReturnType, result: true);
		ParameterInfo[] parameters = method.GetParameters();
		for (int i = 0; i < parameters.Length; i++)
		{
			CheckType(parameters[i].ParameterType, result: false);
		}
		CustomAttributeData cached = method.GetCustomAttributesData().SingleOrDefault((CustomAttributeData a) => a.AttributeType.FullName == "Il2CppInterop.Common.Attributes.CachedScanResultsAttribute");
		if (cached == null)
		{
			throw new NotSupportedException("Native scan metadata missing: " + method);
		}
		long num = Field("XrefRangeStart");
		long num2 = Field("XrefRangeEnd");
		long num3 = Field("RefRangeStart");
		long num4 = Field("RefRangeEnd");
		if ((num == 0L && num2 == 0L) || (num == num3 && num2 == num4 && num2 - num >= 1024))
		{
			throw new NotSupportedException("Empty/shared native implementation is not a hook target: " + method);
		}
		long Field(string name)
		{
			return Convert.ToInt64(cached.NamedArguments.Single((CustomAttributeNamedArgument a) => a.MemberName == name).TypedValue.Value);
		}
	}

	private static void CheckType(Type type, bool result)
	{
		if (type == typeof(void) && result)
		{
			return;
		}
		if (type.IsByRef || type.IsPointer || type.ContainsGenericParameters)
		{
			throw new NotSupportedException("No byref/pointer native hook arguments or results: " + type);
		}
		Type type2 = type;
		while (type2 != null)
		{
			if (type2.FullName == "Il2CppSystem.ValueType")
			{
				throw new NotSupportedException("No boxed native-struct detours: " + type);
			}
			type2 = type2.BaseType;
		}
		if (!type.IsValueType || type.IsPrimitive || type.IsEnum || (type.FullName == "Core.Utils.UniqueId" && !result))
		{
			return;
		}
		throw new NotSupportedException("Value type not in the inspected hook allowlist: " + type);
	}
}
