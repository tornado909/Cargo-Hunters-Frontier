using System;
using UnityEngine;

namespace Frontier.Reserve;

public sealed class ReserveLoop : MonoBehaviour
{
	public ReserveLoop(IntPtr pointer)
		: base(pointer)
	{
	}

	public void Update()
	{
		try
		{
			Loader.Tick();
			WorldBridge.Tick();
			Trace.Save();
		}
		catch (Exception e)
		{
			Trace.Error("frame", e);
			Loader.Fail(e);
		}
	}
}
