using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Il2CppInterop.Runtime.InteropTypes;

namespace Frontier.Core;

internal static class Reflect
{
	[CompilerGenerated]
	private sealed class _003CValues_003Ed__5 : System.Collections.Generic.IEnumerable<object>, System.Collections.IEnumerable, System.Collections.Generic.IEnumerator<object>, System.IDisposable, System.Collections.IEnumerator
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		private int _003C_003El__initialThreadId;

		private object list;

		public object _003C_003E3__list;

		private int _003Ccount_003E5__2;

		private MethodInfo _003Cindexer_003E5__3;

		private int _003Ci_003E5__4;

		object System.Collections.Generic.IEnumerator<object>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object System.Collections.IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CValues_003Ed__5(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
			_003C_003El__initialThreadId = Environment.CurrentManagedThreadId;
		}

		[DebuggerHidden]
		void System.IDisposable.Dispose()
		{
			_003Cindexer_003E5__3 = null;
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			int num = _003C_003E1__state;
			if (num != 0)
			{
				if (num != 1)
				{
					return false;
				}
				_003C_003E1__state = -1;
				goto IL_00e1;
			}
			_003C_003E1__state = -1;
			_003Ccount_003E5__2 = Count(list);
			if (_003Ccount_003E5__2 < 0 || _003Ccount_003E5__2 > 100000)
			{
				throw new InvalidDataException("Unexpected collection size");
			}
			_003Cindexer_003E5__3 = Enumerable.Single<MethodInfo>((System.Collections.Generic.IEnumerable<MethodInfo>)list.GetType().GetMethods((BindingFlags)60), (Func<MethodInfo, bool>)((MethodInfo m) => ((MemberInfo)m).Name == "get_Item" && ((MethodBase)m).GetParameters().Length == 1 && ((MethodBase)m).GetParameters()[0].ParameterType == typeof(int)));
			_003Ci_003E5__4 = 0;
			goto IL_0098;
			IL_0098:
			if (_003Ci_003E5__4 < _003Ccount_003E5__2)
			{
				object obj = ((MethodBase)_003Cindexer_003E5__3).Invoke(list, new object[1] { _003Ci_003E5__4 });
				if (obj != null)
				{
					_003C_003E2__current = obj;
					_003C_003E1__state = 1;
					return true;
				}
				goto IL_00e1;
			}
			return false;
			IL_00e1:
			_003Ci_003E5__4++;
			goto IL_0098;
		}

		bool System.Collections.IEnumerator.MoveNext()
		{
			return MoveNext();
		}

		[DebuggerHidden]
		void System.Collections.IEnumerator.Reset()
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			throw new NotSupportedException();
		}

		[DebuggerHidden]
		System.Collections.Generic.IEnumerator<object> System.Collections.Generic.IEnumerable<object>.GetEnumerator()
		{
			_003CValues_003Ed__5 _003CValues_003Ed__;
			if (_003C_003E1__state == -2 && _003C_003El__initialThreadId == Environment.CurrentManagedThreadId)
			{
				_003C_003E1__state = 0;
				_003CValues_003Ed__ = this;
			}
			else
			{
				_003CValues_003Ed__ = new _003CValues_003Ed__5(0);
			}
			_003CValues_003Ed__.list = _003C_003E3__list;
			return _003CValues_003Ed__;
		}

		[DebuggerHidden]
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return (System.Collections.IEnumerator)((System.Collections.Generic.IEnumerable<object>)this).GetEnumerator();
		}
	}

	internal const BindingFlags Flags = (BindingFlags)60;

	internal static object? Read(object? instance, string member)
	{
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		if (instance == null)
		{
			return null;
		}
		for (System.Type type = instance.GetType(); type != null; type = type.BaseType)
		{
			PropertyInfo property = type.GetProperty(member, (BindingFlags)62);
			if (property != null)
			{
				return property.GetValue(instance);
			}
			FieldInfo field = type.GetField(member, (BindingFlags)62);
			if (field != null)
			{
				return field.GetValue(instance);
			}
		}
		throw new MissingMemberException(instance.GetType().FullName, member);
	}

	internal static void Write(object instance, string member, object? value)
	{
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		for (System.Type type = instance.GetType(); type != null; type = type.BaseType)
		{
			PropertyInfo property = type.GetProperty(member, (BindingFlags)62);
			if (((property != null) ? property.SetMethod : null) != null)
			{
				property.SetValue(instance, value);
				return;
			}
			FieldInfo field = type.GetField(member, (BindingFlags)62);
			if (field != null)
			{
				field.SetValue(instance, value);
				return;
			}
		}
		throw new MissingMemberException(instance.GetType().FullName, member);
	}

	internal static object? Call(object instance, string name, params object?[] args)
	{
		return ((MethodBase)Enumerable.Single<MethodInfo>(Enumerable.Where<MethodInfo>((System.Collections.Generic.IEnumerable<MethodInfo>)instance.GetType().GetMethods((BindingFlags)60), (Func<MethodInfo, bool>)((MethodInfo m) => ((MemberInfo)m).Name == name && !((MethodBase)m).ContainsGenericParameters && ((MethodBase)m).GetParameters().Length == args.Length)), (Func<MethodInfo, bool>)((MethodInfo m) => Enumerable.All<bool>(Enumerable.Select<ParameterInfo, bool>((System.Collections.Generic.IEnumerable<ParameterInfo>)((MethodBase)m).GetParameters(), (Func<ParameterInfo, int, bool>)((ParameterInfo p, int i) => args[i] == null || p.ParameterType.IsInstanceOfType(args[i]))), (Func<bool, bool>)((bool x) => x))))).Invoke(instance, args);
	}

	internal static int Count(object list)
	{
		try
		{
			return Convert.ToInt32(Read(list, "Count"));
		}
		catch (MissingMemberException)
		{
			System.Type type = list.GetType();
			if (type.IsGenericType && type.GetGenericTypeDefinition().FullName == "Il2CppSystem.Collections.Generic.IReadOnlyList`1")
			{
				System.Type concrete = FindType("Il2CppSystem.Collections.Generic.IReadOnlyCollection`1").MakeGenericType(type.GetGenericArguments());
				return Convert.ToInt32(Read(AsConcrete(list, concrete), "Count"));
			}
			return Convert.ToInt32(Read(list, "Length"));
		}
	}

	[IteratorStateMachine(typeof(_003CValues_003Ed__5))]
	internal static System.Collections.Generic.IEnumerable<object> Values(object list)
	{
		int num = Count(list);
		if (num < 0 || num > 100000)
		{
			throw new InvalidDataException("Unexpected collection size");
		}
		MethodInfo val = Enumerable.Single<MethodInfo>((System.Collections.Generic.IEnumerable<MethodInfo>)list.GetType().GetMethods((BindingFlags)60), (Func<MethodInfo, bool>)((MethodInfo m) => ((MemberInfo)m).Name == "get_Item" && ((MethodBase)m).GetParameters().Length == 1 && ((MethodBase)m).GetParameters()[0].ParameterType == typeof(int)));
		for (int num2 = 0; num2 < num; num2++)
		{
			object obj = ((MethodBase)val).Invoke(list, new object[1] { num2 });
			if (obj != null)
			{
				yield return obj;
			}
		}
	}

	internal static string Id(object instance)
	{
		try
		{
			return Read(instance, "Id")?.ToString() ?? "";
		}
		catch (MissingMemberException)
		{
			return Call(instance, "GetId")?.ToString() ?? "";
		}
	}

	internal static object ParseId(System.Type type, string id)
	{
		if (type == typeof(int))
		{
			return int.Parse(id, (IFormatProvider)(object)CultureInfo.InvariantCulture);
		}
		if (type == typeof(string))
		{
			return id;
		}
		return ((MethodBase)Enumerable.Single<MethodInfo>((System.Collections.Generic.IEnumerable<MethodInfo>)type.GetMethods((BindingFlags)60), (Func<MethodInfo, bool>)((MethodInfo m) => ((MemberInfo)m).Name == "Parse" && ((MethodBase)m).IsStatic && ((MethodBase)m).GetParameters().Length == 1 && ((MethodBase)m).GetParameters()[0].ParameterType == typeof(string)))).Invoke((object)null, new object[1] { id });
	}

	internal static System.Type FindType(string fullName)
	{
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
		for (int i = 0; i < assemblies.Length; i++)
		{
			System.Type type = assemblies[i].GetType(fullName, false);
			if (type != null)
			{
				return type;
			}
		}
		throw new TypeLoadException("Required interop type missing: " + fullName);
	}

	internal static void LoadAssembly(string name, string directory)
	{
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		if (!Enumerable.Any<Assembly>((System.Collections.Generic.IEnumerable<Assembly>)AppDomain.CurrentDomain.GetAssemblies(), (Func<Assembly, bool>)((Assembly a) => a.GetName().Name == name)))
		{
			string text = Path.Combine(directory, name + ".dll");
			if (!File.Exists(text))
			{
				throw new FileNotFoundException("Missing generated interop assembly", text);
			}
			Assembly.LoadFrom(text);
		}
	}

	internal static object AsConcrete(object proxy, System.Type concrete)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		if (concrete.IsInstanceOfType(proxy))
		{
			return proxy;
		}
		Il2CppObjectBase val = (Il2CppObjectBase)((proxy is Il2CppObjectBase) ? proxy : null);
		if (val == null)
		{
			throw new InvalidCastException("Expected IL2CPP object proxy");
		}
		return Activator.CreateInstance(concrete, new object[1] { val.Pointer }) ?? throw new InvalidOperationException("Could not wrap repository");
	}

	internal static bool SameNative(object? first, object? second)
	{
		Il2CppObjectBase val = (Il2CppObjectBase)((first is Il2CppObjectBase) ? first : null);
		if (val != null)
		{
			Il2CppObjectBase val2 = (Il2CppObjectBase)((second is Il2CppObjectBase) ? second : null);
			if (val2 != null)
			{
				return val.Pointer == val2.Pointer;
			}
		}
		return false;
	}

	internal static MethodInfo UniqueMethod(System.Type type, string name, int count)
	{
		return Enumerable.Single<MethodInfo>((System.Collections.Generic.IEnumerable<MethodInfo>)type.GetMethods((BindingFlags)60), (Func<MethodInfo, bool>)((MethodInfo m) => ((MemberInfo)m).Name == name && ((MethodBase)m).GetParameters().Length == count));
	}
}
