using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace Frontier.ReferenceFix;

internal sealed class TerrainTexture
{
	private readonly Evidence report;

	private readonly Type texture = R.Type("UnityEngine.CoreModule", "UnityEngine.Texture2D");

	private readonly Type rt = R.Type("UnityEngine.CoreModule", "UnityEngine.RenderTexture");

	private readonly Type graphics = R.Type("UnityEngine.CoreModule", "UnityEngine.Graphics");

	private readonly Type rect = R.Type("UnityEngine.CoreModule", "UnityEngine.Rect");

	private readonly Type format = R.Type("UnityEngine.CoreModule", "UnityEngine.TextureFormat");

	private MapPixels? pixels;

	private object? output;

	private long sourceId;

	private int sourceW;

	private int sourceH;

	private int outputSize;

	private double nextRead;

	private double nextDraw;

	private byte[] buffer = Array.Empty<byte>();

	private float previousX = float.NaN;

	private float previousZ;

	private float previousHeading;

	private double lastUpload;

	internal TerrainTexture(Evidence e)
	{
		report = e;
	}

	internal void Clear()
	{
		Destroy(output);
		output = null;
		pixels = null;
		sourceId = 0L;
		sourceW = (sourceH = 0);
		previousX = float.NaN;
		nextRead = (nextDraw = 0.0);
		buffer = Array.Empty<byte>();
	}

	private void Destroy(object? obj)
	{
		if (obj == null)
		{
			return;
		}
		try
		{
			R.Call(R.Type("UnityEngine.CoreModule", "UnityEngine.Object"), "Destroy", obj);
		}
		catch (Exception e)
		{
			report.Error("texture cleanup", e);
		}
	}

	private object NewTexture(int w, int h)
	{
		return Activator.CreateInstance(texture, w, h, Enum.Parse(format, "RGBA32"), false);
	}

	private object Rect(float x, float y, float w, float h)
	{
		return Activator.CreateInstance(rect, x, y, w, h);
	}

	private void Capture(TerrainMap map)
	{
		object obj = null;
		object obj2 = null;
		object value = null;
		try
		{
			int num = R.Int(R.Get(map.Texture, "width"));
			int num2 = R.Int(R.Get(map.Texture, "height"));
			if (num < 2 || num2 < 2 || num > 32768 || num2 > 32768)
			{
				throw new InvalidOperationException("Map is a placeholder/invalid texture");
			}
			float num3 = Math.Min(1f, 2048f / (float)Math.Max(num, num2));
			int num4 = Math.Max(2, (int)((float)num * num3));
			int num5 = Math.Max(2, (int)((float)num2 * num3));
			value = R.Get(rt, "active");
			obj = R.Call(rt, "GetTemporary", num4, num5, 0);
			if (obj == null)
			{
				throw new InvalidOperationException("Map readback render texture unavailable");
			}
			R.Call(graphics, "Blit", map.Texture, obj);
			R.Set(rt, "active", obj);
			obj2 = NewTexture(num4, num5);
			R.Call(obj2, "ReadPixels", Rect(0f, 0f, num4, num5), 0, 0, false);
			R.Call(obj2, "Apply", false, false);
			byte[] array = ((R.Call(obj2, "GetRawTextureData") as Il2CppStructArray<byte>) ?? throw new InvalidOperationException("Unexpected native RGBA array")).AsSpan().ToArray();
			if (array.Length != num4 * num5 * 4)
			{
				throw new InvalidOperationException("Native RGBA byte count mismatch");
			}
			MapPixels p = new MapPixels(array, num4, num5);
			if (!TerrainRaster.HasDetail(p))
			{
				pixels = null;
				report.Data["rasterStatus"] = "SOURCE_TEXTURE_UNIFORM_OR_TRANSPARENT";
				report.Add("unreadyTextureSamples", 1L);
			}
			else
			{
				pixels = p;
				report.Data["rasterStatus"] = "NATIVE_TEXTURE_PIXELS_CAPTURED";
				report.Add("pixelCaptures", 1L);
			}
			sourceId = R.Pointer(map.Texture);
			sourceW = num;
			sourceH = num2;
			report.Data["pixelSource"] = new
			{
				textureName = R.Key(R.Get(map.Texture, "name")),
				width = num,
				height = num2,
				readbackWidth = num4,
				readbackHeight = num5,
				rgbaBytes = array.Length,
				hasDetail = (pixels != null)
			};
		}
		finally
		{
			R.Set(rt, "active", value);
			if (obj != null)
			{
				R.Call(rt, "ReleaseTemporary", obj);
			}
			Destroy(obj2);
		}
	}

	internal object? Get(TerrainMap map, int size, float x, float z, float heading)
	{
		try
		{
			long num = R.Pointer(map.Texture);
			int num2 = R.Int(R.Get(map.Texture, "width"));
			int num3 = R.Int(R.Get(map.Texture, "height"));
			if (num != sourceId || num2 != sourceW || num3 != sourceH)
			{
				pixels = null;
				sourceId = num;
				sourceW = num2;
				sourceH = num3;
				previousX = float.NaN;
				nextRead = 0.0;
			}
			if (pixels == null && R.Now >= nextRead)
			{
				nextRead = R.Now + 5.0;
				Capture(map);
				nextDraw = 0.0;
			}
			if (pixels == null)
			{
				return null;
			}
			if (output == null || outputSize != size)
			{
				Destroy(output);
				output = NewTexture(size, size);
				outputSize = size;
				buffer = new byte[size * size * 4];
				previousX = float.NaN;
				nextDraw = 0.0;
			}
			if (R.Now >= nextDraw && RasterUploadPolicy.Changed(previousX, previousZ, previousHeading, x, z, heading, R.Now - lastUpload))
			{
				nextDraw = R.Now + 0.05;
				double now = R.Now;
				TerrainRaster.Render(pixels, buffer, size, x, z, heading, map.MinX, map.MaxX, map.MinZ, map.MaxZ, map.U, map.V, map.UW, map.VH, Channel("r"), Channel("g"), Channel("b"));
				GCHandle gCHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
				try
				{
					R.Call(output, "LoadRawTextureData", gCHandle.AddrOfPinnedObject(), buffer.Length);
					R.Call(output, "Apply", false, false);
				}
				finally
				{
					gCHandle.Free();
				}
				bool flag = x >= map.MinX && x <= map.MaxX && z >= map.MinZ && z <= map.MaxZ;
				report.Data["rasterStatus"] = (flag ? "ROTATED_RGBA_READY" : "PLAYER_OUTSIDE_NATIVE_MAP_BOUNDS");
				report.Data["playerMapPosition"] = new
				{
					x = x,
					z = z,
					inside = flag
				};
				report.Add("rasterUploads", 1L);
				previousX = x;
				previousZ = z;
				previousHeading = heading;
				lastUpload = R.Now;
				report.Timing("rasterMs", (R.Now - now) * 1000.0);
			}
			return output;
		}
		catch (Exception e)
		{
			report.Data["rasterStatus"] = "TEXTURE_RENDER_ERROR";
			report.Error("map pixels", e);
			return null;
		}
		byte Channel(string name)
		{
			return (byte)Math.Clamp((int)(R.Float(R.Get(map.Background, name)) * 255f), 0, 255);
		}
	}
}
