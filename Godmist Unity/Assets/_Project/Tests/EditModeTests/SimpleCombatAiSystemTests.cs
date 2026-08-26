using System;
using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class SimpleCombatAiSystemTests
    {
        private readonly List<UnityEngine.Object> createdAssets = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in createdAssets)
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void CalculateSkillScore_PrefersGuaranteedKillOverNonLethalAreaDamage()
        {
            var caster = CreateUnit("caster", 100, 10, 10);
            var lowHealthEnemy = CreateUnit("low", 10, 0, 0);
            var firstAreaEnemy = CreateUnit("area-first", 20, 0, 0);
            var secondAreaEnemy = CreateUnit("area-second", 20, 0, 0);
            var killSkill = CreateSkill(CreateEffect(EffectType.DealDamage, 1f));
            var areaSkill = CreateSkill(CreateEffect(EffectType.DealDamage, 0.9f));

            var killScore = CalculateScore(caster, killSkill, lowHealthEnemy);
            var areaScore = CalculateScore(caster, areaSkill,
                firstAreaEnemy, secondAreaEnemy);

            Assert.That(killScore, Is.GreaterThan(areaScore));
        }

        [Test]
        public void CalculateSkillScore_PrefersHardControlOverOrdinaryStatus()
        {
            var caster = CreateUnit("caster", 100, 10, 10);
            var enemy = CreateUnit("enemy", 100, 0, 0);
            var stun = CreateEffect(EffectType.ApplyStatus, 1f);
            SetField(stun, "statusId", "stun");
            var poison = CreateEffect(EffectType.ApplyStatus, 1f);
            SetField(poison, "statusId", "poison");

            var stunScore = CalculateScore(caster, CreateSkill(stun), enemy);
            var poisonScore = CalculateScore(caster, CreateSkill(poison), enemy);

            Assert.That(stunScore, Is.GreaterThan(poisonScore));
        }

        [Test]
        public void CalculateSkillScore_PrefersReviveOverShield()
        {
            var caster = CreateUnit("caster", 100, 10, 10);
            var downedAlly = CreateUnit("downed", 0, 0, 0);
            downedAlly.Character.LifeState = CharacterLifeState.Downed;
            var heal = CreateEffect(EffectType.Heal, 1f);
            SetField(heal, "baseValue", 20);
            var shield = CreateEffect(EffectType.Shield, 1f);
            SetField(shield, "baseValue", 100);

            var reviveScore = CalculateScore(caster, CreateSkill(heal), downedAlly);
            var shieldScore = CalculateScore(caster, CreateSkill(shield), downedAlly);

            Assert.That(reviveScore, Is.GreaterThan(shieldScore));
        }

        [Test]
        public void GetPreferredCombatRange_IgnoresLongRangeHealForMeleeDps()
        {
            var meleeAttack = CreateSkill(CreateEffect(EffectType.DealDamage, 1f));
            ConfigureTargetMode(meleeAttack, TargetingType.Enemies, 1);
            var longRangeHeal = CreateSkill(CreateEffect(EffectType.Heal, 1f));
            ConfigureTargetMode(longRangeHeal, TargetingType.Allies, 4);
            var unit = CreateUnit("dps", 100, 10, 10);
            unit.Character.Skills.Add(meleeAttack);
            unit.Character.Skills.Add(longRangeHeal);
            var method = typeof(SimpleCombatAiSystem).GetMethod(
                "GetPreferredCombatRange",
                BindingFlags.Static | BindingFlags.NonPublic);

            var range = (int)method.Invoke(null, new object[] { unit });

            Assert.That(range, Is.EqualTo(1));
        }

        [Test]
        public void CombatFeedbackEvent_CapturesTargetPositionAtPublishTime()
        {
            var target = CreateUnit("target", 100, 0, 0);
            target.Position = new GridPosition(2, 3);

            var feedback = new CombatFeedbackEvent(
                target,
                CombatFeedbackType.Damage,
                amount: 12);

            target.Position = new GridPosition(7, 6);

            Assert.That(feedback.Position, Is.EqualTo(new GridPosition(2, 3)));
        }

        [Test]
        public void TryExecuteNextAction_ExecutesOneZeroCostSkillPerCallAndThenStops()
        {
            var aiUnit = CreateAiCombatUnit(
                "ai",
                isPlayerSide: false,
                position: new GridPosition(0, 0));
            var target = CreateAiCombatUnit(
                "target",
                isPlayerSide: true,
                position: new GridPosition(1, 0));
            var firstSkill = CreateZeroCostDamageSkill("skill.first");
            var secondSkill = CreateZeroCostDamageSkill("skill.second");
            aiUnit.Character.Skills.Add(firstSkill);
            aiUnit.Character.Skills.Add(secondSkill);

            var session = CreateAiSession(aiUnit, target);
            var aiSystem = CreateExecutableAiSystem();
            var turnState = aiSystem.CreateTurnState();

            Assert.That(
                aiSystem.TryExecuteNextAction(
                    session,
                    aiUnit,
                    turnState,
                    out var firstAction),
                Is.True);
            Assert.That(firstAction.Type, Is.EqualTo(SimpleCombatAiActionType.Skill));
            Assert.That(firstAction.Skill, Is.SameAs(firstSkill));
            Assert.That(target.Character.CurrentHealth, Is.EqualTo(99));

            Assert.That(
                aiSystem.TryExecuteNextAction(
                    session,
                    aiUnit,
                    turnState,
                    out var secondAction),
                Is.True);
            Assert.That(secondAction.Type, Is.EqualTo(SimpleCombatAiActionType.Skill));
            Assert.That(secondAction.Skill, Is.SameAs(secondSkill));
            Assert.That(target.Character.CurrentHealth, Is.EqualTo(98));

            Assert.That(
                aiSystem.TryExecuteNextAction(
                    session,
                    aiUnit,
                    turnState,
                    out _),
                Is.False);
            Assert.That(target.Character.CurrentHealth, Is.EqualTo(98));
        }

        [Test]
        public void ExecuteSimpleAiTurn_EndsAfterZeroCostSkillsAreExhausted()
        {
            var aiUnit = CreateAiCombatUnit(
                "ai",
                isPlayerSide: false,
                position: new GridPosition(0, 0));
            var target = CreateAiCombatUnit(
                "target",
                isPlayerSide: true,
                position: new GridPosition(1, 0));
            aiUnit.Character.Skills.Add(CreateZeroCostDamageSkill("skill.first"));
            aiUnit.Character.Skills.Add(CreateZeroCostDamageSkill("skill.second"));

            var session = CreateAiSession(aiUnit, target);
            var endTurnCalls = 0;
            var aiSystem = new SimpleCombatAiSystem(
                endTurn: _ => endTurnCalls++,
                skillExecutionSystem: new SkillExecutionSystem(),
                targetingService: new CombatTargetingService(),
                movementSystem: new CombatMovementSystem(),
                lineOfSightService: new CombatLineOfSightService());

            aiSystem.ExecuteSimpleAiTurn(session, aiUnit);

            Assert.That(target.Character.CurrentHealth, Is.EqualTo(98));
            Assert.That(endTurnCalls, Is.EqualTo(1));
        }

        [Test]
        public void TryMoveAlongPath_PublishesSingleApAndMomentumFeedback()
        {
            var unit = CreateAiCombatUnit(
                "moving-unit",
                isPlayerSide: false,
                position: new GridPosition(0, 0));
            unit.Character.ResourcePool = new ResourcePool(
                ResourceType.Momentum,
                currentValue: 0,
                maxValue: 200,
                regeneration: 0);

            var session = new CombatSession
            {
                CombatId = "test.path-feedback",
                Outcome = CombatOutcomeState.Ongoing,
                Grid = new BattleGrid(3, 1),
                ActiveUnit = unit,
                Units = new List<BattleUnit> { unit }
            };
            var feedbackBus = new CombatFeedbackBus();
            var feedbackEvents = new List<CombatFeedbackEvent>();
            feedbackBus.Published += feedbackEvents.Add;
            var movementSystem = new CombatMovementSystem(
                resourceRuleSystem: new ResourceRuleSystem(feedbackBus: feedbackBus),
                feedbackBus: feedbackBus);

            Assert.That(
                movementSystem.TryMoveAlongPath(
                    session,
                    unit,
                    new[] { new GridPosition(1, 0), new GridPosition(2, 0) }),
                Is.True);

            Assert.That(
                feedbackEvents.FindAll(e => e.Type == CombatFeedbackType.ActionPointSpend),
                Has.Count.EqualTo(1));
            Assert.That(
                feedbackEvents.FindAll(e => e.Type == CombatFeedbackType.ResourceGain),
                Has.Count.EqualTo(1));
            Assert.That(
                feedbackEvents.Find(e => e.Type == CombatFeedbackType.ActionPointSpend).Amount,
                Is.EqualTo(3));
            Assert.That(
                feedbackEvents.Find(e => e.Type == CombatFeedbackType.ResourceGain).Amount,
                Is.EqualTo(6));
        }

        [Test]
        public void TryExecuteNextAction_MovesReachablePathPrefixWhenTargetIsFartherThanAp()
        {
            var aiUnit = CreateAiCombatUnit(
                "ai",
                isPlayerSide: false,
                position: new GridPosition(0, 0));
            aiUnit.CurrentActionPoints = 3;
            var target = CreateAiCombatUnit(
                "target",
                isPlayerSide: true,
                position: new GridPosition(3, 0));
            var session = new CombatSession
            {
                CombatId = "test.ai-partial-path",
                Outcome = CombatOutcomeState.Ongoing,
                Grid = new BattleGrid(4, 1),
                ActiveUnit = aiUnit,
                Units = new List<BattleUnit> { aiUnit, target }
            };

            var aiSystem = CreateExecutableAiSystem();

            Assert.That(
                aiSystem.TryExecuteNextAction(
                    session,
                    aiUnit,
                    aiSystem.CreateTurnState(),
                    out var action),
                Is.True);
            Assert.That(action.Type, Is.EqualTo(SimpleCombatAiActionType.Movement));
            Assert.That(aiUnit.Position, Is.EqualTo(new GridPosition(2, 0)));
            Assert.That(aiUnit.CurrentActionPoints, Is.EqualTo(0));
        }

        private float CalculateScore(
            BattleUnit caster,
            SkillData skill,
            params BattleUnit[] targets)
        {
            var method = typeof(SimpleCombatAiSystem).GetMethod(
                "CalculateSkillScore",
                BindingFlags.Static | BindingFlags.NonPublic);
            var targetsByMode = new Dictionary<int, IReadOnlyList<BattleUnit>>
            {
                [0] = targets
            };

            return (float)method.Invoke(null, new object[]
            {
                caster,
                skill,
                targetsByMode
            });
        }

        private SkillData CreateSkill(SkillEffectData effect)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            createdAssets.Add(skill);
            SetField(skill, "targetModes", new List<SkillTargetModeData>
            {
                new()
            });
            SetField(skill, "effects", new List<SkillEffectData> { effect });
            return skill;
        }

        private static void ConfigureTargetMode(
            SkillData skill,
            TargetingType targetingType,
            int maxRange)
        {
            var mode = skill.TargetModes[0];
            SetField(mode, "targetType", TargetType.Single);
            SetField(mode, "targetingType", targetingType);
            SetField(mode, "absoluteRange",
                new SkillRangeData(RangeShape.Square, 1, maxRange));
        }

        private static SkillEffectData CreateEffect(EffectType type, float multiplier)
        {
            var effect = new SkillEffectData();
            SetField(effect, "effectType", type);
            SetField(effect, "targetModeIndex", 0);
            SetField(effect, "damageMultiplier", multiplier);
            SetField(effect, "damageValueSource", DamageValueSource.AverageAttack);
            return effect;
        }

        private static BattleUnit CreateUnit(
            string id,
            int health,
            float minAttack,
            float maxAttack)
        {
            return new BattleUnit
            {
                Character = new CharacterInstance
                {
                    CharacterId = id,
                    CurrentHealth = health,
                    LifeState = CharacterLifeState.Alive,
                    Stats = new CharacterStats
                    {
                        MaxHealth = new Stat(100f),
                        MinAttack = new Stat(minAttack),
                        MaxAttack = new Stat(maxAttack)
                    }
                }
            };
        }

        private BattleUnit CreateAiCombatUnit(
            string id,
            bool isPlayerSide,
            GridPosition position)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            createdAssets.Add(definition);
            SetAutoProperty(definition, "DisplayName", id);

            return new BattleUnit
            {
                Character = new CharacterInstance
                {
                    CharacterId = id,
                    Definition = definition,
                    CurrentHealth = 100,
                    LifeState = CharacterLifeState.Alive,
                    ResourcePool = new ResourcePool(ResourceType.None, 0, 0, 0),
                    Stats = new CharacterStats
                    {
                        MaxHealth = new Stat(100f),
                        MinAttack = new Stat(10f),
                        MaxAttack = new Stat(10f),
                        PhysicalDefense = new Stat(0f),
                        MagicalDefense = new Stat(0f),
                        Dodge = new Stat(0f),
                        CritChance = new Stat(0f),
                        CritDamage = new Stat(1.5f),
                        Speed = new Stat(10f),
                        WeaponAccuracy = new Stat(100f)
                    }
                },
                Position = position,
                CurrentActionPoints = 10,
                MaxActionPoints = 10,
                IsPlayerSide = isPlayerSide
            };
        }

        private static CombatSession CreateAiSession(
            BattleUnit aiUnit,
            BattleUnit target)
        {
            return new CombatSession
            {
                CombatId = "test.ai-action-step",
                Outcome = CombatOutcomeState.Ongoing,
                // Keep the adjacent units in a one-row arena so that the
                // post-skill assertion cannot accidentally observe a move
                // around the target as another valid AI action.
                Grid = new BattleGrid(2, 1),
                ActiveUnit = aiUnit,
                Units = new List<BattleUnit> { aiUnit, target }
            };
        }

        private SkillData CreateZeroCostDamageSkill(string skillId)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            createdAssets.Add(skill);
            SetAutoProperty(skill, "SkillId", skillId);
            SetAutoProperty(skill, "DisplayName", skillId);
            SetAutoProperty(skill, "ActionCost", 0);
            SetAutoProperty(skill, "SkillAccuracy", 100f);
            SetAutoProperty(skill, "RequiresLineOfSight", false);

            var targetMode = new SkillTargetModeData();
            SetField(targetMode, "targetType", TargetType.Single);
            SetField(targetMode, "targetingType", TargetingType.Enemies);
            SetField(targetMode, "absoluteRange",
                new SkillRangeData(RangeShape.Square, 1, 1));

            var effect = CreateEffect(EffectType.DealDamage, 0.1f);
            SetField(effect, "alwaysHits", true);
            SetField(effect, "damageValueSource", DamageValueSource.AverageAttack);
            SetField(effect, "damageType", DamageType.Physical);

            SetField(skill, "targetModes", new List<SkillTargetModeData> { targetMode });
            SetField(skill, "effects", new List<SkillEffectData> { effect });
            return skill;
        }

        private static SimpleCombatAiSystem CreateExecutableAiSystem()
        {
            return new SimpleCombatAiSystem(
                skillExecutionSystem: new SkillExecutionSystem(),
                targetingService: new CombatTargetingService(),
                movementSystem: new CombatMovementSystem(),
                lineOfSightService: new CombatLineOfSightService());
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }

        private static void SetAutoProperty(object target, string propertyName, object value)
        {
            SetField(target, $"<{propertyName}>k__BackingField", value);
        }
    }
}
