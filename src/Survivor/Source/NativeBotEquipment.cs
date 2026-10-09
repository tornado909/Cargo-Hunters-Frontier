#nullable enable
using System;
using System.Runtime.CompilerServices;
using Core.Utils;
using Il2CppInterop.Runtime;
using Equipment = Game.SpawnPointSystem.BotSpawnPoints.Data.BotSpawnPointData.LeveledEquipment;
using EquipmentList = Il2CppSystem.Collections.Generic.List<Game.SpawnPointSystem.BotSpawnPoints.Data.BotSpawnPointData.LeveledEquipment>;
namespace Frontier.Survivor;

// LeveledEquipment is a 24-byte NATIVE value type, not a native object reference.
// The shipped generic Add wrapper passes a boxed object header as its value.
// Use the same native List<T>.Add, with the actual unboxed argument; no detour.
internal static class NativeBotEquipment
{
    const int ValueBytes = 24;
    static readonly IntPtr AddMethod = Method("Add");
    static readonly IntPtr GetMethod = Method("get_Item");
    static IntPtr Method(string name)
    {
        RuntimeHelpers.RunClassConstructor(typeof(EquipmentList).TypeHandle);
        var klass = Il2CppClassPointerStore<EquipmentList>.NativeClassPtr;
        if (klass == IntPtr.Zero) throw new InvalidOperationException("Native equipment list class missing");
        IL2CPP.il2cpp_runtime_class_init(klass);
        var method = IL2CPP.il2cpp_class_get_method_from_name(klass, name, 1);
        if (method == IntPtr.Zero) throw new MissingMethodException("Native equipment list: " + name);
        return method;
    }
    static unsafe void CheckBox(IntPtr boxed, UniqueId expected)
    {
        var klass = Il2CppClassPointerStore<Equipment>.NativeClassPtr;
        if (boxed == IntPtr.Zero || IL2CPP.il2cpp_object_get_class(boxed) != klass)
            throw new InvalidOperationException("Equipment read-back is not the expected boxed value");
        uint alignment = 0;
        int bytes = IL2CPP.il2cpp_class_value_size(klass, ref alignment);
        var requirement = IL2CPP.GetIl2CppField(klass, "UnlockRequirement");
        var preset = IL2CPP.GetIl2CppField(klass, "BotPresetId");
        if (IntPtr.Size != 8 || sizeof(UniqueId) != 16 || bytes != ValueBytes ||
            IL2CPP.il2cpp_field_get_offset(requirement) != 16 || IL2CPP.il2cpp_field_get_offset(preset) != 24)
            throw new InvalidOperationException("Native equipment value layout changed");
        byte* value = (byte*)IL2CPP.il2cpp_object_unbox(boxed);
        // Check raw pointer before any object wrapper can follow corrupt metadata.
        if (value == null || *(IntPtr*)value != IntPtr.Zero || *(UniqueId*)(value + 8) != expected || expected == UniqueId.Empty)
            throw new InvalidOperationException("Native bot equipment payload differs from the declared preset");
    }
    internal static unsafe EquipmentList Create(UniqueId preset)
    {
        var list = new EquipmentList();
        using var listRoot = new NativeRoot(list);
        var value = new Equipment { UnlockRequirement = null, BotPresetId = preset };
        using var valueRoot = new NativeRoot(value);
        CheckBox(value.Pointer, preset);
        if (list.Count != 0) throw new InvalidOperationException("New equipment list is not empty");
        IntPtr* args = stackalloc IntPtr[1];
        args[0] = IL2CPP.il2cpp_object_unbox(value.Pointer);
        IntPtr error = IntPtr.Zero;
        IL2CPP.il2cpp_runtime_invoke(AddMethod, list.Pointer, (void**)args, ref error);
        Il2CppException.RaiseExceptionIfNecessary(error);
        Validate(list, preset);
        Trace.Add("nativeBotEquipmentValuesWritten");
        return list;
    }
    internal static unsafe void Validate(EquipmentList list, UniqueId preset)
    {
        if (list == null || list.WasCollected || list.Count != 1)
            throw new InvalidOperationException("Reserve bot equipment list must contain one preset");
        using var listRoot = new NativeRoot(list);
        int index = 0; IntPtr* args = stackalloc IntPtr[1]; args[0] = (IntPtr)(&index);
        IntPtr error = IntPtr.Zero;
        var result = IL2CPP.il2cpp_runtime_invoke(GetMethod, list.Pointer, (void**)args, ref error);
        Il2CppException.RaiseExceptionIfNecessary(error);
        if (result == IntPtr.Zero) throw new InvalidOperationException("Native equipment list read-back returned null");
        var boxed = new Il2CppSystem.Object(result);
        using var resultRoot = new NativeRoot(boxed);
        CheckBox(result, preset);
        Trace.Add("nativeBotEquipmentReadbacks");
    }
}
