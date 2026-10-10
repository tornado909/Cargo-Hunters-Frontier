using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Frontier.Core;

internal static class EliteClientTracker
{
	private sealed record Context(object System, object World, object Session, object Ids)
	{
		[CompilerGenerated]
		private global::System.Type EqualityContract
		{
			[CompilerGenerated]
			get
			{
				return typeof(Context);
			}
		}

		[CompilerGenerated]
		public override string ToString()
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Expected O, but got Unknown
			StringBuilder val = new StringBuilder();
			val.Append("Context");
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
			builder.Append("System = ");
			builder.Append(System);
			builder.Append(", World = ");
			builder.Append(World);
			builder.Append(", Session = ");
			builder.Append(Session);
			builder.Append(", Ids = ");
			builder.Append(Ids);
			return true;
		}

		[CompilerGenerated]
		public bool Equals(Context? other)
		{
			if ((object)this != other)
			{
				if ((object)other != null && EqualityContract == other.EqualityContract && EqualityComparer<object>.Default.Equals(System, other.System) && EqualityComparer<object>.Default.Equals(World, other.World) && EqualityComparer<object>.Default.Equals(Session, other.Session))
				{
					return EqualityComparer<object>.Default.Equals(Ids, other.Ids);
				}
				return false;
			}
			return true;
		}
	}

	internal sealed record Target(string Id, GameObject Root, object Player, ElitePresentation.RankStyle Rank)
	{
		[CompilerGenerated]
		private System.Type EqualityContract
		{
			[CompilerGenerated]
			get
			{
				return typeof(Target);
			}
		}

		[CompilerGenerated]
		public override string ToString()
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Expected O, but got Unknown
			StringBuilder val = new StringBuilder();
			val.Append("Target");
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
			builder.Append("Id = ");
			builder.Append((object)Id);
			builder.Append(", Root = ");
			builder.Append((object)Root);
			builder.Append(", Player = ");
			builder.Append(Player);
			builder.Append(", Rank = ");
			builder.Append((object)Rank);
			return true;
		}

		[CompilerGenerated]
		public bool Equals(Target? other)
		{
			if ((object)this != other)
			{
				if ((object)other != null && EqualityContract == other.EqualityContract && EqualityComparer<string>.Default.Equals(Id, other.Id) && EqualityComparer<GameObject>.Default.Equals(Root, other.Root) && EqualityComparer<object>.Default.Equals(Player, other.Player))
				{
					return EqualityComparer<ElitePresentation.RankStyle>.Default.Equals(Rank, other.Rank);
				}
				return false;
			}
			return true;
		}
	}

	private static readonly Dictionary<long, Context> Contexts = new Dictionary<long, Context>();

	private static MethodInfo? hasCompanion;

	private static MethodInfo? getCompanion;

	internal static void Install(Harmony h)
	{
		TrackerRepair.Install(h);
	}

	private static void Capture(object __instance, object __0)
	{
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			object obj = Reflect.Read(__0, "IsAuthoritative");
			if (obj is bool && !(bool)obj)
			{
				object obj2 = Reflect.Read(__instance, "World");
				if ((Reflect.Read(obj2, "Name")?.ToString() ?? "").StartsWith("ClientWorld", (StringComparison)4))
				{
					object ids = Activator.CreateInstance(((MethodBase)Reflect.UniqueMethod(__0.GetType(), "GetPlayerIds", 1)).GetParameters()[0].ParameterType);
					Contexts[((Il2CppObjectBase)obj2).Pointer.ToInt64()] = new Context(__instance, obj2, __0, ids);
				}
			}
		}
		catch (System.Exception e)
		{
			ElitePresentation.Warn("Client observer", e);
		}
	}

	private static void Remove(object __instance)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		KeyValuePair<long, Context>[] array = Enumerable.ToArray<KeyValuePair<long, Context>>((System.Collections.Generic.IEnumerable<KeyValuePair<long, Context>>)Contexts);
		for (int i = 0; i < array.Length; i++)
		{
			KeyValuePair<long, Context> val = array[i];
			if (Reflect.SameNative(val.Value.System, __instance))
			{
				Contexts.Remove(val.Key);
			}
		}
	}

	internal static bool Alive(object player)
	{
		try
		{
			object obj = Reflect.Read(player, "IsDead");
			int num;
			if (obj is bool && !(bool)obj)
			{
				obj = Reflect.Read(player, "EntityHasBeenSpawned");
				num = ((obj is bool && (bool)obj) ? 1 : 0);
			}
			else
			{
				num = 0;
			}
			return (byte)num != 0;
		}
		catch
		{
			return false;
		}
	}

	internal static List<Target> Collect()
	{
		return TrackerRepair.Collect();
	}
}
