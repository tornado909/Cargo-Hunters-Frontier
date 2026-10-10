namespace Frontier.Survivor;

internal sealed record RunHeader(int Version, string Session, string Map, string Geometry, string Run, long Sequence);
