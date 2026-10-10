using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace Frontier.Survivor;

internal static class Trace
{
	internal static readonly Dictionary<string, object?> Data = new Dictionary<string, object>();

	internal static ManualLogSource Log = null;

	private static double next;

	private static string pending = "";

	private static int busy;

	private static string path = "";

	private static readonly object gate = new object();

	internal static void Init(ManualLogSource log)
	{
		Log = log;
		path = Path.Combine(Paths.BepInExRootPath, "Frontier-survivor-report.json");
		Data["version"] = "0.2.0";
		Data["packRevision"] = "v10.10.2-survivor-coop.2";
		Data["nativePlaytest"] = false;
		Data["mapId"] = 3466001;
		Data["mapName"] = "Frontier_Survivor";
		Data["saveWritesByPlugin"] = 0;
		Data["status"] = "WAITING_FOR_REPOSITORIES";
	}

	internal static void Add(string key, int n = 1)
	{
		Data[key] = ((Data.TryGetValue(key, out object value) && value is long num) ? num : 0) + n;
	}

	internal static void Error(string stage, Exception e)
	{
		string message = e.GetBaseException().Message;
		Data["lastError"] = stage + ": " + message;
		Log.LogError((object)(stage + ": " + e));
		Save(force: true);
	}

	internal static void Save(bool force = false)
	{
		double realtimeSinceStartupAsDouble = Time.realtimeSinceStartupAsDouble;
		if (!force && realtimeSinceStartupAsDouble < next)
		{
			return;
		}
		next = realtimeSinceStartupAsDouble + 5.0;
		Data["utc"] = DateTime.UtcNow.ToString("O");
		Data["reportWriteMode"] = ReportFile.Mode;
		string text = JsonSerializer.Serialize(Data, new JsonSerializerOptions
		{
			WriteIndented = true
		});
		lock (gate)
		{
			pending = text;
		}
		if (Interlocked.CompareExchange(ref busy, 1, 0) != 0)
		{
			return;
		}
		ThreadPool.QueueUserWorkItem(delegate
		{
			try
			{
				while (true)
				{
					string text2;
					lock (gate)
					{
						text2 = pending;
						pending = "";
					}
					if (text2.Length == 0)
					{
						break;
					}
					string temporary = path + ".tmp";
					File.WriteAllText(temporary, text2, Encoding.UTF8);
					ReportFile.Publish(temporary, path, text2);
				}
			}
			catch (Exception ex)
			{
				Log.LogWarning((object)("Reserve report: " + ex.Message));
			}
			finally
			{
				Interlocked.Exchange(ref busy, 0);
			}
		});
	}
}
