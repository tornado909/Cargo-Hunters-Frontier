namespace Frontier.Engine;

public sealed record ReadinessResult(bool Ready, string Reason, int ReadyCount, int MemberCount);
