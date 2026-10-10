using System;
using Game.UI;
using Game.UI.Controllers;
using Game.UI.Views;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Collections.Generic;
using UnityEngine;

namespace Frontier.ReferenceFix;

internal static class NativeUiVisibility
{
	internal static bool Window(object? value)
	{
		Il2CppObjectBase val = (Il2CppObjectBase)((value is Il2CppObjectBase) ? value : null);
		if (val == null || val.WasCollected || val.Pointer == IntPtr.Zero)
		{
			return false;
		}
		AbstractController abstractController = val.TryCast<AbstractController>();
		if ((object)abstractController != null)
		{
			if (!abstractController.IsVisible)
			{
				return false;
			}
			BaseView view = abstractController.View;
			GameObject gameObject = (((object)view == null) ? abstractController.gameObject : view.gameObject);
			if (gameObject != null)
			{
				return gameObject.activeInHierarchy;
			}
			return false;
		}
		BaseView baseView = val.TryCast<BaseView>();
		if ((object)baseView == null || !baseView.IsVisible)
		{
			return false;
		}
		GameObject gameObject2 = baseView.gameObject;
		if (gameObject2 != null)
		{
			return gameObject2.activeInHierarchy;
		}
		return false;
	}

	internal static bool HasBlockingModal(object uiObject, long popup, long notices, long npc, Evidence report)
	{
		Stack<UiManager.Modal> modals = ((UiManager)R.ConvertFor(uiObject, typeof(UiManager)))._modals;
		if (modals == null)
		{
			report.Data["modalStackCount"] = 0;
			return false;
		}
		int size = modals._size;
		int version = modals._version;
		Il2CppArrayBase<UiManager.Modal> array = modals._array;
		int num = UiWindowPolicy.ActiveCount(size, array?.Length ?? 0);
		report.Data["modalStackCount"] = num;
		bool result = false;
		for (int num2 = num - 1; num2 >= 0; num2--)
		{
			UiManager.Modal modal = array[num2];
			if (modal != null && !((Il2CppObjectBase)modal).WasCollected && !modal.IsHidden)
			{
				AbstractController controller = modal.Controller;
				if ((object)controller != null && !((Il2CppObjectBase)controller).WasCollected)
				{
					long num3 = ((Il2CppObjectBase)controller).Pointer.ToInt64();
					bool flag = num3 != 0L && (num3 == popup || num3 == notices || num3 == npc);
					if (flag)
					{
						report.Add("passiveModalEntriesIgnored", 1L);
					}
					else
					{
						bool isVisible = controller.IsVisible;
						BaseView view = controller.View;
						GameObject gameObject = (((object)view == null) ? controller.gameObject : view.gameObject);
						bool activeHierarchy = gameObject != null && gameObject.activeInHierarchy;
						if (UiWindowPolicy.IsBlocking(hidden: false, flag, isVisible, activeHierarchy))
						{
							result = true;
							report.Data["blockingModalController"] = ((object)controller).GetType().FullName;
							break;
						}
					}
				}
			}
		}
		if (modals._version != version || modals._size != size)
		{
			throw new InvalidOperationException("Native modal stack changed during visibility snapshot");
		}
		return result;
	}
}
