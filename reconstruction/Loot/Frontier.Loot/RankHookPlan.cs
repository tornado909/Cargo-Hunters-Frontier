using System;
using System.Reflection;
using Core.Utils;
using Frontier.Safety;
using Game.AISystem;
using Game.Application.Server;
using Game.ItemsSystem;
using Game.ItemsSystem.Factories;
using Game.ItemsSystem.ItemComponents;
using Game.SpawnPointSystem.BotSpawnPoints.Data;
using Game.UI.Views;
using Il2CppSystem.Collections.Generic;

namespace Frontier.Loot;

internal static class RankHookPlan
{
	internal static MethodInfo Factory()
	{
		return NativeHookContract.Exact(typeof(ItemFactory), "CreateInstance", true, typeof(IItem), typeof(IItemTemplatesRepository), typeof(UniqueId), typeof(UniqueId));
	}

	internal static MethodInfo Modifiers()
	{
		return NativeHookContract.Exact(typeof(AttributeModifierComponent), "GetAttributeModificationSettings", false, typeof(AttributeModificationSettings), typeof(AttributeSourceActivationType), typeof(AttributeSourceStateType));
	}

	internal static MethodInfo Equipment()
	{
		return NativeHookContract.Exact(typeof(AiCharacterSpawnSystem), "GetPlayerEquipment", false, typeof(Equipment), typeof(int), typeof(BotPresetData), typeof(List<IItem>));
	}

	internal static MethodInfo Raid()
	{
		return NativeHookContract.Exact(typeof(ServerBattleState), "EnterBattleState", false, typeof(void));
	}

	internal static MethodInfo View(string name)
	{
		switch (name)
		{
		case "InitializeFrom":
			return NativeHookContract.Exact(typeof(ItemView), name, false, typeof(void), typeof(IItem), typeof(bool), typeof(bool));
		case "Clear":
		case "OnDisable":
			return NativeHookContract.Exact(typeof(ItemView), name, false, typeof(void));
		default:
			throw new ArgumentException("Unknown item-view hook", "name");
		}
	}
}
