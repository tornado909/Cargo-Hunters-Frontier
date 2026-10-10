namespace Frontier.ReferenceFix;

public sealed record PartyIdentity(int Version, string Session, string Map, int PlayerId, string Equipment, string Character, string Name);
