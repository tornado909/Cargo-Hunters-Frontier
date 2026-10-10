namespace Frontier.Loot;

public sealed record BotSpawnRecipe<TId, TEquipment>(TId Id, string Alias, TId AISettingId, TId PlatformPresetId, TId EquipmentPresetId, TId LootPresetId, TEquipment Equipments, int OverrideBotAccountLevel)
{
	public BotSpawnRecipe<TId, TEquipment> WithRankAI(TId ai)
	{
		return this with
		{
			AISettingId = ai
		};
	}
}
