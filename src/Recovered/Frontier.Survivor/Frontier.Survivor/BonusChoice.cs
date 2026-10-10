namespace Frontier.Survivor;

internal sealed record BonusChoice(RunHeader Header, ulong Member, int PlayerId, long ChoiceSequence, int OfferSerial, int Index);
