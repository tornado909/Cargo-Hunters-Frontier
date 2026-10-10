using System;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

namespace Frontier.Reserve;

internal sealed class NativeRoot : IDisposable
{
	private IntPtr handle;

	public NativeRoot(Il2CppObjectBase obj)
	{
		if (obj == null || obj.WasCollected)
		{
			throw new ArgumentException("Native object unavailable");
		}
		handle = IL2CPP.il2cpp_gchandle_new(obj.Pointer, false);
		if (handle == IntPtr.Zero)
		{
			throw new InvalidOperationException("Native root allocation failed");
		}
	}

	public void Dispose()
	{
		if (handle != IntPtr.Zero)
		{
			IL2CPP.il2cpp_gchandle_free(handle);
			handle = IntPtr.Zero;
		}
	}
}
