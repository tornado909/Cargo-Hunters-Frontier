using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using Il2CppInterop.Runtime;
using Unity.Entities;

namespace Frontier.Loot;

internal static class EcsAccess
{
	internal readonly record struct SpanMap(int Managed, int Native, int Bytes, string Path);

	private sealed class Map
	{
		internal int Index;

		internal int NativeSize;

		internal int ManagedSize;

		internal readonly List<SpanMap> Fields = new List<SpanMap>();
	}

	private static readonly Dictionary<Type, Map> Maps = new Dictionary<Type, Map>();

	private static readonly Dictionary<(Type, IntPtr), int> Headers = new Dictionary<(Type, IntPtr), int>();

	private static readonly Dictionary<Type, (int Index, IntPtr Class)> Types = new Dictionary<Type, (int, IntPtr)>();

	private static readonly Dictionary<string, object> Evidence = new Dictionary<string, object>();

	private static readonly Dictionary<string, string[]> ReadPaths = new Dictionary<string, string[]>
	{
		["Game.UsableItemSystem.Components.UsableItem"] = new string[3] { "OwnerPlayerId", "ItemId", "IsActive" },
		["Game.Weapons.Components.WeaponCharacteristicsOverride"] = new string[1] { "IsInfiniteMagazineEnabled" },
		["Game.Weapons.Components.WeaponCharacteristics"] = new string[1] { "ShotDuration" },
		["Game.Weapons.WeaponPredictedState"] = new string[2] { "AmmoInClip", "Random.state" },
		["Game.Weapons.WeaponInterpolatedState"] = new string[1] { "FireTick" },
		["Game.ProjectilesSystem.ProjectileSpawnRequest"] = new string[13]
		{
			"OwnerId", "SpawnerId", "Tick", "DamageData.InstantKill", "DamageData.IsMeleeDamage", "DamageData.IsSpreadDamage", "DamageData.Damage", "DamageData.MinDamage", "DamageData.ArmorDamage", "DamageData.MinArmorDamage",
			"DamageData.ArmorPenetratedDamage", "DamageData.MinArmorPenetratedDamage", "DamageData.ArmorPiercingClass"
		},
		["Game.DamageSystem.DamageEvent"] = new string[11]
		{
			"Assaulter", "Target", "AssaultingItemId", "ProjectileId", "DamageData.InstantKill", "DamageData.IsMeleeDamage", "DamageData.IsSpreadDamage", "BodyPartId", "ArmorHit", "ArmorIndex",
			"IsResistedByArmor"
		},
		["Game.Network.PlayerId"] = new string[1] { "Value" },
		["Game.DamageSystem.BodyPart"] = new string[1] { "Type" }
	};

	internal static Dictionary<string, object> Report()
	{
		return new Dictionary<string, object>(Evidence);
	}

	private static (int Index, IntPtr Class) NativeType<T>() where T : unmanaged
	{
		Type typeFromHandle = typeof(T);
		if (Types.TryGetValue(typeFromHandle, out (int, IntPtr) value))
		{
			return value;
		}
		RuntimeHelpers.RunClassConstructor(typeFromHandle.TypeHandle);
		IntPtr nativeClassPtr = Il2CppClassPointerStore<T>.NativeClassPtr;
		if (nativeClassPtr == IntPtr.Zero || !IL2CPP.il2cpp_class_is_valuetype(nativeClassPtr))
		{
			throw new InvalidOperationException("Missing native value type: " + typeFromHandle.FullName);
		}
		int typeIndex = TypeManager.GetTypeIndex(Il2CppType.Of<T>());
		if (typeIndex == 0)
		{
			throw new InvalidOperationException("Unregistered ECS type: " + typeFromHandle.FullName);
		}
		value = (typeIndex, nativeClassPtr);
		Types.Add(typeFromHandle, value);
		return value;
	}

	internal static bool Has<T>(EntityManager em, Entity e) where T : unmanaged
	{
		if (!em.Exists(e))
		{
			return false;
		}
		ComponentType type = new ComponentType
		{
			TypeIndex = NativeType<T>().Index,
			AccessModeType = ComponentType.AccessMode.ReadOnly
		};
		return em.HasComponent(e, type);
	}

	private unsafe static Map GetMap<T>() where T : unmanaged
	{
		//IL_02bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c6: Expected O, but got Unknown
		Type typeFromHandle = typeof(T);
		if (Maps.TryGetValue(typeFromHandle, out Map value))
		{
			return value;
		}
		(int, IntPtr) tuple = NativeType<T>();
		uint num = 0u;
		int num2 = IL2CPP.il2cpp_class_value_size(tuple.Item2, ref num);
		if (num2 <= 0 || num2 > 16384)
		{
			throw new InvalidOperationException("Invalid native payload size for " + typeFromHandle.FullName + ": " + num2);
		}
		if (!ReadPaths.TryGetValue(typeFromHandle.FullName, out string[] value2))
		{
			throw new InvalidOperationException("No field access contract: " + typeFromHandle.FullName);
		}
		Map map = new Map
		{
			Index = tuple.Item1,
			NativeSize = num2,
			ManagedSize = sizeof(T)
		};
		string[] array = value2;
		foreach (string text in array)
		{
			Resolve(map, typeFromHandle, tuple.Item2, 0, 0, text.Split('.'), 0, "", 0);
		}
		foreach (SpanMap field in map.Fields)
		{
			if (field.Managed < 0 || field.Native < 0 || field.Managed + field.Bytes > map.ManagedSize || field.Native + field.Bytes > num2)
			{
				throw new InvalidOperationException($"Field outside component payload: {typeFromHandle.FullName}.{field.Path} CLR={field.Managed}+{field.Bytes}/{map.ManagedSize} native={field.Native}+{field.Bytes}/{num2}");
			}
		}
		Maps.Add(typeFromHandle, map);
		Evidence[typeFromHandle.FullName] = new
		{
			nativeSize = num2,
			managedSize = sizeof(T),
			typeIndex = tuple.Item1,
			fields = map.Fields.ConvertAll((SpanMap f) => new
			{
				path = f.Path,
				managed = f.Managed,
				native = f.Native,
				bytes = f.Bytes
			})
		};
		Runtime.Inc("ecsFieldMapsValidated", 1L);
		ManualLogSource log = Runtime.Log;
		bool flag = default(bool);
		BepInExInfoLogInterpolatedStringHandler val = new BepInExInfoLogInterpolatedStringHandler(44, 4, out flag);
		if (flag)
		{
			((BepInExLogInterpolatedStringHandler)val).AppendLiteral("Loot ECS field map: ");
			((BepInExLogInterpolatedStringHandler)val).AppendFormatted<string>(typeFromHandle.FullName);
			((BepInExLogInterpolatedStringHandler)val).AppendLiteral("; ");
			((BepInExLogInterpolatedStringHandler)val).AppendFormatted<int>(map.Fields.Count);
			((BepInExLogInterpolatedStringHandler)val).AppendLiteral(" leaves; native=");
			((BepInExLogInterpolatedStringHandler)val).AppendFormatted<int>(num2);
			((BepInExLogInterpolatedStringHandler)val).AppendLiteral(", CLR=");
			((BepInExLogInterpolatedStringHandler)val).AppendFormatted<int>(sizeof(T));
		}
		log.LogInfo(val);
		return map;
	}

	private static int Width(Type t)
	{
		if (t.IsEnum)
		{
			t = Enum.GetUnderlyingType(t);
		}
		if (t == typeof(bool) || t == typeof(byte) || t == typeof(sbyte))
		{
			return 1;
		}
		if (t == typeof(char) || t == typeof(short) || t == typeof(ushort))
		{
			return 2;
		}
		if (t == typeof(float) || t == typeof(int) || t == typeof(uint))
		{
			return 4;
		}
		if (t == typeof(double) || t == typeof(long) || t == typeof(ulong))
		{
			return 8;
		}
		if (t.IsPointer || t == typeof(IntPtr) || t == typeof(UIntPtr))
		{
			return IntPtr.Size;
		}
		return 0;
	}

	private static int NativeHeader(Type t, IntPtr k)
	{
		if (Headers.TryGetValue((t, k), out var value))
		{
			return value;
		}
		FieldInfo[] fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		foreach (FieldInfo fieldInfo in fields)
		{
			FieldOffsetAttribute customAttribute = fieldInfo.GetCustomAttribute<FieldOffsetAttribute>();
			if (customAttribute == null || customAttribute.Value != 0)
			{
				continue;
			}
			IntPtr intPtr = IL2CPP.il2cpp_class_get_field_from_name(k, fieldInfo.Name);
			if (!(intPtr == IntPtr.Zero))
			{
				int num = checked((int)IL2CPP.il2cpp_field_get_offset(intPtr));
				if (num != 0 && num != 2 * IntPtr.Size)
				{
					throw new InvalidOperationException("Unsupported native header probe: " + t.FullName + "." + fieldInfo.Name + "=" + num);
				}
				Headers[(t, k)] = num;
				return num;
			}
		}
		throw new InvalidOperationException("No offset-zero header probe: " + t.FullName);
	}

	private static void Resolve(Map map, Type mt, IntPtr nk, int mo, int no, string[] parts, int pos, string prefix, int depth)
	{
		if (depth > 12)
		{
			throw new InvalidOperationException("Nested field contract too deep: " + prefix);
		}
		FieldInfo[] fields = mt.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		foreach (FieldInfo fieldInfo in fields)
		{
			if (pos < parts.Length && fieldInfo.Name != parts[pos])
			{
				continue;
			}
			string text = ((prefix.Length == 0) ? fieldInfo.Name : (prefix + "." + fieldInfo.Name));
			FieldOffsetAttribute customAttribute = fieldInfo.GetCustomAttribute<FieldOffsetAttribute>();
			if (customAttribute == null)
			{
				throw new InvalidOperationException("Missing explicit CLR field offset: " + mt.FullName + "." + fieldInfo.Name);
			}
			IntPtr intPtr = IL2CPP.il2cpp_class_get_field_from_name(nk, fieldInfo.Name);
			if (intPtr == IntPtr.Zero)
			{
				throw new InvalidOperationException("Missing native field: " + mt.FullName + "." + fieldInfo.Name);
			}
			int num = checked((int)IL2CPP.il2cpp_field_get_offset(intPtr));
			int num2 = num - NativeHeader(mt, nk);
			if (num2 < 0)
			{
				throw new InvalidOperationException("Invalid boxed field offset: " + text + "=" + num);
			}
			IntPtr intPtr2 = IL2CPP.il2cpp_class_from_type(IL2CPP.il2cpp_field_get_type(intPtr));
			if (intPtr2 == IntPtr.Zero)
			{
				throw new InvalidOperationException("Missing field type: " + text);
			}
			int num3 = Width(fieldInfo.FieldType);
			uint num4 = 0u;
			int num5 = IL2CPP.il2cpp_class_value_size(intPtr2, ref num4);
			uint num6 = 0u;
			int num7 = IL2CPP.il2cpp_class_value_size(nk, ref num6);
			if (num2 + num5 > num7 || num5 <= 0)
			{
				throw new InvalidOperationException("Native child extent invalid: " + text);
			}
			if (num3 > 0)
			{
				if (pos + 1 < parts.Length)
				{
					throw new InvalidOperationException("Scalar inside field path: " + text);
				}
				if (num5 != num3)
				{
					throw new InvalidOperationException($"Native scalar width mismatch: {text} CLR={num3}, native={num5}");
				}
				SpanMap item = new SpanMap(mo + customAttribute.Value, no + num2, num3, text);
				if (!map.Fields.Contains(item))
				{
					map.Fields.Add(item);
				}
			}
			else
			{
				if (!fieldInfo.FieldType.IsValueType || !IL2CPP.il2cpp_class_is_valuetype(intPtr2))
				{
					throw new InvalidOperationException("Reference field not permitted: " + text);
				}
				Resolve(map, fieldInfo.FieldType, intPtr2, mo + customAttribute.Value, no + num2, parts, pos + 1, text, depth + 1);
			}
			if (pos < parts.Length)
			{
				return;
			}
		}
		if (pos < parts.Length)
		{
			throw new InvalidOperationException("CLR field path not found: " + mt.FullName + "." + parts[pos]);
		}
	}

	private unsafe static byte* Pointer<T>(EntityManager em, Entity e, Map map) where T : unmanaged
	{
		if (!Has<T>(em, e))
		{
			throw new InvalidOperationException("Component disappeared: " + typeof(T).FullName);
		}
		byte* componentDataRawRW = (byte*)em.GetComponentDataRawRW(e, map.Index);
		if (componentDataRawRW == null)
		{
			throw new InvalidOperationException("Null ECS payload: " + typeof(T).FullName);
		}
		return componentDataRawRW;
	}

	internal unsafe static T Read<T>(EntityManager em, Entity e) where T : unmanaged
	{
		Map map = GetMap<T>();
		byte* intPtr = Pointer<T>(em, e, map);
		T result = default(T);
		byte* ptr = (byte*)(&result);
		CopyFields((IntPtr)intPtr, (IntPtr)ptr, map.Fields, read: true);
		return result;
	}

	internal unsafe static void WriteFields<T>(EntityManager em, Entity e, T value, params string[] names) where T : unmanaged
	{
		Map map = GetMap<T>();
		List<SpanMap> list = new List<SpanMap>();
		foreach (string text in names)
		{
			bool flag = false;
			foreach (SpanMap field in map.Fields)
			{
				if (field.Path == text || field.Path.StartsWith(text + ".", StringComparison.Ordinal))
				{
					list.Add(field);
					flag = true;
				}
			}
			if (!flag)
			{
				throw new InvalidOperationException("Field not mapped for write: " + typeof(T).FullName + "." + text);
			}
		}
		byte* intPtr = Pointer<T>(em, e, map);
		byte* ptr = (byte*)(&value);
		CopyFields((IntPtr)intPtr, (IntPtr)ptr, list, read: false);
	}

	internal unsafe static void CopyFields(IntPtr native, IntPtr managed, List<SpanMap> fields, bool read)
	{
		if (native == IntPtr.Zero || managed == IntPtr.Zero)
		{
			throw new ArgumentException("Null test/storage pointer");
		}
		byte* ptr = (byte*)(void*)native;
		byte* ptr2 = (byte*)(void*)managed;
		foreach (SpanMap field in fields)
		{
			if (read)
			{
				Buffer.MemoryCopy(ptr + field.Native, ptr2 + field.Managed, field.Bytes, field.Bytes);
			}
			else
			{
				Buffer.MemoryCopy(ptr2 + field.Managed, ptr + field.Native, field.Bytes, field.Bytes);
			}
		}
	}
}
