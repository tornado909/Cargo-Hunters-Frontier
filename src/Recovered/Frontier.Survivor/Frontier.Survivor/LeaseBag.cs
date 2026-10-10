using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes;

namespace Frontier.Survivor;

internal sealed class LeaseBag : IDisposable
{
	private readonly List<NativeRoot> roots = new List<NativeRoot>();

	internal T Keep<T>(T obj) where T : Il2CppObjectBase
	{
		roots.Add(new NativeRoot((Il2CppObjectBase)(object)obj));
		return obj;
	}

	public void Dispose()
	{
		foreach (NativeRoot root in roots)
		{
			root.Dispose();
		}
		roots.Clear();
	}
}
