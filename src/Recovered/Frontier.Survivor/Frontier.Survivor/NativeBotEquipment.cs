using System;
using System.Runtime.CompilerServices;
using Core.Utils;
using Game.SpawnPointSystem.BotSpawnPoints.Data;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppSystem;
using Il2CppSystem.Collections.Generic;

namespace Frontier.Survivor;

internal static class NativeBotEquipment
{
	private const int ValueBytes = 24;

	private static readonly System.IntPtr AddMethod = Method("Add");

	private static readonly System.IntPtr GetMethod = Method("get_Item");

	private static System.IntPtr Method(string name)
	{
		RuntimeHelpers.RunClassConstructor(typeof(List<BotSpawnPointData.LeveledEquipment>).TypeHandle);
		System.IntPtr nativeClassPtr = Il2CppClassPointerStore<List<BotSpawnPointData.LeveledEquipment>>.NativeClassPtr;
		if (nativeClassPtr == System.IntPtr.Zero)
		{
			throw new System.InvalidOperationException("Native equipment list class missing");
		}
		IL2CPP.il2cpp_runtime_class_init(nativeClassPtr);
		System.IntPtr intPtr = IL2CPP.il2cpp_class_get_method_from_name(nativeClassPtr, name, 1);
		if (intPtr == System.IntPtr.Zero)
		{
			throw new System.MissingMethodException("Native equipment list: " + name);
		}
		return intPtr;
	}

	private unsafe static void CheckBox(System.IntPtr boxed, UniqueId expected)
	{
		System.IntPtr nativeClassPtr = Il2CppClassPointerStore<BotSpawnPointData.LeveledEquipment>.NativeClassPtr;
		if (boxed == System.IntPtr.Zero || IL2CPP.il2cpp_object_get_class(boxed) != nativeClassPtr)
		{
			throw new System.InvalidOperationException("Equipment read-back is not the expected boxed value");
		}
		uint num = 0u;
		int num2 = IL2CPP.il2cpp_class_value_size(nativeClassPtr, ref num);
		System.IntPtr il2CppField = IL2CPP.GetIl2CppField(nativeClassPtr, "UnlockRequirement");
		System.IntPtr il2CppField2 = IL2CPP.GetIl2CppField(nativeClassPtr, "BotPresetId");
		if (System.IntPtr.Size != 8 || sizeof(UniqueId) != 16 || num2 != 24 || IL2CPP.il2cpp_field_get_offset(il2CppField) != 16 || IL2CPP.il2cpp_field_get_offset(il2CppField2) != 24)
		{
			throw new System.InvalidOperationException("Native equipment value layout changed");
		}
		byte* ptr = (byte*)(void*)IL2CPP.il2cpp_object_unbox(boxed);
		if (ptr == null || *(System.IntPtr*)ptr != System.IntPtr.Zero || *(UniqueId*)(ptr + 8) != expected || expected == UniqueId.Empty)
		{
			throw new System.InvalidOperationException("Native bot equipment payload differs from the declared preset");
		}
	}

	internal unsafe static List<BotSpawnPointData.LeveledEquipment> Create(UniqueId preset)
	{
		List<BotSpawnPointData.LeveledEquipment> list = new List<BotSpawnPointData.LeveledEquipment>();
		using (new NativeRoot((Il2CppObjectBase)(object)list))
		{
			BotSpawnPointData.LeveledEquipment leveledEquipment = new BotSpawnPointData.LeveledEquipment
			{
				UnlockRequirement = null,
				BotPresetId = preset
			};
			using (new NativeRoot((Il2CppObjectBase)(object)leveledEquipment))
			{
				CheckBox(((Il2CppObjectBase)leveledEquipment).Pointer, preset);
				if (list.Count != 0)
				{
					throw new System.InvalidOperationException("New equipment list is not empty");
				}
				System.IntPtr* ptr = stackalloc System.IntPtr[1];
				*ptr = IL2CPP.il2cpp_object_unbox(((Il2CppObjectBase)leveledEquipment).Pointer);
				System.IntPtr zero = System.IntPtr.Zero;
				IL2CPP.il2cpp_runtime_invoke(AddMethod, ((Il2CppObjectBase)list).Pointer, (void**)ptr, ref zero);
				Il2CppException.RaiseExceptionIfNecessary(zero);
				Validate(list, preset);
				Trace.Add("nativeBotEquipmentValuesWritten");
				return list;
			}
		}
	}

	internal unsafe static void Validate(List<BotSpawnPointData.LeveledEquipment> list, UniqueId preset)
	{
		if (list == null || ((Il2CppObjectBase)list).WasCollected || list.Count != 1)
		{
			throw new System.InvalidOperationException("Reserve bot equipment list must contain one preset");
		}
		using (new NativeRoot((Il2CppObjectBase)(object)list))
		{
			int num = 0;
			System.IntPtr* ptr = stackalloc System.IntPtr[1];
			*ptr = (System.IntPtr)(&num);
			System.IntPtr zero = System.IntPtr.Zero;
			System.IntPtr intPtr = IL2CPP.il2cpp_runtime_invoke(GetMethod, ((Il2CppObjectBase)list).Pointer, (void**)ptr, ref zero);
			Il2CppException.RaiseExceptionIfNecessary(zero);
			if (intPtr == System.IntPtr.Zero)
			{
				throw new System.InvalidOperationException("Native equipment list read-back returned null");
			}
			using (new NativeRoot((Il2CppObjectBase)(object)new Il2CppSystem.Object(intPtr)))
			{
				CheckBox(intPtr, preset);
				Trace.Add("nativeBotEquipmentReadbacks");
			}
		}
	}
}
