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
    public class ScheduledCombatEffectTests
    {
        private readonly List<Object> createdAssets = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in createdAssets)
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ProcessScheduledEffects_ExecutesAfterNextCasterTurnOnly()
        {
            var session = new CombatSession { Grid = new BattleGrid(5, 5) };
            var caster = CreateUnit("caster", new GridPosition(1, 1));
            var otherCaster = CreateUnit("other", new GridPosition(1, 2));
            var target = CreateUnit("target", new GridPosition(2, 1));
            session.Units.Add(caster);
            session.Units.Add(otherCaster);
            session.Units.Add(target);
            var skill = CreateSkill(CreateDelayedDamageEffect());
            var system = new SkillExecutionSystem();

            session.ScheduledEffects.Add(new ScheduledCombatEffect(
                caster.Character.CharacterId, skill, skill.Effects[0], 0,
                remainingCasterTurns: 2, target.Character.CharacterId,
                target.Position));

            system.ProcessScheduledEffects(session, otherCaster);
            Assert.That(session.ScheduledEffects[0].RemainingCasterTurns, Is.EqualTo(2));

            system.ProcessScheduledEffects(session, caster);
            Assert.That(session.ScheduledEffects[0].RemainingCasterTurns, Is.EqualTo(1));

            system.ProcessScheduledEffects(session, caster);
            Assert.That(session.ScheduledEffects, Is.Empty);
            Assert.That(target.Character.CurrentHealth, Is.LessThan(100));
        }

        [Test]
        public void CancelScheduledEffectsByCaster_RemovesOnlyMatchingCasterEvents()
        {
            var session = new CombatSession();
            var first = CreateUnit("first", new GridPosition(1, 1));
            var second = CreateUnit("second", new GridPosition(1, 2));
            var skill = CreateSkill(CreateDelayedDamageEffect());

            session.ScheduledEffects.Add(new ScheduledCombatEffect(
                "first", skill, skill.Effects[0], 0, 2, string.Empty, null));
            session.ScheduledEffects.Add(new ScheduledCombatEffect(
                "second", skill, skill.Effects[0], 0, 2, string.Empty, null));

            var removed = new SkillExecutionSystem()
                .CancelScheduledEffectsByCaster(session, first.Character.CharacterId);

            Assert.That(removed, Is.EqualTo(1));
            Assert.That(session.ScheduledEffects, Has.Count.EqualTo(1));
            Assert.That(session.ScheduledEffects[0].CasterCharacterId, Is.EqualTo("second"));
        }

        [Test]
        public void ProcessScheduledEffects_DownedCasterStillExecutesPhaseB()
        {
            var session = new CombatSession { Grid = new BattleGrid(5, 5) };
            var caster = CreateUnit("caster", new GridPosition(1, 1));
            var target = CreateUnit("target", new GridPosition(2, 1));
            session.Units.Add(caster);
            session.Units.Add(target);
            var skill = CreateSkill(CreateDelayedDamageEffect());
            caster.Character.LifeState = CharacterLifeState.Downed;

            session.ScheduledEffects.Add(new ScheduledCombatEffect(
                caster.Character.CharacterId, skill, skill.Effects[0], 0, 1,
                target.Character.CharacterId, target.Position));

            new SkillExecutionSystem().ProcessScheduledEffects(session, caster);

            Assert.That(session.ScheduledEffects, Is.Empty);
            Assert.That(target.Character.CurrentHealth, Is.LessThan(100));
        }

        private BattleUnit CreateUnit(string id, GridPosition position)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            createdAssets.Add(definition);
            SetAutoProperty(definition, "Id", id);
            SetAutoProperty(definition, "DisplayName", id);

            return new BattleUnit
            {
                Character = new CharacterInstance
                {
                    CharacterId = id,
                    Definition = definition,
                    LifeState = CharacterLifeState.Alive,
                    CurrentHealth = 100,
                    Stats = new CharacterStats
                    {
                        MaxHealth = new Stat(100f),
                        MinAttack = new Stat(10f),
                        MaxAttack = new Stat(10f),
                        Speed = new Stat(60f)
                    }
                },
                Position = position
            };
        }

        private SkillData CreateSkill(SkillEffectData effect)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            createdAssets.Add(skill);
            SetAutoProperty(skill, "SkillId", "skill.delayed");
            SetAutoProperty(skill, "DisplayName", "Delayed Test");
            SetField(skill, "targetModes", new List<SkillTargetModeData>
            {
                new SkillTargetModeData()
            });
            SetField(skill, "effects", new List<SkillEffectData> { effect });
            return skill;
        }

        private static SkillEffectData CreateDelayedDamageEffect()
        {
            var effect = new SkillEffectData();
            SetField(effect, "effectType", EffectType.DealDamage);
            SetField(effect, "damageType", DamageType.Direct);
            SetField(effect, "damageValueSource", DamageValueSource.AverageAttack);
            SetField(effect, "damageMultiplier", 1f);
            SetField(effect, "delayCasterTurns", 1);
            return effect;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private static void SetAutoProperty(object target, string propertyName, object value)
        {
            target.GetType().GetField($"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }
    }
}
