using System;
using Game.DamageSystem;
using Game.ItemsSystem;
using Game.ItemsSystem.ItemComponents;
using Game.WeaponSystem.ItemComponents;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Collections.Generic;

namespace Frontier.Loot;

internal static class InstanceStructure
{
	[ThreadStatic]
	private static bool busy;

	internal static void Ensure(IItem item, Item model)
	{
		if (busy || model._template == null)
		{
			return;
		}
		try
		{
			busy = true;
			Magazine(item, model);
			Body(item, model);
		}
		finally
		{
			busy = false;
		}
	}

	private static void Magazine(IItem item, Item model)
	{
		MagazineComponent component = item.GetComponent<MagazineComponent>();
		if (component?._data == null || component._container == null)
		{
			return;
		}
		MagazineComponent.ReadonlyData readonlyData = model._template.GetComponent<MagazineComponent>()?._data;
		if (readonlyData == null || (((Il2CppObjectBase)component._data).Pointer == ((Il2CppObjectBase)readonlyData).Pointer && component._container._data != null && ((Il2CppArrayBase<MagazineComponent.LoadedCartridge>)(object)component._container._data).Length >= readonlyData.Capacity))
		{
			return;
		}
		int cartridgesLeft = component.CartridgesLeft;
		if (readonlyData.CaliberType != component.CaliberType || readonlyData.InsideWeaponOnlyMode != component.InsideWeaponOnlyMode || readonlyData.Capacity < cartridgesLeft || readonlyData.Capacity < 1 || readonlyData.Capacity > 4096)
		{
			throw new InvalidOperationException("Unsafe magazine structural refresh rejected");
		}
		MagazineComponent.ReadonlyData data = component._data;
		MagazineComponent.CartridgesContainer container = component._container;
		try
		{
			if (container._data == null || ((Il2CppArrayBase<MagazineComponent.LoadedCartridge>)(object)container._data).Length < readonlyData.Capacity)
			{
				List<MagazineComponent.Cartridge> cartridgesData = component.GetCartridgesData();
				MagazineComponent.CartridgesContainer cartridgesContainer = new MagazineComponent.CartridgesContainer(readonlyData.Capacity);
				cartridgesContainer.UpdateWith(cartridgesData);
				if (!SameCartridges(container, cartridgesContainer, cartridgesLeft))
				{
					throw new InvalidOperationException("Cartridge data changed during capacity refresh");
				}
				component._container = cartridgesContainer;
			}
			component._data = readonlyData;
			if (component.CartridgesLeft != cartridgesLeft || !SameCartridges(container, component._container, cartridgesLeft) || component.Capacity != readonlyData.Capacity)
			{
				throw new InvalidOperationException("Magazine refresh readback failed");
			}
			Runtime.Inc("rebalanceMagazineRefreshes", 1L);
		}
		catch
		{
			component._container = container;
			component._data = data;
			throw;
		}
	}

	private static bool SameCartridges(MagazineComponent.CartridgesContainer a, MagazineComponent.CartridgesContainer b, int count)
	{
		if (a.Count != count || b.Count != count)
		{
			return false;
		}
		for (int i = 0; i < count; i++)
		{
			MagazineComponent.LoadedCartridge loadedCartridge = a.AtIndex(i);
			MagazineComponent.LoadedCartridge loadedCartridge2 = b.AtIndex(i);
			if (Runtime.Id(loadedCartridge.TemplateId) != Runtime.Id(loadedCartridge2.TemplateId) || loadedCartridge.Statuses != loadedCartridge2.Statuses || Math.Abs(loadedCartridge.Mass - loadedCartridge2.Mass) > 1E-06f)
			{
				return false;
			}
		}
		return true;
	}

	private static void Body(IItem item, Item model)
	{
		BodyPartComponent component = item.GetComponent<BodyPartComponent>();
		if (component?._data == null)
		{
			return;
		}
		BodyPartComponent.ReadonlyData readonlyData = model._template.GetComponent<BodyPartComponent>()?._data;
		if (readonlyData == null)
		{
			return;
		}
		BodyPartState bodyPartState = component.BodyPartState;
		if (((Il2CppObjectBase)component._data).Pointer == ((Il2CppObjectBase)readonlyData).Pointer && Math.Abs(bodyPartState.MaxHealth - readonlyData.MaxHealth) < 0.0001f)
		{
			return;
		}
		if (readonlyData.BodyPartType != component._data.BodyPartType || !float.IsFinite(readonlyData.MaxHealth) || readonlyData.MaxHealth <= 0f || !float.IsFinite(bodyPartState.Health) || bodyPartState.Health > readonlyData.MaxHealth)
		{
			throw new InvalidOperationException("Unsafe body maximum refresh rejected");
		}
		BodyPartComponent.ReadonlyData data = component._data;
		try
		{
			BodyPartState bodyPartState2 = bodyPartState;
			bodyPartState2.MaxHealth = readonlyData.MaxHealth;
			component._data = readonlyData;
			component.Set(bodyPartState2);
			BodyPartState bodyPartState3 = component.BodyPartState;
			if (Math.Abs(bodyPartState3.MaxHealth - bodyPartState2.MaxHealth) > 0.02f || Math.Abs(bodyPartState3.Health - bodyPartState.Health) > 0.02f || bodyPartState3.Status != bodyPartState.Status || bodyPartState3.IsDismembered != bodyPartState.IsDismembered)
			{
				throw new InvalidOperationException("Body maximum refresh readback failed");
			}
			Runtime.Inc("rebalanceBodyMaximumRefreshes", 1L);
		}
		catch
		{
			component._data = data;
			component.Set(bodyPartState);
			throw;
		}
	}
}
