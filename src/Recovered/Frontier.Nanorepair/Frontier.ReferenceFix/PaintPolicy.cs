using System;

namespace Frontier.ReferenceFix;

internal static class PaintPolicy
{
	internal static bool IsRepaint(object? value)
	{
		if (value is Enum)
		{
			return Convert.ToInt32(value) == 7;
		}
		return false;
	}
}
