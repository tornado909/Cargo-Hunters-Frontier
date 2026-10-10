using Game.ExitZoneSystem.ItemComponents;
using Game.InteractionSystem.ItemComponents;
using Game.ItemsSystem;
using Game.ItemsSystem.ItemComponents;
using Game.ItemsSystem.ItemFilters;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppSystem;

namespace Frontier.Reserve;

internal static class TemplateContract
{
	internal sealed record Inspection(string Id, string Visual, bool Simple, bool Split, bool Pickable, bool Exit, bool GeneralInteractable, bool HasLock, bool LockDataValid, bool OpenByDefault, string OpenFilter, int Priority, string? Rejection);

	internal static T? Component<T>(Item item) where T : Il2CppObjectBase
	{
		IItemComponent component = item.GetComponent(Il2CppType.Of<T>());
		if (component == null)
		{
			return default(T);
		}
		return ((Il2CppObjectBase)component).TryCast<T>();
	}

	internal static Inspection Inspect(Item item)
	{
		BaseComponent baseComponent = Component<BaseComponent>(item);
		string text = baseComponent?.VisualName ?? "";
		if (string.IsNullOrWhiteSpace(text))
		{
			text = baseComponent?.DroppedVisualName ?? "";
		}
		bool flag = Component<SimpleContainerComponent>(item) != null;
		bool flag2 = Component<SplittedContainerComponent>(item) != null;
		bool flag3 = Component<IsPickableComponent>(item) != null;
		bool flag4 = Component<ExitZoneComponent>(item) != null;
		bool generalInteractable = Component<InteractableComponent>(item) != null;
		LockComponent lockComponent = Component<LockComponent>(item);
		LockComponent.ReadonlyData readonlyData = lockComponent?._data;
		bool flag5 = lockComponent == null || readonlyData != null;
		bool flag6 = readonlyData?.IsOpenByDefault ?? false;
		object obj;
		if (readonlyData == null)
		{
			obj = null;
		}
		else
		{
			IItemsFilter openItemFilter = readonlyData.OpenItemFilter;
			obj = ((openItemFilter == null) ? null : ((Il2CppObjectBase)openItemFilter).TryCast<Object>()?.GetIl2CppType().FullName);
		}
		if (obj == null)
		{
			obj = "";
		}
		string openFilter = (string)obj;
		int num = text.Length;
		if (readonlyData != null)
		{
			if (!flag6)
			{
				num += 1000;
			}
			if (readonlyData.OpenItemFilter != null && ((Il2CppObjectBase)readonlyData.OpenItemFilter).TryCast<TrueFilter>() == null)
			{
				num += 20000;
			}
		}
		string rejection = ((!flag && !flag2) ? "NOT_A_CONTAINER" : (flag3 ? "PICKABLE_CONTAINER" : (flag4 ? "EXIT_TEMPLATE" : (string.IsNullOrWhiteSpace(text) ? "NO_VISUAL" : ((!flag5) ? "MISSING_LOCK_DATA" : null)))));
		return new Inspection(item.Id.ToString(), text, flag, flag2, flag3, flag4, generalInteractable, lockComponent != null, flag5, flag6, openFilter, flag5 ? num : int.MaxValue, rejection);
	}
}
