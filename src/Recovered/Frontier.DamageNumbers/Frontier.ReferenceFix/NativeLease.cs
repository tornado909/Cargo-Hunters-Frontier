using System;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

namespace Frontier.ReferenceFix;

internal sealed class NativeLease : IDisposable
{
	private IntPtr handle;

	internal NativeLease(Il2CppObjectBase value)
	{
		if (value.WasCollected)
		{
			throw new InvalidOperationException("Cannot retain a collected native object");
		}
		handle = IL2CPP.il2cpp_gchandle_new(value.Pointer, false);
		if (handle == IntPtr.Zero)
		{
			throw new InvalidOperationException("Native root allocation failed");
		}
	}

	public void Dispose()
	{
		if (!(handle == IntPtr.Zero))
		{
			IntPtr intPtr = handle;
			handle = IntPtr.Zero;
			IL2CPP.il2cpp_gchandle_free(intPtr);
		}
	}
}
