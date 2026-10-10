#nullable enable
using System;
using Game.ItemsSystem;
using Game.ItemsSystem.ItemComponents;
using Game.ExitZoneSystem.ItemComponents;
using Game.InteractionSystem.ItemComponents;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

namespace Frontier.Survivor;

/// <summary>Structural container checks. Eligibility additionally requires a native container-group recipe in LootProfiles.</summary>
internal static class TemplateContract
{
    internal sealed record Inspection(string Id, string Visual, bool Simple, bool Split,
        bool Pickable, bool Exit, bool GeneralInteractable, bool HasLock,
        bool LockDataValid, bool OpenByDefault, string OpenFilter, int Priority, string? Rejection);

    internal static T? Component<T>(Item item) where T : Il2CppObjectBase => item.GetComponent(Il2CppType.Of<T>())?.TryCast<T>();

    internal static Inspection Inspect(Item item)
    {
        BaseComponent? baseData = Component<BaseComponent>(item);
        string visual = baseData?.VisualName ?? "";
        if (string.IsNullOrWhiteSpace(visual)) visual = baseData?.DroppedVisualName ?? "";
        bool simple = Component<SimpleContainerComponent>(item) != null;
        bool split = Component<SplittedContainerComponent>(item) != null;
        bool pickable = Component<IsPickableComponent>(item) != null;
        bool exit = Component<ExitZoneComponent>(item) != null;
        bool interactable = Component<InteractableComponent>(item) != null;
        LockComponent? itemLock = Component<LockComponent>(item);
        var data = itemLock?._data;
        bool valid = itemLock == null || data != null;
        bool open = data?.IsOpenByDefault ?? false;
        string filter = data?.OpenItemFilter?.TryCast<Il2CppSystem.Object>()?.GetIl2CppType().FullName ?? "";
        int priority = visual.Length;
        if (data != null)
        {
            if (!open) priority += 1000;
            if (data.OpenItemFilter != null && data.OpenItemFilter.TryCast<Game.ItemsSystem.ItemFilters.TrueFilter>() == null) priority += 20000;
        }
        string? rejection = !simple && !split ? "NOT_A_CONTAINER" : pickable ? "PICKABLE_CONTAINER" :
            exit ? "EXIT_TEMPLATE" :
            string.IsNullOrWhiteSpace(visual) ? "NO_VISUAL" : !valid ? "MISSING_LOCK_DATA" : null;
        return new Inspection(item.Id.ToString(), visual, simple, split, pickable, exit, interactable,
            itemLock != null, valid, open, filter, valid ? priority : int.MaxValue, rejection);
    }
}
