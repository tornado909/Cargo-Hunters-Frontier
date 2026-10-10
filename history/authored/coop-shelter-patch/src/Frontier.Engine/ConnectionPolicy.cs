namespace Frontier.Engine;
/// <summary>Only native-identified local shelters may connect before raid deployment.</summary>
public static class ConnectionPolicy
{
    public static bool AllowLocalShelter(bool nativeShelterConnection, bool isSteam, bool loopback, bool catalogReady)
        => nativeShelterConnection && !isSteam && loopback && catalogReady;
    public static bool DeploymentReady(bool isOwner, bool ownerLocked, bool rosterFrozen, string phase)
        => isOwner ? ownerLocked && rosterFrozen : phase == "locked";
}


/// <summary>
/// Tracks native shelter intent, not a cached connection captured too early.
/// An Enter alone gives no permission: ConnectToShelter must be observed, and
/// the incoming connection must match that state's CURRENT assigned field.
/// Kept free of Unity/Steam dependencies so lifecycle regressions can be tested.
/// </summary>
public sealed class ShelterConnectionBinding
{
    private object? state;
    public bool IntentObserved { get; private set; }
    public bool HasNativeState => IntentObserved && state is not null;

    public void Enter() { IntentObserved = true; state = null; }
    public void Leave() { IntentObserved = false; state = null; }

    public void ObserveNativeCall(object ownerState)
    {
        if (ownerState is null) throw new ArgumentNullException(nameof(ownerState));
        IntentObserved = true;
        state = ownerState;
        // No field read here. The game has not necessarily allocated it yet.
    }

    public bool MatchesCurrent(object? incoming, Func<object, object?> readConnection,
        Func<object?, object?, bool> sameNative)
    {
        object? ownerState = state;
        if (!IntentObserved || ownerState is null || incoming is null) return false;
        object? current = readConnection(ownerState);
        return current is not null &&
            (ReferenceEquals(current, incoming) || sameNative(current, incoming));
    }

    public void ClearIfOwnedBy(object ownerState, Func<object?, object?, bool> sameNative)
    {
        if (state is not null &&
            (ReferenceEquals(state, ownerState) || sameNative(state, ownerState))) Leave();
    }
}
