using System;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading;
using HarmonyLib;

namespace Frontier.Loot;

internal static class AliasExpansionHook
{
	private static long calls;

	private static long leaves;

	private static long added;

	private static long normalized;

	private static long balancedMagazines;

	private static long balancedBodies;

	private static long modifierComponents;

	private static bool installed;

	internal static void Install(Harmony h)
	{
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Expected O, but got Unknown
		MethodInfo methodInfo = AppDomain.CurrentDomain.GetAssemblies().Single((Assembly a) => a.GetName().Name == "Frontier.Engine").GetType("Frontier.Engine.PatchEngine", throwOnError: true)
			.GetMethod("ExpandAliases", BindingFlags.Static | BindingFlags.Public, null, new Type[2]
			{
				typeof(JsonNode),
				typeof(JsonObject)
			}, null) ?? throw new MissingMethodException("PatchEngine.ExpandAliases(JsonNode, JsonObject)");
		h.Patch((MethodBase)methodInfo, new HarmonyMethod(typeof(AliasExpansionHook).GetMethod("ReplaceExpansion", BindingFlags.Static | BindingFlags.NonPublic))
		{
			priority = 800
		}, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		installed = true;
	}

	private static bool ReplaceExpansion(JsonNode __0, JsonObject __1)
	{
		AliasExpansionPolicy.Counts counts = AliasExpansionPolicy.Expand(__0, __1);
		StructuralBalance.Counts counts2 = StructuralBalance.Apply(__0, InstanceStats.Configuration);
		Interlocked.Add(ref balancedMagazines, counts2.Magazines);
		Interlocked.Add(ref balancedBodies, counts2.BodyParts);
		Interlocked.Add(ref modifierComponents, counts2.ModifierComponents);
		Interlocked.Increment(ref calls);
		Interlocked.Add(ref leaves, counts.Leaves);
		Interlocked.Add(ref added, counts.Added);
		Interlocked.Add(ref normalized, counts.Normalized);
		return false;
	}

	internal static object Snapshot()
	{
		return new
		{
			installed = installed,
			managedOnly = true,
			nativeEquipChecksOverridden = false,
			calls = Interlocked.Read(ref calls),
			templateLeaves = Interlocked.Read(ref leaves),
			aliasIdsAdded = Interlocked.Read(ref added),
			singletonAllFiltersNormalized = Interlocked.Read(ref normalized),
			balancedMagazineTemplates = Interlocked.Read(ref balancedMagazines),
			balancedBodyTemplates = Interlocked.Read(ref balancedBodies),
			modifierComponentsAdded = Interlocked.Read(ref modifierComponents)
		};
	}
}
