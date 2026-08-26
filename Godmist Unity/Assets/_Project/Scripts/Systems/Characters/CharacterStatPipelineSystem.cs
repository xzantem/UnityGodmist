using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Items;
using UnityEngine;

namespace Godmist.Systems.Characters
{
    public class CharacterStatPipelineSystem
    {
        public void Refresh(CharacterInstance character)
        {
            if (character?.Stats == null) return;

            var accumulators = new Dictionary<CharacterStatType, StatModifierAccumulator>();

            ApplyEquipmentModifiers(character.Equipment, accumulators);
            ApplyPassiveEffectModifiers(character.PassiveEffects, accumulators);
            ApplySkillTreeGlobalPassiveModifiers(character, accumulators);

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

        private static void ApplyEquipmentModifiers(
            EquipmentSet equipment,
            Dictionary<CharacterStatType, StatModifierAccumulator> accumulators)
        {
            if (equipment == null) return;

            foreach (var item in equipment.GetEquippedItems())
            {
                ApplyModifiers(item.StatModifiers, accumulators);
            }
        }

        private static void ApplyPassiveEffectModifiers(
            List<PassiveEffectRuntime> passiveEffects,
            Dictionary<CharacterStatType, StatModifierAccumulator> accumulators)
        {
            if (passiveEffects == null) return;

            foreach (var passiveEffect in passiveEffects)
            {
                if (passiveEffect == null) continue;

                ApplyModifiers(
                    passiveEffect
                        .GetPayload<StatModifiersPayload>()
                        ?.Modifiers,
                    accumulators);
            }
        }

        private static void ApplySkillTreeGlobalPassiveModifiers(
            CharacterInstance character,
            Dictionary<CharacterStatType, StatModifierAccumulator> accumulators)
        {
            foreach (var tree in character?.ClassData?.SkillTrees ?? new())
            {
                if (tree == null || string.IsNullOrWhiteSpace(tree.Id)) continue;

                ApplyModifiers(
                    SkillTreeProgressionSystem.GetGlobalPassiveModifiers(
                        tree,
                        SkillTreeProgressionSystem.GetInvestedRanks(character, tree.Id)),
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
