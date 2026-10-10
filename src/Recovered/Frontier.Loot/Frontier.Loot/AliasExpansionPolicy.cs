using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;

namespace Frontier.Loot;

internal static class AliasExpansionPolicy
{
	internal readonly record struct Counts(long Leaves, long Added, long Normalized);

	internal static Counts Expand(JsonNode? root, JsonObject aliases)
	{
		if (root == null)
		{
			return default(Counts);
		}
		long num = 0L;
		long num2 = 0L;
		long num3 = 0L;
		Stack<JsonNode> stack = new Stack<JsonNode>();
		stack.Push(root);
		while (stack.Count > 0)
		{
			JsonNode jsonNode = stack.Pop();
			if (jsonNode is JsonObject jsonObject)
			{
				if (jsonObject["$t"] is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var value) && value == 23661 && jsonObject["_templateIds"] is JsonArray jsonArray)
				{
					num++;
					HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
					foreach (JsonNode item in jsonArray)
					{
						hashSet.Add(Scalar(item));
					}
					int count = hashSet.Count;
					string[] array = new string[count];
					hashSet.CopyTo(array);
					string[] array2 = array;
					foreach (string propertyName in array2)
					{
						if (!(aliases[propertyName] is JsonArray jsonArray2))
						{
							continue;
						}
						foreach (JsonNode item2 in jsonArray2)
						{
							if (hashSet.Add(Scalar(item2)))
							{
								jsonArray.Add((item2 == null) ? null : JsonNode.Parse(item2.ToJsonString()));
								num2++;
							}
						}
					}
					JsonNode? jsonNode2 = jsonObject["_filterPolicy"];
					if ((jsonNode2 == null || jsonNode2.GetValue<int>() == 0) && count == 1 && hashSet.Count > 1)
					{
						jsonObject["_filterPolicy"] = 1;
						num3++;
					}
				}
				foreach (KeyValuePair<string, JsonNode> item3 in jsonObject)
				{
					if (item3.Value != null)
					{
						stack.Push(item3.Value);
					}
				}
			}
			else
			{
				if (!(jsonNode is JsonArray jsonArray3))
				{
					continue;
				}
				foreach (JsonNode item4 in jsonArray3)
				{
					if (item4 != null)
					{
						stack.Push(item4);
					}
				}
			}
		}
		return new Counts(num, num2, num3);
	}

	private static string Scalar(JsonNode? n)
	{
		string value;
		return (n is JsonValue jsonValue && jsonValue.TryGetValue<string>(out value)) ? value : (n?.ToJsonString() ?? throw new InvalidDataException("Null ID"));
	}
}
