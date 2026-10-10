using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontier.ReferenceFix;

internal static class TemplatePolicy
{
	internal static bool? Evaluate(Guid actual, IReadOnlyList<Guid> requirements, int policy, IReadOnlyDictionary<Guid, Guid> variants)
	{
		if (policy < 0 || policy > 2 || !variants.TryGetValue(actual, out var canonical) || !requirements.Contains(canonical))
		{
			return null;
		}
		return policy switch
		{
			0 => requirements.All(Match), 
			1 => requirements.Any(Match), 
			2 => !requirements.Any(Match), 
			_ => null, 
		};
		bool Match(Guid expected)
		{
			if (!(expected == actual))
			{
				return expected == canonical;
			}
			return true;
		}
	}
}
