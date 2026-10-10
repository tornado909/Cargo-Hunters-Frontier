using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BepInEx;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace Frontier.StackX5;

[BepInPlugin("frontier.cargohunters.stackx5.runtime", "Frontier Stack x5", "0.6.1")]
public sealed class Plugin : BasePlugin
{
	private Harmony? harmony;

	internal static ManualLogSource? Logger;

	public override void Load()
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Expected O, but got Unknown
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Expected O, but got Unknown
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		Logger = ((BasePlugin)this).Log;
		harmony = new Harmony("frontier.cargohunters.stackx5.runtime.safejson.v2");
		System.Type type = Enumerable.FirstOrDefault<System.Type>(Enumerable.Select<Assembly, System.Type>((System.Collections.Generic.IEnumerable<Assembly>)AppDomain.CurrentDomain.GetAssemblies(), (Func<Assembly, System.Type>)delegate(Assembly a)
		{
			try
			{
				return a.GetType("Frontier.Engine.PatchEngine", false);
			}
			catch
			{
				return (System.Type)null;
			}
		}), (Func<System.Type, bool>)((System.Type t) => t != (System.Type)null)) ?? throw new InvalidOperationException("Frontier.Engine.PatchEngine not found");
		MethodInfo val = Enumerable.FirstOrDefault<MethodInfo>((System.Collections.Generic.IEnumerable<MethodInfo>)type.GetMethods((BindingFlags)52), (Func<MethodInfo, bool>)((MethodInfo m) => ((MemberInfo)m).Name == "Transform" && m.ReturnType == typeof(byte[]) && ((MethodBase)m).GetParameters().Length == 2 && ((MethodBase)m).GetParameters()[0].ParameterType == typeof(string)));
		if (val == (MethodInfo)null)
		{
			throw new MissingMethodException(type.FullName, "Transform(string,byte[])");
		}
		harmony.Patch((MethodBase)(object)val, (HarmonyMethod)null, new HarmonyMethod(typeof(Plugin).GetMethod("AfterTransform", (BindingFlags)40)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		((BasePlugin)this).Log.LogInfo((object)"Frontier Stack x5 active: safe JSON transform; no IL2CPP repository object traversal");
	}

	public override bool Unload()
	{
		Harmony? obj = harmony;
		if (obj != null)
		{
			obj.UnpatchSelf();
		}
		return true;
	}

	private static void AfterTransform(object[] __args, ref byte[] __result)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Expected O, but got Unknown
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Expected O, but got Unknown
		try
		{
			if (__args.Length == 0 || !string.Equals(Convert.ToString(__args[0]), "item_templates", (StringComparison)4))
			{
				return;
			}
			JsonNode val = JsonNode.Parse(Encoding.UTF8.GetString(__result), (JsonNodeOptions?)null, default(JsonDocumentOptions));
			if (val == null)
			{
				return;
			}
			int num = Scale(val);
			if (num == 0)
			{
				WriteReport(0, __result.Length, __result.Length, null);
				return;
			}
			byte[] bytes = Encoding.UTF8.GetBytes(val.ToJsonString(new JsonSerializerOptions
			{
				WriteIndented = false
			}));
			int before = __result.Length;
			__result = bytes;
			WriteReport(num, before, bytes.Length, null);
			ManualLogSource logger = Logger;
			if (logger != null)
			{
				bool flag = default(bool);
				BepInExInfoLogInterpolatedStringHandler val2 = new BepInExInfoLogInterpolatedStringHandler(94, 1, out flag);
				if (flag)
				{
					((BepInExLogInterpolatedStringHandler)val2).AppendLiteral("Stack x5: multiplied StackCapacity on ");
					((BepInExLogInterpolatedStringHandler)val2).AppendFormatted<int>(num);
					((BepInExLogInterpolatedStringHandler)val2).AppendLiteral(" existing stackable templates; StackCapacity=1 untouched");
				}
				logger.LogInfo(val2);
			}
		}
		catch (System.Exception ex)
		{
			ManualLogSource? logger2 = Logger;
			if (logger2 != null)
			{
				logger2.LogError((object)("Stack x5 transform failed; original catalog left unchanged: " + (object)ex));
			}
			byte[] obj = __result;
			int before2 = ((obj != null) ? obj.Length : 0);
			byte[] obj2 = __result;
			WriteReport(0, before2, (obj2 != null) ? obj2.Length : 0, ((object)ex).ToString());
		}
	}

	private static int Scale(JsonNode node)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		int num = 0;
		JsonObject val = (JsonObject)(object)((node is JsonObject) ? node : null);
		if (val != null)
		{
			KeyValuePair<string, JsonNode>[] array = Enumerable.ToArray<KeyValuePair<string, JsonNode>>((System.Collections.Generic.IEnumerable<KeyValuePair<string, JsonNode>>)val);
			int num2 = default(int);
			for (int i = 0; i < array.Length; i++)
			{
				KeyValuePair<string, JsonNode> val2 = array[i];
				if (val2.Key == "StackCapacity")
				{
					JsonNode value = val2.Value;
					JsonValue val3 = (JsonValue)(object)((value is JsonValue) ? value : null);
					if (val3 != null && val3.TryGetValue<int>(out num2))
					{
						if (num2 > 1)
						{
							long num3 = (long)num2 * 5L;
							if (num3 > 2147483647)
							{
								num3 = 2147483647L;
							}
							((JsonNode)val)[val2.Key] = (JsonNode)(int)num3;
							num++;
						}
						continue;
					}
				}
				if (val2.Value != null)
				{
					num += Scale(val2.Value);
				}
			}
		}
		else
		{
			JsonArray val4 = (JsonArray)(object)((node is JsonArray) ? node : null);
			if (val4 != null)
			{
				System.Collections.Generic.IEnumerator<JsonNode> enumerator = val4.GetEnumerator();
				try
				{
					while (((System.Collections.IEnumerator)enumerator).MoveNext())
					{
						JsonNode current = enumerator.Current;
						if (current != null)
						{
							num += Scale(current);
						}
					}
				}
				finally
				{
					((System.IDisposable)enumerator)?.Dispose();
				}
			}
		}
		return num;
	}

	private static void WriteReport(int changed, int before, int after, string? error)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Expected O, but got Unknown
		try
		{
			File.WriteAllText(Path.Combine(Paths.BepInExRootPath, "Frontier-stackx5-report.json"), JsonSerializer.Serialize(new
			{
				version = "0.6.1",
				adapter = "safe-json-transform-v2",
				multiplier = 5,
				changed = changed,
				bytesBefore = before,
				bytesAfter = after,
				stackCapacityOnePreserved = true,
				nativeObjectTraversal = false,
				error = error
			}, new JsonSerializerOptions
			{
				WriteIndented = true
			}));
		}
		catch
		{
		}
	}
}
