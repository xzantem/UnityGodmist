using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class CombatRecoverySystemTests
    {
        private readonly List<CharacterDefinition> createdDefinitions = new();
        private readonly List<SkillData> createdSkills = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var definition in createdDefinitions)
            {
                if (definition != null)
                {
                    Object.DestroyImmediate(definition);
                }
            }

            foreach (var skill in createdSkills)
            {
                if (skill != null)
                {
                    Object.DestroyImmediate(skill);
                }
            }

            createdDefinitions.Clear();
            createdSkills.Clear();
        }

        [Test]
        public void Heal_ClampsHealthToMaximum()
        {
            var unit = CreateUnit(
                health: 85,
                maxHealth: 100,
                resourceCurrent: 0,
                resourceMax: 50,
                isPlayerSide: true);

            var recoveredHealth = new CombatRecoverySystem().Heal(
                unit,
                requestedAmount: 30);

            Assert.That(recoveredHealth, Is.EqualTo(15));
            Assert.That(unit.Character.CurrentHealth, Is.EqualTo(100));
            Assert.That(
                unit.Character.LifeState,
                Is.EqualTo(CharacterLifeState.Alive));
        }

        [Test]
        public void Heal_DownedTarget_RevivesAndResetsDownedCounter()
        {
            var unit = CreateUnit(
                health: 0,
                maxHealth: 100,
                resourceCurrent: 0,
                resourceMax: 50,
                isPlayerSide: true);

            unit.Character.LifeState = CharacterLifeState.Downed;
            unit.DownedTurnsRemaining = 2;

            var recoveredHealth = new CombatRecoverySystem().Heal(
                unit,
                requestedAmount: 25);

            Assert.That(recoveredHealth, Is.EqualTo(25));
            Assert.That(unit.Character.CurrentHealth, Is.EqualTo(25));
            Assert.That(
                unit.Character.LifeState,
                Is.EqualTo(CharacterLifeState.Alive));
            Assert.That(unit.DownedTurnsRemaining, Is.EqualTo(0));
        }

        [Test]
        public void Heal_DeadTarget_DoesNotRestoreHealth()
        {
            var unit = CreateUnit(
                health: 0,
                maxHealth: 100,
                resourceCurrent: 0,
                resourceMax: 50,
                isPlayerSide: false);

            unit.Character.LifeState = CharacterLifeState.Dead;

            var recoveredHealth = new CombatRecoverySystem().Heal(
                unit,
                requestedAmount: 25);

            Assert.That(recoveredHealth, Is.EqualTo(0));
            Assert.That(unit.Character.CurrentHealth, Is.EqualTo(0));
            Assert.That(
                unit.Character.LifeState,
                Is.EqualTo(CharacterLifeState.Dead));
        }

        [Test]
        public void RestoreResource_ClampsAtResourceMaximum()
        {
            var unit = CreateUnit(
                health: 100,
                maxHealth: 100,
                resourceCurrent: 35,
                resourceMax: 50,
                isPlayerSide: true);

            var restoredResource = new CombatRecoverySystem().RestoreResource(
                unit,
                requestedAmount: 30);

            Assert.That(restoredResource, Is.EqualTo(15));
            Assert.That(
                unit.Character.ResourcePool.CurrentValue,
                Is.EqualTo(50));
        }

        [Test]
        public void RestoreResource_DownedTarget_DoesNotRestoreResource()
        {
            var unit = CreateUnit(
                health: 0,
                maxHealth: 100,
                resourceCurrent: 10,
                resourceMax: 50,
                isPlayerSide: true);

            unit.Character.LifeState = CharacterLifeState.Downed;

            var restoredResource = new CombatRecoverySystem().RestoreResource(
                unit,
                requestedAmount: 25);

            Assert.That(restoredResource, Is.EqualTo(0));
            Assert.That(
                unit.Character.ResourcePool.CurrentValue,
                Is.EqualTo(10));
        }

        [Test]
        public void RestoreResource_ResourceBlock_PreventsAnyPositiveRestore()
        {
            var unit = CreateUnit(
                health: 100,
                maxHealth: 100,
                resourceCurrent: 10,
                resourceMax: 50,
                isPlayerSide: true);
            unit.Character.PassiveEffects.Add(new PassiveEffectRuntime
            {
                BlocksResourceRegeneration = true
            });

            var restoredResource = new CombatRecoverySystem().RestoreResource(
                unit,
                requestedAmount: 25);

            Assert.That(restoredResource, Is.EqualTo(0));
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(10));
        }

        [Test]
        public void TryExecute_HealSkill_RevivesDownedTarget()
        {
            var session = CreateSession(
                out var caster,
                out var target);

            target.Character.CurrentHealth = 0;
            target.Character.LifeState = CharacterLifeState.Downed;
            target.DownedTurnsRemaining = 3;

            var healSkill = CreateRecoverySkill(
                EffectType.Heal,
                baseValue: 25);

            var executed = new SkillExecutionSystem().TryExecute(
                session,
                caster,
                healSkill,
                target);

            Assert.That(executed, Is.True);
            Assert.That(target.Character.CurrentHealth, Is.EqualTo(25));
            Assert.That(
                target.Character.LifeState,
                Is.EqualTo(CharacterLifeState.Alive));
            Assert.That(target.DownedTurnsRemaining, Is.EqualTo(0));
        }

        [Test]
        public void GetTargets_IncludeDownedTargets_OnlyReturnsDownedWhenEnabled()
        {
            var session = CreateSession(
                out var caster,
                out var target);

            target.Character.CurrentHealth = 0;
            target.Character.LifeState = CharacterLifeState.Downed;

            var mode = new SkillTargetModeData();

            SetPrivateField(
                mode,
                "targetingType",
                TargetingType.Allies);

            SetPrivateField(
                mode,
                "absoluteRange",
                new SkillRangeData(
                    RangeShape.Square,
                    minRange: 0,
                    maxRange: 2));

            var targetingService = new CombatTargetingService();

            var withoutDownedTargets = targetingService.GetTargets(
                session,
                caster,
                mode,
                target.Position,
                includeDownedTargets: false);

            var withDownedTargets = targetingService.GetTargets(
                session,
                caster,
                mode,
                target.Position,
                includeDownedTargets: true);

            Assert.That(withoutDownedTargets, Is.Empty);
            Assert.That(withDownedTargets, Has.Count.EqualTo(1));
            Assert.That(withDownedTargets[0], Is.SameAs(target));
        }

        private CombatSession CreateSession(
            out BattleUnit caster,
            out BattleUnit target)
        {
            caster = CreateUnit(
                health: 100,
                maxHealth: 100,
                resourceCurrent: 20,
                resourceMax: 50,
                isPlayerSide: true);

            target = CreateUnit(
                health: 100,
                maxHealth: 100,
                resourceCurrent: 0,
                resourceMax: 50,
                isPlayerSide: true);

            caster.Position = new GridPosition(1, 1);
            target.Position = new GridPosition(2, 1);

            return new CombatSession
            {
                CombatId = "test.recovery",
                Outcome = CombatOutcomeState.Ongoing,
                Grid = new BattleGrid(4, 4),
                ActiveUnit = caster,
                Units = new List<BattleUnit>
                {
                    caster,
                    target
                }
            };
        }

        private BattleUnit CreateUnit(
            int health,
            int maxHealth,
            int resourceCurrent,
            int resourceMax,
            bool isPlayerSide)
        {
            var definition =
                ScriptableObject.CreateInstance<CharacterDefinition>();

            createdDefinitions.Add(definition);

            return new BattleUnit
            {
                Character = new CharacterInstance
                {
                    Definition = definition,
                    CurrentHealth = health,
                    LifeState = CharacterLifeState.Alive,
                    ResourcePool = new ResourcePool(
                        ResourceType.Momentum,
                        resourceCurrent,
                        resourceMax,
                        regeneration: 0),
                    Stats = new CharacterStats
                    {
                        MaxHealth = new Stat(maxHealth)
                    }
                },
                CurrentActionPoints = 100,
                MaxActionPoints = 100,
                IsPlayerSide = isPlayerSide
            };
        }

        private SkillData CreateRecoverySkill(
            EffectType effectType,
            int baseValue)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            createdSkills.Add(skill);

            var effect = new SkillEffectData();

            SetPrivateField(effect, "effectType", effectType);
            SetPrivateField(effect, "baseValue", baseValue);
            SetPrivateField(effect, "targetModeIndex", 0);

            SetPrivateField(
                skill,
                "targetModes",
                new List<SkillTargetModeData>
                {
                    new()
                });

            SetPrivateField(
                skill,
                "effects",
                new List<SkillEffectData>
                {
                    effect
                });

            return skill;
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
                $"Nie znaleziono pola {fieldName}.");

            field.SetValue(target, value);
        }
    }
}
