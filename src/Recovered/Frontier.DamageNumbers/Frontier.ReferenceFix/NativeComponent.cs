using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;

namespace Frontier.ReferenceFix;

internal sealed class NativeComponent
{
	internal readonly Type Type;

	internal readonly int Size;

	internal readonly int Index;

	private readonly object componentType;

	private readonly Dictionary<string, (int Offset, int Width, string Native)> fields = new Dictionary<string, (int, int, string)>();

	private static readonly Type Manager = R.Type("Unity.Entities", "Unity.Entities.EntityManager");

	private static readonly MethodInfo Raw = R.Method(Manager, "GetComponentDataRawRW", 2);

	private static readonly MethodInfo Has = Manager.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Single((MethodInfo m) => m.Name == "HasComponent" && !m.IsGenericMethod && m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType.FullName == "Unity.Entities.ComponentType");

	internal NativeComponent(string assembly, string name, params string[] paths)
	{
		Type = R.Type(assembly, name);
		IntPtr intPtr = NativeChecked.Class(Type);
		uint num = 0u;
		if (!IL2CPP.il2cpp_class_is_valuetype(intPtr))
		{
			throw new InvalidOperationException("Not a native component value: " + name);
		}
		Size = IL2CPP.il2cpp_class_value_size(intPtr, ref num);
		if (Size <= 0 || Size > 4096)
		{
			throw new InvalidOperationException("Invalid component size " + name);
		}
		object obj = typeof(Il2CppType).GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Single((MethodInfo m) => m.Name == "Of" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0).MakeGenericMethod(Type)
			.Invoke(null, null);
		Index = R.Int(R.Call(R.Type("Unity.Entities", "Unity.Entities.TypeManager"), "GetTypeIndex", obj));
		if (Index == 0)
		{
			throw new InvalidOperationException("Unregistered component " + name);
		}
		componentType = Activator.CreateInstance(R.Type("Unity.Entities", "Unity.Entities.ComponentType"));
		R.Set(componentType, "TypeIndex", Index);
		R.Set(componentType, "AccessModeType", 0);
		foreach (string text in paths)
		{
			fields.Add(text, Resolve(Type, intPtr, text.Split('.'), 0, 0));
		}
	}

	private (int, int, string) Resolve(Type t, IntPtr k, string[] path, int depth, int basis)
	{
		FieldInfo fieldInfo = (from f in t.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			where !f.IsStatic
			select f).FirstOrDefault(delegate(FieldInfo f)
		{
			FieldOffsetAttribute? customAttribute = f.GetCustomAttribute<FieldOffsetAttribute>();
			return customAttribute != null && customAttribute.Value == 0;
		});
		if (fieldInfo == null)
		{
			throw new InvalidOperationException("No independent offset-zero probe: " + t.FullName);
		}
		IntPtr intPtr = IL2CPP.il2cpp_class_get_field_from_name(k, fieldInfo.Name);
		if (intPtr == IntPtr.Zero)
		{
			throw new MissingFieldException(fieldInfo.Name);
		}
		int num = checked((int)IL2CPP.il2cpp_field_get_offset(intPtr));
		if (num != 0 && num != 2 * IntPtr.Size)
		{
			throw new InvalidOperationException("Unknown boxed value layout");
		}
		FieldInfo fieldInfo2 = t.GetField(path[depth], BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new MissingFieldException(t.FullName, path[depth]);
		IntPtr intPtr2 = IL2CPP.il2cpp_class_get_field_from_name(k, fieldInfo2.Name);
		if (intPtr2 == IntPtr.Zero)
		{
			throw new MissingFieldException(fieldInfo2.Name);
		}
		int num2 = checked((int)IL2CPP.il2cpp_field_get_offset(intPtr2)) - num;
		IntPtr intPtr3 = IL2CPP.il2cpp_field_get_type(intPtr2);
		IntPtr intPtr4 = IL2CPP.il2cpp_class_from_type(intPtr3);
		uint num3 = 0u;
		int num4 = IL2CPP.il2cpp_class_value_size(intPtr4, ref num3);
		int num5 = IL2CPP.il2cpp_class_value_size(k, ref num3);
		if (num2 < 0 || num4 <= 0 || num2 + num4 > num5 || basis + num2 + num4 > Size)
		{
			throw new InvalidOperationException("Field extent invalid " + fieldInfo2.Name);
		}
		if (depth + 1 < path.Length)
		{
			return Resolve(fieldInfo2.FieldType, intPtr4, path, depth + 1, basis + num2);
		}
		string item = IL2CPP.il2cpp_type_get_name_(intPtr3) ?? "";
		Type type = (fieldInfo2.FieldType.IsEnum ? Enum.GetUnderlyingType(fieldInfo2.FieldType) : fieldInfo2.FieldType);
		int num6 = ((type == typeof(bool) || type == typeof(byte) || type == typeof(sbyte)) ? 1 : ((type == typeof(short) || type == typeof(ushort)) ? 2 : ((type == typeof(float) || type == typeof(int) || type == typeof(uint)) ? 4 : ((type == typeof(long) || type == typeof(ulong) || type == typeof(double)) ? 8 : 0))));
		if (num6 == 0 || num4 != num6)
		{
			throw new InvalidOperationException("Only explicitly sized scalar leaves are allowed: " + fieldInfo2.Name);
		}
		return (basis + num2, num4, item);
	}

	internal bool Exists(object manager, object entity)
	{
		if (R.Bool(R.Call(manager, "Exists", entity)))
		{
			return R.Bool(Has.Invoke(manager, new object[2] { entity, componentType }));
		}
		return false;
	}

	private unsafe IntPtr Address(object manager, object entity)
	{
		if (!Exists(manager, entity))
		{
			throw new InvalidOperationException("Component no longer exists: " + Type.FullName);
		}
		object obj = Raw.Invoke(manager, new object[2] { entity, Index });
		IntPtr intPtr = ((obj is IntPtr intPtr2) ? intPtr2 : ((IntPtr)Pointer.Unbox(obj)));
		if (intPtr == IntPtr.Zero)
		{
			throw new InvalidOperationException("Null component storage");
		}
		return intPtr;
	}

	internal byte[] Read(object manager, object entity)
	{
		byte[] array = new byte[Size];
		Marshal.Copy(Address(manager, entity), array, 0, Size);
		return array;
	}

	internal int Offset(string path)
	{
		return fields[path].Offset;
	}

	internal int Int(byte[] data, string path)
	{
		return BitConverter.ToInt32(data, Offset(path));
	}

	internal uint UInt(byte[] data, string path)
	{
		return BitConverter.ToUInt32(data, Offset(path));
	}

	internal short Short(byte[] data, string path)
	{
		return BitConverter.ToInt16(data, Offset(path));
	}

	internal ulong ULong(byte[] data, string path)
	{
		return BitConverter.ToUInt64(data, Offset(path));
	}

	internal float Float(byte[] data, string path)
	{
		return BitConverter.ToSingle(data, Offset(path));
	}

	internal bool Bool(byte[] data, string path)
	{
		return data[Offset(path)] != 0;
	}

	internal byte Byte(byte[] data, string path)
	{
		return data[Offset(path)];
	}

	internal object Entity(byte[] data, string path)
	{
		object? obj = Activator.CreateInstance(R.Type("Unity.Entities", "Unity.Entities.Entity"));
		R.Set(obj, "Index", Int(data, path + ".Index"));
		R.Set(obj, "Version", Int(data, path + ".Version"));
		return obj;
	}

	internal void WriteFloat(object manager, object entity, string path, float value)
	{
		(int, int, string) tuple = fields[path];
		if (tuple.Item2 != 4 || tuple.Item3 != "System.Single" || !float.IsFinite(value))
		{
			throw new InvalidOperationException("Invalid float write contract " + path);
		}
		Marshal.Copy(BitConverter.GetBytes(value), 0, Address(manager, entity) + tuple.Item1, 4);
	}

	internal unsafe object BoxExact(byte[] data)
	{
		if (Marshal.SizeOf(Type) != Size)
		{
			throw new InvalidOperationException("Wrapper size differs from native value");
		}
		foreach (FieldInfo item in from x in Type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			where !x.IsStatic
			select x)
		{
			if (!fields.TryGetValue(item.Name, out (int, int, string) value) || item.GetCustomAttribute<FieldOffsetAttribute>()?.Value != value.Item1)
			{
				throw new InvalidOperationException("Full-value boxing contract incomplete: " + item.Name);
			}
		}
		fixed (byte* ptr = data)
		{
			return Marshal.PtrToStructure((IntPtr)ptr, Type);
		}
	}

	internal object Report()
	{
		return new
		{
			type = Type.FullName,
			nativeSize = Size,
			typeIndex = Index,
			fields = Enumerable.ToDictionary(fields, (KeyValuePair<string, (int Offset, int Width, string Native)> x) => x.Key, (KeyValuePair<string, (int Offset, int Width, string Native)> x) => new
			{
				offset = x.Value.Offset,
				width = x.Value.Width,
				nativeType = x.Value.Native
			})
		};
	}
}
