using System;
using Game.GlobalMap;
using Game.GlobalMap.UI;
using Game.GlobalMapSystem;
using Game.UI.Utils;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppSystem.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Frontier.Reserve;

internal static class Menu
{
	private static GlobalMapFakeController? ui;

	private static ReserveButton? button;

	private static GlobalMapLocationInformationController? adapter;

	private static GlobalMapLocation? target;

	internal static TMP_FontAsset? Font;

	private static bool changing;

	internal static void Locations(GlobalMapManager m)
	{
		if (m != null && !((Il2CppObjectBase)m).WasCollected)
		{
			Repositories.Register(m);
			EnsureLocation(m);
		}
	}

	internal static void Capture(GlobalMapFakeController owner)
	{
		if (owner == null || ((Il2CppObjectBase)owner).WasCollected)
		{
			return;
		}
		try
		{
			if (ui != null && ((Il2CppObjectBase)ui).Pointer != ((Il2CppObjectBase)owner).Pointer)
			{
				button?.Dispose();
				button = null;
				adapter = null;
				target = null;
			}
			ui = owner;
			if (owner._unitsCount != null)
			{
				Font = owner._unitsCount.font;
			}
			Repositories.Register(owner._globalMapManager);
		}
		catch (Exception e)
		{
			Trace.Error("menu capture", e);
		}
	}

	internal static void Tick(GlobalMapFakeController owner)
	{
		if (owner == null || ((Il2CppObjectBase)owner).WasCollected || changing)
		{
			return;
		}
		try
		{
			changing = true;
			Repositories.Drain();
			if (ui == null || ((Il2CppObjectBase)ui).Pointer != ((Il2CppObjectBase)owner).Pointer)
			{
				Capture(owner);
			}
			bool flag = owner._dataProvider != null && owner.IsVisible && owner.View != null && owner.View.IsVisible && owner.View.gameObject.activeInHierarchy;
			if (flag)
			{
				Repositories.Register(owner._globalMapManager);
				if (button == null || !button.Alive)
				{
					button?.Dispose();
					button = new ReserveButton(owner, Deploy);
					Trace.Data["directButtonCreated"] = true;
				}
			}
			if (adapter != null && !((Il2CppObjectBase)adapter).WasCollected && (bool)adapter.gameObject)
			{
				adapter.SetVisible(value: false);
			}
			button?.State(flag, Repositories.Ready && NativePrefabSources.Ready, owner._deployToMatchStarted || owner._isPrepareDeploy, Loader.Failed);
		}
		catch (Exception e)
		{
			Trace.Error("menu", e);
		}
		finally
		{
			changing = false;
		}
	}

	internal static void Close(GlobalMapFakeController owner)
	{
		if (!(ui == null) && !(owner == null) && !(((Il2CppObjectBase)ui).Pointer != ((Il2CppObjectBase)owner).Pointer))
		{
			button?.Dispose();
			button = null;
			adapter = null;
			target = null;
			ui = null;
		}
	}

	private static GlobalMapLocation? EnsureLocation(GlobalMapManager m)
	{
		if (!Repositories.Ready || Repositories.LastMap == null)
		{
			return null;
		}
		List<GlobalMapLocation> locations = m.GetLocations();
		if (locations == null || locations.Count > 256)
		{
			return null;
		}
		for (int i = 0; i < locations.Count; i++)
		{
			GlobalMapLocation globalMapLocation = locations[i];
			if (globalMapLocation != null && globalMapLocation.MapId == 3465004)
			{
				if (globalMapLocation.MapName != "Frontier_Reserve")
				{
					throw new InvalidOperationException("Reserve location identity conflict");
				}
				return globalMapLocation;
			}
		}
		GlobalMapLocation globalMapLocation2 = new GlobalMapLocation(Repositories.LastMap);
		globalMapLocation2.SetStatus(GlobalMapLocation.Status.Available);
		locations.Add(globalMapLocation2);
		Trace.Add("locationRegistrations");
		return globalMapLocation2;
	}

	private static void Deploy()
	{
		try
		{
			Trace.Add("deploymentClicks");
			if (ui == null || ((Il2CppObjectBase)ui).WasCollected || !ui.IsVisible || ui._isPrepareDeploy || ui._deployToMatchStarted)
			{
				return;
			}
			if (!Repositories.Ensure() || Repositories.LastMap == null)
			{
				Trace.Data["lastDeploy"] = Repositories.Reason;
				return;
			}
			NativePrefabSources.Require();
			Loader.Preflight();
			Repositories.RegisterScene(ui._globalMapManager._sceneManager);
			target = EnsureLocation(ui._globalMapManager) ?? throw new InvalidOperationException("Reserve deployment context not available");
			target.SetStatus(GlobalMapLocation.Status.Available);
			GenericListController<GlobalMapLocationInformationController> locationInformationList = ui._locationInformationList;
			if (locationInformationList == null || locationInformationList.Count > 256)
			{
				throw new InvalidOperationException("Native deploy adapter list unavailable");
			}
			if (adapter == null || ((Il2CppObjectBase)adapter).WasCollected || !adapter.gameObject)
			{
				for (int i = 0; i < locationInformationList.Count; i++)
				{
					GlobalMapLocationInformationController globalMapLocationInformationController = locationInformationList.Get(i);
					if ((object)globalMapLocationInformationController != null && globalMapLocationInformationController.GetMapId() == 3465004)
					{
						adapter = locationInformationList.Get(i);
					}
				}
				if (adapter == null)
				{
					int count = locationInformationList.Count;
					ui.AddLocationInformation(target, Vector3.zero);
					for (int j = count; j < locationInformationList.Count; j++)
					{
						GlobalMapLocationInformationController globalMapLocationInformationController2 = locationInformationList.Get(j);
						if ((object)globalMapLocationInformationController2 != null && globalMapLocationInformationController2.GetMapId() == 3465004)
						{
							adapter = locationInformationList.Get(j);
						}
					}
				}
			}
			if (adapter == null || adapter.GetMapName() != "Frontier_Reserve")
			{
				throw new InvalidOperationException("Reserve deployment adapter was not initialized");
			}
			adapter.SetVisible(value: false);
			GlobalMapFakeController.IDeployButtonController deployButtonController = ((Il2CppObjectBase)adapter).TryCast<GlobalMapFakeController.IDeployButtonController>();
			if (deployButtonController == null)
			{
				throw new InvalidOperationException("Native deployment interface unavailable");
			}
			ui.OnDeployButtonClicked(target, deployButtonController);
			Trace.Data["lastDeploy"] = "NATIVE_DEPLOY_HANDLER_CALLED";
			Trace.Add("nativeDeployCalls");
		}
		catch (Exception e)
		{
			Trace.Error("deploy", e);
		}
		finally
		{
			Trace.Save(force: true);
		}
	}
}
