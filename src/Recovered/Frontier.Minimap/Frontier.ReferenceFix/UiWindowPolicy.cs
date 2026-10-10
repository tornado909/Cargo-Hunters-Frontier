using System.IO;

namespace Frontier.ReferenceFix;

internal static class UiWindowPolicy
{
	internal static int ActiveCount(int size, int capacity)
	{
		if (size < 0 || size > 128 || capacity < 0 || size > capacity)
		{
			throw new InvalidDataException("Invalid native modal stack bounds");
		}
		return size;
	}

	internal static bool IsBlocking(bool hidden, bool passiveHud, bool visible, bool activeHierarchy)
	{
		return !hidden && !passiveHud && visible && activeHierarchy;
	}
}
