namespace Frontier.Survivor;

internal sealed record PersonalSnapshot(RunHeader Header, ulong Member, int PlayerId, string Equipment, string Character, double Seconds, int Alive, bool Ended, ProgressState Progress);
