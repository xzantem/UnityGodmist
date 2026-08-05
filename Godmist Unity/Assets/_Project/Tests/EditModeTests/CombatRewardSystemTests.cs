using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class CombatRewardSystemTests
    {
        [Test]
        public void TryGrantVictoryRewards_RewardsOnlyActivePartyAndOnlyOnce()
        {
            var gameSession = new GameSession();

            var firstActiveCharacter = CreateCharacter("character.active_1");
            var secondActiveCharacter = CreateCharacter("character.active_2");
            var benchCharacter = CreateCharacter("character.bench");

            gameSession.TryRegisterCharacter(firstActiveCharacter);
            gameSession.TryRegisterCharacter(secondActiveCharacter);
            gameSession.TryRegisterCharacter(benchCharacter);

            gameSession.TryAddCharacterToActiveParty(firstActiveCharacter.CharacterId);
            gameSession.TryAddCharacterToActiveParty(secondActiveCharacter.CharacterId);
            gameSession.TryAddCharacterToBench(benchCharacter.CharacterId);

            var itemDefinition = CreateItemDefinition("item.reward_material");
            var droppedItem = new ItemInstance(
                itemDefinition,
                "item.reward_instance");

            var combatSession = new CombatSession
            {
                Outcome = CombatOutcomeState.Victory,
                Rewards = new CombatRewardResult
                {
                    ExperiencePerActiveCharacter = 24,
                    Gold = 15,
                    Items = { droppedItem }
                }
            };

            var combatRewardSystem = new CombatRewardSystem();

            var firstGrantSucceeded = combatRewardSystem.TryGrantVictoryRewards(
                gameSession,
                combatSession);

            var secondGrantSucceeded = combatRewardSystem.TryGrantVictoryRewards(
                gameSession,
                combatSession);

            Assert.That(firstGrantSucceeded, Is.True);
            Assert.That(secondGrantSucceeded, Is.False);
            Assert.That(combatSession.RewardsGranted, Is.True);

            Assert.That(firstActiveCharacter.Level, Is.EqualTo(2));
            Assert.That(firstActiveCharacter.AvailableSkillPoints, Is.EqualTo(1));

            Assert.That(secondActiveCharacter.Level, Is.EqualTo(2));
            Assert.That(secondActiveCharacter.AvailableSkillPoints, Is.EqualTo(1));

            Assert.That(benchCharacter.Level, Is.EqualTo(1));
            Assert.That(benchCharacter.AvailableSkillPoints, Is.EqualTo(0));

            Assert.That(gameSession.Gold, Is.EqualTo(15));
            Assert.That(gameSession.PendingLoot, Has.Count.EqualTo(1));
            Assert.That(gameSession.PendingLoot[0], Is.SameAs(droppedItem));
            Assert.That(gameSession.LastCombatRewards, Is.SameAs(combatSession.Rewards));
        }

        [Test]
        public void TryGrantVictoryRewards_NonVictoryOutcome_DoesNotGrantAnything()
        {
            var gameSession = new GameSession();
            var character = CreateCharacter("character.active");

            gameSession.TryRegisterCharacter(character);
            gameSession.TryAddCharacterToActiveParty(character.CharacterId);

            var combatSession = new CombatSession
            {
                Outcome = CombatOutcomeState.Escaped,
                Rewards = new CombatRewardResult
                {
                    ExperiencePerActiveCharacter = 24,
                    Gold = 15
                }
            };

            var combatRewardSystem = new CombatRewardSystem();

            var succeeded = combatRewardSystem.TryGrantVictoryRewards(
                gameSession,
                combatSession);

            Assert.That(succeeded, Is.False);
            Assert.That(character.Level, Is.EqualTo(1));
            Assert.That(gameSession.Gold, Is.EqualTo(0));
            Assert.That(gameSession.PendingLoot, Is.Empty);
        }

        private static CharacterInstance CreateCharacter(string characterId)
        {
            return new CharacterInstance
            {
                CharacterId = characterId,
                Level = 1,
                CurrentExperience = 0,
                AvailableSkillPoints = 0,
                LifeState = CharacterLifeState.Alive,
                AvailabilityState = CharacterAvailabilityState.Available
            };
        }

        private static ItemDefinition CreateItemDefinition(string id)
        {
            var definition = ScriptableObject.CreateInstance<ItemDefinition>();

            SetAutoProperty(definition, "Id", id);
            SetAutoProperty(definition, "DisplayName", id);
            SetAutoProperty(definition, "ItemType", ItemType.Material);
            SetAutoProperty(definition, "BaseRarity", RarityTier.Common);

            return definition;
        }

        private static void SetAutoProperty(
            object target,
            string propertyName,
            object value)
        {
            var field = target.GetType().GetField(
                $"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(
                field,
                Is.Not.Null,
                $"Nie znaleziono pola backing field dla {propertyName}.");

            field.SetValue(target, value);
        }
    }
}