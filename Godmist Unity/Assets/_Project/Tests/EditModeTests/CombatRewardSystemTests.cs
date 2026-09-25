using System.Collections.Generic;
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
        public void VictoryExperience_UsesEachCharactersLevelAndGrantsOnlyOnce()
        {
            var session = new GameSession();
            var low = CreateCharacter("low");
            var high = CreateCharacter("high");
            high.Level = 7;
            session.TryRegisterCharacter(low); session.TryRegisterCharacter(high);
            session.TryAddCharacterToActiveParty(low.CharacterId); session.TryAddCharacterToActiveParty(high.CharacterId);
            var combat = new CombatSession { Outcome = CombatOutcomeState.Victory };
            combat.Rewards.EnemyRewards.Add(new CombatEnemyReward { EnemyInstanceId = "enemy", ExperienceLevel = 1, ExperiencePerActiveCharacter = 60 });
            combat.TryMarkEnemyDefeated("enemy");
            var system = new CombatRewardSystem();
            Assert.That(system.TryGrantVictoryRewards(session, combat), Is.True);
            Assert.That(low.CurrentExperience, Is.EqualTo(60));
            Assert.That(high.CurrentExperience, Is.EqualTo(6));
            Assert.That(system.TryGrantVictoryRewards(session, combat), Is.False);
            Assert.That(high.CurrentExperience, Is.EqualTo(6));
        }

        [Test]
        public void TryGrantVictoryRewards_RewardsOnlyLivingAndDownedActivePartyForDefeatedEnemies()
        {
            var gameSession = new GameSession();

            var firstActiveCharacter = CreateCharacter("character.active_1");
            var secondActiveCharacter = CreateCharacter("character.active_2");
            secondActiveCharacter.LifeState = CharacterLifeState.Downed;
            var deadActiveCharacter = CreateCharacter("character.dead");
            deadActiveCharacter.LifeState = CharacterLifeState.Dead;
            deadActiveCharacter.Honor = 7;
            var benchCharacter = CreateCharacter("character.bench");

            gameSession.TryRegisterCharacter(firstActiveCharacter);
            gameSession.TryRegisterCharacter(secondActiveCharacter);
            gameSession.TryRegisterCharacter(deadActiveCharacter);
            gameSession.TryRegisterCharacter(benchCharacter);

            gameSession.TryAddCharacterToActiveParty(firstActiveCharacter.CharacterId);
            gameSession.TryAddCharacterToActiveParty(secondActiveCharacter.CharacterId);
            gameSession.PlayerParty.TryAddActiveMember(deadActiveCharacter.CharacterId);
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
                    EnemyRewards =
                    {
                        new CombatEnemyReward
                        {
                            EnemyInstanceId = "enemy.defeated",
                            ExperiencePerActiveCharacter = 2400,
                            Gold = 15,
                            Items = { droppedItem }
                        },
                        new CombatEnemyReward
                        {
                            EnemyInstanceId = "enemy.not_defeated",
                            ExperiencePerActiveCharacter = 2400,
                            Gold = 15
                        }
                    }
                }
            };

            combatSession.TryMarkEnemyDefeated("enemy.defeated");

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
            Assert.That(firstActiveCharacter.AvailableSkillPoints, Is.EqualTo(2));

            Assert.That(secondActiveCharacter.Level, Is.EqualTo(2));
            Assert.That(secondActiveCharacter.AvailableSkillPoints, Is.EqualTo(2));
            Assert.That(firstActiveCharacter.Honor, Is.EqualTo(1));
            Assert.That(secondActiveCharacter.Honor, Is.EqualTo(1));

            Assert.That(deadActiveCharacter.Level, Is.EqualTo(1));
            Assert.That(deadActiveCharacter.Honor, Is.EqualTo(7));

            Assert.That(benchCharacter.Level, Is.EqualTo(1));
            Assert.That(benchCharacter.AvailableSkillPoints, Is.EqualTo(0));

            Assert.That(gameSession.Gold, Is.EqualTo(0));
            Assert.That(gameSession.PendingLoot, Is.Empty);
            Assert.That(
                combatSession.Rewards.EnemyRewards[0].Gold,
                Is.EqualTo(15));
            Assert.That(
                combatSession.Rewards.EnemyRewards[0].Items,
                Is.EquivalentTo(new[] { droppedItem }));
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
                    ExperiencePerActiveCharacter = 2400,
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

        [Test]
        public void TryApplyEscapePenalty_PenalizesOnlyLivingAndDownedActivePartyOnce()
        {
            var gameSession = new GameSession();
            var aliveCharacter = CreateCharacter("character.alive");
            aliveCharacter.Honor = 8;
            var downedCharacter = CreateCharacter("character.downed");
            downedCharacter.LifeState = CharacterLifeState.Downed;
            downedCharacter.Honor = 3;
            var deadCharacter = CreateCharacter("character.dead");
            deadCharacter.LifeState = CharacterLifeState.Dead;
            deadCharacter.Honor = 4;
            var benchCharacter = CreateCharacter("character.bench");
            benchCharacter.Honor = 9;

            gameSession.TryRegisterCharacter(aliveCharacter);
            gameSession.TryRegisterCharacter(downedCharacter);
            gameSession.TryRegisterCharacter(deadCharacter);
            gameSession.TryRegisterCharacter(benchCharacter);
            gameSession.TryAddCharacterToActiveParty(aliveCharacter.CharacterId);
            gameSession.TryAddCharacterToActiveParty(downedCharacter.CharacterId);
            gameSession.PlayerParty.TryAddActiveMember(deadCharacter.CharacterId);
            gameSession.TryAddCharacterToBench(benchCharacter.CharacterId);

            var combatSession = new CombatSession
            {
                Outcome = CombatOutcomeState.Escaped
            };

            var combatRewardSystem = new CombatRewardSystem();

            Assert.That(
                combatRewardSystem.TryApplyEscapePenalty(gameSession, combatSession),
                Is.True);
            Assert.That(
                combatRewardSystem.TryApplyEscapePenalty(gameSession, combatSession),
                Is.False);
            Assert.That(aliveCharacter.Honor, Is.EqualTo(3));
            Assert.That(downedCharacter.Honor, Is.EqualTo(-2));
            Assert.That(deadCharacter.Honor, Is.EqualTo(4));
            Assert.That(benchCharacter.Honor, Is.EqualTo(9));
        }

        [Test]
        public void TryApplyEscapePenalty_PenalizesCharacterCapturedBeforeBeingAbandoned()
        {
            var gameSession = new GameSession();
            var abandonedCharacter = CreateCharacter("character.abandoned");
            abandonedCharacter.LifeState = CharacterLifeState.Dead;
            abandonedCharacter.Honor = 2;

            gameSession.TryRegisterCharacter(abandonedCharacter);
            gameSession.PlayerParty.TryAddActiveMember(
                abandonedCharacter.CharacterId);

            var combatSession = new CombatSession
            {
                Outcome = CombatOutcomeState.Escaped,
                HasEscapePenaltyEligibleCharacters = true
            };

            combatSession.EscapePenaltyEligibleCharacterIds.Add(
                abandonedCharacter.CharacterId);

            var combatRewardSystem = new CombatRewardSystem();

            Assert.That(
                combatRewardSystem.TryApplyEscapePenalty(gameSession, combatSession),
                Is.True);
            Assert.That(abandonedCharacter.Honor, Is.EqualTo(-3));
        }

        [Test]
        public void TryApplyEscapePenalty_AfterEscapePenalizesAliveAndDownedCombatants()
        {
            var gameSession = new GameSession();
            var escapingCharacter = CreateCharacter("character.escaping");
            escapingCharacter.Honor = 10;
            var downedCharacter = CreateCharacter("character.downed");
            downedCharacter.LifeState = CharacterLifeState.Downed;
            downedCharacter.Honor = 4;

            gameSession.TryRegisterCharacter(escapingCharacter);
            gameSession.TryRegisterCharacter(downedCharacter);
            gameSession.TryAddCharacterToActiveParty(escapingCharacter.CharacterId);
            gameSession.TryAddCharacterToActiveParty(downedCharacter.CharacterId);

            var escapingUnit = new BattleUnit
            {
                IsPlayerSide = true,
                IsReadyToEscape = true,
                Character = escapingCharacter
            };
            var downedUnit = new BattleUnit
            {
                IsPlayerSide = true,
                Character = downedCharacter
            };
            var combatSession = new CombatSession
            {
                ActiveUnit = escapingUnit,
                Units = new List<BattleUnit> { escapingUnit, downedUnit },
                Outcome = CombatOutcomeState.Ongoing,
                CurrentEscapeChancePercent = CombatEscapeSystem.BaseEscapeChancePercent
            };

            Assert.That(
                new CombatEscapeSystem().TryEscape(combatSession, escapingUnit),
                Is.True);
            Assert.That(combatSession.Outcome, Is.EqualTo(CombatOutcomeState.Escaped));
            Assert.That(downedCharacter.LifeState, Is.EqualTo(CharacterLifeState.Dead));

            Assert.That(
                new CombatRewardSystem().TryApplyEscapePenalty(
                    gameSession,
                    combatSession),
                Is.True);
            Assert.That(escapingCharacter.Honor, Is.EqualTo(5));
            Assert.That(downedCharacter.Honor, Is.EqualTo(-1));
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
