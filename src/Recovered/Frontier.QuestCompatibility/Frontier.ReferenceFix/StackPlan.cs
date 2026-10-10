using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontier.ReferenceFix;

internal static class StackPlan
{
	internal static IEnumerable<(StackRow Source, StackRow Target)> Pairs(IEnumerable<StackRow> rows)
	{
		foreach (IGrouping<string, StackRow> item in (from x in rows
			where x.Quantity > 0 && x.Capacity > 1
			group x by x.Template).OrderBy<IGrouping<string, StackRow>, string>((IGrouping<string, StackRow> g) => g.Key, StringComparer.Ordinal))
		{
			StackRow[] candidates = item.OrderByDescending((StackRow x) => x.Quantity).ThenBy<StackRow, string>((StackRow x) => x.Id, StringComparer.Ordinal).ToArray();
			for (int i = 0; i < candidates.Length; i++)
			{
				StackRow target = candidates[i];
				if (target.Quantity < target.Capacity)
				{
					for (int j = candidates.Length - 1; j > i; j--)
					{
						yield return (Source: candidates[j], Target: target);
					}
				}
			}
		}
	}
}
