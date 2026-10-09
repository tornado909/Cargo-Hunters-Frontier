using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BepInEx;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Frontier.Engine;
using Frontier.Modular;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace Frontier.Core;

[BepInPlugin("frontier.cargohunters.core", "Frontier Framework", "0.6.0")]
public sealed class Plugin : BasePlugin
{
	private sealed record RepositoryLoadState(string Name, string Hash, bool Applied, object Repository, JsonNode Expected, Stopwatch Timer)
	{
		[CompilerGenerated]
		private System.Type EqualityContract
		{
			[CompilerGenerated]
			get
			{
				return typeof(RepositoryLoadState);
			}
		}

		[CompilerGenerated]
		public override string ToString()
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Expected O, but got Unknown
			StringBuilder val = new StringBuilder();
			val.Append("RepositoryLoadState");
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
			builder.Append("Name = ");
			builder.Append((object)Name);
			builder.Append(", Hash = ");
			builder.Append((object)Hash);
			builder.Append(", Applied = ");
			builder.Append(((object)Applied/*cast due to .constrained prefix*/).ToString());
			builder.Append(", Repository = ");
			builder.Append(Repository);
			builder.Append(", Expected = ");
			builder.Append((object)Expected);
			builder.Append(", Timer = ");
			builder.Append((object)Timer);
			return true;
		}

		[CompilerGenerated]
		public bool Equals(RepositoryLoadState? other)
		{
			if ((object)this != other)
			{
				if ((object)other != null && EqualityContract == other.EqualityContract && EqualityComparer<string>.Default.Equals(Name, other.Name) && EqualityComparer<string>.Default.Equals(Hash, other.Hash) && EqualityComparer<bool>.Default.Equals(Applied, other.Applied) && EqualityComparer<object>.Default.Equals(Repository, other.Repository) && EqualityComparer<JsonNode>.Default.Equals(Expected, other.Expected))
				{
					return EqualityComparer<Stopwatch>.Default.Equals(Timer, other.Timer);
				}
				return false;
			}
			return true;
		}
	}

	internal static Plugin Current;

	internal PackSettings Settings;

	internal PatchEngine Engine;

	internal CoopController Coop;

	internal RuntimeAudit Audit;

	internal RuntimeObservations Observations;

	internal SaveSnapshots Saves;

	private readonly Dictionary<string, string> loadedCatalogHashes = new Dictionary<string, string>((IEqualityComparer<string>)(object)StringComparer.Ordinal);

	internal readonly Dictionary<string, string> Repositories = new Dictionary<string, string>((IEqualityComparer<string>)(object)StringComparer.Ordinal);

	internal readonly List<string> ObservedPaths = new List<string>();

	internal string Mode = "initializing";

	internal string LastEnemy = "none";

	internal int EnemyChanges;

	internal int LocalizationHits;

	private Harmony? harmony;

	private JsonObject strings = new JsonObject((JsonNodeOptions?)null);

	private EnemyRoller? enemies;

	private string reportPath = "";

	private readonly object reportLock = new object();

	private bool fatal;

	internal string CatalogDigest
	{
		get
		{
			lock (reportLock)
			{
				return (Active && Engine != null && Audit != null && Audit.CatalogReady && Observations.Healthy) ? CoopProtocol.CatalogDigest((IReadOnlyDictionary<string, string>)(object)loadedCatalogHashes, (System.Collections.Generic.IEnumerable<string>)Engine.RepositoryNames) : "";
			}
		}
	}

	internal bool Active
	{
		get
		{
			if (Settings != null && Settings.Enabled && !Settings.DiagnosticsOnly)
			{
				return !fatal;
			}
			return false;
		}
	}

	public override void Load()
	{
		//IL_0227: Unknown result type (might be due to invalid IL or missing references)
		//IL_022e: Expected O, but got Unknown
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Expected O, but got Unknown
		Current = this;
		reportPath = Path.Combine(Paths.BepInExRootPath, "Frontier-report.json");
		try
		{
			string directoryName = Path.GetDirectoryName(((object)this).GetType().Assembly.Location);
			ModuleRuntime.PrepareOrStop(directoryName, Paths.ConfigPath, Paths.BepInExRootPath);
			string text = Path.Combine(directoryName, "compiled");
			string text2 = Path.Combine(Paths.ConfigPath, "FrontierModular.generated.json");
			if (!File.Exists(text2))
			{
				Directory.CreateDirectory(Paths.ConfigPath);
				File.Copy(Path.Combine(text, "pack-settings.example.json"), text2);
			}
			Settings = PackSettings.Read(text2);
			if (!Settings.Enabled)
			{
				Mode = "DISABLED: do not load a Frontier save";
				((BasePlugin)this).Log.LogWarning((object)Mode);
				WriteReport();
				return;
			}
			Engine = new PatchEngine(text, Settings.EnabledModules, Settings.RuntimeTestMode);
			Audit = new RuntimeAudit(this);
			Observations = new RuntimeObservations(this);
			Saves = new SaveSnapshots(this);
			strings = PatchEngine.ReadObject(Path.Combine(text, "localization.json"));
			VerifyGameFiles();
			string directory = Path.Combine(Paths.BepInExRootPath, "interop");
			string[] array = new string[5] { "Core.Shared.Utils", "DataRepository", "Game.Shared", "Game", "Unity.Localization" };
			for (int i = 0; i < array.Length; i++)
			{
				Reflect.LoadAssembly(array[i], directory);
			}
			harmony = new Harmony("frontier.cargohunters.core");
			enemies = new EnemyRoller(PatchEngine.ReadObject(Path.Combine(text, "elites.json")), Settings);
			Coop = new CoopController(this, directoryName, text);
			InstallHooks();
			if (ModuleRuntime.OverlayGate(Engine, "elites"))
			{
				try
				{
					((BasePlugin)this).AddComponent<EliteWorldOverlay>();
				}
				catch (System.Exception ex)
				{
					((BasePlugin)this).Log.LogWarning((object)("Elite rank overlay unavailable: " + ex.Message));
				}
			}
			Mode = (Active ? "ACTIVE / PRODUCTION SETTINGS - matched co-op peers required" : "DIAGNOSTIC - repositories are NOT changed");
			ManualLogSource log = ((BasePlugin)this).Log;
			bool flag = default(bool);
			BepInExWarningLogInterpolatedStringHandler val = new BepInExWarningLogInterpolatedStringHandler(15, 3, ref flag);
			if (flag)
			{
				((BepInExLogInterpolatedStringHandler)val).AppendLiteral("Frontier ");
				((BepInExLogInterpolatedStringHandler)val).AppendFormatted<string>("0.6.0-modular");
				((BepInExLogInterpolatedStringHandler)val).AppendLiteral(" | ");
				((BepInExLogInterpolatedStringHandler)val).AppendFormatted<string>(Mode);
				((BepInExLogInterpolatedStringHandler)val).AppendLiteral(" | ");
				((BepInExLogInterpolatedStringHandler)val).AppendFormatted<string>(Engine.GameVersion);
			}
			log.LogWarning(val);
			if (Settings.ShowOverlay)
			{
				try
				{
					((BasePlugin)this).AddComponent<FrontierOverlay>();
				}
				catch (System.Exception ex2)
				{
					((BasePlugin)this).Log.LogWarning((object)("Optional overlay unavailable: " + ex2.Message));
				}
			}
			WriteReport();
		}
		catch (System.Exception e)
		{
			Fail("Initialization", e);
		}
	}

	private void VerifyGameFiles()
	{
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		string gameRootPath = Paths.GameRootPath;
		string text = Path.Combine(gameRootPath, "version.txt");
		if (!File.Exists(text) || File.ReadAllText(text).Trim() != Engine.GameVersion)
		{
			throw new InvalidDataException("Requires analyzed version " + Engine.GameVersion + "; no automatic update adaptation.");
		}
		System.Collections.Generic.IEnumerator<KeyValuePair<string, JsonNode>> enumerator = Engine.FileHashes.GetEnumerator();
		try
		{
			while (((System.Collections.IEnumerator)enumerator).MoveNext())
			{
				KeyValuePair<string, JsonNode> current = enumerator.Current;
				string text2 = Path.Combine(gameRootPath, current.Key.Replace('/', Path.DirectorySeparatorChar));
				if (!File.Exists(text2) || PatchEngine.Hash(File.ReadAllBytes(text2)) != current.Value.GetValue<string>())
				{
					throw new InvalidDataException("Game binary fingerprint mismatch: " + current.Key);
				}
			}
		}
		finally
		{
			((System.IDisposable)enumerator)?.Dispose();
		}
	}

	private static HarmonyMethod PatchMethod(string name)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Expected O, but got Unknown
		return new HarmonyMethod(typeof(Plugin).GetMethod(name, (BindingFlags)40));
	}

	private void InstallHooks()
	{
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c2: Expected O, but got Unknown
		MethodInfo val = Reflect.UniqueMethod(Reflect.FindType("Game.Data.DataRepositoryLoader"), "ParseDataFromBytes", 2);
		if (val.ReturnType != typeof(void) || ((MethodBase)val).GetParameters()[1].ParameterType != typeof(Il2CppStructArray<byte>))
		{
			throw new MissingMethodException("ParseDataFromBytes signature differs from the analyzed IL2CPP byte[] method");
		}
		System.Type type = Reflect.FindType("UnityEngine.Localization.Settings.LocalizedStringDatabase");
		MethodInfo val2 = Reflect.UniqueMethod(type, "GenerateLocalizedString", 6);
		MethodInfo val3 = Reflect.UniqueMethod(type, "ProcessUntranslatedText", 5);
		if (val2.ReturnType != typeof(string) || val3.ReturnType != typeof(string))
		{
			throw new MissingMethodException("Localization signatures changed");
		}
		harmony.Patch((MethodBase)(object)val2, PatchMethod("LocalizationPrefix"), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		harmony.Patch((MethodBase)(object)val3, PatchMethod("MissingTextPrefix"), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		if (Engine.IsEnabled("elites"))
		{
			MethodInfo val4 = Reflect.UniqueMethod(Reflect.FindType("Game.SpawnPointSystem.BotSpawnPoints.BotSpawnPointEquipmentManager"), "TryGetBotPreset", 3);
			if (val4.ReturnType != typeof(bool) || !((MethodBase)val4).GetParameters()[2].ParameterType.IsByRef || ((MethodBase)val4).GetParameters()[2].ParameterType.GetElementType()?.FullName != "Game.SpawnPointSystem.BotSpawnPoints.Data.BotPresetData")
			{
				throw new MissingMethodException("TryGetBotPreset signature changed");
			}
			MethodInfo val5 = typeof(Plugin).GetMethod("EnemyPostfix", (BindingFlags)40).MakeGenericMethod(new System.Type[1] { ((MethodBase)val4).GetParameters()[2].ParameterType.GetElementType() });
			harmony.Patch((MethodBase)(object)val4, (HarmonyMethod)null, new HarmonyMethod(val5), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
		if (Engine.IsEnabled("elites"))
		{
			ElitePresentation.Install(harmony, Path.Combine(Path.GetDirectoryName(((object)this).GetType().Assembly.Location), "compiled"));
		}
		RigOSLocalizationFix.Install(harmony);
		CoopHooks.Install(harmony);
		Observations.Install(harmony);
		Saves.Install(harmony);
		harmony.Patch((MethodBase)(object)val, PatchMethod("RepositoryPrefix"), PatchMethod("RepositoryPostfix"), (HarmonyMethod)null, PatchMethod("RepositoryFinalizer"), (HarmonyMethod)null);
		((BasePlugin)this).Log.LogInfo((object)"Required native methods resolved and Harmony patches requested. Invocation still requires in-game verification.");
		ModuleRuntime.InstallLoot(this);
	}

	private static void RepositoryPrefix(object __0, ref Il2CppStructArray<byte> __1, out RepositoryLoadState? __state)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_016f: Expected O, but got Unknown
		__state = null;
		Plugin current = Current;
		try
		{
			if (__0 == null || __1 == null)
			{
				throw new InvalidDataException("Null data-loader argument");
			}
			string text = Reflect.Read(__0, "FilePath")?.ToString() ?? throw new InvalidDataException("Repository FilePath is null");
			lock (current.reportLock)
			{
				if (!current.ObservedPaths.Contains(text))
				{
					current.ObservedPaths.Add(text);
				}
			}
			string text2 = current.Engine.ResolveRepository(text);
			if (text2 != null)
			{
				Stopwatch val = Stopwatch.StartNew();
				byte[] array = __1.AsSpan().ToArray();
				byte[] array2 = current.Engine.Transform(text2, array);
				bool active = current.Active;
				if (active)
				{
					__1 = new Il2CppStructArray<byte>(array2);
				}
				JsonNode expected = JsonNode.Parse(PatchEngine.NormalizedText(active ? array2 : array), (JsonNodeOptions?)null, default(JsonDocumentOptions));
				__state = new RepositoryLoadState(text2, PatchEngine.Hash(active ? array2 : array), active, __0, expected, val);
				current.Audit.Invalidate(text2);
				lock (current.reportLock)
				{
					current.loadedCatalogHashes.Remove(text2);
					current.Repositories[text2] = "PENDING_NATIVE_READBACK";
				}
				ManualLogSource log = ((BasePlugin)current).Log;
				bool flag = default(bool);
				BepInExInfoLogInterpolatedStringHandler val2 = new BepInExInfoLogInterpolatedStringHandler(80, 5, ref flag);
				if (flag)
				{
					((BepInExLogInterpolatedStringHandler)val2).AppendFormatted<string>(text2);
					((BepInExLogInterpolatedStringHandler)val2).AppendLiteral(": prepared ");
					((BepInExLogInterpolatedStringHandler)val2).AppendFormatted<int>(array.Length);
					((BepInExLogInterpolatedStringHandler)val2).AppendLiteral(" -> ");
					((BepInExLogInterpolatedStringHandler)val2).AppendFormatted<int>(array2.Length);
					((BepInExLogInterpolatedStringHandler)val2).AppendLiteral(" bytes in ");
					((BepInExLogInterpolatedStringHandler)val2).AppendFormatted<long>(val.ElapsedMilliseconds);
					((BepInExLogInterpolatedStringHandler)val2).AppendLiteral(" ms; applied argument=");
					((BepInExLogInterpolatedStringHandler)val2).AppendFormatted<bool>(active);
					((BepInExLogInterpolatedStringHandler)val2).AppendLiteral("; native objects still unverified");
				}
				log.LogInfo(val2);
			}
		}
		catch (System.Exception e)
		{
			current.Fail("Repository prefix", e);
		}
	}

	private static void RepositoryPostfix(RepositoryLoadState? __state)
	{
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Expected O, but got Unknown
		if ((object)__state == null)
		{
			return;
		}
		Plugin current = Current;
		try
		{
			current.Audit.Verify(__state.Name, __state.Repository, __state.Expected, __state.Applied);
			lock (current.reportLock)
			{
				current.Repositories[__state.Name] = (__state.Applied ? "objects-verified" : "baseline-verified-not-applied");
				if (__state.Applied)
				{
					current.loadedCatalogHashes[__state.Name] = __state.Hash;
				}
			}
			ManualLogSource log = ((BasePlugin)current).Log;
			bool flag = default(bool);
			BepInExInfoLogInterpolatedStringHandler val = new BepInExInfoLogInterpolatedStringHandler(29, 3, ref flag);
			if (flag)
			{
				((BepInExLogInterpolatedStringHandler)val).AppendFormatted<string>(__state.Name);
				((BepInExLogInterpolatedStringHandler)val).AppendLiteral(": ");
				((BepInExLogInterpolatedStringHandler)val).AppendFormatted<string>(current.Repositories[__state.Name]);
				((BepInExLogInterpolatedStringHandler)val).AppendLiteral("; read-back finished in ");
				((BepInExLogInterpolatedStringHandler)val).AppendFormatted<long>(__state.Timer.ElapsedMilliseconds);
				((BepInExLogInterpolatedStringHandler)val).AppendLiteral(" ms");
			}
			log.LogInfo(val);
			current.WriteReport();
		}
		catch (System.Exception ex)
		{
			current.Audit.Broken(__state.Name, ex);
			lock (current.reportLock)
			{
				current.Repositories[__state.Name] = "BROKEN";
				current.loadedCatalogHashes.Remove(__state.Name);
			}
			current.Fail("Native read-back: " + __state.Name, ex);
		}
	}

	private static System.Exception? RepositoryFinalizer(System.Exception? __exception, RepositoryLoadState? __state)
	{
		if (__exception != null && (object)__state != null)
		{
			Current.Audit.Broken(__state.Name, __exception);
			lock (Current.reportLock)
			{
				Current.Repositories[__state.Name] = "BROKEN";
				Current.loadedCatalogHashes.Remove(__state.Name);
			}
			Current.Fail("Native repository deserialization: " + __state.Name, __exception);
		}
		return __exception;
	}

	internal void ResetEnemySession()
	{
		enemies?.BeginAuthoritativeSession();
	}

	private static bool LocalizationPrefix(object[] __args, ref string __result)
	{
		Plugin current = Current;
		if (!current.Active)
		{
			return true;
		}
		try
		{
			long id = Convert.ToInt64(Reflect.Read(__args[3], "KeyId"));
			return !current.TryText(id, __args[4], ref __result);
		}
		catch (System.Exception e)
		{
			current.Fail("Localization", e);
			return true;
		}
	}

	private static bool MissingTextPrefix(object[] __args, ref string __result)
	{
		if (!Current.Active)
		{
			return true;
		}
		try
		{
			return !Current.TryText(Convert.ToInt64(__args[1]), __args[4], ref __result);
		}
		catch (System.Exception e)
		{
			Current.Fail("Missing localization", e);
			return true;
		}
	}

	private bool TryText(long id, object? locale, ref string result)
	{
		JsonNode obj = ((JsonNode)strings)[id.ToString((IFormatProvider)(object)CultureInfo.InvariantCulture)];
		JsonObject val = (JsonObject)(object)((obj is JsonObject) ? obj : null);
		if (val == null)
		{
			return false;
		}
		string text = ((locale == null) ? "en" : (Reflect.Read(Reflect.Read(locale, "Identifier"), "Code")?.ToString() ?? "en"));
		result = ((JsonNode)val)[text.StartsWith("ru", (StringComparison)5) ? "ru" : "en"].GetValue<string>();
		LocalizationHits++;
		return true;
	}

	private static void EnemyPostfix<T>(bool __result, object __instance, object __0, ref T __2) where T : Il2CppObjectBase
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		if (!__result || !Current.Active || Current.enemies == null)
		{
			return;
		}
		try
		{
			if (__2 == null)
			{
				throw new InvalidDataException("Native TryGetBotPreset returned true with a null preset");
			}
			string description;
			object obj = Current.enemies.Replace(__instance, __0, __2, out description);
			if (obj != null)
			{
				T val = (T)obj;
				__2 = val;
				Current.EnemyChanges++;
				Current.LastEnemy = description;
				((BasePlugin)Current).Log.LogInfo((object)("Elite selected: " + description));
				Current.WriteReport();
			}
		}
		catch (System.Exception e)
		{
			Current.Fail("Enemy variant selection", e);
		}
	}

	internal void Fail(string stage, System.Exception e)
	{
		if (!fatal)
		{
			bool active = Active;
			fatal = true;
			Mode = "FAILED: " + stage + ": " + e.GetBaseException().Message;
			((BasePlugin)this).Log.LogError((object)(Mode + "\n" + (object)e));
			WriteReport(((object)e).ToString());
			if (active)
			{
				((BasePlugin)this).Log.LogFatal((object)"Frontier is terminating the process (exit 78) to avoid continuing with a mixed catalog. Read BepInEx/Frontier-report.json.");
				Environment.Exit(78);
			}
			Harmony? obj = harmony;
			if (obj != null)
			{
				obj.UnpatchSelf();
			}
		}
	}

	internal void WriteReport(string? error = null)
	{
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		//IL_0160: Expected O, but got Unknown
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Expected O, but got Unknown
		if (string.IsNullOrEmpty(reportPath))
		{
			return;
		}
		lock (reportLock)
		{
			try
			{
				var anon = new
				{
					pack = "0.6.0-modular",
					mode = Mode,
					utc = System.DateTime.UtcNow.ToString("O"),
					enabledModules = Settings?.EnabledModules,
					repositories = Repositories,
					observedRepositoryPaths = ObservedPaths,
					runtimeAudit = Audit?.Snapshot,
					runtimeObservations = Observations?.Snapshot,
					elitePresentation = ElitePresentation.Snapshot,
					saveSnapshots = Saves?.Snapshot,
					runtimeTestMode = Settings?.RuntimeTestMode,
					expectedRepositories = Engine?.RepositoryNames,
					eliteChanges = EnemyChanges,
					lastElite = LastEnemy,
					localizationHits = LocalizationHits,
					catalogDigest = CatalogDigest,
					loadedCatalogHashes = loadedCatalogHashes,
					coop = Coop?.Snapshot,
					resolvedCoopContracts = CoopHooks.Resolved,
					authoritativeEnemyCalls = (enemies?.AuthoritativeCalls ?? 0),
					skippedClientEnemyCalls = (enemies?.ClientCallsSkipped ?? 0),
					runtimePlaytestClaim = false,
					error = (error ?? (fatal ? Mode : null))
				};
				string text = reportPath + ".tmp";
				File.WriteAllText(text, JsonSerializer.Serialize(anon, new JsonSerializerOptions
				{
					WriteIndented = true
				}), (Encoding)new UTF8Encoding(false));
				File.Move(text, reportPath, true);
			}
			catch (System.Exception ex)
			{
				((BasePlugin)this).Log.LogWarning((object)("Could not write diagnostic report: " + ex.Message));
			}
		}
	}

	public override bool Unload()
	{
		return false;
	}
}
