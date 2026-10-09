#nullable enable
using System;
using System.Reflection;
using Il2CppInterop.Runtime;
using Unity.Entities;
namespace Frontier.Reserve;

// RuntimeInvoke returns boxed values. Keep the installed interop assemblies unchanged.
internal static class NativeEcsValues
{
    static readonly IntPtr infoMethod = (IntPtr)(typeof(TypeManager).GetField(
        "NativeMethodInfoPtr_GetTypeInfo_Public_Static_byref_TypeInfo_Int32_0",
        BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null)
        ?? throw new MissingFieldException("Installed TypeManager.GetTypeInfo metadata"));
    internal static unsafe TypeManager.TypeInfo TypeInfo(int index)
    {
        IntPtr* args = stackalloc IntPtr[1]; args[0] = (IntPtr)(&index);
        IntPtr error = IntPtr.Zero;
        IntPtr result = IL2CPP.il2cpp_runtime_invoke(infoMethod, IntPtr.Zero, (void**)args, ref error);
        Il2CppException.RaiseExceptionIfNecessary(error);
        if (result == IntPtr.Zero || IL2CPP.il2cpp_object_get_class(result) != Il2CppClassPointerStore<TypeManager.TypeInfo>.NativeClassPtr)
            throw new InvalidOperationException("TypeInfo return is not the expected boxed native value");
        var value = new Il2CppSystem.Object(result);
        using var root = new NativeRoot(value);
        var info = value.Unbox<TypeManager.TypeInfo>();
        if (info.TypeIndex != index || info.SizeInChunk < 0 || info.SizeInChunk > 16384)
            throw new InvalidOperationException("Native TypeInfo identity or size is invalid");
        return info;
    }
    static class Setter<T> where T : new()
    {
        internal static readonly IntPtr Method = Resolve();
        static IntPtr Resolve()
        {
            var open = typeof(EntityManager).GetNestedType("MethodInfoStoreGeneric_SetComponentData_Public_Void_Entity_T_0`1", BindingFlags.NonPublic)
                ?? throw new MissingMemberException("Native SetComponentData generic metadata");
            var closed = open.MakeGenericType(typeof(T));
            return (IntPtr)(closed.GetField("Pointer", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
                ?? throw new MissingFieldException("Native SetComponentData method pointer"));
        }
    }
    internal static unsafe void Write<T>(EntityManager manager, Entity entity, Il2CppSystem.Object boxed, int expectedSize) where T : new()
    {
        var type = Il2CppType.Of<T>(); int index = TypeManager.GetTypeIndex(type);
        if (index == 0) throw new InvalidOperationException("Native component is not registered");
        using var root = new NativeRoot(boxed);
        var klass = IL2CPP.il2cpp_object_get_class(boxed.Pointer); uint alignment = 0;
        int size = IL2CPP.il2cpp_class_value_size(klass, ref alignment); var info = TypeInfo(index);
        if (klass != Il2CppClassPointerStore<T>.NativeClassPtr || size != expectedSize || info.SizeInChunk != size)
            throw new InvalidOperationException("Component ABI mismatch: " + type.FullName + " expected=" + expectedSize + " boxed=" + size + " chunk=" + info.SizeInChunk);
        Trace.Data["componentAbi:" + type.FullName] = new { index, boxedBytes = size, chunkBytes = info.SizeInChunk, typeBytes = info.TypeSize, writer = "native SetComponentData with unboxed argument" };
        manager.AddComponent(entity, ComponentType.ReadWrite(index));
        IntPtr* args = stackalloc IntPtr[2]; args[0] = (IntPtr)(&entity); args[1] = IL2CPP.il2cpp_object_unbox(boxed.Pointer);
        IntPtr error = IntPtr.Zero;
        IL2CPP.il2cpp_runtime_invoke(Setter<T>.Method, (IntPtr)(&manager), (void**)args, ref error);
        Il2CppException.RaiseExceptionIfNecessary(error);
    }
}
