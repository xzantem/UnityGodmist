using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Items;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class ModifyStatSystemTests
    {
        private CharacterStatPipelineSystem statPipeline;
        private PassiveEffectLifecycleSystem lifecycleSystem;
        private ModifyStatSystem modifyStatSystem;

        [SetUp]
        public void SetUp()
        {
            statPipeline = new CharacterStatPipelineSystem();
            lifecycleSystem = new PassiveEffectLifecycleSystem(
                statPipeline: statPipeline);

            modifyStatSystem = new ModifyStatSystem(
                passiveEffectLifecycleSystem: lifecycleSystem);
        }

        [Test]
        public void Apply_TimedModifier_ChangesStatAndExpires()
        {
            var target = CreateUnit();
            var skill = CreateSkill("skill.speed_boost");
            var effect = CreateModifyStatEffect(
                CharacterStatType.Speed,
                StatModifierOperation.FlatBase,
                StatModifierStacking.Additive,
                value: 15f,
                durationTurns: 2);

            var applied = modifyStatSystem.Apply(
                source: null,
                target,
                skill,
                effect,
                effectIndex: 0);

            Assert.That(applied, Is.True);
            Assert.That(target.Character.Stats.Speed.GetValue(), Is.EqualTo(75f));
            Assert.That(target.Character.PassiveEffects, Has.Count.EqualTo(1));
            Assert.That(
                target.Character.PassiveEffects[0].Lifetime,
                Is.EqualTo(PassiveEffectLifetime.Timed));

            lifecycleSystem.ProcessTurnEnd(target);

            Assert.That(target.Character.Stats.Speed.GetValue(), Is.EqualTo(75f));
            Assert.That(
                target.Character.PassiveEffects[0].RemainingDuration,
                Is.EqualTo(1));

            lifecycleSystem.ProcessTurnEnd(target);

            Assert.That(target.Character.PassiveEffects, Is.Empty);
            Assert.That(target.Character.Stats.Speed.GetValue(), Is.EqualTo(60f));

            Object.DestroyImmediate(skill);
        }

        [Test]
        public void Apply_CombatScopedModifier_RemainsUntilCombatEnds()
        {
            var target = CreateUnit();
            var skill = CreateSkill("skill.combat_focus");
            var effect = CreateModifyStatEffect(
                CharacterStatType.MaxHealth,
                StatModifierOperation.FlatBase,
                StatModifierStacking.Additive,
                value: 40f,
                durationTurns: 0);

            var applied = modifyStatSystem.Apply(
                source: null,
                target,
                skill,
                effect,
                effectIndex: 0);

            Assert.That(applied, Is.True);
            Assert.That(
                target.Character.Stats.MaxHealth.GetValue(),
                Is.EqualTo(140f));

            var runtimeEffect = target.Character.PassiveEffects[0];

            Assert.That(
                runtimeEffect.Lifetime,
                Is.EqualTo(PassiveEffectLifetime.CombatScoped));

            var session = new CombatSession();
            session.Units.Add(target);

            var removedCount = lifecycleSystem.RemoveCombatScopedEffects(session);

            Assert.That(removedCount, Is.EqualTo(1));
            Assert.That(target.Character.PassiveEffects, Is.Empty);
            Assert.That(
                target.Character.Stats.MaxHealth.GetValue(),
                Is.EqualTo(100f));

            Object.DestroyImmediate(skill);
        }

        [Test]
        public void Apply_SameEffect_RefreshesDurationAndReplacesValue()
        {
            var target = CreateUnit();
            var skill = CreateSkill("skill.focus");
            var effect = CreateModifyStatEffect(
                CharacterStatType.Speed,
                StatModifierOperation.FlatBase,
                StatModifierStacking.Additive,
                value: 10f,
                durationTurns: 1);

            modifyStatSystem.Apply(
                source: null,
                target,
                skill,
                effect,
                effectIndex: 0);

            SetPrivateField(effect, "modifierValue", 30f);
            SetPrivateField(effect, "durationTurns", 3);

            modifyStatSystem.Apply(
                source: null,
                target,
                skill,
                effect,
                effectIndex: 0);

            Assert.That(target.Character.PassiveEffects, Has.Count.EqualTo(1));
            Assert.That(
                target.Character.PassiveEffects[0].RemainingDuration,
                Is.EqualTo(3));

            Assert.That(
                target.Character.Stats.Speed.GetValue(),
                Is.EqualTo(90f));

            Object.DestroyImmediate(skill);
        }

        [Test]
        public void Apply_TimedResistanceModifier_IsUsedAndExpires()
        {
            var target = CreateUnit();
            target.Character.Resistances = new ResistanceSet
            {
                BleedResistance = .5f
            };
            var skill = CreateSkill("skill.resistance");
            var effect = new SkillEffectData();
            SetPrivateField(effect, "effectType", EffectType.ModifyResistance);
            SetPrivateField(effect, "resistanceType", ResistanceType.Bleed);
            SetPrivateField(effect, "modifierValue", -.25f);
            SetPrivateField(effect, "durationTurns", 2);
            var resistanceSystem = new ModifyResistanceSystem(
                passiveEffectLifecycleSystem: lifecycleSystem);

            Assert.That(
                resistanceSystem.Apply(null, target, skill, effect, 0),
                Is.True);
            Assert.That(
                ModifyResistanceSystem.GetModifier(target, ResistanceType.Bleed),
                Is.EqualTo(-.25f));
            Assert.That(target.Character.PassiveEffects, Has.Count.EqualTo(1));
            Assert.That(
                target.Character.PassiveEffects[0]
                    .GetPayload<ResistanceModifierPayload>(),
                Is.Not.Null);

            lifecycleSystem.ProcessTurnEnd(target);
            Assert.That(
                ModifyResistanceSystem.GetModifier(target, ResistanceType.Bleed),
                Is.EqualTo(-.25f));

            lifecycleSystem.ProcessTurnEnd(target);
            Assert.That(target.Character.PassiveEffects, Is.Empty);
            Assert.That(
                ModifyResistanceSystem.GetModifier(target, ResistanceType.Bleed),
                Is.EqualTo(0f));

            Object.DestroyImmediate(skill);
        }

        [Test]
        public void SkillExecution_AppliesModifyResistanceToTarget()
        {
            var caster = CreateUnit();
            var target = CreateUnit();
            target.Character.BaseResistances = new ResistanceSet { BleedResistance = .5f };
            target.Character.Resistances.BleedResistance = .5f;
            caster.Position = new GridPosition(0, 0);
            target.Position = new GridPosition(1, 0);

            var skill = CreateSkill("skill.execute_resistance");
            var targetMode = new SkillTargetModeData();
            SetPrivateField(targetMode, "targetType", TargetType.Single);
            SetPrivateField(targetMode, "targetingType", TargetingType.Enemies);
            SetPrivateField(targetMode, "absoluteRange", new SkillRangeData(RangeShape.Square, 1, 1));

            var effect = new SkillEffectData();
            SetPrivateField(effect, "effectType", EffectType.ModifyResistance);
            SetPrivateField(effect, "targetModeIndex", 0);
            SetPrivateField(effect, "resistanceType", ResistanceType.Bleed);
            SetPrivateField(effect, "modifierValue", -.25f);
            SetPrivateField(effect, "durationTurns", 2);

            SetPrivateField(skill, "targetModes", new List<SkillTargetModeData> { targetMode });
            SetPrivateField(skill, "effects", new List<SkillEffectData> { effect });

            var session = new CombatSession
            {
                Grid = new BattleGrid(3, 3),
                ActiveUnit = caster,
                Units = new List<BattleUnit> { caster, target }
            };

            Assert.That(
                new SkillExecutionSystem().TryExecute(session, caster, skill, target),
                Is.True);
            Assert.That(
                ModifyResistanceSystem.GetModifier(target, ResistanceType.Bleed),
                Is.EqualTo(-.25f));
            Assert.That(
                target.Character.Resistances.BleedResistance,
                Is.EqualTo(.25f));

            Object.DestroyImmediate(skill);
        }

        private static BattleUnit CreateUnit()
        {
            var character = new CharacterInstance
            {
                CurrentHealth = 100,
                LifeState = CharacterLifeState.Alive,
                Stats = new CharacterStats
                {
                    MaxHealth = new Stat(100f),
                    MinAttack = new Stat(10f),
                    MaxAttack = new Stat(20f),
                    PhysicalDefense = new Stat(5f),
                    MagicalDefense = new Stat(5f),
                    Dodge = new Stat(0f),
                    CritChance = new Stat(0f),
                    CritDamage = new Stat(1.5f),
                    Speed = new Stat(60f),
                    ResourceRegeneration = new Stat(0f)
                },
                Equipment = new EquipmentSet(),
                Resistances = new ResistanceSet(),
                PassiveEffects = new List<PassiveEffectRuntime>()
            };

            return new BattleUnit
            {
                Character = character
            };
        }

        private static SkillData CreateSkill(string skillId)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();

            SetAutoProperty(skill, "SkillId", skillId);

            return skill;
        }

        private static SkillEffectData CreateModifyStatEffect(
            CharacterStatType statType,
            StatModifierOperation operation,
            StatModifierStacking stacking,
            float value,
            int durationTurns)
        {
            var effect = new SkillEffectData();

            SetPrivateField(effect, "effectType", EffectType.ModifyStat);
            SetPrivateField(effect, "modifierStatType", statType);
            SetPrivateField(effect, "modifierOperation", operation);
            SetPrivateField(effect, "modifierStacking", stacking);
            SetPrivateField(effect, "modifierValue", value);
            SetPrivateField(effect, "durationTurns", durationTurns);

            return effect;
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
