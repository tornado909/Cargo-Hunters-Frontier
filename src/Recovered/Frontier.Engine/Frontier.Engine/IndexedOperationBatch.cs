using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;

namespace Frontier.Engine;

internal sealed class IndexedOperationBatch
{
	private JsonArray? indexedArray;

	private string? indexedKey;

	private Dictionary<string, List<int>>? ids;

	private int indexedCount;

	internal JsonNode Root { get; private set; }

	internal int FullScans { get; private set; }

	internal int IndexedLookups { get; private set; }

	internal IndexedOperationBatch(JsonNode root)
	{
		Root = root;
	}

	private void Invalidate()
	{
		indexedArray = null;
		indexedKey = null;
		ids = null;
		indexedCount = 0;
	}

	private void EnsureIndex(JsonArray array, string key)
	{
		if (indexedArray == array && indexedKey == key && indexedCount == array.Count && ids != null)
		{
			return;
		}
		Dictionary<string, List<int>> dictionary = new Dictionary<string, List<int>>(StringComparer.Ordinal);
		for (int i = 0; i < array.Count; i++)
		{
			string key2 = PatchEngine.Scalar(array[i]?[key]);
			if (!dictionary.TryGetValue(key2, out var value))
			{
				dictionary.Add(key2, value = new List<int>(1));
			}
			value.Add(i);
		}
		indexedArray = array;
		indexedKey = key;
		indexedCount = array.Count;
		ids = dictionary;
		FullScans++;
	}

	internal JsonNode Apply(JsonObject operation)
	{
		string text = PatchEngine.Str(operation, "op");
		if (text == "root" || text == "append")
		{
			Invalidate();
			Root = PatchEngine.ApplyOperation(Root, operation);
			return Root;
		}
		if (!(Root is JsonArray jsonArray))
		{
			throw new InvalidDataException("Expected repository array");
		}
		string text2 = PatchEngine.Str(operation, "key");
		string text3 = PatchEngine.Scalar(operation["source"]);
		EnsureIndex(jsonArray, text2);
		IndexedLookups++;
		ids.TryGetValue(text3, out List<int> value);
		int num = value?.Count ?? 0;
		if (num != 1)
		{
			throw new InvalidDataException($"Expected one {text2}={text3}, got {num}");
		}
		int num2 = value[0];
		if (text == "clone")
		{
			string text4 = PatchEngine.Scalar(operation["id"]);
			if (ids.ContainsKey(text4))
			{
				throw new InvalidDataException("Duplicate clone " + text4);
			}
			JsonNode jsonNode = PatchEngine.ApplyEdits(PatchEngine.Copy(jsonArray[num2]), (JsonArray)operation["edits"]);
			if (PatchEngine.Scalar(jsonNode[text2]) != text4)
			{
				throw new InvalidDataException("Clone ID differs from declared ID");
			}
			int count = jsonArray.Count;
			jsonArray.Add(jsonNode);
			ids.Add(text4, new List<int>(1) { count });
			indexedCount = jsonArray.Count;
		}
		else
		{
			if (text != "edit")
			{
				throw new InvalidDataException("Unknown operation: " + text);
			}
			JsonNode jsonNode2 = (jsonArray[num2] = PatchEngine.ApplyEdits(PatchEngine.Copy(jsonArray[num2]), (JsonArray)operation["edits"]));
			string key;
			try
			{
				key = PatchEngine.Scalar(jsonNode2[text2]);
			}
			catch (InvalidDataException)
			{
				Invalidate();
				return Root;
			}
			catch (InvalidOperationException)
			{
				Invalidate();
				return Root;
			}
			value.Remove(num2);
			if (value.Count == 0)
			{
				ids.Remove(text3);
			}
			if (!ids.TryGetValue(key, out List<int> value2))
			{
				ids.Add(key, value2 = new List<int>(1));
			}
			value2.Add(num2);
		}
		return Root;
	}
}
