using System;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public readonly struct CombatSkillDamageRange
    {
        public CombatSkillDamageRange(int minimum, int maximum, bool hasDamage)
        {
            Minimum = Mathf.Max(0, minimum);
            Maximum = Mathf.Max(Minimum, maximum);
            HasDamage = hasDamage;
        }

        public int Minimum { get; }
        public int Maximum { get; }
        public bool HasDamage { get; }
    }

    public readonly struct CombatSkillTargetPreview
    {
        public CombatSkillTargetPreview(
            CombatSkillDamageRange damage,
            float hitChance,
            bool hasHitChance)
        {
            Damage = damage;
            HitChance = Mathf.Clamp01(hitChance);
            HasHitChance = hasHitChance;
        }

        public CombatSkillDamageRange Damage { get; }
        public float HitChance { get; }
        public bool HasHitChance { get; }
        public bool IsValid => Damage.HasDamage;
    }

    /// <summary>
    /// Shared deterministic preview math for combat UI. It deliberately reports a
    /// worst/best-case range for a regular hit: optional bonus multipliers are
    /// included only in the upper bound. Critical damage is intentionally not
    /// part of the displayed range.
    /// </summary>
    public static class CombatSkillPreviewCalculator
    {
        public static CombatSkillDamageRange CalculateDamageRange(
            BattleUnit caster,
            SkillData skill)
        {
            var minimum = 0;
            var maximum = 0;
            var hasDamage = false;

            foreach (var effect in skill?.Effects ?? Array.Empty<SkillEffectData>())
            {
                if (effect?.EffectType != EffectType.DealDamage)
                {
                    continue;
                }

                var effectRange = CalculateDamageRange(caster, effect);
                minimum += effectRange.Minimum;
                maximum += effectRange.Maximum;
                hasDamage |= effectRange.HasDamage;
            }

            return new CombatSkillDamageRange(minimum, maximum, hasDamage);
        }

        /// <summary>
        /// Calculates the complete damage contribution of one DealDamage effect,
        /// including its hit count and caster-side damage modifiers. This is kept
        /// separate from the skill overload so tooltip lines can describe each
        /// effect without rebuilding a temporary SkillData asset.
        /// </summary>
        public static CombatSkillDamageRange CalculateDamageRange(
            BattleUnit caster,
            SkillEffectData effect)
        {
            if (effect?.EffectType != EffectType.DealDamage)
            {
                return new CombatSkillDamageRange(0, 0, false);
            }

            var range = CalculateEffectDamageRange(caster, effect);
            if (!range.HasDamage)
            {
                return range;
            }

            var hitCount = Mathf.Max(1, effect.DamageHitCount);
            return new CombatSkillDamageRange(
                ApplyCurrentDamageModifiers(caster, null, range.Minimum) * hitCount,
                ApplyCurrentDamageModifiers(caster, null, range.Maximum) * hitCount,
                true);
        }

        public static CombatSkillTargetPreview CalculateTargetPreview(
            BattleUnit caster,
            BattleUnit target,
            SkillData skill,
            int targetModeIndex = -1)
        {
            var minimum = 0;
            var maximum = 0;
            var hasDamage = false;
            var hasGuaranteedHit = true;

            foreach (var effect in skill?.Effects ?? Array.Empty<SkillEffectData>())
            {
                if (effect?.EffectType != EffectType.DealDamage)
                {
                    continue;
                }

                if (targetModeIndex >= 0
                    && effect.TargetModeIndex != targetModeIndex)
                {
                    continue;
                }

                var range = CalculateEffectDamageRange(caster, effect);
                var hitCount = Mathf.Max(1, effect.DamageHitCount);
                for (var hitIndex = 0; hitIndex < hitCount; hitIndex++)
                {
                    var minimumAfterDefense = CombatDamageSystem.ApplyDefense(
                        caster,
                        target,
                        range.Minimum,
                        effect.DamageType,
                        effect.BonusPenetration);
                    var maximumAfterDefense = CombatDamageSystem.ApplyDefense(
                        caster,
                        target,
                        range.Maximum,
                        effect.DamageType,
                        effect.BonusPenetration);
                    minimum += ApplyCurrentDamageModifiers(
                        caster,
                        target,
                        minimumAfterDefense);
                    maximum += ApplyCurrentDamageModifiers(
                        caster,
                        target,
                        maximumAfterDefense);
                }
                hasDamage = true;

                if (!effect.AlwaysHits
                    && !CombatDamageSystem.IsGuaranteedHitDamageType(effect.DamageType))
                {
                    hasGuaranteedHit = false;
                }
            }

            var hitChance = 1f;
            if (hasDamage && !hasGuaranteedHit)
            {
                var dodge = target?.Character?.Stats == null
                    ? 0f
                    : target.Character.Stats.Dodge.GetValue();
                hitChance = CombatDamageSystem.CalculateHitChance(
                    caster?.Character?.WeaponAccuracy ?? 0f,
                    skill?.SkillAccuracy ?? 0f,
                    dodge);
            }

            return new CombatSkillTargetPreview(
                new CombatSkillDamageRange(minimum, maximum, hasDamage),
                hitChance,
                hasDamage);
        }

        private static CombatSkillDamageRange CalculateEffectDamageRange(
            BattleUnit caster,
            SkillEffectData effect)
        {
            if (caster?.Character?.Stats == null || effect == null)
            {
                return new CombatSkillDamageRange(0, 0, false);
            }

            var minimumAttack = Mathf.Max(0,
                Mathf.RoundToInt(caster.Character.Stats.MinAttack.GetValue()));
            var maximumAttack = Mathf.Max(minimumAttack,
                Mathf.RoundToInt(caster.Character.Stats.MaxAttack.GetValue()));

            var baseMinimum = effect.DamageValueSource switch
            {
                DamageValueSource.MinAttack => minimumAttack,
                DamageValueSource.MaxAttack => maximumAttack,
                DamageValueSource.AverageAttack =>
                    Mathf.RoundToInt((minimumAttack + maximumAttack) * 0.5f),
                _ => minimumAttack
            };
            var baseMaximum = effect.DamageValueSource switch
            {
                DamageValueSource.MinAttack => minimumAttack,
                DamageValueSource.MaxAttack => maximumAttack,
                DamageValueSource.AverageAttack => baseMinimum,
                _ => maximumAttack
            };

            var multiplier = Mathf.Max(0f, effect.DamageMultiplier);
            var minimum = Mathf.CeilToInt(baseMinimum * multiplier);
            var maximum = 0;
            var maximumMultiplier = multiplier;
            if (effect.BonusDamageMultiplierChance > 0f)
            {
                maximumMultiplier *= effect.BonusDamageMultiplier;
            }

            maximum = Mathf.CeilToInt(baseMaximum * maximumMultiplier);
            return new CombatSkillDamageRange(
                minimum,
                maximum,
                true);
        }

        private static int ApplyCurrentDamageModifiers(
            BattleUnit caster,
            BattleUnit target,
            int damage)
        {
            if (damage <= 0)
            {
                return 0;
            }

            var variableSystem = new CombatVariableSystem();
            var dealtMultiplier = variableSystem.GetMultiplier(
                caster?.Character,
                GameplayVariableType.DamageDealtPercent);
            var receivedMultiplier = variableSystem.GetMultiplier(
                target?.Character,
                GameplayVariableType.DamageReceivedPercent);

            return Mathf.Max(
                0,
                Mathf.RoundToInt(damage * dealtMultiplier * receivedMultiplier));
        }
    }
}
