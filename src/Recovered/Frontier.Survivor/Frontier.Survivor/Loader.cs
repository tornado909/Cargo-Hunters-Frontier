using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem;
using Il2CppSystem.Collections.Generic;
using Il2CppSystem.Threading.Tasks;
using SickscoreGames.HUDNavigationSystem;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Frontier.Survivor;

internal static class Loader
{
	internal static Scene Scene;

	private static int releaseAt = -1;

	internal static NativeBuildBarrier Gate = null;

	private static TaskCompletionSource<Il2CppSystem.Object>? completion;

	private static LeaseBag leases = new LeaseBag();

	private static bool building;

	private static double started;

	private static int mainThread;

	private static NativeVisualLayer? visuals;

	private static NavigationLayer? navigation;

	private static readonly System.Collections.Generic.List<AsyncOperation> unloads = new System.Collections.Generic.List<AsyncOperation>();

	private static Texture2D? mapTexture;

	private static Sprite? mapSprite;

	private static HNSMapProfile? mapProfile;

	private static System.Collections.Generic.List<Scene> oldScenes = new System.Collections.Generic.List<Scene>();

	private static int sceneGeneration;

	private static bool waitingForTransition;

	private static LoadSceneMode requestedMode;

	private static readonly System.Collections.Generic.List<Scene> pendingTransitions = new System.Collections.Generic.List<Scene>();

	internal static bool InProgress
	{
		get
		{
			if (!building)
			{
				return Ready;
			}
			return true;
		}
	}

	internal static bool Ready { get; private set; }

	internal static bool Failed { get; private set; }

	internal static HNSSceneConfiguration? NavigationConfig { get; private set; }

	internal static void ScheduleRelease()
	{
		releaseAt = Time.frameCount + 2;
	}

	internal static void Preflight()
	{
		Repositories.Drain();
		if (!Repositories.Ensure())
		{
			throw new System.InvalidOperationException(Repositories.Reason);
		}
		Repositories.ValidateResolvedTemplates();
		if (File.ReadAllBytes(Path.Combine(Identity.Folder, "reserve-map.rgba")).Length != 1048576)
		{
			throw new InvalidDataException("Minimap texture length mismatch");
		}
		TaskCompletionSource<Il2CppSystem.Object> taskCompletionSource = new TaskCompletionSource<Il2CppSystem.Object>();
		using (new NativeRoot((Il2CppObjectBase)(object)taskCompletionSource))
		{
			if (!taskCompletionSource.TrySetResult(null))
			{
				throw new System.InvalidOperationException("Native scene task bridge failed");
			}
			if (NavMesh.GetSettingsCount() < 1)
			{
				throw new System.InvalidOperationException("No native NavMesh settings available");
			}
		}
	}

	internal static Task Begin(LoadSceneMode mode)
	{
		try
		{
			if (building && completion != null)
			{
				return completion.Task;
			}
			if (Ready && Scene.IsValid() && Scene.isLoaded)
			{
				return Task.CompletedTask;
			}
			if (WorldBridge.ActiveCount > 0)
			{
				throw new System.InvalidOperationException("Another Reserve world is still alive");
			}
			releaseAt = -1;
			Release();
			Preflight();
			mainThread = Thread.CurrentThread.ManagedThreadId;
			Gate = new NativeBuildBarrier(delegate
			{
				if (Thread.CurrentThread.ManagedThreadId != mainThread)
				{
					throw new System.InvalidOperationException("Scene operation is not on Unity thread");
				}
			});
			completion = new TaskCompletionSource<Il2CppSystem.Object>();
			leases.Keep(completion);
			building = true;
			Failed = false;
			started = Time.realtimeSinceStartupAsDouble;
			sceneGeneration++;
			Trace.Data["status"] = "BUILDING_RESERVE";
			Trace.Data["sceneGeneration"] = sceneGeneration;
			requestedMode = mode;
			waitingForTransition = true;
			pendingTransitions.Clear();
			for (int num = 0; num < SceneManager.sceneCount; num++)
			{
				Scene sceneAt = SceneManager.GetSceneAt(num);
				if (sceneAt.IsValid() && !sceneAt.isLoaded)
				{
					pendingTransitions.Add(sceneAt);
				}
			}
			Trace.Data["beginNativeTransitionSnapshot"] = SceneSnapshot();
			Trace.Data["loadStage"] = "WAIT_FOR_NATIVE_LOADING_SCENE";
			return completion.Task;
		}
		catch (System.Exception e)
		{
			return Fault(e);
		}
	}

	internal static void Tick()
	{
		Repositories.Drain();
		NavigationLayer.CollectRetired();
		if (releaseAt >= 0 && Time.frameCount >= releaseAt)
		{
			releaseAt = -1;
			Release();
		}
		Repositories.Ensure();
		if (!building || Failed)
		{
			return;
		}
		try
		{
			if (Time.realtimeSinceStartupAsDouble - started > 90.0)
			{
				throw new System.TimeoutException("Reserve scene preparation exceeded 90 seconds");
			}
			if (waitingForTransition)
			{
				foreach (Scene pendingTransition in pendingTransitions)
				{
					if (!pendingTransition.IsValid())
					{
						throw new System.InvalidOperationException("Native transition scene was cancelled before completion");
					}
					if (!pendingTransition.isLoaded)
					{
						return;
					}
				}
				waitingForTransition = false;
				pendingTransitions.Clear();
				CreateOwnScene(requestedMode);
			}
			else
			{
				if (unloads.Any((AsyncOperation x) => !x.isDone))
				{
					return;
				}
				if (!Scene.IsValid() || !Scene.isLoaded)
				{
					Trace.Data["failedSceneSnapshot"] = SceneSnapshot();
					throw new System.InvalidOperationException("Reserve scene is not valid or loaded after previous scene unload completed; construction stopped");
				}
				if (visuals == null)
				{
					Shader shader = ChooseShader();
					if (shader == null)
					{
						throw new System.InvalidOperationException("No supported renderer shader found");
					}
					Trace.Data["loadStage"] = "BUILD_VISUALS";
					visuals = new NativeVisualLayer(Content.Map, Gate, Vector3.zero, shader);
					SceneManager.MoveGameObjectToScene(visuals.Root, Scene);
					Trace.Data["loadStage"] = "BUILD_NAVIGATION";
					navigation = new NavigationLayer(Content.Map);
					Trace.Data["loadStage"] = "BUILD_MINIMAP";
					CreateMap();
					Trace.Data["status"] = "BAKING_NAVIGATION";
				}
				else if (navigation != null && navigation.Done)
				{
					Trace.Data["loadStage"] = "PUBLISH_NAVIGATION";
					navigation.Publish();
					visuals.Activate();
					Trace.Data["loadStage"] = "PRESENTATION";
					Presentation.Add(Scene, Menu.Font);
					SceneContract.ValidateActive(Scene);
					Ready = true;
					building = false;
					Trace.Data["status"] = "SCENE_READY_WAITING_FOR_NATIVE_WORLDS";
					Trace.Data["renderedSourceFingerprint"] = visuals.CollisionFingerprint;
					Trace.Data["navigationSourceFingerprint"] = navigation.Fingerprint;
					completion.TrySetResult(null);
					Trace.Save(force: true);
				}
			}
		}
		catch (System.Exception e)
		{
			Fail(e);
		}
	}

	private static void CreateOwnScene(LoadSceneMode mode)
	{
		Gate.Check();
		Trace.Data["beforeCreateSceneSnapshot"] = SceneSnapshot();
		for (int i = 0; i < SceneManager.sceneCount; i++)
		{
			Scene sceneAt = SceneManager.GetSceneAt(i);
			if (sceneAt.IsValid() && sceneAt.isLoaded && sceneAt.name != "DontDestroyOnLoad")
			{
				oldScenes.Add(sceneAt);
			}
		}
		Trace.Data["loadStage"] = "CREATE_SCENE";
		Scene = SceneManager.CreateScene("Frontier_Survivor", new CreateSceneParameters
		{
			m_LocalPhysicsMode = LocalPhysicsMode.None
		});
		Trace.Data["createdSceneHandle"] = Scene.handle;
		Trace.Data["afterCreateSceneSnapshot"] = SceneSnapshot();
		if (!Scene.IsValid() || !Scene.isLoaded)
		{
			throw new System.InvalidOperationException("Own runtime scene creation failed");
		}
		SceneManager.SetActiveScene(Scene);
		Trace.Data["requestedUnloadScenes"] = oldScenes.Select((Scene s) => new { s.handle, s.name }).ToArray();
		if (mode == LoadSceneMode.Single)
		{
			foreach (Scene oldScene in oldScenes)
			{
				if (oldScene.handle == Scene.handle)
				{
					throw new System.InvalidOperationException("New Reserve scene appeared in previous-scene unload list");
				}
				AsyncOperation asyncOperation = SceneManager.UnloadSceneAsync(oldScene);
				if (asyncOperation != null)
				{
					leases.Keep(asyncOperation);
					unloads.Add(asyncOperation);
				}
			}
		}
		oldScenes.Clear();
		Trace.Data["loadStage"] = "WAIT_FOR_PREVIOUS_SCENE_UNLOAD";
	}

	private static object[] SceneSnapshot()
	{
		int sceneCount = SceneManager.sceneCount;
		if (sceneCount < 0 || sceneCount > 32)
		{
			throw new System.InvalidOperationException("Unexpected native scene count");
		}
		System.Collections.Generic.List<object> list = new System.Collections.Generic.List<object>(sceneCount);
		for (int i = 0; i < sceneCount; i++)
		{
			Scene sceneAt = SceneManager.GetSceneAt(i);
			list.Add(new
			{
				handle = sceneAt.handle,
				name = sceneAt.name,
				loaded = sceneAt.isLoaded,
				valid = sceneAt.IsValid(),
				active = (SceneManager.GetActiveScene().handle == sceneAt.handle)
			});
		}
		return list.ToArray();
	}

	private static Shader? ChooseShader()
	{
		string[] array = new string[4] { "Universal Render Pipeline/Lit", "Standard", "HDRP/Lit", "Legacy Shaders/Diffuse" };
		foreach (string text in array)
		{
			Shader shader = Shader.Find(text);
			if (shader != null && shader.isSupported)
			{
				Trace.Data["selectedShader"] = text;
				return shader;
			}
		}
		return null;
	}

	private static void CreateMap()
	{
		byte[] array = File.ReadAllBytes(Path.Combine(Identity.Folder, "reserve-map.rgba"));
		mapTexture = new Texture2D(512, 512, TextureFormat.RGBA32, mipChain: false);
		mapTexture.name = "Reserve map";
		mapTexture.LoadRawTextureData((Il2CppStructArray<byte>)(array));
		mapTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
		mapTexture.wrapMode = TextureWrapMode.Clamp;
		mapTexture.filterMode = FilterMode.Bilinear;
		mapSprite = Sprite.Create(mapTexture, new Rect(0f, 0f, 512f, 512f), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
		mapSprite.name = "Reserve map";
		mapProfile = ScriptableObject.CreateInstance<HNSMapProfile>();
		mapProfile.name = "Frontier Reserve Map";
		mapProfile.CustomLayers = new Il2CppSystem.Collections.Generic.List<CustomLayer>();
		mapProfile.Init(mapSprite, new Vector2(512f, 512f), new Color(0.08f, 0.09f, 0.1f, 1f), new Bounds(new Vector3(0f, 3f, 0f), new Vector3(208f, 12f, 188f)));
		NavigationConfig = ScriptableObject.CreateInstance<HNSSceneConfiguration>();
		NavigationConfig.name = "Frontier Reserve Navigation";
		NavigationConfig.overrideMinimapSettings = true;
		NavigationConfig.useMinimap = true;
		NavigationConfig.minimapProfile = mapProfile;
		NavigationConfig.minimapRadius = 100f;
		NavigationConfig.minimapScale = 1f;
		NavigationConfig.showMinimapBounds = true;
		NavigationConfig.overrideRadarSettings = false;
		NavigationConfig.overrideCompassBarSettings = false;
		NavigationConfig.overrideIndicatorSettings = false;
		leases.Keep(mapTexture);
		leases.Keep(mapSprite);
		leases.Keep(mapProfile);
		leases.Keep(NavigationConfig);
	}

	internal static Task WaitForNavigation()
	{
		if (Ready && NavigationConfig != null)
		{
			return Task.CompletedTask;
		}
		if (completion != null)
		{
			return completion.Task;
		}
		return Fault(new System.InvalidOperationException("Reserve navigation requested before scene preparation"));
	}

	internal static Task Fault(System.Exception e)
	{
		Fail(e);
		if (completion != null && completion.Task.IsFaulted)
		{
			return completion.Task;
		}
		TaskCompletionSource<Il2CppSystem.Object> taskCompletionSource = new TaskCompletionSource<Il2CppSystem.Object>();
		leases.Keep(taskCompletionSource);
		taskCompletionSource.TrySetException(new Il2CppSystem.InvalidOperationException(e.GetBaseException().Message));
		return taskCompletionSource.Task;
	}

	internal static void Fail(System.Exception e)
	{
		if (!Failed)
		{
			Failed = true;
			Ready = false;
			building = false;
			Trace.Data["status"] = "FAILED_SCENE_PREPARATION";
			Trace.Error("scene preparation", e);
			if (completion != null && !completion.Task.IsCompleted)
			{
				completion.TrySetException(new Il2CppSystem.InvalidOperationException(e.GetBaseException().Message));
			}
		}
	}

	internal static void Release()
	{
		if (WorldBridge.ActiveCount <= 0)
		{
			waitingForTransition = false;
			pendingTransitions.Clear();
			Ready = false;
			building = false;
			Failed = false;
			if (completion != null && !completion.Task.IsCompleted)
			{
				completion.TrySetCanceled();
			}
			if (navigation != null)
			{
				navigation.Dispose();
				navigation = null;
			}
			if (visuals != null)
			{
				visuals.Dispose();
				visuals = null;
			}
			Presentation.Clear();
			if (NavigationConfig != null)
			{
				UnityEngine.Object.Destroy(NavigationConfig);
			}
			if (mapProfile != null)
			{
				UnityEngine.Object.Destroy(mapProfile);
			}
			if (mapSprite != null)
			{
				UnityEngine.Object.Destroy(mapSprite);
			}
			if (mapTexture != null)
			{
				UnityEngine.Object.Destroy(mapTexture);
			}
			NavigationConfig = null;
			mapProfile = null;
			mapSprite = null;
			mapTexture = null;
			unloads.Clear();
			oldScenes.Clear();
			leases.Dispose();
			leases = new LeaseBag();
			completion = null;
		}
	}
}
