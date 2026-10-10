using System;
using Game.SceneManagement;
using UnityEngine.SceneManagement;

namespace Frontier.Reserve;

internal static class SceneContract
{
	internal const string Resource = "Frontier_Reserve.unity";

	internal static bool OwnResource(string? name)
	{
		return string.Equals(name, "Frontier_Reserve.unity", StringComparison.Ordinal);
	}

	internal static void ValidateActive(UnityEngine.SceneManagement.Scene scene)
	{
		if (!scene.IsValid() || !scene.isLoaded || !Identity.Own(scene.name) || UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle != scene.handle)
		{
			throw new InvalidOperationException("Reserve is not the actual loaded active Unity scene");
		}
		bool flag = Game.SceneManagement.SceneManager.IsSceneAlreadyLoadedGlobally("Frontier_Reserve.unity");
		bool flag2 = Game.SceneManagement.SceneManager.IsSceneActivated("Frontier_Reserve.unity");
		Trace.Data["sceneActivationContract"] = new
		{
			logicalMap = "Frontier_Reserve",
			sceneResource = "Frontier_Reserve.unity",
			unityName = scene.name,
			sceneHandle = scene.handle,
			nativeLoaded = flag,
			nativeActivated = flag2,
			nativePredicatesPatchedByReserve = false
		};
		if (!flag || !flag2)
		{
			throw new InvalidOperationException("Native scene-resource/activation contract is not satisfied");
		}
	}
}
