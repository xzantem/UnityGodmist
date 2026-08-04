using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Systems.Items;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class LootCollectionServiceTests
    {
        private LootCollectionService lootCollectionService;

        [SetUp]
        public void SetUp()
        {
            lootCollectionService = new LootCollectionService();
        }

        [Test]
        public void TryClaimPendingLoot_ActiveCharacter_MovesItemToInventory()
        {
            var gameSession = new GameSession();
            var character = CreateCharacter("character.active");

            gameSession.TryRegisterCharacter(character);
            gameSession.TryAddCharacterToActiveParty(character.CharacterId);

            var item = CreateItem("item.loot_01", weight: 2f);
            gameSession.AddPendingLoot(new[] { item });

            var claimed = lootCollectionService.TryClaimPendingLoot(
                gameSession,
                character.CharacterId,
                item.InstanceId);

            Assert.That(claimed, Is.True);
            Assert.That(gameSession.PendingLoot, Is.Empty);
            Assert.That(character.Inventory.Items, Has.Count.EqualTo(1));
            Assert.That(character.Inventory.Items[0], Is.SameAs(item));
        }

        [Test]
        public void TryClaimPendingLoot_OverweightItem_LeavesItemPending()
        {
            var gameSession = new GameSession();
            var character = CreateCharacter("character.active");

            character.Inventory.MaxWeight = 1f;

            gameSession.TryRegisterCharacter(character);
            gameSession.TryAddCharacterToActiveParty(character.CharacterId);

            var item = CreateItem("item.heavy_loot", weight: 2f);
            gameSession.AddPendingLoot(new[] { item });

            var claimed = lootCollectionService.TryClaimPendingLoot(
                gameSession,
                character.CharacterId,
                item.InstanceId);

            Assert.That(claimed, Is.False);
            Assert.That(gameSession.PendingLoot, Has.Count.EqualTo(1));
            Assert.That(gameSession.PendingLoot[0], Is.SameAs(item));
            Assert.That(character.Inventory.Items, Is.Empty);
        }

        [Test]
        public void TryClaimPendingLoot_BenchCharacter_IsRejected()
        {
            var gameSession = new GameSession();
            var character = CreateCharacter("character.bench");

            gameSession.TryRegisterCharacter(character);
            gameSession.TryAddCharacterToBench(character.CharacterId);

            var item = CreateItem("item.pending_loot", weight: 1f);
            gameSession.AddPendingLoot(new[] { item });

            var claimed = lootCollectionService.TryClaimPendingLoot(
                gameSession,
                character.CharacterId,
                item.InstanceId);

            Assert.That(claimed, Is.False);
            Assert.That(gameSession.PendingLoot, Has.Count.EqualTo(1));
            Assert.That(character.Inventory.Items, Is.Empty);
        }

        private static CharacterInstance CreateCharacter(string characterId)
        {
            return new CharacterInstance
            {
                CharacterId = characterId,
                LifeState = CharacterLifeState.Alive,
                AvailabilityState = CharacterAvailabilityState.Available,
                Inventory = new Inventory()
            };
        }

        private static ItemInstance CreateItem(
            string instanceId,
            float weight)
        {
            var definition = ScriptableObject.CreateInstance<ItemDefinition>();

            SetAutoProperty(definition, "Id", $"definition.{instanceId}");
            SetAutoProperty(definition, "DisplayName", instanceId);
            SetAutoProperty(definition, "ItemType", ItemType.Material);
            SetAutoProperty(definition, "BaseRarity", RarityTier.Common);
            SetAutoProperty(definition, "Weight", weight);

            return new ItemInstance(definition, instanceId);
        }

        private static void SetAutoProperty(
            object target,
            string propertyName,
            object value)
        {
            var field = target.GetType().GetField(
                $"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }
    }
}