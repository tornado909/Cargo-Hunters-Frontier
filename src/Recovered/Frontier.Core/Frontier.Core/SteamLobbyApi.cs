using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Frontier.Engine;

namespace Frontier.Core;

internal sealed class SteamLobbyApi
{
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate System.IntPtr GetInterface();

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate System.IntPtr GetLobbyText(System.IntPtr self, ulong lobby, [MarshalAs((UnmanagedType)48)] string key);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate System.IntPtr GetMemberText(System.IntPtr self, ulong lobby, ulong member, [MarshalAs((UnmanagedType)48)] string key);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate void SetMemberText(System.IntPtr self, ulong lobby, [MarshalAs((UnmanagedType)48)] string key, [MarshalAs((UnmanagedType)48)] string value);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	[return: MarshalAs((UnmanagedType)3)]
	private delegate bool SetLobbyText(System.IntPtr self, ulong lobby, [MarshalAs((UnmanagedType)48)] string key, [MarshalAs((UnmanagedType)48)] string value);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate int GetCount(System.IntPtr self, ulong lobby);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate ulong GetMemberId(System.IntPtr self, ulong lobby, int index);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate ulong GetOwnerId(System.IntPtr self, ulong lobby);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	[return: MarshalAs((UnmanagedType)3)]
	private delegate bool RequestData(System.IntPtr self, ulong lobby);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	[return: MarshalAs((UnmanagedType)3)]
	private delegate bool SetJoinability(System.IntPtr self, ulong lobby, [MarshalAs((UnmanagedType)3)] bool joinable);

	private readonly GetInterface accessor;

	private readonly GetLobbyText getData;

	private readonly SetLobbyText setData;

	private readonly GetMemberText getMemberData;

	private readonly SetMemberText setMemberData;

	private readonly GetCount count;

	private readonly GetMemberId memberAt;

	private readonly GetOwnerId getOwner;

	private readonly RequestData request;

	private readonly SetJoinability joinable;

	internal static readonly string[] RequiredExports = new string[10] { "SteamAPI_SteamMatchmaking_v009", "SteamAPI_ISteamMatchmaking_GetLobbyData", "SteamAPI_ISteamMatchmaking_SetLobbyData", "SteamAPI_ISteamMatchmaking_GetLobbyMemberData", "SteamAPI_ISteamMatchmaking_SetLobbyMemberData", "SteamAPI_ISteamMatchmaking_GetNumLobbyMembers", "SteamAPI_ISteamMatchmaking_GetLobbyMemberByIndex", "SteamAPI_ISteamMatchmaking_GetLobbyOwner", "SteamAPI_ISteamMatchmaking_RequestLobbyData", "SteamAPI_ISteamMatchmaking_SetLobbyJoinable" };

	[DllImport("kernel32.dll", CharSet = (CharSet)3, ExactSpelling = true)]
	private static extern System.IntPtr GetModuleHandleW(string moduleName);

	public SteamLobbyApi()
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess)
		{
			throw new PlatformNotSupportedException("Co-op adapter targets Windows x64");
		}
		System.IntPtr module = GetModuleHandleW("steam_api64.dll");
		if (module == System.IntPtr.Zero)
		{
			throw new InvalidOperationException("The game has not loaded steam_api64.dll");
		}
		accessor = Export<GetInterface>(RequiredExports[0]);
		getData = Export<GetLobbyText>(RequiredExports[1]);
		setData = Export<SetLobbyText>(RequiredExports[2]);
		getMemberData = Export<GetMemberText>(RequiredExports[3]);
		setMemberData = Export<SetMemberText>(RequiredExports[4]);
		count = Export<GetCount>(RequiredExports[5]);
		memberAt = Export<GetMemberId>(RequiredExports[6]);
		getOwner = Export<GetOwnerId>(RequiredExports[7]);
		request = Export<RequestData>(RequiredExports[8]);
		joinable = Export<SetJoinability>(RequiredExports[9]);
		[CompilerGenerated]
		T Export<T>(string name) where T : notnull, System.Delegate
		{
			return Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(module, name));
		}
	}

	private System.IntPtr Interface()
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		System.IntPtr intPtr = accessor();
		if (!(intPtr != System.IntPtr.Zero))
		{
			throw new InvalidOperationException("Steam matchmaking is unavailable");
		}
		return intPtr;
	}

	private static string Text(System.IntPtr p)
	{
		object obj;
		if (!(p == System.IntPtr.Zero))
		{
			obj = Marshal.PtrToStringUTF8(p);
			if (obj == null)
			{
				return "";
			}
		}
		else
		{
			obj = "";
		}
		return (string)obj;
	}

	public string Get(ulong lobby, string key)
	{
		return Text(getData(Interface(), lobby, key));
	}

	public void Set(ulong lobby, string key, string value)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		if (value.Length > 1024 || !key.StartsWith("frontier_", (StringComparison)4))
		{
			throw new ArgumentException("Invalid Frontier lobby data");
		}
		if (!setData(Interface(), lobby, key, value))
		{
			throw new InvalidOperationException("Steam rejected lobby metadata: " + key);
		}
	}

	public void Ready(ulong lobby, string token)
	{
		setMemberData(Interface(), lobby, "frontier_ready", token);
	}

	public ulong Owner(ulong lobby)
	{
		return getOwner(Interface(), lobby);
	}

	public bool Request(ulong lobby)
	{
		return request(Interface(), lobby);
	}

	public bool SetJoinable(ulong lobby, bool value)
	{
		return joinable(Interface(), lobby, value);
	}

	public PeerReadiness[] Members(ulong lobby)
	{
		System.IntPtr self = Interface();
		int num = count(self, lobby);
		if (num < 1 || num > 250)
		{
			return System.Array.Empty<PeerReadiness>();
		}
		PeerReadiness[] array = new PeerReadiness[num];
		for (int i = 0; i < num; i++)
		{
			ulong num2 = memberAt(self, lobby, i);
			array[i] = new PeerReadiness(num2, Text(getMemberData(self, lobby, num2, "frontier_ready")));
		}
		if (count(self, lobby) != num)
		{
			return System.Array.Empty<PeerReadiness>();
		}
		return array;
	}
}
