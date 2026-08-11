using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class CombatDataValidatorTests
    {
        [Test]
        public void ValidateSkill_ValidSkill_ReturnsValidReport()
        {
            var skill = CreateValidSkill();

            var report = CombatDataValidator.ValidateSkill(skill);

            Assert.That(report.IsValid, Is.True);
            Assert.That(report.Errors, Is.Empty);

            Object.DestroyImmediate(skill);
        }

        [Test]
        public void ValidateSkill_EffectTargetModeOutsideRange_ReturnsError()
        {
            var skill = CreateValidSkill();
            var effect = skill.Effects[0];

            SetPrivateField(effect, "targetModeIndex", 4);

            var report = CombatDataValidator.ValidateSkill(skill);

            Assert.That(report.IsValid, Is.False);
            Assert.That(
                report.Errors,
                Has.Some.Contains("nie wskazuje istniejącego Target Mode"));

            Object.DestroyImmediate(skill);
        }

        [Test]
        public void ValidateSkill_ApplyStatusWithoutStatusId_ReturnsError()
        {
            var skill = CreateValidSkill();
            var effect = skill.Effects[0];

            SetPrivateField(effect, "effectType", EffectType.ApplyStatus);
            SetPrivateField(effect, "statusId", string.Empty);

            var report = CombatDataValidator.ValidateSkill(skill);

            Assert.That(report.IsValid, Is.False);
            Assert.That(
                report.Errors,
                Has.Some.Contains("Status Id jest wymagane"));

            Object.DestroyImmediate(skill);
        }

        [Test]
        public void ValidateEncounter_ValidEncounter_ReturnsValidReport()
        {
            var encounter = CreateValidEncounter();

            var report = CombatDataValidator.ValidateEncounter(encounter);

            Assert.That(report.IsValid, Is.True);
            Assert.That(report.Errors, Is.Empty);

            DestroyEncounterDependencies(encounter);
        }

        [Test]
        public void ValidateEncounter_DuplicateSpawnPosition_ReturnsError()
        {
            var encounter = CreateValidEncounter();
            var secondEnemy = encounter.Enemies[1];

            SetAutoProperty(
                secondEnemy,
                "SpawnPosition",
                encounter.Enemies[0].SpawnPosition);

            var report = CombatDataValidator.ValidateEncounter(encounter);

            Assert.That(report.IsValid, Is.False);
            Assert.That(
                report.Errors,
                Has.Some.Contains("jest używana przez więcej niż jednego"));

            DestroyEncounterDependencies(encounter);
        }

        [Test]
        public void ValidateEncounter_EnemyOutsideGrid_ReturnsError()
        {
            var encounter = CreateValidEncounter();
            var enemy = encounter.Enemies[0];

            SetAutoProperty(enemy, "SpawnPosition", new Vector2Int(20, 20));

            var report = CombatDataValidator.ValidateEncounter(encounter);

            Assert.That(report.IsValid, Is.False);
            Assert.That(
                report.Errors,
                Has.Some.Contains("poza gridem encountera"));

            DestroyEncounterDependencies(encounter);
        }

        private static SkillData CreateValidSkill()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();

            SetAutoProperty(skill, "SkillId", "skill.validator.test");
            SetAutoProperty(skill, "DisplayName", "Validator Test");
            SetAutoProperty(skill, "SkillAccuracy", 100f);
            SetAutoProperty(skill, "ActionCost", 50);
            SetAutoProperty(skill, "ResourceCost", 0);

            var targetMode = new SkillTargetModeData();
            var effect = new SkillEffectData();

            SetPrivateField(effect, "effectType", EffectType.DealDamage);
            SetPrivateField(effect, "targetModeIndex", 0);
            SetPrivateField(effect, "damageMultiplier", 1f);

            SetPrivateField(
                skill,
                "targetModes",
                new List<SkillTargetModeData> { targetMode });

            SetPrivateField(
                skill,
                "effects",
                new List<SkillEffectData> { effect });

            return skill;
        }

        private static EncounterDefinition CreateValidEncounter()
        {
            var encounter = ScriptableObject.CreateInstance<EncounterDefinition>();

            SetAutoProperty(encounter, "Id", "encounter.validator.test");
            SetAutoProperty(encounter, "DisplayName", "Validator Test");
            SetAutoProperty(encounter, "GridWidth", 8);
            SetAutoProperty(encounter, "GridHeight", 8);

            SetAutoProperty(
                encounter,
                "Enemies",
                new List<EncounterEnemyData>
                {
                    CreateEnemy("enemy.validator.01", new Vector2Int(5, 2)),
                    CreateEnemy("enemy.validator.02", new Vector2Int(6, 2))
                });

            return encounter;
        }

        private static EncounterEnemyData CreateEnemy(
            string instanceId,
            Vector2Int spawnPosition)
        {
            var characterDefinition =
                ScriptableObject.CreateInstance<CharacterDefinition>();

            var classData =
                ScriptableObject.CreateInstance<CharacterClassData>();

            var enemy = new EncounterEnemyData();

            SetAutoProperty(enemy, "InstanceId", instanceId);
            SetAutoProperty(
                enemy,
                "CharacterDefinition",
                characterDefinition);
            SetAutoProperty(enemy, "ClassData", classData);
            SetAutoProperty(enemy, "SpawnPosition", spawnPosition);

            return enemy;
        }

        private static void DestroyEncounterDependencies(
            EncounterDefinition encounter)
        {
            foreach (var enemy in encounter.Enemies)
            {
                Object.DestroyImmediate(enemy.CharacterDefinition);
                Object.DestroyImmediate(enemy.ClassData);
            }

            Object.DestroyImmediate(encounter);
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

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(
                field,
                Is.Not.Null,
                $"Nie znaleziono prywatnego pola {fieldName}.");

            field.SetValue(target, value);
        }
    }
}