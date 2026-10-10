using System;
using Game.SceneManagement;
using UnityEngine.SceneManagement;

namespace Frontier.Survivor;

internal static class SceneContract
{
	internal const string Resource = "Frontier_Survivor.unity";

	internal static bool OwnResource(string? name)
	{
		return string.Equals(name, "Frontier_Survivor.unity", StringComparison.Ordinal);
	}

	internal static void ValidateActive(UnityEngine.SceneManagement.Scene scene)
	{
		if (!scene.IsValid() || !scene.isLoaded || !Identity.Own(scene.name) || UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle != scene.handle)
		{
			throw new InvalidOperationException("Reserve is not the actual loaded active Unity scene");
		}
		bool flag = Game.SceneManagement.SceneManager.IsSceneAlreadyLoadedGlobally("Frontier_Survivor.unity");
		bool flag2 = Game.SceneManagement.SceneManager.IsSceneActivated("Frontier_Survivor.unity");
		Trace.Data["sceneActivationContract"] = new
		{
			logicalMap = "Frontier_Survivor",
			sceneResource = "Frontier_Survivor.unity",
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
