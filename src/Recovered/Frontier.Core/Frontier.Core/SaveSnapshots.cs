using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Frontier.Engine;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace Frontier.Core;

internal sealed class SaveSnapshots
{
	private sealed record Capture(string Path, string Hash, long Bytes)
	{
		[CompilerGenerated]
		private System.Type EqualityContract
		{
			[CompilerGenerated]
			get
			{
				return typeof(Capture);
			}
		}

		[CompilerGenerated]
		public override string ToString()
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Expected O, but got Unknown
			StringBuilder val = new StringBuilder();
			val.Append("Capture");
			val.Append(" { ");
			if (PrintMembers(val))
			{
				val.Append(' ');
			}
			val.Append('}');
			return ((object)val).ToString();
		}

		[CompilerGenerated]
		private bool PrintMembers(StringBuilder builder)
		{
			RuntimeHelpers.EnsureSufficientExecutionStack();
			builder.Append("Path = ");
			builder.Append((object)Path);
			builder.Append(", Hash = ");
			builder.Append((object)Hash);
			builder.Append(", Bytes = ");
			builder.Append(((object)Bytes/*cast due to .constrained prefix*/).ToString());
			return true;
		}

		[CompilerGenerated]
		public bool Equals(Capture? other)
		{
			if ((object)this != other)
			{
				if ((object)other != null && EqualityContract == other.EqualityContract && EqualityComparer<string>.Default.Equals(Path, other.Path) && EqualityComparer<string>.Default.Equals(Hash, other.Hash))
				{
					return EqualityComparer<long>.Default.Equals(Bytes, other.Bytes);
				}
				return false;
			}
			return true;
		}
	}

	private readonly Plugin p;

	private readonly object sync = new object();

	private readonly string folder;

	private string? latestPath;

	private string? latestHash;

	private string? lastError;

	private bool parsed;

	internal object Snapshot
	{
		get
		{
			lock (sync)
			{
				return new
				{
					verified = parsed,
					path = latestPath,
					sha256 = latestHash,
					lastError = lastError,
					source = "OfflineAccountContextPersistenceService.DeserializeAccountContextSource input bytes",
					scope = "offline account only; not Steam settings; not a verified restore procedure"
				};
			}
		}
	}

	private static SaveSnapshots S => Plugin.Current.Saves;

	internal SaveSnapshots(Plugin p)
	{
		this.p = p;
		folder = Path.Combine(Paths.BepInExRootPath, "Frontier-save-snapshots");
	}

	internal void Install(Harmony h)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		MethodInfo val = Reflect.UniqueMethod(Reflect.FindType("Game.Application.Offline.OfflineAccountContextPersistenceService"), "DeserializeAccountContextSource", 1);
		if (((MethodBase)val).GetParameters()[0].ParameterType != typeof(Il2CppStructArray<byte>))
		{
			throw new MissingMethodException("Offline deserializer signature changed");
		}
		h.Patch((MethodBase)(object)val, H("Before"), H("After"), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		[CompilerGenerated]
		static HarmonyMethod H(string name)
		{
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Expected O, but got Unknown
			return new HarmonyMethod(typeof(SaveSnapshots).GetMethod(name, (BindingFlags)40));
		}
	}

	private static void Before(Il2CppStructArray<byte> __0, out Capture? __state)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		__state = null;
		try
		{
			if (__0 == null || ((Il2CppArrayBase<byte>)(object)__0).Length == 0 || ((Il2CppArrayBase<byte>)(object)__0).Length > 134217728)
			{
				throw new InvalidDataException("Unexpected offline save length");
			}
			byte[] array = __0.AsSpan().ToArray();
			string text = PatchEngine.Hash(array);
			Directory.CreateDirectory(S.folder);
			string text2 = Path.Combine(S.folder, text + ".save");
			lock (S.sync)
			{
				if (!File.Exists(text2))
				{
					File.WriteAllBytes(text2 + ".tmp", array);
					File.Move(text2 + ".tmp", text2);
				}
				if (PatchEngine.Hash(File.ReadAllBytes(text2)) != text)
				{
					throw new IOException("Offline snapshot hash mismatch");
				}
			}
			__state = new Capture(text2, text, array.LongLength);
		}
		catch (System.Exception ex)
		{
			lock (S.sync)
			{
				S.lastError = ex.GetBaseException().Message;
			}
			((BasePlugin)Plugin.Current).Log.LogError((object)("Offline snapshot failed: " + S.lastError));
		}
	}

	private static void After(object? __result, Capture? __state)
	{
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Expected O, but got Unknown
		if ((object)__state == null || __result == null)
		{
			return;
		}
		try
		{
			lock (S.sync)
			{
				S.latestPath = __state.Path;
				S.latestHash = __state.Hash;
				S.parsed = true;
				S.lastError = null;
				var anon = new
				{
					schema = 1,
					utc = System.DateTime.UtcNow.ToString("O"),
					path = __state.Path,
					sha256 = __state.Hash,
					bytes = __state.Bytes,
					nativeDeserialized = true,
					source = "offline deserializer input",
					activeAtCapture = Plugin.Current.Active,
					pack = "0.6.0-modular"
				};
				File.WriteAllText(Path.Combine(S.folder, "latest.json"), JsonSerializer.Serialize(anon, new JsonSerializerOptions
				{
					WriteIndented = true
				}));
			}
			((BasePlugin)Plugin.Current).Log.LogInfo((object)("Actual offline save captured before loading: " + __state.Path));
			Plugin.Current.WriteReport();
		}
		catch (System.Exception ex)
		{
			lock (S.sync)
			{
				S.parsed = false;
				S.lastError = ex.GetBaseException().Message;
			}
			((BasePlugin)Plugin.Current).Log.LogError((object)("Offline snapshot metadata failed: " + S.lastError));
		}
	}
}
