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
            bool hasHitChance,
            string statusId = null,
            float statusChance = 0f)
        {
            Damage = damage;
            HitChance = Mathf.Clamp01(hitChance);
            HasHitChance = hasHitChance;
            StatusId = statusId;
            StatusChance = Mathf.Clamp01(statusChance);
        }

        public CombatSkillDamageRange Damage { get; }
        public float HitChance { get; }
        public bool HasHitChance { get; }
        public string StatusId { get; }
        public float StatusChance { get; }
        public bool HasStatusChance => !string.IsNullOrWhiteSpace(StatusId);
        public bool IsValid => Damage.HasDamage || HasStatusChance;
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

            foreach (var baseEffect in skill?.Effects ?? Array.Empty<SkillEffectData>())
            {
                var effect = PassiveEffectCombatExecutionSystem.ResolveEffect(caster, skill, baseEffect);
                if (effect?.EffectType != EffectType.DealDamage)
                {
                    continue;
                }

                var effectRange = CalculateDamageRange(caster, effect, skill);
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
            SkillEffectData effect, SkillData skill = null)
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

            var mageMultiplier = MageCombatSystem.DamageMultiplier(new PassiveEffectCombatContext(source:caster, skill:skill, skillId:skill?.SkillId));
            range = new CombatSkillDamageRange(Mathf.RoundToInt(range.Minimum * mageMultiplier), Mathf.RoundToInt(range.Maximum * mageMultiplier), true);
            var hitCount = Mathf.Max(1, effect.DamageHitCount);
            return new CombatSkillDamageRange(
                ApplyCurrentDamageModifiers(caster, null, range.Minimum, skill) * hitCount,
                ApplyCurrentDamageModifiers(caster, null, range.Maximum, skill) * effect.MaximumHitCount,
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
            string statusId = null;
            var statusChance = 0f;

            foreach (var baseEffect in skill?.Effects ?? Array.Empty<SkillEffectData>())
            {
                var effect = PassiveEffectCombatExecutionSystem.ResolveEffect(caster, skill, baseEffect);
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
                var distance = Mathf.Abs(caster.Position.X-target.Position.X)+Mathf.Abs(caster.Position.Y-target.Position.Y);
                var attackType=skill.ResolveAttackType(distance);
                var context=new PassiveEffectCombatContext(source:caster,target:target,skill:skill,skillId:skill.SkillId,attackType:attackType,damageType:effect.DamageType,distance:distance);
                var scoutMultiplier=PaladinCombatSystem.DamageMultiplier(context)*MageCombatSystem.DamageMultiplier(context)*WarriorCombatSystem.DamageMultiplier(context,true)*ScoutCombatSystem.DamageMultiplier(context)*(attackType==AttackType.Melee?skill.PointBlankDamageMultiplier:1f);
                if(skill.GuaranteedCritical)scoutMultiplier*=Mathf.Max(1f,caster.Character.Stats.CritDamage.GetValue()+ScoutCombatSystem.WeakPointCriticalDamage(target));
                range=new CombatSkillDamageRange(Mathf.RoundToInt(range.Minimum*scoutMultiplier),Mathf.RoundToInt(range.Maximum*scoutMultiplier),true);
                var hitCount = Mathf.Max(1, effect.DamageHitCount);
                for (var hitIndex = 0; hitIndex < effect.MaximumHitCount; hitIndex++)
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
                    if (hitIndex < hitCount) minimum += ApplyCurrentDamageModifiers(
                        caster,
                        target,
                        minimumAfterDefense, skill);
                    maximum += ApplyCurrentDamageModifiers(
                        caster,
                        target,
                        maximumAfterDefense, skill);
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
                    PassiveEffectCombatExecutionSystem.ResolveParameter(caster, skill, "accuracy", skill?.SkillAccuracy ?? 0f),
                    dodge);
                hitChance = CombatDamageSystem.ApplyHitChanceBonus(caster?.Character, hitChance);
            }

            // Show only a status whose trigger can be estimated from the current
            // target. Conditional effects with other dependencies are omitted.
            var effects = skill?.Effects;
            if (effects != null)
            {
                for (var index = 0; index < effects.Count; index++)
                {
                    var effect = PassiveEffectCombatExecutionSystem.ResolveEffect(caster, skill, effects[index]);
                    if (effect == null || caster == null || target == null
                        || caster.IsPlayerSide == target.IsPlayerSide
                        || effect.Polarity != EffectPolarity.Negative
                        || targetModeIndex >= 0 && effect.TargetModeIndex != targetModeIndex)
                        continue;

                    var id = effect.EffectType == EffectType.ApplyStatus
                        ? effect.StatusId
                        : effect.EffectType == EffectType.DealDamage ? effect.OnHitStatusId : null;
                    if (string.IsNullOrWhiteSpace(id)) continue;

                    if (effect.EffectType == EffectType.DealDamage && effect.Conditions != null)
                    {
                        var conditional = false;
                        foreach (var condition in effect.Conditions)
                            conditional |= condition != null
                                && condition.ConditionType != EffectConditionType.Always;
                        if (conditional) continue;
                    }

                    var triggerChance = 1f;
                    if (effect.EffectType == EffectType.ApplyStatus)
                    {
                        if (effect.Conditions != null)
                        {
                            var unsupported = false;
                            foreach (var condition in effect.Conditions)
                            {
                                if (condition == null || condition.ConditionType == EffectConditionType.Always) continue;
                                if (condition.ConditionType != EffectConditionType.ReferencedEffectSucceeded
                                    || condition.ReferencedEffectIndex < 0
                                    || condition.ReferencedEffectIndex >= index)
                                {
                                    unsupported = true;
                                    break;
                                }

                                var prior = PassiveEffectCombatExecutionSystem.ResolveEffect(
                                    caster, skill, effects[condition.ReferencedEffectIndex]);
                                if (prior?.EffectType != EffectType.DealDamage)
                                {
                                    unsupported = true;
                                    break;
                                }
                                var priorHit = prior.AlwaysHits
                                    || CombatDamageSystem.IsGuaranteedHitDamageType(prior.DamageType)
                                    ? 1f : CalculateHitChance(caster, target, skill);
                                triggerChance *= 1f - Mathf.Pow(1f - priorHit, prior.DamageHitCount)
                                    * Mathf.Pow(1f - prior.AdditionalHitChance * priorHit,
                                        prior.AdditionalHitTrials);
                            }
                            if (unsupported) continue;
                        }
                        if (!effect.AlwaysHits && effect.Polarity == EffectPolarity.Negative)
                            triggerChance *= CalculateHitChance(caster, target, skill);
                    }
                    else
                    {
                        var perHit = effect.AlwaysHits
                            || CombatDamageSystem.IsGuaranteedHitDamageType(effect.DamageType)
                            ? 1f : CalculateHitChance(caster, target, skill);
                        var application = StatusEffectSystem.CalculateStatusApplicationChance(
                            caster, target, id, effect.OnHitStatusChance);
                        statusId = id;
                        statusChance = 1f - Mathf.Pow(1f - perHit * application,
                                effect.DamageHitCount)
                            * Mathf.Pow(1f - effect.AdditionalHitChance * perHit * application,
                                effect.AdditionalHitTrials);
                        break;
                    }

                    statusId = id;
                    statusChance = triggerChance * StatusEffectSystem.CalculateStatusApplicationChance(
                        caster, target, id, effect.BaseChance);
                    break;
                }
            }

            return new CombatSkillTargetPreview(
                new CombatSkillDamageRange(minimum, maximum, hasDamage),
                hitChance,
                hasDamage,
                statusId,
                statusChance);
        }

        private static float CalculateHitChance(BattleUnit caster, BattleUnit target, SkillData skill)
        {
            var dodge = target?.Character?.Stats?.Dodge.GetValue() ?? 0f;
            var chance = CombatDamageSystem.CalculateHitChance(
                caster?.Character?.WeaponAccuracy ?? 0f,
                PassiveEffectCombatExecutionSystem.ResolveParameter(caster, skill,
                    "accuracy", skill?.SkillAccuracy ?? 0f), dodge);
            return CombatDamageSystem.ApplyHitChanceBonus(caster?.Character, chance);
        }

        private static CombatSkillDamageRange CalculateEffectDamageRange(
            BattleUnit caster,
            SkillEffectData effect)
        {
            if (caster?.Character?.Stats == null || effect == null)
            {
                return new CombatSkillDamageRange(0, 0, false);
            }

            if (effect.DamageValueSource == DamageValueSource.AlchemicalPower)
            {
                var strength = Mathf.Max(0f, effect.DamageMultiplier);
                var maximumStrength = effect.BonusDamageMultiplierChance > 0f
                    ? strength * effect.BonusDamageMultiplier : strength;
                return new CombatSkillDamageRange(
                    CombatDamageValueCalculator.CalculateAlchemicalDamage(caster.Character.Level, strength),
                    CombatDamageValueCalculator.CalculateAlchemicalDamage(caster.Character.Level, maximumStrength), true);
            }

            var minimumAttack = Mathf.Max(0,
                Mathf.RoundToInt(caster.Character.Stats.MinAttack.GetValue()));
            var maximumAttack = Mathf.Max(minimumAttack,
                Mathf.RoundToInt(caster.Character.Stats.MaxAttack.GetValue()));

            if (CombatDamageSystem.HasUniqueBuff(caster, UniqueBuffType.MageMaximumDamage)) minimumAttack = maximumAttack;
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
            int damage, SkillData skill = null)
        {
            if (damage <= 0)
            {
                return 0;
            }

            var variableSystem = new CombatVariableSystem();
            var dealtMultiplier = variableSystem.GetMultiplier(
                caster?.Character,
                GameplayVariableType.DamageDealtPercent);
            dealtMultiplier=WarriorCombatSystem.PreviewDealtMultiplier(caster,skill,dealtMultiplier);
            var receivedMultiplier = variableSystem.GetMultiplier(
                target?.Character,
                GameplayVariableType.DamageReceivedPercent);

            return Mathf.Max(
                0,
                Mathf.RoundToInt(damage * dealtMultiplier * receivedMultiplier * WarriorCombatSystem.Received(null,caster,target,DamageType.Physical) * ScoutCombatSystem.ReceivedDamageMultiplier(caster,target)
                    * PassiveEffectCombatExecutionSystem.ResolveParameter(caster, skill, "damage_multiplier", 1f)));
        }
    }
}
