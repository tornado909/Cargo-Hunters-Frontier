namespace Frontier.Survivor;

internal sealed record ProgressState(int Level, int Experience, int Kills, int Pending, int OfferSerial, int[] Upgrades, int[] Offer);
