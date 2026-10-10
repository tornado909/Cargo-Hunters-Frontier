using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Logging;

namespace Frontier.ReferenceFix;

internal sealed class Evidence
{
	internal readonly Dictionary<string, object?> Data = new Dictionary<string, object>(StringComparer.Ordinal);

	private readonly string path;

	private readonly ManualLogSource log;

	private double next;

	private readonly HashSet<string> warnings = new HashSet<string>(StringComparer.Ordinal);

	private string? pending;

	private int writer;

	internal Evidence(string file, ManualLogSource log, string version, string adapter)
	{
		path = Path.Combine(Paths.BepInExRootPath, file);
		this.log = log;
		Data["version"] = version;
		Data["adapter"] = adapter;
		Data["nativePlaytestClaim"] = false;
		Data["error"] = "";
	}

	internal void Add(string name, long amount = 1L)
	{
		Data[name] = (Data.TryGetValue(name, out object value) ? Convert.ToInt64(value) : 0) + amount;
	}

	internal void Error(string stage, Exception e)
	{
		string text = stage + ": " + e.GetBaseException().Message;
		Data["error"] = text;
		if (warnings.Count < 20 && warnings.Add(text))
		{
			log.LogWarning((object)text);
		}
		Save();
	}

	internal void Timing(string name, double milliseconds)
	{
		if (!(milliseconds < 0.0) && double.IsFinite(milliseconds))
		{
			string key = name + "Max";
			object value;
			double num = (Data.TryGetValue(key, out value) ? Convert.ToDouble(value) : 0.0);
			if (milliseconds > num)
			{
				Data[key] = Math.Round(milliseconds, 3);
			}
			if (milliseconds >= 50.0)
			{
				Add(name + "Over50", 1L);
			}
		}
	}

	internal void Save(bool force = false)
	{
		if (!force && R.Now < next)
		{
			return;
		}
		next = R.Now + 5.0;
		try
		{
			Data["utc"] = DateTime.UtcNow.ToString("O");
			Data["reportFileIo"] = "single background writer, immutable managed JSON, 5-second cadence";
			Interlocked.Exchange(ref pending, JsonSerializer.Serialize(Data, new JsonSerializerOptions
			{
				WriteIndented = true
			}));
			StartWriter();
		}
		catch (Exception ex)
		{
			if (warnings.Add("report"))
			{
				log.LogWarning((object)("Report: " + ex.Message));
			}
		}
	}

	private void StartWriter()
	{
		if (Interlocked.CompareExchange(ref writer, 1, 0) != 0)
		{
			return;
		}
		Task.Run(delegate
		{
			try
			{
				string contents;
				while ((contents = Interlocked.Exchange(ref pending, null)) != null)
				{
					try
					{
						string sourceFileName = path + ".tmp";
						File.WriteAllText(sourceFileName, contents);
						File.Move(sourceFileName, path, overwrite: true);
					}
					catch
					{
					}
				}
			}
			finally
			{
				Volatile.Write(ref writer, 0);
				if (Volatile.Read(ref pending) != null)
				{
					StartWriter();
				}
			}
		});
	}
}
