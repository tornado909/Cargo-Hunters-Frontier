using System.Runtime.InteropServices;
using Frontier.Engine;

namespace Frontier.Core;

/// <summary>
/// Uses the game's ALREADY loaded Steam API. Does not call Init, replace DLLs, register callbacks,
/// open ports, or bypass ownership/authentication. All callers must be on the game's main thread.
/// Flat-API exports are verified in the supplied steam_api64.dll; managed member-data wrappers
/// are stripped from this game's Steamworks.NET interop, hence these small native bindings.
/// </summary>
internal sealed class SteamLobbyApi
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern IntPtr GetModuleHandleW(string moduleName);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr GetInterface();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr GetLobbyText(IntPtr self, ulong lobby, [MarshalAs(UnmanagedType.LPUTF8Str)] string key);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr GetMemberText(IntPtr self, ulong lobby, ulong member, [MarshalAs(UnmanagedType.LPUTF8Str)] string key);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SetMemberText(IntPtr self, ulong lobby, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool SetLobbyText(IntPtr self, ulong lobby, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetCount(IntPtr self, ulong lobby);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate ulong GetMemberId(IntPtr self, ulong lobby, int index);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate ulong GetOwnerId(IntPtr self, ulong lobby);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] private delegate bool RequestData(IntPtr self, ulong lobby);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] private delegate bool SetJoinability(IntPtr self, ulong lobby, [MarshalAs(UnmanagedType.I1)] bool joinable);

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
    internal static readonly string[] RequiredExports = {
        "SteamAPI_SteamMatchmaking_v009", "SteamAPI_ISteamMatchmaking_GetLobbyData", "SteamAPI_ISteamMatchmaking_SetLobbyData",
        "SteamAPI_ISteamMatchmaking_GetLobbyMemberData", "SteamAPI_ISteamMatchmaking_SetLobbyMemberData",
        "SteamAPI_ISteamMatchmaking_GetNumLobbyMembers", "SteamAPI_ISteamMatchmaking_GetLobbyMemberByIndex",
        "SteamAPI_ISteamMatchmaking_GetLobbyOwner", "SteamAPI_ISteamMatchmaking_RequestLobbyData", "SteamAPI_ISteamMatchmaking_SetLobbyJoinable" };

    public SteamLobbyApi()
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess) throw new PlatformNotSupportedException("Co-op adapter targets Windows x64");
        IntPtr module = GetModuleHandleW("steam_api64.dll");
        if (module == IntPtr.Zero) throw new InvalidOperationException("The game has not loaded steam_api64.dll");
        T Export<T>(string name) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(module, name));
        accessor = Export<GetInterface>(RequiredExports[0]);
        getData = Export<GetLobbyText>(RequiredExports[1]); setData = Export<SetLobbyText>(RequiredExports[2]);
        getMemberData = Export<GetMemberText>(RequiredExports[3]); setMemberData = Export<SetMemberText>(RequiredExports[4]);
        count = Export<GetCount>(RequiredExports[5]); memberAt = Export<GetMemberId>(RequiredExports[6]);
        getOwner = Export<GetOwnerId>(RequiredExports[7]); request = Export<RequestData>(RequiredExports[8]); joinable = Export<SetJoinability>(RequiredExports[9]);
    }
    private IntPtr Interface()
    {
        IntPtr pointer = accessor();
        return pointer != IntPtr.Zero ? pointer : throw new InvalidOperationException("Steam matchmaking is unavailable");
    }
    private static string Text(IntPtr p) => p == IntPtr.Zero ? "" : Marshal.PtrToStringUTF8(p) ?? "";
    public string Get(ulong lobby, string key) => Text(getData(Interface(), lobby, key));
    public void Set(ulong lobby, string key, string value)
    {
        if (value.Length > 1024 || !key.StartsWith("frontier_", StringComparison.Ordinal)) throw new ArgumentException("Invalid Frontier lobby data");
        if (!setData(Interface(), lobby, key, value)) throw new InvalidOperationException("Steam rejected lobby metadata: " + key);
    }
    public void Ready(ulong lobby, string token) => setMemberData(Interface(), lobby, CoopProtocol.ReadyKey, token);
    public ulong Owner(ulong lobby) => getOwner(Interface(), lobby);
    public bool Request(ulong lobby) => request(Interface(), lobby);
    public bool SetJoinable(ulong lobby, bool value) => joinable(Interface(), lobby, value);
    public PeerReadiness[] Members(ulong lobby)
    {
        IntPtr api = Interface();
        int n = count(api, lobby);
        if (n is < 1 or > 250) return Array.Empty<PeerReadiness>();
        var result = new PeerReadiness[n];
        for (int i = 0; i < n; i++)
        {
            ulong id = memberAt(api, lobby, i);
            result[i] = new(id, Text(getMemberData(api, lobby, id, CoopProtocol.ReadyKey)));
        }
        // A join/leave while enumerating is a retry, never approval of a partial roster.
        if (count(api, lobby) != n) return Array.Empty<PeerReadiness>();
        return result;
    }
}
