using System;
using System.Runtime.CompilerServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

namespace Frontier.ReferenceFix;

internal static class NativeChecked
{
	internal static IntPtr Class(Type type)
	{
		RuntimeHelpers.RunClassConstructor(type.TypeHandle);
		IntPtr nativeClassPointer = Il2CppClassPointerStore.GetNativeClassPointer(type);
		if (nativeClassPointer == IntPtr.Zero)
		{
			throw new TypeLoadException("Native class unavailable: " + type.FullName);
		}
		return nativeClassPointer;
	}

	internal static bool Matches(object? value, Type expected)
	{
		Il2CppObjectBase val = (Il2CppObjectBase)((value is Il2CppObjectBase) ? value : null);
		if (val == null || val.WasCollected || val.Pointer == IntPtr.Zero)
		{
			return false;
		}
		IntPtr intPtr = IL2CPP.il2cpp_object_get_class(val.Pointer);
		if (intPtr != IntPtr.Zero)
		{
			return IL2CPP.il2cpp_class_is_assignable_from(Class(expected), intPtr);
		}
		return false;
	}

	internal static object? As(object? value, Type expected)
	{
		if (!Matches(value, expected))
		{
			return null;
		}
		return R.ConvertFor(value, expected);
	}
}
