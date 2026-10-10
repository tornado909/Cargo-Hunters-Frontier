using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
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

	internal void Save(bool force = false)
	{
		if (!force && R.Now < next)
		{
			return;
		}
		next = R.Now + 2.0;
		try
		{
			Data["utc"] = DateTime.UtcNow.ToString("O");
			string sourceFileName = path + ".tmp";
			File.WriteAllText(sourceFileName, JsonSerializer.Serialize(Data, new JsonSerializerOptions
			{
				WriteIndented = true
			}));
			File.Move(sourceFileName, path, overwrite: true);
		}
		catch (Exception ex)
		{
			if (warnings.Add("report"))
			{
				log.LogWarning((object)("Report: " + ex.Message));
			}
		}
	}
}
