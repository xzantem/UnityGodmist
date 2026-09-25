using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Runtime.Characters;
using Godmist.Systems.Characters;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class CharacterLevelGrowthSystemTests
    {
        private CharacterLevelGrowthSystem growthSystem;

        [SetUp]
        public void SetUp()
        {
            growthSystem = new CharacterLevelGrowthSystem();
        }

        [Test]
        public void GetCumulativeGrowthMultiplier_UsesConfiguredLevelBands()
        {
            Assert.That(growthSystem.GetCumulativeGrowthMultiplier(1), Is.EqualTo(0f));
            Assert.That(growthSystem.GetCumulativeGrowthMultiplier(10), Is.EqualTo(9f));
            Assert.That(growthSystem.GetCumulativeGrowthMultiplier(11), Is.EqualTo(11f));
            Assert.That(growthSystem.GetCumulativeGrowthMultiplier(21), Is.EqualTo(32f));
            Assert.That(growthSystem.GetCumulativeGrowthMultiplier(41), Is.EqualTo(118f));
        }

        [Test]
        public void Apply_LevelGrowth_UpdatesBaseStatsAndAddsMaxHealthGainToCurrentHealth()
        {
            var character = CreateCharacter(level: 2, currentHealth: 50, maxHealthGrowth: 12.5f);

            growthSystem.Apply(character, grantCurrentHealthGrowth: true);

            Assert.That(character.Stats.MaxHealth.BaseValue, Is.EqualTo(112.5f));
            Assert.That(character.CurrentHealth, Is.EqualTo(62));
        }

        [Test]
        public void Apply_RestoreMode_RebuildsHighLevelStatsWithoutHealingCharacter()
        {
            var character = CreateCharacter(level: 11, currentHealth: 50, maxHealthGrowth: 10f);

            growthSystem.Apply(character, grantCurrentHealthGrowth: false);

            Assert.That(character.Stats.MaxHealth.BaseValue, Is.EqualTo(210f));
            Assert.That(character.CurrentHealth, Is.EqualTo(50));
        }

        [Test]
        public void AddExperience_LevelUp_AppliesGrowthAndGrantsSkillPoint()
        {
            var character = CreateCharacter(level: 1, currentHealth: 70, maxHealthGrowth: 10f);
            var progressionSystem = new CharacterProgressionSystem(growthSystem);

            var levelsGained = progressionSystem.AddExperience(character, progressionSystem.GetExperienceRequiredForNextLevel(character.Level));

            Assert.That(levelsGained, Is.EqualTo(1));
            Assert.That(character.Stats.MaxHealth.BaseValue, Is.EqualTo(110f));
            Assert.That(character.CurrentHealth, Is.EqualTo(80));
            Assert.That(character.AvailableSkillPoints, Is.EqualTo(2));
        }

        [Test]
        public void CharacterFactory_InitializesBaseResistancesFromDefinition()
        {
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            var classData = ScriptableObject.CreateInstance<CharacterClassData>();
            var baseResistances = new CharacterResistanceData();
            SetAutoProperty(definition, "Id", "character.resistance.test");
            SetAutoProperty(classData, "Id", "class.resistance.test");
            SetAutoProperty(classData, "ResourceType", ResourceType.None);
            SetAutoProperty(baseResistances, "BleedResistance", .5f);
            SetAutoProperty(baseResistances, "PoisonResistance", .7f);
            SetAutoProperty(baseResistances, "FireResistance", .4f);
            SetAutoProperty(baseResistances, "WeakeningResistance", .5f);
            SetAutoProperty(baseResistances, "SuppressionResistance", .35f);
            SetAutoProperty(baseResistances, "MoveResistance", .35f);
            SetAutoProperty(definition, "BaseResistances", baseResistances);

            var character = new CharacterInstanceFactory().Create(definition, classData);

            Assert.That(character.Resistances.BleedResistance, Is.EqualTo(.5f));
            Assert.That(character.Resistances.PoisonResistance, Is.EqualTo(.7f));
            Assert.That(character.Resistances.FireResistance, Is.EqualTo(.4f));
            Assert.That(character.Resistances.WeakeningResistance, Is.EqualTo(.5f));
            Assert.That(character.Resistances.SuppressionResistance, Is.EqualTo(.35f));
            Assert.That(character.Resistances.MoveResistance, Is.EqualTo(.35f));
        }

        private static CharacterInstance CreateCharacter(
            int level,
            int currentHealth,
            float maxHealthGrowth)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            var baseStats = new CharacterBaseStatsData();
            var growthData = new CharacterGrowthData();
            SetAutoProperty(baseStats, "MaxHealth", 100);
            SetAutoProperty(growthData, "MaxHealth", maxHealthGrowth);
            SetAutoProperty(definition, "BaseStats", baseStats);
            SetAutoProperty(definition, "GrowthData", growthData);

            return new CharacterInstance
            {
                Definition = definition,
                Level = level,
                CurrentHealth = currentHealth,
                Stats = new CharacterStats
                {
                    MaxHealth = new Stat(100f),
                    MinAttack = new Stat(10f),
                    MaxAttack = new Stat(15f),
                    PhysicalDefense = new Stat(5f),
                    MagicalDefense = new Stat(5f),
                    Dodge = new Stat(5f),
                    CritChance = new Stat(0.1f),
                    CritDamage = new Stat(1.5f),
                    Speed = new Stat(50f),
                    ResourceRegeneration = new Stat(0f)
                },
                ResourcePool = new ResourcePool(),
                PassiveEffects = null,
                Equipment = null
            };
        }

        private static void SetAutoProperty<T>(object target, string propertyName, T value)
        {
            var field = target.GetType().GetField(
                $"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, $"Nie znaleziono pola dla {propertyName}.");
            field.SetValue(target, value);
        }
    }
}
