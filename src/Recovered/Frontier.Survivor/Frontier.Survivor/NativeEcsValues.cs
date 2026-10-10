using System;
using System.Reflection;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppSystem;
using Unity.Entities;

namespace Frontier.Survivor;

internal static class NativeEcsValues
{
	private static class Setter<T> where T : new()
	{
		internal static readonly System.IntPtr Method = Resolve();

		private static System.IntPtr Resolve()
		{
			return (System.IntPtr)((typeof(EntityManager).GetNestedType("MethodInfoStoreGeneric_SetComponentData_Public_Void_Entity_T_0`1", BindingFlags.NonPublic) ?? throw new System.MissingMemberException("Native SetComponentData generic metadata")).MakeGenericType(typeof(T)).GetField("Pointer", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null) ?? throw new System.MissingFieldException("Native SetComponentData method pointer"));
		}
	}

	private static readonly System.IntPtr infoMethod = (System.IntPtr)(typeof(TypeManager).GetField("NativeMethodInfoPtr_GetTypeInfo_Public_Static_byref_TypeInfo_Int32_0", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) ?? throw new System.MissingFieldException("Installed TypeManager.GetTypeInfo metadata"));

	internal unsafe static TypeManager.TypeInfo TypeInfo(int index)
	{
		System.IntPtr* ptr = stackalloc System.IntPtr[1];
		*ptr = (System.IntPtr)(&index);
		System.IntPtr zero = System.IntPtr.Zero;
		System.IntPtr intPtr = IL2CPP.il2cpp_runtime_invoke(infoMethod, System.IntPtr.Zero, (void**)ptr, ref zero);
		Il2CppException.RaiseExceptionIfNecessary(zero);
		if (intPtr == System.IntPtr.Zero || IL2CPP.il2cpp_object_get_class(intPtr) != Il2CppClassPointerStore<TypeManager.TypeInfo>.NativeClassPtr)
		{
			throw new System.InvalidOperationException("TypeInfo return is not the expected boxed native value");
		}
		Il2CppSystem.Object obj = new Il2CppSystem.Object(intPtr);
		using (new NativeRoot((Il2CppObjectBase)(object)obj))
		{
			TypeManager.TypeInfo result = ((Il2CppObjectBase)obj).Unbox<TypeManager.TypeInfo>();
			if (result.TypeIndex != index || result.SizeInChunk < 0 || result.SizeInChunk > 16384)
			{
				throw new System.InvalidOperationException("Native TypeInfo identity or size is invalid");
			}
			return result;
		}
	}

	internal unsafe static void Write<T>(EntityManager manager, Entity entity, Il2CppSystem.Object boxed, int expectedSize) where T : new()
	{
		Il2CppSystem.Type type = Il2CppType.Of<T>();
		int typeIndex = TypeManager.GetTypeIndex(type);
		if (typeIndex == 0)
		{
			throw new System.InvalidOperationException("Native component is not registered");
		}
		using (new NativeRoot((Il2CppObjectBase)(object)boxed))
		{
			System.IntPtr intPtr = IL2CPP.il2cpp_object_get_class(((Il2CppObjectBase)boxed).Pointer);
			uint num = 0u;
			int num2 = IL2CPP.il2cpp_class_value_size(intPtr, ref num);
			TypeManager.TypeInfo typeInfo = TypeInfo(typeIndex);
			if (intPtr != Il2CppClassPointerStore<T>.NativeClassPtr || num2 != expectedSize || typeInfo.SizeInChunk != num2)
			{
				throw new System.InvalidOperationException("Component ABI mismatch: " + type.FullName + " expected=" + expectedSize + " boxed=" + num2 + " chunk=" + typeInfo.SizeInChunk);
			}
			Trace.Data["componentAbi:" + type.FullName] = new
			{
				index = typeIndex,
				boxedBytes = num2,
				chunkBytes = typeInfo.SizeInChunk,
				typeBytes = typeInfo.TypeSize,
				writer = "native SetComponentData with unboxed argument"
			};
			manager.AddComponent(entity, ComponentType.ReadWrite(typeIndex));
			System.IntPtr* ptr = stackalloc System.IntPtr[2];
			*ptr = (System.IntPtr)(&entity);
			ptr[1] = IL2CPP.il2cpp_object_unbox(((Il2CppObjectBase)boxed).Pointer);
			System.IntPtr zero = System.IntPtr.Zero;
			IL2CPP.il2cpp_runtime_invoke(Setter<T>.Method, (System.IntPtr)(&manager), (void**)ptr, ref zero);
			Il2CppException.RaiseExceptionIfNecessary(zero);
		}
	}
}
