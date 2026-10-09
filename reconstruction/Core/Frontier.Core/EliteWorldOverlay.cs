using System;
using UnityEngine;

namespace Frontier.Core;

public sealed class EliteWorldOverlay : MonoBehaviour
{
	public EliteWorldOverlay(System.IntPtr pointer)
		: base(pointer)
	{
	}

	public void LateUpdate()
	{
		EliteWorldEffects.Tick();
	}

	public void OnDestroy()
	{
		EliteWorldEffects.Shutdown();
	}
}
