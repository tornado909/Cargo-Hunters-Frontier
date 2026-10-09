#nullable enable
using System;
using UnityEngine.SceneManagement;
using GameSceneManager = Game.SceneManagement.SceneManager;
namespace Frontier.Survivor;

// Logical map identity and scene-resource identity are distinct native contracts.
// The native activation loop compares MainScene.Name with activeScene.name + ".unity".
internal static class SceneContract
{
    internal const string Resource = Identity.Map + ".unity";
    internal static bool OwnResource(string? name) => string.Equals(name, Resource, StringComparison.Ordinal);
    internal static void ValidateActive(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || !Identity.Own(scene.name) ||
            SceneManager.GetActiveScene().handle != scene.handle)
            throw new InvalidOperationException("Reserve is not the actual loaded active Unity scene");
        bool loaded = GameSceneManager.IsSceneAlreadyLoadedGlobally(Resource);
        bool activated = GameSceneManager.IsSceneActivated(Resource);
        Trace.Data["sceneActivationContract"] = new {
            logicalMap = Identity.Map, sceneResource = Resource, unityName = scene.name,
            sceneHandle = scene.handle, nativeLoaded = loaded, nativeActivated = activated,
            nativePredicatesPatchedByReserve = false
        };
        if (!loaded || !activated)
            throw new InvalidOperationException("Native scene-resource/activation contract is not satisfied");
    }
}
