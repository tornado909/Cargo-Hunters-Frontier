namespace Frontier.Survivor;

internal static class NativePrefabCatalog
{
	internal sealed record Entry(string Name, string GhostId, int? NetworkId, string[] Components);

	internal const string AssetSha256 = "6ddfeb37285ad613b78f4502fc90c32b6afe680a66b30b481cd721acc434b2b8";

	internal static readonly Entry[] Entries = new Entry[14]
	{
		new Entry("NetworkCharacter", "1a966299129f24a7f857d4ba40a325a3", 873215959, new string[11]
		{
			"Unity.NetCode.GhostAuthoringComponent", "Unity.NetCode.GhostOwnerComponentAuthoring", "Game.Network.PlayerIdAuthoring", "Game.Character.CharacterAuthoring", "Unity.Physics.Authoring.PhysicsShapeAuthoring", "Unity.Physics.Authoring.PhysicsBodyAuthoring", "Game.MovementSystems.LocomotionAuthoring", "Game.MovementSystems.MovablePhysicalBodyAuthoring", "Game.DamageSystem.HealthAuthoring", "Game.DamageSystem.DeathReactionAuthoring",
			"Game.Network.NetworkPrefabAuthoring"
		}),
		new Entry("NetworkDoor", "a6f39ead0931d154daf0e41dd4b1e986", null, new string[2] { "Unity.NetCode.GhostAuthoringComponent", "Game.DoorsSystem.NetworkDoorAuthoring" }),
		new Entry("NetworkDroppedItem", "9c8a0e26b82c741879718ad87bbfda39", 2081739303, new string[6] { "Unity.NetCode.GhostAuthoringComponent", "Game.ItemsSystem.Components.DroppedItemAuthoring", "Game.InteractionSystem.InteractiveObjectAuthoring", "Game.InteractionSystem.HighlightedObjectAuthoring", "Game.ItemsSystem.Components.OverrideInteractionColliderAuthoring", "Game.Network.NetworkPrefabAuthoring" }),
		new Entry("NetworkMeleeWeapon", "885b2f705a8db40789c090a2b140bd60", null, new string[3] { "Unity.NetCode.GhostAuthoringComponent", "Unity.NetCode.GhostOwnerComponentAuthoring", "Game.Weapons.WeaponAuthoring" }),
		new Entry("NetworkProjectile", "5bf7d4c79717d4853a084fa7767a310e", null, new string[3] { "Unity.NetCode.GhostAuthoringComponent", "Game.ProjectilesSystem.GhostProjectileAuthoring", "Unity.NetCode.GhostOwnerComponentAuthoring" }),
		new Entry("NetworkWeapon", "8630c1ecdab2c4410aa737bb9ab42ba3", 714877343, new string[4] { "Unity.NetCode.GhostAuthoringComponent", "Unity.NetCode.GhostOwnerComponentAuthoring", "Game.Weapons.WeaponAuthoring", "Game.Network.NetworkPrefabAuthoring" }),
		new Entry("NetworkUsableItem", "d85b62280d8079e4fab98a235d2709d1", 1480988092, new string[4] { "Unity.NetCode.GhostAuthoringComponent", "Unity.NetCode.GhostOwnerComponentAuthoring", "Game.UsableItemSystem.UsableItemAuthoring", "Game.Network.NetworkPrefabAuthoring" }),
		new Entry("NetworkDebugObject", "1316ae0183159a944882f07f3eb554bc", null, new string[3] { "Game.DebugSystem.NetworkDebugObjectAuthoring", "Unity.NetCode.GhostAuthoringComponent", "Core.HybridEntity.HybridCompanionPrefabAuthoring" }),
		new Entry("NetworkSceneObject", "e3b382aefbf89bc479bb35602a4fdffc", null, new string[2] { "Unity.NetCode.GhostAuthoringComponent", "Game.SceneObjectsSystem.SceneObjectGhostAuthoring" }),
		new Entry("NetworkDroppedItem_HighPriority", "46cd7f380358e43c39cc829ef3b4d7ce", 1624735793, new string[6] { "Unity.NetCode.GhostAuthoringComponent", "Game.ItemsSystem.Components.DroppedItemAuthoring", "Game.InteractionSystem.InteractiveObjectAuthoring", "Game.InteractionSystem.HighlightedObjectAuthoring", "Game.ItemsSystem.Components.OverrideInteractionColliderAuthoring", "Game.Network.NetworkPrefabAuthoring" }),
		new Entry("NetworkDroppedItem_Invisible", "9b05bd6aa4cfda34e9f97f3288019f8c", 360489006, new string[6] { "Unity.NetCode.GhostAuthoringComponent", "Game.ItemsSystem.Components.DroppedItemAuthoring", "Game.InteractionSystem.InteractiveObjectAuthoring", "Game.InteractionSystem.HighlightedObjectAuthoring", "Game.ItemsSystem.Components.OverrideInteractionColliderAuthoring", "Game.Network.NetworkPrefabAuthoring" }),
		new Entry("NetworkMeleeWeapon_WithDurability", "1b7564b1acf7c4f61b315cbe30b6bf6b", 1060314713, new string[5] { "Unity.NetCode.GhostAuthoringComponent", "Unity.NetCode.GhostOwnerComponentAuthoring", "Game.Weapons.WeaponAuthoring", "Game.Network.NetworkPrefabAuthoring", "Game.ItemsSystem.Components.DurabilityAuthoring" }),
		new Entry("NetworkUsableItem_WithDurability", "c6ae703657b454344b6f6ce2fa92bf67", 1816392190, new string[5] { "Unity.NetCode.GhostAuthoringComponent", "Unity.NetCode.GhostOwnerComponentAuthoring", "Game.UsableItemSystem.UsableItemAuthoring", "Game.Network.NetworkPrefabAuthoring", "Game.ItemsSystem.Components.DurabilityAuthoring" }),
		new Entry("NetworkCharacter_Helicopter", "a558689cc2be848a1a42d20365f5758d", 356436594, new string[11]
		{
			"Unity.NetCode.GhostAuthoringComponent", "Unity.NetCode.GhostOwnerComponentAuthoring", "Game.Network.PlayerIdAuthoring", "Game.Character.CharacterAuthoring", "Unity.Physics.Authoring.PhysicsShapeAuthoring", "Unity.Physics.Authoring.PhysicsBodyAuthoring", "Game.MovementSystems.LocomotionAuthoring", "Game.MovementSystems.MovablePhysicalBodyAuthoring", "Game.DamageSystem.HealthAuthoring", "Game.DamageSystem.DeathReactionAuthoring",
			"Game.Network.NetworkPrefabAuthoring"
		})
	};
}
