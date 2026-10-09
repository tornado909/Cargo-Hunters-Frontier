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
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Expected O, but got Unknown
		MagazineComponent component = item.GetComponent<MagazineComponent>();
		if (((component != null) ? component._data : null) == null || component._container == null)
		{
			return;
		}
		MagazineComponent component2 = model._template.GetComponent<MagazineComponent>();
		ReadonlyData val = ((component2 != null) ? component2._data : null);
		if (val == null || (((Il2CppObjectBase)component._data).Pointer == ((Il2CppObjectBase)val).Pointer && component._container._data != null && ((Il2CppArrayBase<LoadedCartridge>)(object)component._container._data).Length >= val.Capacity))
		{
			return;
		}
		int cartridgesLeft = component.CartridgesLeft;
		if (val.CaliberType != component.CaliberType || val.InsideWeaponOnlyMode != component.InsideWeaponOnlyMode || val.Capacity < cartridgesLeft || val.Capacity < 1 || val.Capacity > 4096)
		{
			throw new InvalidOperationException("Unsafe magazine structural refresh rejected");
		}
		ReadonlyData data = component._data;
		CartridgesContainer container = component._container;
		try
		{
			if (container._data == null || ((Il2CppArrayBase<LoadedCartridge>)(object)container._data).Length < val.Capacity)
			{
				List<Cartridge> cartridgesData = component.GetCartridgesData();
				CartridgesContainer val2 = new CartridgesContainer(val.Capacity);
				val2.UpdateWith(cartridgesData);
				if (!SameCartridges(container, val2, cartridgesLeft))
				{
					throw new InvalidOperationException("Cartridge data changed during capacity refresh");
				}
				component._container = val2;
			}
			component._data = val;
			if (component.CartridgesLeft != cartridgesLeft || !SameCartridges(container, component._container, cartridgesLeft) || component.Capacity != val.Capacity)
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

	private static bool SameCartridges(CartridgesContainer a, CartridgesContainer b, int count)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		if (a.Count != count || b.Count != count)
		{
			return false;
		}
		for (int i = 0; i < count; i++)
		{
			LoadedCartridge val = a.AtIndex(i);
			LoadedCartridge val2 = b.AtIndex(i);
			if (Runtime.Id(val.TemplateId) != Runtime.Id(val2.TemplateId) || val.Statuses != val2.Statuses || Math.Abs(val.Mass - val2.Mass) > 1E-06f)
			{
				return false;
			}
		}
		return true;
	}

	private static void Body(IItem item, Item model)
	{
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		BodyPartComponent component = item.GetComponent<BodyPartComponent>();
		if (((component != null) ? component._data : null) == null)
		{
			return;
		}
		BodyPartComponent component2 = model._template.GetComponent<BodyPartComponent>();
		ReadonlyData val = ((component2 != null) ? component2._data : null);
		if (val == null)
		{
			return;
		}
		BodyPartState bodyPartState = component.BodyPartState;
		if (((Il2CppObjectBase)component._data).Pointer == ((Il2CppObjectBase)val).Pointer && Math.Abs(bodyPartState.MaxHealth - val.MaxHealth) < 0.0001f)
		{
			return;
		}
		if (val.BodyPartType != component._data.BodyPartType || !float.IsFinite(val.MaxHealth) || val.MaxHealth <= 0f || !float.IsFinite(bodyPartState.Health) || bodyPartState.Health > val.MaxHealth)
		{
			throw new InvalidOperationException("Unsafe body maximum refresh rejected");
		}
		ReadonlyData data = component._data;
		try
		{
			BodyPartState val2 = bodyPartState;
			val2.MaxHealth = val.MaxHealth;
			component._data = val;
			component.Set(val2);
			BodyPartState bodyPartState2 = component.BodyPartState;
			if (Math.Abs(bodyPartState2.MaxHealth - val2.MaxHealth) > 0.02f || Math.Abs(bodyPartState2.Health - bodyPartState.Health) > 0.02f || bodyPartState2.Status != bodyPartState.Status || bodyPartState2.IsDismembered != bodyPartState.IsDismembered)
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
