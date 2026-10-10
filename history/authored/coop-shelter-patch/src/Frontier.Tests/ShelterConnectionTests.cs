using Frontier.Engine;

internal static class ShelterConnectionTests
{
    private sealed class Connection { internal readonly int Id; internal Connection(int id) { Id = id; } }
    private sealed class State { internal object? Current; }
    private static bool SameNative(object? a, object? b) =>
        a is Connection x && b is Connection y && x.Id == y.Id;

    public static void Run()
    {
        int count = 0, reads = 0;
        void Check(bool condition, string label)
        { count++; if (!condition) throw new InvalidOperationException("Shelter late-binding regression: " + label); }
        object? Read(object value) { reads++; return ((State)value).Current; }
        var binding = new ShelterConnectionBinding();
        var owner = new State();
        var first = new Connection(11);
        var firstOtherWrapper = new Connection(11);
        var unrelated = new Connection(22);

        Check(!binding.MatchesCurrent(first, Read, SameNative), "no shelter intent");
        Check(reads == 0, "no read before native path");
        binding.Enter();
        Check(binding.IntentObserved && !binding.HasNativeState, "Enter alone has no owner state");
        Check(!binding.MatchesCurrent(first, Read, SameNative), "Enter alone cannot bypass raid gate");

        // Actual reported lifecycle: prefix -> allocate -> assign _connection -> Connect.
        // The previous implementation threw at the first line when Current was null.
        binding.ObserveNativeCall(owner);
        Check(binding.HasNativeState, "prefix records state with null connection");
        Check(reads == 0, "prefix must not access uninitialized field");
        Check(!binding.MatchesCurrent(first, Read, SameNative), "null field still denies at Connect");
        owner.Current = first;
        Check(binding.MatchesCurrent(first, Read, SameNative), "assignment inside native method is observed");
        Check(binding.MatchesCurrent(firstOtherWrapper, Read, SameNative), "distinct proxy with same native identity");
        Check(!binding.MatchesCurrent(unrelated, Read, SameNative), "unrelated native object denied");
        Check(!binding.MatchesCurrent(null, Read, SameNative), "null incoming denied");

        // Never retain stale privileges when the game replaces the connection.
        owner.Current = unrelated;
        Check(!binding.MatchesCurrent(first, Read, SameNative), "old connection not cached");
        Check(binding.MatchesCurrent(unrelated, Read, SameNative), "replacement connection observed");
        owner.Current = null;
        Check(!binding.MatchesCurrent(unrelated, Read, SameNative), "cleared field denies reconnect");
        owner.Current = first;
        binding.ClearIfOwnedBy(new State(), SameNative);
        Check(binding.MatchesCurrent(first, Read, SameNative), "foreign finalizer cannot clear current owner");
        binding.ClearIfOwnedBy(owner, SameNative);
        Check(!binding.IntentObserved && !binding.HasNativeState, "failed native call clears binding");
        Check(!binding.MatchesCurrent(first, Read, SameNative), "failed native call grants nothing");

        binding.Enter(); binding.ObserveNativeCall(owner);
        binding.Leave();
        Check(!binding.MatchesCurrent(first, Read, SameNative), "shelter Leave revokes exemption");
        binding.Enter();
        Check(!binding.MatchesCurrent(first, Read, SameNative), "new Enter cannot reuse old owner");
        var secondOwner = new State { Current = unrelated };
        binding.ObserveNativeCall(secondOwner);
        binding.ClearIfOwnedBy(owner, SameNative);
        Check(binding.MatchesCurrent(unrelated, Read, SameNative), "old failure cannot invalidate new shelter");
        Check(!binding.MatchesCurrent(first, Read, SameNative), "old owner's connection denied");

        bool failed = false;
        try { binding.MatchesCurrent(unrelated, _ => throw new InvalidOperationException("field unavailable"), SameNative); }
        catch (InvalidOperationException) { failed = true; }
        Check(failed, "reflection failure must not implicitly allow a connection");

        // All combinations of the existing destination/catalog policy.
        for (int mask = 0; mask < 16; mask++)
        {
            bool path = (mask & 1) != 0, steam = (mask & 2) != 0;
            bool loopback = (mask & 4) != 0, ready = (mask & 8) != 0;
            Check(ConnectionPolicy.AllowLocalShelter(path, steam, loopback, ready) ==
                (path && !steam && loopback && ready), "local-only policy " + mask);
        }
        Check(!ConnectionPolicy.DeploymentReady(true, false, false, "lobby"), "host raid still needs lock");
        Check(!ConnectionPolicy.DeploymentReady(true, true, false, "locked"), "host raid still needs frozen roster");
        Check(ConnectionPolicy.DeploymentReady(true, true, true, "locked"), "fully approved host raid");
        Check(!ConnectionPolicy.DeploymentReady(false, false, false, "lobby"), "client raid waits for host lock");
        Check(ConnectionPolicy.DeploymentReady(false, false, false, "locked"), "client locked phase proceeds to existing checks");
        Console.WriteLine($"PASS: {count} co-op shelter late-binding assertions. Native/Steam execution NOT exercised.");
    }
}
