using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Items;
using Godmist.Systems.Combat;
using UnityEngine;

namespace Godmist.Systems.Characters
{
    public class CharacterStatPipelineSystem
    {
        public void Refresh(CharacterInstance character)
        {
            if (character?.Stats == null) return;

            if(character.ActiveSkillLoadout.IsInitialized)
            {
                var known=new HashSet<string>();
                foreach(var skill in character.GetKnownSkills())known.Add(skill.SkillId);
                foreach(var id in character.ActiveSkillLoadout.ActiveSkillIds)
                    if(!known.Contains(id))character.ActiveSkillLoadout.TryRemoveSkill(id);
            }
            new PassiveEffectReconciliationSystem().Reconcile(character);
            var accumulators = new Dictionary<CharacterStatType, StatModifierAccumulator>();

            ApplyPassiveEffectModifiers(character.PassiveEffects, accumulators);
            RefreshResistances(character);

            foreach (CharacterStatType statType in Enum.GetValues(typeof(CharacterStatType)))
            {
                ref var stat = ref character.Stats.GetStat(statType);
                stat.ResetEffectiveValue();

                var effectiveValue = stat.BaseValue;

                if (accumulators.TryGetValue(statType, out var accumulator))
                {
                    effectiveValue = CalculateEffectiveValue(effectiveValue, accumulator);
                }

                effectiveValue = statType == CharacterStatType.MaxHealth
                    ? Mathf.Max(1f, effectiveValue)
                    : IsDefensePenetration(statType)
                        ? Mathf.Clamp01(effectiveValue)
                        : Mathf.Max(0f, effectiveValue);

                stat.SetEffectiveValue(effectiveValue);
            }

            SyncDependentRuntimeValues(character);
        }

        private static float CalculateEffectiveValue(
            float baseValue,
            StatModifierAccumulator accumulator)
        {
            var effectiveValue = (baseValue + accumulator.FlatBaseBonus)
                * (1f + accumulator.AdditivePercentBonus)
                * accumulator.MultiplicativePercentMultiplier
                + accumulator.FlatAbsoluteBonus;

            return accumulator.HasMaximum
                ? Mathf.Min(effectiveValue, accumulator.MaximumValue)
                : effectiveValue;
        }

        private static void ApplyPassiveEffectModifiers(
            List<PassiveEffectRuntime> passiveEffects,
            Dictionary<CharacterStatType, StatModifierAccumulator> accumulators)
        {
            if (passiveEffects == null) return;

            foreach (var passiveEffect in passiveEffects)
            {
                if (!PassiveEffectLifecycleSystem.IsActive(passiveEffect)) continue;

                ApplyModifiers(
                    passiveEffect
                        .GetPayload<StatModifiersPayload>()
                        ?.Modifiers,
                    accumulators);
            }
        }

        private static void ApplyModifiers(
            List<StatModifier> modifiers,
            Dictionary<CharacterStatType, StatModifierAccumulator> accumulators)
        {
            if (modifiers == null) return;

            foreach (var modifier in modifiers)
            {
                if (modifier == null) continue;

                var accumulator = accumulators.TryGetValue(
                    modifier.StatType,
                    out var existingAccumulator)
                    ? existingAccumulator
                    : StatModifierAccumulator.Create();

                accumulator.Add(modifier);
                accumulators[modifier.StatType] = accumulator;
            }
        }

        private static void SyncDependentRuntimeValues(CharacterInstance character)
        {
            var maxHealth = Mathf.Max(
                1,
                Mathf.RoundToInt(character.Stats.MaxHealth.GetValue()));

            character.CurrentHealth = Mathf.Clamp(character.CurrentHealth, 0, maxHealth);

            if (character.ResourcePool == null) return;

            character.ResourcePool.Regeneration = Mathf.Max(
                0,
                Mathf.RoundToInt(character.Stats.ResourceRegeneration.GetValue()));
            character.WeaponAccuracy = Mathf.Max(
                0f,
                character.Stats.WeaponAccuracy.GetValue());
            character.ResourcePool.MaxValue = Mathf.Max(
                0,
                Mathf.RoundToInt(character.Stats.MaxResource.GetValue()));
            character.ResourcePool.CurrentValue = Mathf.Clamp(
                character.ResourcePool.CurrentValue,
                0,
                character.ResourcePool.MaxValue);
        }

        private static void RefreshResistances(CharacterInstance character)
        {
            character.Resistances ??= new ResistanceSet();
            var baseValue = character.BaseResistances ?? new ResistanceSet();
            character.Resistances.BleedResistance = baseValue.BleedResistance;
            character.Resistances.PoisonResistance = baseValue.PoisonResistance;
            character.Resistances.FireResistance = baseValue.FireResistance;
            character.Resistances.WeakeningResistance = baseValue.WeakeningResistance;
            character.Resistances.SuppressionResistance = baseValue.SuppressionResistance;
            character.Resistances.MoveResistance = baseValue.MoveResistance;

            foreach (var passiveEffect in character.PassiveEffects
                         ?? new List<PassiveEffectRuntime>())
            {
                if (!PassiveEffectLifecycleSystem.IsActive(passiveEffect)) continue;

                var modifier = passiveEffect.GetPayload<ResistanceModifierPayload>();
                if (modifier == null || modifier.Value == 0f) continue;

                switch (modifier.ResistanceType)
                {
                    case ResistanceType.Bleed: character.Resistances.BleedResistance += modifier.Value; break;
                    case ResistanceType.Poison: character.Resistances.PoisonResistance += modifier.Value; break;
                    case ResistanceType.Fire: character.Resistances.FireResistance += modifier.Value; break;
                    case ResistanceType.Weakening: character.Resistances.WeakeningResistance += modifier.Value; break;
                    case ResistanceType.Suppression: character.Resistances.SuppressionResistance += modifier.Value; break;
                    case ResistanceType.Move: character.Resistances.MoveResistance += modifier.Value; break;
                }
            }

            character.Resistances.BleedResistance = Mathf.Clamp01(character.Resistances.BleedResistance);
            character.Resistances.PoisonResistance = Mathf.Clamp01(character.Resistances.PoisonResistance);
            character.Resistances.FireResistance = Mathf.Clamp01(character.Resistances.FireResistance);
            character.Resistances.WeakeningResistance = Mathf.Clamp01(character.Resistances.WeakeningResistance);
            character.Resistances.SuppressionResistance = Mathf.Clamp01(character.Resistances.SuppressionResistance);
            character.Resistances.MoveResistance = Mathf.Clamp01(character.Resistances.MoveResistance);
        }

        private static bool IsDefensePenetration(CharacterStatType statType)
        {
            return statType == CharacterStatType.PhysicalDefensePenetration
                || statType == CharacterStatType.MagicalDefensePenetration;
        }

        private struct StatModifierAccumulator
        {
            public float FlatBaseBonus;
            public float AdditivePercentBonus;
            public float MultiplicativePercentMultiplier;
            public float FlatAbsoluteBonus;
            public bool HasMaximum;
            public float MaximumValue;

            public static StatModifierAccumulator Create()
            {
                return new StatModifierAccumulator
                {
                    MultiplicativePercentMultiplier = 1f
                };
            }

            public void Add(StatModifier modifier)
            {
                switch (modifier.Operation)
                {
                    case StatModifierOperation.FlatBase:
                        FlatBaseBonus += modifier.Value;
                        return;

                    case StatModifierOperation.FlatAbsolute:
                        FlatAbsoluteBonus += modifier.Value;
                        return;

                    case StatModifierOperation.Percent:
                        if (modifier.Stacking == StatModifierStacking.Additive)
                        {
                            AdditivePercentBonus += modifier.Value;
                            return;
                        }

                        MultiplicativePercentMultiplier *= Mathf.Max(
                            0f,
                            1f + modifier.Value);
                        return;

                    case StatModifierOperation.Maximum:
                        if (!HasMaximum)
                        {
                            HasMaximum = true;
                            MaximumValue = modifier.Value;
                            return;
                        }

                        MaximumValue = Mathf.Min(MaximumValue, modifier.Value);
                        return;
                }
            }
        }
    }
}
