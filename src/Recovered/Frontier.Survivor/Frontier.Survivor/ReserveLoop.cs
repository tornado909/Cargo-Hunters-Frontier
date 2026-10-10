using System;
using UnityEngine;

namespace Frontier.Survivor;

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
			SurvivorRun.Tick();
			SurvivorHud.Tick();
			Trace.Save();
		}
		catch (Exception e)
		{
			Trace.Error("frame", e);
			Loader.Fail(e);
		}
	}
}
