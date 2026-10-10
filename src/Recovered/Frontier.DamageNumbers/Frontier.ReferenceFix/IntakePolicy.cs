using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontier.ReferenceFix;

internal static class IntakePolicy
{
	private const string P = "Game.ItemsSystem.ItemOperations.";

	internal static IntakeKind Classify(string n)
	{
		switch (n)
		{
		case "Game.ItemsSystem.ItemOperations.SortItemsContainerOperation":
			return IntakeKind.Sort;
		case "Game.ItemsSystem.ItemOperations.TakeItemsFromMailboxLetterOperationRequest":
			return IntakeKind.Reward;
		case "Game.QuestSystem.Operations.TakeQuestRewardOperationRequest":
			return IntakeKind.Reward;
		case "Game.ItemsSystem.ItemOperations.MoveItemsToInventoryOperationRequest":
			return IntakeKind.Manual;
		case "Game.ItemsSystem.ItemOperations.UnloadCartridgesContinuousOperation":
			return IntakeKind.Unload;
		case "Game.ItemsSystem.ItemOperations.AddItemOperation":
		case "Game.ItemsSystem.ItemOperations.MoveStackableItemOperation":
		case "Game.ItemsSystem.ItemOperations.SplitItemOperationRequest":
		case "Game.ItemsSystem.ItemOperations.SwapItemsOperation":
		case "Game.ItemsSystem.ItemOperations.RotateItemOperation":
		case "Game.ItemsSystem.ItemOperations.MoveItemOperation":
		case "Game.ItemsSystem.ItemOperations.RemoveItemOperation":
			return IntakeKind.Manual;
		default:
			if (n.StartsWith("Game.ItemsSystem.ItemOperations.Move", StringComparison.Ordinal) || n.StartsWith("Game.ItemsSystem.ItemOperations.Split", StringComparison.Ordinal) || n.StartsWith("Game.ItemsSystem.ItemOperations.Swap", StringComparison.Ordinal) || n.StartsWith("Game.ItemsSystem.ItemOperations.Rotate", StringComparison.Ordinal))
			{
				return IntakeKind.Manual;
			}
			return IntakeKind.Ignore;
		}
	}

	internal static bool CapacityCovers(IReadOnlyDictionary<string, long> needed, IReadOnlyDictionary<string, long> room)
	{
		long value;
		if (needed.Count > 0)
		{
			return needed.All<KeyValuePair<string, long>>((KeyValuePair<string, long> p) => p.Value > 0 && room.TryGetValue(p.Key, out value) && value >= p.Value);
		}
		return false;
	}
}
