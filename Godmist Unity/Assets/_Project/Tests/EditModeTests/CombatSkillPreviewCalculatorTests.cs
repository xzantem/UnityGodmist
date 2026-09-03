using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
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
    public class CombatSkillPreviewCalculatorTests
    {
        private readonly List<Object> createdAssets = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in createdAssets)
            {
                if (asset != null) Object.DestroyImmediate(asset);
            }

            createdAssets.Clear();
        }

        [Test]
        public void CalculateDamageRange_SumsHitsAndIncludesOptionalBestCase()
        {
            var caster = CreateUnit(8, 12, critChance: 0.25f);
            var effect = CreateDamageEffect(
                hitCount: 2,
                bonusChance: 1f,
                bonusMultiplier: 2f);
            var skill = CreateSkill(effect);

            var range = CombatSkillPreviewCalculator.CalculateDamageRange(
                caster,
                skill);

            Assert.That(range.HasDamage, Is.True);
            Assert.That(range.Minimum, Is.EqualTo(16));
            Assert.That(range.Maximum, Is.EqualTo(48));
        }

        [Test]
        public void CalculateTargetPreview_AppliesDefensePerHitBeforeSumming()
        {
            var caster = CreateUnit(8, 12, critChance: 0f);
            var target = CreateUnit(1, 1, critChance: 0f);
            target.Character.Stats.PhysicalDefense = new Stat(10f);
            var skill = CreateSkill(CreateDamageEffect(hitCount: 2));

            var preview = CombatSkillPreviewCalculator.CalculateTargetPreview(
                caster,
                target,
                skill);

            Assert.That(preview.Damage.Minimum, Is.EqualTo(8));
            Assert.That(preview.Damage.Maximum, Is.EqualTo(14));
            Assert.That(preview.HitChance, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void CombatFeedbackEvent_CarriesEffectName()
        {
            var target = CreateUnit(1, 1, critChance: 0f);

            var feedback = new CombatFeedbackEvent(
                target,
                CombatFeedbackType.BuffApplied,
                effectName: "Stalwart Protection");

            Assert.That(feedback.EffectName, Is.EqualTo("Stalwart Protection"));
        }

        private SkillData CreateSkill(SkillEffectData effect)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            createdAssets.Add(skill);
            SetField(skill, "effects", new List<SkillEffectData> { effect });
            SetField(skill, "<SkillAccuracy>k__BackingField", 100f);
            return skill;
        }

        private static SkillEffectData CreateDamageEffect(
            int hitCount = 1,
            float bonusChance = 0f,
            float bonusMultiplier = 1f)
        {
            var effect = new SkillEffectData();
            SetField(effect, "effectType", EffectType.DealDamage);
            SetField(effect, "damageValueSource", DamageValueSource.AttackRange);
            SetField(effect, "damageMultiplier", 1f);
            SetField(effect, "damageHitCount", hitCount);
            SetField(effect, "bonusDamageMultiplierChance", bonusChance);
            SetField(effect, "bonusDamageMultiplier", bonusMultiplier);
            SetField(effect, "damageType", DamageType.Physical);
            return effect;
        }

        private static BattleUnit CreateUnit(
            float minAttack,
            float maxAttack,
            float critChance)
        {
            return new BattleUnit
            {
                Character = new CharacterInstance
                {
                    WeaponAccuracy = 100f,
                    LifeState = CharacterLifeState.Alive,
                    Stats = new CharacterStats
                    {
                        MinAttack = new Stat(minAttack),
                        MaxAttack = new Stat(maxAttack),
                        CritChance = new Stat(critChance),
                        CritDamage = new Stat(1.5f)
                    },
                    PassiveEffects = new List<PassiveEffectRuntime>()
                }
            };
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {fieldName}");
            field.SetValue(target, value);
        }
    }
}
