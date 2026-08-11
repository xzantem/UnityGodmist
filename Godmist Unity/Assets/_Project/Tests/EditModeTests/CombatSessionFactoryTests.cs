using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Game;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class CombatSessionFactoryTests
    {
        [Test]
        public void TryCreate_ValidEncounter_UsesActivePartyAndEnemyPositions()
        {
            var gameSession = new GameSession();
            var jerzy = CreatePartyCharacter("jerzy", 60f);
            var wladimir = CreatePartyCharacter("wladimir", 50f);

            gameSession.TryRegisterCharacter(jerzy);
            gameSession.TryRegisterCharacter(wladimir);
            gameSession.TryAddCharacterToActiveParty("jerzy");
            gameSession.TryAddCharacterToActiveParty("wladimir");

            var encounter = CreateEncounter(
                new Vector2Int(8, 8),
                new List<EncounterEnemyData>
                {
                    CreateEnemy(
                        "enemy.test.01",
                        new Vector2Int(6, 4)),
                    CreateEnemy(
                        "enemy.test.02",
                        new Vector2Int(6, 2))
                });

            var factory = new CombatSessionFactory(0.25f);

            var succeeded = factory.TryCreate(
                gameSession,
                encounter,
                out var combatSession);

            Assert.That(succeeded, Is.True);
            Assert.That(combatSession, Is.Not.Null);
            Assert.That(combatSession.Units, Has.Count.EqualTo(4));

            Assert.That(
                combatSession.Units[0].Character,
                Is.SameAs(jerzy));

            Assert.That(
                combatSession.Units[0].Position,
                Is.EqualTo(new GridPosition(1, 1)));

            Assert.That(
                combatSession.Units[1].Character,
                Is.SameAs(wladimir));

            Assert.That(
                combatSession.Units[1].Position,
                Is.EqualTo(new GridPosition(1, 2)));

            Assert.That(
                combatSession.Units[2].Position,
                Is.EqualTo(new GridPosition(6, 4)));

            Assert.That(
                combatSession.Units[3].Position,
                Is.EqualTo(new GridPosition(6, 2)));

            Object.DestroyImmediate(encounter);
        }

        [Test]
        public void TryCreate_WithoutActiveParty_ReturnsFalse()
        {
            var gameSession = new GameSession();
            var encounter = CreateEncounter(
                new Vector2Int(8, 8),
                new List<EncounterEnemyData>
                {
                    CreateEnemy(
                        "enemy.test.01",
                        new Vector2Int(6, 4))
                });

            var factory = new CombatSessionFactory();

            var succeeded = factory.TryCreate(
                gameSession,
                encounter,
                out _);

            Assert.That(succeeded, Is.False);

            Object.DestroyImmediate(encounter);
        }

        private static CharacterInstance CreatePartyCharacter(
            string characterId,
            float speed)
        {
            return new CharacterInstance
            {
                CharacterId = characterId,
                CurrentHealth = 100,
                LifeState = CharacterLifeState.Alive,
                AvailabilityState = CharacterAvailabilityState.Available,
                Stats = new CharacterStats
                {
                    MaxHealth = new Stat(100f),
                    Speed = new Stat(speed)
                },
                ResourcePool = new ResourcePool(
                    ResourceType.Momentum,
                    currentValue: 0,
                    maxValue: 60,
                    regeneration: 0)
            };
        }

        private static EncounterDefinition CreateEncounter(
            Vector2Int gridSize,
            List<EncounterEnemyData> enemies)
        {
            var encounter = ScriptableObject.CreateInstance<EncounterDefinition>();

            SetAutoProperty(
                encounter,
                "Id",
                "combat.encounter.test");

            SetAutoProperty(
                encounter,
                "DisplayName",
                "Encounter Test");

            SetAutoProperty(
                encounter,
                "GridWidth",
                gridSize.x);

            SetAutoProperty(
                encounter,
                "GridHeight",
                gridSize.y);

            SetAutoProperty(
                encounter,
                "Enemies",
                enemies);

            return encounter;
        }

        private static EncounterEnemyData CreateEnemy(
            string instanceId,
            Vector2Int position)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            var classData = ScriptableObject.CreateInstance<CharacterClassData>();

            SetAutoProperty(
                definition,
                "Id",
                instanceId + ".definition");

            SetAutoProperty(
                definition,
                "BaseStats",
                new CharacterBaseStatsData());

            SetAutoProperty(
                classData,
                "Id",
                "class.warrior");

            SetAutoProperty(
                classData,
                "ClassName",
                "Warrior");

            SetAutoProperty(
                classData,
                "ResourceType",
                ResourceType.Rage);

            var enemy = new EncounterEnemyData();

            SetAutoProperty(enemy, "InstanceId", instanceId);
            SetAutoProperty(
                enemy,
                "CharacterDefinition",
                definition);

            SetAutoProperty(enemy, "ClassData", classData);
            SetAutoProperty(enemy, "SpawnPosition", position);

            return enemy;
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