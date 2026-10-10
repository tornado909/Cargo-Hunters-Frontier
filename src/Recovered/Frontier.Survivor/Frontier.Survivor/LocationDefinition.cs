using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Frontier.Survivor;

public sealed class LocationDefinition
{
	public string Format { get; set; } = "";

	public string Revision { get; set; } = "";

	public string Name { get; set; } = "";

	public string Units { get; set; } = "";

	public string UpAxis { get; set; } = "";

	public AgentDef Agent { get; set; } = new AgentDef();

	public RoomDef[] Rooms { get; set; } = Array.Empty<RoomDef>();

	public LinkDef[] Links { get; set; } = Array.Empty<LinkDef>();

	public BoxDef[] Boxes { get; set; } = Array.Empty<BoxDef>();

	public Anchor[] Anchors { get; set; } = Array.Empty<Anchor>();

	public Dictionary<string, MaterialDef> Materials { get; set; } = new Dictionary<string, MaterialDef>();

	public static LocationDefinition Load(string path)
	{
		byte[] array = File.ReadAllBytes(path);
		if (array.Length > 8000000)
		{
			throw new InvalidDataException("Location exceeds 8 MB");
		}
		LocationDefinition? obj = JsonSerializer.Deserialize<LocationDefinition>(array, new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		}) ?? throw new InvalidDataException("Empty location");
		obj.Validate();
		return obj;
	}

	public void Validate()
	{
		if (Format != "frontier.survivor.location/1" || Units != "metres" || UpAxis != "Y")
		{
			throw new InvalidDataException("Unsupported location coordinates or format");
		}
		if (Boxes.Length < 1 || Boxes.Length > 12000 || Anchors.Length > 1024 || Rooms.Length < 1 || Rooms.Length > 128)
		{
			throw new InvalidDataException("Object budget exceeded");
		}
		if (!float.IsFinite(Agent.Radius) || Agent.Radius < 0.2f || Agent.Radius > 2f || !float.IsFinite(Agent.Height) || Agent.Height < 1f || Agent.Height > 5f)
		{
			throw new InvalidDataException("Invalid agent dimensions");
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		BoxDef[] boxes = Boxes;
		foreach (BoxDef boxDef in boxes)
		{
			if (string.IsNullOrWhiteSpace(boxDef.Id) || !hashSet.Add(boxDef.Id))
			{
				throw new InvalidDataException("Duplicate/empty box ID");
			}
			V3(boxDef.Center);
			V3(boxDef.Size);
			if (boxDef.Size.Any((float v) => v <= 0f || v > 1000f))
			{
				throw new InvalidDataException("Invalid box size: " + boxDef.Id);
			}
			if (!Materials.ContainsKey(boxDef.Material))
			{
				throw new InvalidDataException("Unknown material: " + boxDef.Material);
			}
		}
		hashSet.Clear();
		Anchor[] anchors = Anchors;
		foreach (Anchor a in anchors)
		{
			if (!hashSet.Add(a.Id))
			{
				throw new InvalidDataException("Duplicate anchor");
			}
			V3(a.Position);
			if (a.Yaw % 90 != 0 || !float.IsFinite(a.SpawnChance) || a.SpawnChance < 0f || a.SpawnChance > 1f)
			{
				throw new InvalidDataException("Invalid placement orientation/probability");
			}
			if (!Rooms.Any((RoomDef r) => r.Id == a.Room))
			{
				throw new InvalidDataException("Anchor room missing");
			}
			bool flag;
			switch (a.Kind)
			{
			case "player":
			case "enemy":
			case "loot":
			case "exit":
			case "patrol":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (!flag)
			{
				throw new InvalidDataException("Unknown anchor kind");
			}
		}
		if (Anchors.Count((Anchor anchor) => anchor.Kind == "exit") < 1 || Anchors.Count((Anchor anchor) => anchor.Kind == "player") < 1)
		{
			throw new InvalidDataException("No entry or exit");
		}
	}

	private static void V3(float[] v)
	{
		if (v.Length != 3 || v.Any((float x) => !float.IsFinite(x) || Math.Abs(x) > 10000f))
		{
			throw new InvalidDataException("Invalid vector");
		}
	}

	public string CollisionFingerprint()
	{
		using IncrementalHash incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
		foreach (BoxDef item in Boxes.Where((BoxDef b) => b.Solid).OrderBy<BoxDef, string>((BoxDef b) => b.Id, StringComparer.Ordinal))
		{
			incrementalHash.AppendData(Encoding.UTF8.GetBytes(item.Id + "\0"));
			foreach (float item2 in item.Center.Concat(item.Size))
			{
				incrementalHash.AppendData(BitConverter.GetBytes(item2));
			}
		}
		return Convert.ToHexString(incrementalHash.GetHashAndReset()).ToLowerInvariant();
	}
}
