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

        [TestCase(0, 0, true)]
        [TestCase(1, 0, true)]
        [TestCase(-1, 0, true)]
        [TestCase(0, 1, true)]
        [TestCase(0, -1, true)]
        [TestCase(1, 1, true)]
        [TestCase(1, -1, true)]
        [TestCase(-1, 1, true)]
        [TestCase(-1, -1, true)]
        [TestCase(2, 0, false)]
        [TestCase(-2, 0, false)]
        [TestCase(0, 2, false)]
        [TestCase(0, -2, false)]
        [TestCase(2, 1, false)]
        [TestCase(-2, -1, false)]
        [TestCase(1, 2, false)]
        [TestCase(-1, -2, false)]
        [TestCase(2, 2, false)]
        public void ManualReviveRequiresAtMostOneTileIncludingDiagonals(int dx, int dy, bool expected)
        {
            var session = CreateSession(out var actor, out var target);
            session.Grid = new BattleGrid(8, 8);
            actor.Position = new GridPosition(3, 3);
            target.Position = new GridPosition(3 + dx, 3 + dy);
            target.Character.LifeState = CharacterLifeState.Downed;
            target.Character.CurrentHealth = 0;
            target.DownedTurnsRemaining = 2;
            target.Character.ExplorationStatusElapsedSeconds = 15f;
            var logs = new List<string>();

            Assert.That(new CombatReviveSystem(logs.Add).TryRevive(session, actor, target), Is.EqualTo(expected));

            Assert.That(target.Character.LifeState, Is.EqualTo(expected ? CharacterLifeState.Alive : CharacterLifeState.Downed));
            Assert.That(target.Character.CurrentHealth, expected ? Is.GreaterThan(0) : Is.Zero);
            Assert.That(target.DownedTurnsRemaining, Is.EqualTo(expected ? 0 : 2));
            Assert.That(target.Character.ExplorationStatusElapsedSeconds, Is.EqualTo(expected ? 0f : 15f));
            Assert.That(logs.Count, Is.EqualTo(expected ? 1 : 0));
            Assert.That(session.ActiveUnit, Is.SameAs(actor));
            Assert.That(actor.CurrentActionPoints, Is.EqualTo(10000));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ManualReviveCannotReachAnotherLevelWithoutAnAdjacentTraversalConnection(bool connected)
        {
            var session = CreateSession(out var actor, out var target);
            target.Position = new GridPosition(2, 1, 1);
            session.Grid.TraversalNeighbours = position => connected && position == actor.Position
                ? new[] { target.Position } : System.Array.Empty<GridPosition>();
            target.Character.LifeState = CharacterLifeState.Downed;
            target.Character.CurrentHealth = 0;

            Assert.That(new CombatReviveSystem().TryRevive(session, actor, target), Is.EqualTo(connected));
            Assert.That(target.Character.LifeState, Is.EqualTo(connected ? CharacterLifeState.Alive : CharacterLifeState.Downed));
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
        public void TryExecute_Toggle_CanBeActivatedOncePerTurnAndDeactivationIsFree()
        {
            var session = CreateSession(out var caster, out _);
            var skill = CreateToggleSkill(
                activationActionCost: 50,
                activationResourceCost: 5,
                upkeepFlatCost: 0,
                upkeepMaxResourcePercent: 0f);

            var system = new SkillExecutionSystem();

            Assert.That(system.TryExecute(session, caster, skill, caster), Is.True);
            Assert.That(caster.Character.ResourcePool.CurrentValue, Is.EqualTo(15));
            Assert.That(caster.ActiveToggleSkillIds, Does.Contain(skill.SkillId));
            Assert.That(caster.Character.PassiveEffects, Has.Count.EqualTo(1));

            var actionPointsBeforeDeactivation = caster.CurrentActionPoints;
            var resourceBeforeDeactivation = caster.Character.ResourcePool.CurrentValue;

            Assert.That(system.TryExecute(session, caster, skill, caster), Is.True);
            Assert.That(caster.CurrentActionPoints, Is.EqualTo(actionPointsBeforeDeactivation));
            Assert.That(caster.Character.ResourcePool.CurrentValue, Is.EqualTo(resourceBeforeDeactivation));
            Assert.That(caster.ActiveToggleSkillIds, Does.Not.Contain(skill.SkillId));
            Assert.That(caster.Character.PassiveEffects, Is.Empty);

            Assert.That(system.TryExecute(session, caster, skill, caster), Is.False);
        }

        [Test]
        public void ProcessToggleUpkeep_UsesHigherOfFlatAndPercentageCost()
        {
            var session = CreateSession(out var caster, out _);
            caster.Character.ResourcePool.MaxValue = 100;
            caster.Character.ResourcePool.CurrentValue = 50;
            var skill = CreateToggleSkill(
                activationActionCost: 0,
                activationResourceCost: 0,
                upkeepFlatCost: 20,
                upkeepMaxResourcePercent: 0.25f);
            caster.Character.Skills.Add(skill);

            var system = new SkillExecutionSystem();
            Assert.That(system.TryExecute(session, caster, skill, caster), Is.True);

            system.ProcessToggleUpkeep(session, caster);

            Assert.That(caster.Character.ResourcePool.CurrentValue, Is.EqualTo(25));
            Assert.That(caster.ActiveToggleSkillIds, Does.Contain(skill.SkillId));
        }

        [Test]
        public void ProcessToggleUpkeep_UsesCasterPoolWhenSkillResourceTypeIsNone()
        {
            var session = CreateSession(out var caster, out _);
            caster.Character.ResourcePool.MaxValue = 200;
            caster.Character.ResourcePool.CurrentValue = 80;
            var skill = CreateToggleSkill(
                activationActionCost: 0,
                activationResourceCost: 0,
                upkeepFlatCost: 20,
                upkeepMaxResourcePercent: 0.25f);
            SetAutoProperty(skill, "ResourceType", ResourceType.None);
            caster.Character.Skills.Add(skill);

            var system = new SkillExecutionSystem();
            Assert.That(system.TryExecute(session, caster, skill, caster), Is.True);

            system.ProcessToggleUpkeep(session, caster);

            Assert.That(caster.Character.ResourcePool.CurrentValue, Is.EqualTo(30));
            Assert.That(caster.ActiveToggleSkillIds, Does.Contain(skill.SkillId));
        }

        [Test]
        public void ProcessToggleUpkeep_InsufficientResource_DeactivatesAndRemovesEffects()
        {
            var session = CreateSession(out var caster, out _);
            caster.Character.ResourcePool.MaxValue = 100;
            caster.Character.ResourcePool.CurrentValue = 24;
            var skill = CreateToggleSkill(
                activationActionCost: 0,
                activationResourceCost: 0,
                upkeepFlatCost: 20,
                upkeepMaxResourcePercent: 0.25f);
            caster.Character.Skills.Add(skill);

            var system = new SkillExecutionSystem();
            Assert.That(system.TryExecute(session, caster, skill, caster), Is.True);
            caster.ToggleSkillIdsUsedThisTurn.Clear();

            system.ProcessToggleUpkeep(session, caster);

            Assert.That(caster.Character.ResourcePool.CurrentValue, Is.EqualTo(24));
            Assert.That(caster.ActiveToggleSkillIds, Does.Not.Contain(skill.SkillId));
            Assert.That(caster.ToggleSkillIdsUsedThisTurn, Does.Not.Contain(skill.SkillId));
            Assert.That(caster.Character.PassiveEffects, Is.Empty);
        }

        [Test]
        public void ProcessToggleUpkeep_ZeroCost_KeepsToggleActive()
        {
            var session = CreateSession(out var caster, out _);
            var skill = CreateToggleSkill(
                activationActionCost: 0,
                activationResourceCost: 0,
                upkeepFlatCost: 0,
                upkeepMaxResourcePercent: 0f);
            caster.Character.Skills.Add(skill);

            var system = new SkillExecutionSystem();
            Assert.That(system.TryExecute(session, caster, skill, caster), Is.True);

            system.ProcessToggleUpkeep(session, caster);

            Assert.That(caster.Character.ResourcePool.CurrentValue, Is.EqualTo(20));
            Assert.That(caster.ActiveToggleSkillIds, Does.Contain(skill.SkillId));
        }

        [Test]
        public void ProcessToggleUpkeep_PercentageCostWithoutPool_DeactivatesToggle()
        {
            var session = CreateSession(out var caster, out _);
            var skill = CreateToggleSkill(
                activationActionCost: 0,
                activationResourceCost: 0,
                upkeepFlatCost: 0,
                upkeepMaxResourcePercent: 0.25f);
            caster.Character.Skills.Add(skill);

            var system = new SkillExecutionSystem();
            Assert.That(system.TryExecute(session, caster, skill, caster), Is.True);
            caster.ToggleSkillIdsUsedThisTurn.Clear();
            caster.Character.ResourcePool = null;

            system.ProcessToggleUpkeep(session, caster);

            Assert.That(caster.ActiveToggleSkillIds, Does.Not.Contain(skill.SkillId));
            Assert.That(caster.ToggleSkillIdsUsedThisTurn, Does.Not.Contain(skill.SkillId));
        }

        [Test]
        public void UnregisterAura_RemovesStatusesAppliedByThatAuraImmediately()
        {
            var session = CreateSession(out var caster, out var target);
            const string auraId = "skill.test.aura.aura.0";
            caster.ActiveAuras.Add(new CombatAuraRuntime
            {
                AuraId = auraId,
                StatusId = "chill"
            });

            var statusSystem = new StatusEffectSystem();
            Assert.That(statusSystem.ApplyStatus(
                caster, target, "chill", 1f, 3, 1, 0f, 0f, auraId), Is.True);
            Assert.That(target.Character.PassiveEffects, Has.Count.EqualTo(1));

            CombatAuraSystem.Unregister(session, caster, "skill.test.aura");

            Assert.That(caster.ActiveAuras, Is.Empty);
            Assert.That(target.Character.PassiveEffects, Is.Empty);
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
                CurrentActionPoints = 10000,
                MaxActionPoints = 10000,
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

        private SkillData CreateToggleSkill(
            int activationActionCost,
            int activationResourceCost,
            int upkeepFlatCost,
            float upkeepMaxResourcePercent)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            createdSkills.Add(skill);

            SetAutoProperty(skill, "SkillId", "skill.test.toggle");
            SetAutoProperty(skill, "DisplayName", "Test Toggle");
            SetAutoProperty(skill, "ActionCost", activationActionCost);
            SetAutoProperty(skill, "ResourceCost", activationResourceCost);
            SetAutoProperty(skill, "ResourceType", ResourceType.Momentum);
            SetAutoProperty(skill, "IsToggle", true);
            SetAutoProperty(skill, "ToggleUpkeepFlatResourceCost", upkeepFlatCost);
            SetAutoProperty(
                skill,
                "ToggleUpkeepMaxResourcePercent",
                upkeepMaxResourcePercent);

            var targetMode = new SkillTargetModeData();
            SetPrivateField(targetMode, "targetType", TargetType.Caster);
            SetPrivateField(targetMode, "targetingType", TargetingType.Allies);

            var effect = new SkillEffectData();
            SetPrivateField(effect, "effectType", EffectType.ModifyVar);
            SetPrivateField(effect, "targetModeIndex", 0);
            SetPrivateField(effect, "modifierValue", 0.3f);
            SetPrivateField(effect, "durationTurns", 0);

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

        private static void SetAutoProperty(
            object target,
            string propertyName,
            object value)
        {
            var field = target.GetType().GetField(
                $"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, $"Nie znaleziono pola {propertyName}.");
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
                $"Nie znaleziono pola {fieldName}.");

            field.SetValue(target, value);
        }
    }
}
