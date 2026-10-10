using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Frontier.ReferenceFix;

internal sealed class PartyTransport
{
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate IntPtr Accessor();

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate int CountFn(IntPtr self, ulong lobby);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate ulong MemberFn(IntPtr self, ulong lobby, int index);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate IntPtr GetFn(IntPtr self, ulong lobby, ulong member, [MarshalAs(UnmanagedType.LPUTF8Str)] string key);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate void SetFn(IntPtr self, ulong lobby, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

	internal const string Key = "frontier_party_identity_v107";

	private readonly Accessor accessor;

	private readonly CountFn count;

	private readonly MemberFn member;

	private readonly GetFn get;

	private readonly SetFn set;

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
	private static extern IntPtr GetModuleHandleW(string name);

	internal PartyTransport()
	{
		if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess)
		{
			throw new PlatformNotSupportedException("Steam party markers require the game's Windows x64 Steam API");
		}
		IntPtr dll = GetModuleHandleW("steam_api64.dll");
		if (dll == IntPtr.Zero)
		{
			throw new InvalidOperationException("Steam API is not loaded");
		}
		accessor = Load<Accessor>("SteamAPI_SteamMatchmaking_v009");
		count = Load<CountFn>("SteamAPI_ISteamMatchmaking_GetNumLobbyMembers");
		member = Load<MemberFn>("SteamAPI_ISteamMatchmaking_GetLobbyMemberByIndex");
		get = Load<GetFn>("SteamAPI_ISteamMatchmaking_GetLobbyMemberData");
		set = Load<SetFn>("SteamAPI_ISteamMatchmaking_SetLobbyMemberData");
		T Load<T>(string n) where T : notnull, Delegate
		{
			return Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(dll, n));
		}
	}

	private IntPtr Self()
	{
		IntPtr intPtr = accessor();
		if (!(intPtr != IntPtr.Zero))
		{
			throw new InvalidOperationException("Steam matchmaking unavailable");
		}
		return intPtr;
	}

	internal void Publish(ulong lobby, string identity)
	{
		if (lobby == 0L || identity.Length > 900)
		{
			throw new ArgumentException("Party identity bounds");
		}
		set(Self(), lobby, "frontier_party_identity_v107", identity);
	}

	internal KeyValuePair<ulong, string>[] Read(ulong lobby)
	{
		IntPtr self = Self();
		int num = count(self, lobby);
		if (num < 1 || num > 16)
		{
			return Array.Empty<KeyValuePair<ulong, string>>();
		}
		KeyValuePair<ulong, string>[] array = new KeyValuePair<ulong, string>[num];
		for (int i = 0; i < num; i++)
		{
			ulong key = member(self, lobby, i);
			IntPtr intPtr = get(self, lobby, key, "frontier_party_identity_v107");
			array[i] = new KeyValuePair<ulong, string>(key, (intPtr == IntPtr.Zero) ? "" : (Marshal.PtrToStringUTF8(intPtr) ?? ""));
		}
		if (count(self, lobby) != num)
		{
			return Array.Empty<KeyValuePair<ulong, string>>();
		}
		return array;
	}
}
