using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontier.Reserve;

public sealed class NativeVisualLayer : IDisposable
{
	private sealed class MeshData
	{
		internal int Count;

		internal Vector3[] Positions = Array.Empty<Vector3>();

		internal Vector3[] Normals = Array.Empty<Vector3>();

		internal Vector2[] Uv = Array.Empty<Vector2>();

		internal Vector4[] Tangents = Array.Empty<Vector4>();

		internal Color[] Colors = Array.Empty<Color>();

		internal int[][] Indices = Array.Empty<int[]>();
	}

	private sealed class Batch
	{
		internal string Material = "";

		internal string Zone = "";

		internal readonly List<Vector3> Positions = new List<Vector3>();

		internal readonly List<Vector3> Normals = new List<Vector3>();

		internal readonly List<Vector2> Uv = new List<Vector2>();

		internal readonly List<Vector4> Tangents = new List<Vector4>();

		internal readonly List<Color> Colors = new List<Color>();

		internal readonly List<int> Indices = new List<int>();
	}

	private readonly NativeBuildBarrier barrier;

	private readonly LeaseBag roots = new LeaseBag();

	private readonly List<UnityEngine.Object> resources = new List<UnityEngine.Object>();

	private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>(StringComparer.Ordinal);

	private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);

	private readonly Dictionary<string, MeshData> data = new Dictionary<string, MeshData>(StringComparer.Ordinal);

	private readonly Dictionary<string, Batch> batches = new Dictionary<string, Batch>(StringComparer.Ordinal);

	private bool disposed;

	private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true
	};

	public GameObject Root { get; }

	public string CollisionFingerprint { get; }

	public int BoxCount { get; }

	private T Keep<T>(T value) where T : UnityEngine.Object
	{
		roots.Keep(value);
		resources.Add(value);
		return value;
	}

	private static T Read<T>(string path)
	{
		byte[] array = File.ReadAllBytes(path);
		if (array.Length > 8000000)
		{
			throw new InvalidDataException("Stock art manifest budget");
		}
		T val = JsonSerializer.Deserialize<T>(array, Json);
		if (val == null)
		{
			throw new InvalidDataException("Invalid stock art JSON");
		}
		return val;
	}

	private static void Vec(float[] v, int n)
	{
		if (v.Length != n || v.Any((float x) => !float.IsFinite(x) || Math.Abs(x) > 10000f))
		{
			throw new InvalidDataException("Invalid stock art vector");
		}
	}

	private static byte[] Bytes(string folder, StockFile entry)
	{
		string value = Path.GetFullPath(folder) + Path.DirectorySeparatorChar;
		string fullPath = Path.GetFullPath(Path.Combine(folder, entry.File));
		if (!fullPath.StartsWith(value, StringComparison.OrdinalIgnoreCase) || entry.Sha256.Length != 64)
		{
			throw new InvalidDataException("Invalid stock file reference");
		}
		byte[] array = File.ReadAllBytes(fullPath);
		if (array.Length > 32000000 || !string.Equals(Convert.ToHexString(SHA256.HashData(array)), entry.Sha256, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException("Stock art hash/budget mismatch: " + entry.Name);
		}
		return array;
	}

	public NativeVisualLayer(LocationDefinition def, NativeBuildBarrier gate, Vector3 origin, Shader verifiedShader)
	{
		barrier = gate;
		gate.Check();
		def.Validate();
		CollisionFingerprint = def.CollisionFingerprint();
		Root = new GameObject("Frontier.Reserve.NativeStockEnvironment");
		Root.SetActive(value: false);
		Root.transform.position = origin;
		try
		{
			string folder = Identity.Folder;
			StockManifest stockManifest = Read<StockManifest>(Path.Combine(folder, "stock-art.json"));
			StockLayout stockLayout = Read<StockLayout>(Path.Combine(Identity.Folder, "stock-layout.json"));
			if (stockManifest.Format != "frontier.native-art/1" || stockLayout.Format != "frontier.stock-layout/1" || stockLayout.CollisionFingerprint != CollisionFingerprint || stockLayout.Instances.Length < 1 || stockLayout.Instances.Length > 5000 || stockManifest.Meshes.Length > 128 || stockManifest.Textures.Length > 128 || stockManifest.Materials.Length > 128)
			{
				throw new InvalidDataException("Native art / gameplay geometry contract mismatch");
			}
			StockFile[] array = stockManifest.Textures;
			foreach (StockFile stockFile in array)
			{
				if (stockFile.Width < 1 || stockFile.Height < 1 || stockFile.Width > 4096 || stockFile.Height > 4096)
				{
					throw new InvalidDataException("Stock texture dimensions");
				}
				Texture2D texture2D = Keep(new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: true));
				texture2D.name = stockFile.Name;
				if (!texture2D.LoadImage((Il2CppStructArray<byte>)(Bytes(folder, stockFile)), markNonReadable: true) || texture2D.width != stockFile.Width || texture2D.height != stockFile.Height)
				{
					throw new InvalidDataException("Original texture decode failed: " + stockFile.Name);
				}
				texture2D.wrapMode = TextureWrapMode.Repeat;
				texture2D.filterMode = FilterMode.Trilinear;
				texture2D.anisoLevel = 4;
				textures.Add(stockFile.Id, texture2D);
			}
			StockMaterial[] array2 = stockManifest.Materials;
			foreach (StockMaterial stockMaterial in array2)
			{
				Shader shader = Shader.Find(stockMaterial.Shader);
				if (shader == null || !shader.isSupported)
				{
					throw new InvalidDataException("Original game shader unavailable: " + stockMaterial.Shader);
				}
				Material material = Keep(new Material(shader));
				material.name = "Reserve.Stock." + stockMaterial.Name;
				foreach (KeyValuePair<string, float> @float in stockMaterial.Floats)
				{
					if (material.HasProperty(@float.Key))
					{
						if (!float.IsFinite(@float.Value))
						{
							throw new InvalidDataException("Material float");
						}
						material.SetFloat(@float.Key, @float.Value);
					}
				}
				foreach (KeyValuePair<string, float[]> color in stockMaterial.Colors)
				{
					if (material.HasProperty(color.Key))
					{
						Vec(color.Value, 4);
						float[] value = color.Value;
						material.SetVector(color.Key, new Vector4(value[0], value[1], value[2], value[3]));
					}
				}
				StockTextureBinding[] array3 = stockMaterial.Textures;
				foreach (StockTextureBinding stockTextureBinding in array3)
				{
					if (material.HasProperty(stockTextureBinding.Property))
					{
						Vec(stockTextureBinding.Scale, 2);
						Vec(stockTextureBinding.Offset, 2);
						material.SetTexture(stockTextureBinding.Property, textures[stockTextureBinding.Texture]);
						material.SetTextureScale(stockTextureBinding.Property, new Vector2(stockTextureBinding.Scale[0], stockTextureBinding.Scale[1]));
						material.SetTextureOffset(stockTextureBinding.Property, new Vector2(stockTextureBinding.Offset[0], stockTextureBinding.Offset[1]));
					}
				}
				if (stockMaterial.RenderQueue >= 0)
				{
					material.renderQueue = stockMaterial.RenderQueue;
				}
				if (!string.IsNullOrWhiteSpace(stockMaterial.Keywords))
				{
					string[] array4 = stockMaterial.Keywords.Split(' ', StringSplitOptions.RemoveEmptyEntries);
					foreach (string keyword in array4)
					{
						material.EnableKeyword(keyword);
					}
				}
				materials.Add(stockMaterial.Id, material);
			}
			array = stockManifest.Meshes;
			foreach (StockFile stockFile2 in array)
			{
				data.Add(stockFile2.Id, Decode(Bytes(folder, stockFile2)));
			}
			Dictionary<string, StockPrefab> dictionary = stockManifest.Prefabs.ToDictionary<StockPrefab, string>((StockPrefab x) => x.Id, StringComparer.Ordinal);
			HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
			StockInstance[] instances = stockLayout.Instances;
			StockPrefab value2 = default(StockPrefab);
			foreach (StockInstance stockInstance in instances)
			{
				bool flag = !hashSet.Add(stockInstance.Id) || !dictionary.TryGetValue(stockInstance.Asset, out value2) || stockInstance.Yaw % 90 != 0;
				if (!flag)
				{
					int j = stockInstance.Pitch;
					bool flag2 = ((j == 0 || j == 180) ? true : false);
					flag = !flag2;
				}
				if (flag)
				{
					throw new InvalidDataException("Invalid stock placement identity/orientation");
				}
				Vec(stockInstance.Center, 3);
				Vec(stockInstance.Size, 3);
				Vec(value2.BoundsMin, 3);
				Vec(value2.BoundsMax, 3);
				Vec(value2.Size, 3);
				if (stockInstance.Size.Any((float x) => x <= 0f) || value2.Size.Any((float x) => x <= 0f))
				{
					throw new InvalidDataException("Invalid stock placement dimensions");
				}
				StockPart[] parts = value2.Parts;
				foreach (StockPart stockPart in parts)
				{
					MeshData meshData = data[stockPart.Mesh];
					if (stockPart.Materials.Length != meshData.Indices.Length)
					{
						throw new InvalidDataException("Original model material mapping differs");
					}
					for (int num = 0; num < stockPart.Materials.Length; num++)
					{
						Append(stockInstance, value2, meshData, num, stockPart.Materials[num]);
					}
				}
			}
			foreach (Batch value3 in batches.Values)
			{
				Flush(value3);
			}
			BoxCount = stockLayout.Instances.Length;
			Trace.Data["nativeStockArt"] = new
			{
				prefabModels = stockManifest.Prefabs.Length,
				originalMeshes = data.Count,
				originalTextures = textures.Count,
				originalMaterials = materials.Count,
				instances = BoxCount,
				renderBatches = batches.Count,
				visualPrimitiveFallbacks = 0,
				collisionFingerprint = CollisionFingerprint
			};
			data.Clear();
			batches.Clear();
		}
		catch
		{
			Dispose();
			throw;
		}
	}

	private static MeshData Decode(byte[] bytes)
	{
		BinaryReader r = new BinaryReader(new MemoryStream(bytes, writable: false));
		try
		{
			if (new string(r.ReadChars(4)) != "FNM1")
			{
				throw new InvalidDataException("Original mesh format");
			}
			int num;
			int num2;
			uint num3;
			MeshData meshData;
			checked
			{
				num = (int)r.ReadUInt32();
				num2 = (int)r.ReadUInt32();
				num3 = r.ReadUInt32();
				if (num < 1 || num > 60000 || num2 < 1 || num2 > 16 || (num3 & 3) != 3 || (num3 & 0xFFFFFFE0u) != 0)
				{
					throw new InvalidDataException("Original mesh budget/attributes");
				}
				meshData = new MeshData
				{
					Count = num,
					Positions = new Vector3[num],
					Normals = new Vector3[num],
					Uv = new Vector2[num],
					Tangents = new Vector4[num],
					Colors = new Color[num],
					Indices = new int[num2][]
				};
			}
			for (int i = 0; i < num; i++)
			{
				meshData.Positions[i] = new Vector3(F(), F(), F());
			}
			for (int j = 0; j < num; j++)
			{
				meshData.Normals[j] = new Vector3(F(), F(), F());
			}
			for (int k = 0; k < num; k++)
			{
				meshData.Uv[k] = new Vector2(F(), F());
			}
			for (int l = 0; l < num; l++)
			{
				meshData.Tangents[l] = (((num3 & 4) != 0) ? new Vector4(F(), F(), F(), F()) : new Vector4(1f, 0f, 0f, 1f));
			}
			for (int m = 0; m < num; m++)
			{
				meshData.Colors[m] = (((num3 & 8) != 0) ? new Color(F(), F(), F(), F()) : Color.white);
			}
			if ((num3 & 0x10) != 0)
			{
				for (int n = 0; n < num; n++)
				{
					F();
					F();
				}
			}
			for (int num4 = 0; num4 < num2; num4++)
			{
				int num5 = checked((int)r.ReadUInt32());
				if (num5 < 3 || num5 % 3 != 0 || num5 > 900000)
				{
					throw new InvalidDataException("Original indices budget");
				}
				int[] array = new int[num5];
				for (int num6 = 0; num6 < num5; num6++)
				{
					array[num6] = checked((int)r.ReadUInt32());
					if (array[num6] >= num)
					{
						throw new InvalidDataException("Original mesh index");
					}
				}
				meshData.Indices[num4] = array;
			}
			if (r.BaseStream.Position != bytes.Length)
			{
				throw new InvalidDataException("Trailing original mesh data");
			}
			return meshData;
		}
		finally
		{
			if (r != null)
			{
				((IDisposable)r).Dispose();
			}
		}
		float F()
		{
			float num7 = r.ReadSingle();
			if (!float.IsFinite(num7))
			{
				throw new InvalidDataException("Nonfinite original vertex");
			}
			return num7;
		}
	}

	private void Append(StockInstance a, StockPrefab p, MeshData mesh, int sub, string material)
	{
		if (!materials.ContainsKey(material))
		{
			throw new InvalidDataException("Unknown stock material");
		}
		string key = a.Zone + "|" + material;
		if (!batches.TryGetValue(key, out Batch value))
		{
			Dictionary<string, Batch> dictionary = batches;
			Batch obj = new Batch
			{
				Zone = a.Zone,
				Material = material
			};
			value = obj;
			dictionary.Add(key, obj);
		}
		if (value.Positions.Count + mesh.Count > 60000)
		{
			Flush(value);
			value.Positions.Clear();
			value.Normals.Clear();
			value.Uv.Clear();
			value.Tangents.Clear();
			value.Colors.Clear();
			value.Indices.Clear();
		}
		float num = a.Size[0] / p.Size[0];
		float num2 = a.Size[1] / p.Size[1];
		float num3 = a.Size[2] / p.Size[2];
		float num4 = (p.BoundsMin[0] + p.BoundsMax[0]) * 0.5f;
		float num5 = (p.BoundsMin[1] + p.BoundsMax[1]) * 0.5f;
		float num6 = (p.BoundsMin[2] + p.BoundsMax[2]) * 0.5f;
		int num7 = (a.Yaw % 360 + 360) % 360;
		float num8 = num7 switch
		{
			180 => -1, 
			0 => 1, 
			_ => 0, 
		};
		float num9 = num7 switch
		{
			270 => -1, 
			90 => 1, 
			_ => 0, 
		};
		int count = value.Positions.Count;
		for (int i = 0; i < mesh.Count; i++)
		{
			Vector3 vector = mesh.Positions[i];
			float num10 = (vector.x - num4) * num;
			float num11 = (vector.y - num5) * num2;
			float num12 = (vector.z - num6) * num3;
			float num13 = ((a.Pitch != 180) ? 1 : (-1));
			num11 *= num13;
			num12 *= num13;
			value.Positions.Add(new Vector3(a.Center[0] + num8 * num10 + num9 * num12, a.Center[1] + num11, a.Center[2] - num9 * num10 + num8 * num12));
			Vector3 vector2 = mesh.Normals[i];
			num10 = vector2.x / num;
			num11 = vector2.y / num2 * num13;
			num12 = vector2.z / num3 * num13;
			float num14 = MathF.Sqrt(num10 * num10 + num11 * num11 + num12 * num12);
			if (num14 < 1E-08f)
			{
				num14 = 1f;
			}
			value.Normals.Add(new Vector3((num8 * num10 + num9 * num12) / num14, num11 / num14, ((0f - num9) * num10 + num8 * num12) / num14));
			value.Uv.Add(mesh.Uv[i]);
			Vector4 vector3 = mesh.Tangents[i];
			num10 = vector3.x * num;
			num11 = vector3.y * num2 * num13;
			num12 = vector3.z * num3 * num13;
			num14 = MathF.Sqrt(num10 * num10 + num11 * num11 + num12 * num12);
			if (num14 < 1E-08f)
			{
				num14 = 1f;
			}
			value.Tangents.Add(new Vector4((num8 * num10 + num9 * num12) / num14, num11 / num14, ((0f - num9) * num10 + num8 * num12) / num14, vector3.w));
			value.Colors.Add(mesh.Colors[i]);
		}
		int[] array = mesh.Indices[sub];
		foreach (int num15 in array)
		{
			value.Indices.Add(count + num15);
		}
	}

	private void Flush(Batch batch)
	{
		if (batch.Indices.Count != 0)
		{
			Mesh mesh = Keep(new Mesh());
			mesh.name = "Reserve.StockBatch." + batch.Zone;
			mesh.vertices = (Il2CppStructArray<Vector3>)(batch.Positions.ToArray());
			mesh.normals = (Il2CppStructArray<Vector3>)(batch.Normals.ToArray());
			mesh.uv = (Il2CppStructArray<Vector2>)(batch.Uv.ToArray());
			mesh.tangents = (Il2CppStructArray<Vector4>)(batch.Tangents.ToArray());
			mesh.colors = (Il2CppStructArray<Color>)(batch.Colors.ToArray());
			mesh.triangles = (Il2CppStructArray<int>)(batch.Indices.ToArray());
			mesh.RecalculateBounds();
			GameObject gameObject = new GameObject(mesh.name);
			gameObject.transform.SetParent(Root.transform, worldPositionStays: false);
			gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
			MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
			meshRenderer.sharedMaterial = materials[batch.Material];
			meshRenderer.shadowCastingMode = ShadowCastingMode.On;
			meshRenderer.receiveShadows = true;
		}
	}

	public void Activate()
	{
		barrier.Check();
		if (disposed)
		{
			throw new ObjectDisposedException("NativeVisualLayer");
		}
		Root.SetActive(value: true);
	}

	public void Dispose()
	{
		if (disposed)
		{
			return;
		}
		barrier.Check();
		if (Root != null)
		{
			UnityEngine.Object.Destroy(Root);
		}
		foreach (UnityEngine.Object resource in resources)
		{
			if (resource != null)
			{
				UnityEngine.Object.Destroy(resource);
			}
		}
		resources.Clear();
		roots.Dispose();
		materials.Clear();
		textures.Clear();
		data.Clear();
		batches.Clear();
		disposed = true;
	}
}
