using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;

namespace Frontier.ReferenceFix;

internal static class R
{
	internal const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

	private static readonly Dictionary<string, Type> Types = new Dictionary<string, Type>(StringComparer.Ordinal);

	private static readonly Dictionary<(Type, string), MemberInfo> Members = new Dictionary<(Type, string), MemberInfo>();

	internal static double Now => (double)Environment.TickCount64 / 1000.0;

	internal static Type Type(string assembly, string name)
	{
		string text = assembly + ":" + name;
		if (Types.TryGetValue(text, out Type value))
		{
			return value;
		}
		Assembly assembly2 = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault((Assembly x) => x.GetName().Name == assembly);
		if (assembly2 == null)
		{
			string text2 = Path.Combine(Paths.BepInExRootPath, "interop", assembly + ".dll");
			if (!File.Exists(text2))
			{
				throw new TypeLoadException(text);
			}
			assembly2 = Assembly.LoadFrom(text2);
		}
		return Types[text] = assembly2.GetType(name, throwOnError: true);
	}

	internal static MethodInfo Method(Type type, string name, int count)
	{
		MethodInfo[] array = (from m in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			where m.Name == name && !m.IsGenericMethod && m.GetParameters().Length == count
			select m).ToArray();
		if (array.Length != 1)
		{
			throw new MissingMethodException(type.FullName, name + "/" + count + " matches=" + array.Length);
		}
		return array[0];
	}

	internal static object? Get(object? obj, string name)
	{
		if (obj == null)
		{
			return null;
		}
		Type type = (obj as Type) ?? obj.GetType();
		if (!Members.TryGetValue((type, name), out MemberInfo value))
		{
			value = (MemberInfo)(((object)type.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault((PropertyInfo p) => p.Name == name && p.GetIndexParameters().Length == 0)) ?? ((object)(type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new MissingMemberException(type.FullName, name))));
			Members[(type, name)] = value;
		}
		object obj2 = ((obj is Type) ? null : obj);
		if (!(value is PropertyInfo propertyInfo))
		{
			return ((FieldInfo)value).GetValue(obj2);
		}
		return propertyInfo.GetValue(obj2);
	}

	internal static void Set(object obj, string name, object? value)
	{
		Type type = (obj as Type) ?? obj.GetType();
		PropertyInfo propertyInfo = type.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault((PropertyInfo x) => x.Name == name && x.GetIndexParameters().Length == 0);
		object obj2 = ((obj is Type) ? null : obj);
		if (propertyInfo != null)
		{
			propertyInfo.SetValue(obj2, ConvertFor(value, propertyInfo.PropertyType));
			return;
		}
		FieldInfo fieldInfo = type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new MissingMemberException(type.FullName, name);
		fieldInfo.SetValue(obj2, ConvertFor(value, fieldInfo.FieldType));
	}

	internal static object? ConvertFor(object? value, Type type)
	{
		if (type.IsByRef)
		{
			type = type.GetElementType();
		}
		if (value == null || type.IsInstanceOfType(value))
		{
			return value;
		}
		if (type.IsEnum)
		{
			return Enum.ToObject(type, Convert.ToInt32(value, CultureInfo.InvariantCulture));
		}
		if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(type))
		{
			return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
		}
		MethodInfo methodInfo = value.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault((MethodInfo m) => m.Name == "TryCast" && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 1 && m.GetParameters().Length == 0);
		if (methodInfo != null)
		{
			object obj = methodInfo.MakeGenericMethod(type).Invoke(value, null);
			if (obj != null)
			{
				return obj;
			}
		}
		throw new InvalidCastException(value.GetType().FullName + " -> " + type.FullName);
	}

	internal static object? Call(object obj, string name, params object?[] args)
	{
		Type type = (obj as Type) ?? obj.GetType();
		MethodInfo[] array = (from m in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			where m.Name == name && !m.IsGenericMethod && m.GetParameters().Length == args.Length
			select m).ToArray();
		MethodInfo methodInfo = null;
		object[] array2 = null;
		MethodInfo[] array3 = array;
		foreach (MethodInfo methodInfo2 in array3)
		{
			try
			{
				ParameterInfo[] parameters = methodInfo2.GetParameters();
				object[] array4 = new object[args.Length];
				for (int num2 = 0; num2 < array4.Length; num2++)
				{
					array4[num2] = ConvertFor(args[num2], parameters[num2].ParameterType);
				}
				if (methodInfo != null)
				{
					throw new AmbiguousMatchException(type.FullName + "." + name);
				}
				methodInfo = methodInfo2;
				array2 = array4;
			}
			catch (InvalidCastException)
			{
			}
			catch (ArgumentException)
			{
			}
		}
		if (methodInfo == null)
		{
			throw new MissingMethodException(type.FullName, name + "/" + args.Length);
		}
		object? result = methodInfo.Invoke((obj is Type) ? null : obj, array2);
		if (array2 != null)
		{
			Array.Copy(array2, args, args.Length);
		}
		return result;
	}

	internal static int Count(object? obj)
	{
		if (obj == null)
		{
			return 0;
		}
		if (obj is ICollection collection)
		{
			return collection.Count;
		}
		Type type = obj.GetType();
		PropertyInfo? obj2 = type.GetProperty("Count", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) ?? type.GetProperty("Length", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		if (obj2 == null)
		{
			throw new MissingMemberException(type.FullName, "Count/Length");
		}
		return Convert.ToInt32(obj2.GetValue(obj), CultureInfo.InvariantCulture);
	}

	internal static object? At(object list, int index)
	{
		if (list is IList list2)
		{
			return list2[index];
		}
		return list.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Single((PropertyInfo x) => x.Name == "Item" && x.GetIndexParameters().Length == 1 && x.GetIndexParameters()[0].ParameterType == typeof(int))
			.GetValue(list, new object[1] { index });
	}

	internal static void Put(object list, int index, object? value)
	{
		PropertyInfo propertyInfo = list.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Single((PropertyInfo x) => x.Name == "Item" && x.GetIndexParameters().Length == 1 && x.GetIndexParameters()[0].ParameterType == typeof(int));
		propertyInfo.SetValue(list, ConvertFor(value, propertyInfo.PropertyType), new object[1] { index });
	}

	internal static IEnumerable<object> Values(object? list, int limit = 4096)
	{
		if (list == null)
		{
			yield break;
		}
		int count = Count(list);
		if (count < 0 || count > limit)
		{
			throw new InvalidDataException("Collection exceeds bound: " + count);
		}
		for (int i = 0; i < count; i++)
		{
			object obj = At(list, i);
			if (obj != null)
			{
				yield return obj;
			}
		}
	}

	internal static long Pointer(object? obj)
	{
		if (obj != null)
		{
			return ((IntPtr)Get(obj, "Pointer")).ToInt64();
		}
		return 0L;
	}

	internal static string NativeName(object obj)
	{
		MethodInfo methodInfo = obj.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault((MethodInfo x) => x.Name == "GetIl2CppType" && x.GetParameters().Length == 0);
		if (methodInfo == null)
		{
			return obj.GetType().FullName ?? "";
		}
		return Get(methodInfo.Invoke(obj, null), "FullName")?.ToString() ?? "";
	}

	internal static bool Bool(object? x)
	{
		bool flag = default(bool);
		int num;
		if (x is bool)
		{
			flag = (bool)x;
			num = 1;
		}
		else
		{
			num = 0;
		}
		return (byte)((uint)num & (flag ? 1u : 0u)) != 0;
	}

	internal static int Int(object? x)
	{
		return Convert.ToInt32(x, CultureInfo.InvariantCulture);
	}

	internal static float Float(object? x)
	{
		return Convert.ToSingle(x, CultureInfo.InvariantCulture);
	}

	internal static string Key(object? x)
	{
		return x?.ToString() ?? "";
	}

	internal static void RequireFeature(string id)
	{
		Call(Type("Frontier.Modular", "Frontier.Modular.ModuleRuntime"), "AssertRegistered", id);
	}

	internal static void Hook(Harmony h, MethodInfo native, Type owner, string? prefix = null, string? postfix = null)
	{
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		if (native.GetParameters().Any((ParameterInfo p) => p.ParameterType.IsByRef || p.ParameterType.IsPointer || (p.ParameterType.IsValueType && !p.ParameterType.IsPrimitive && !p.ParameterType.IsEnum)))
		{
			throw new InvalidOperationException("Unsafe hook signature: " + native);
		}
		h.Patch((MethodBase)native, (prefix == null) ? ((HarmonyMethod)null) : new HarmonyMethod(owner.GetMethod(prefix, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)), (postfix == null) ? ((HarmonyMethod)null) : new HarmonyMethod(owner.GetMethod(postfix, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
	}
}
