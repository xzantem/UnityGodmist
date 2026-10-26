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
        // The planner uses the same arithmetic without reconciliation, lifecycle
        // or mutation of any CharacterInstance.
        public static float EvaluateModifiers(float baseValue, CharacterStatType type,
            IEnumerable<PassiveEffectRuntime> effects)
        {
            var accumulator = StatModifierAccumulator.Create();
            foreach (var effect in effects)
            {
                if (!PassiveEffectLifecycleSystem.IsActive(effect)) continue;
                var modifiers = effect.GetPayload<StatModifiersPayload>()?.Modifiers;
                if (modifiers == null) continue;
                foreach (var modifier in modifiers)
                    if (modifier != null && modifier.StatType == type) accumulator.Add(modifier);
            }
            var value = CalculateEffectiveValue(baseValue, accumulator);
            return type == CharacterStatType.MaxHealth ? Mathf.Max(1f, value) : Mathf.Max(0f, value);
        }

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
                    : Mathf.Max(0f, effectiveValue);

                stat.SetEffectiveValue(effectiveValue);
            }

            WarriorCombatSystem.ApplyStats(character);
            MageCombatSystem.ApplyStats(character);
            UniqueEquipmentCombatRules.ApplyDerivedStats(character);
            var attackMultiplier = Mathf.Max(0f, character.Definition?.AttackMultiplier ?? 1f);
            character.Stats.MinAttack.SetEffectiveValue(character.Stats.MinAttack.GetValue() * attackMultiplier);
            character.Stats.MaxAttack.SetEffectiveValue(character.Stats.MaxAttack.GetValue() * attackMultiplier);
            SyncDependentRuntimeValues(character);
            character.Stats.Speed.SetEffectiveValue(character.Stats.Speed.GetValue()
                + MomentumResourceRule.SpeedBonus(character.ResourcePool, character.ResourcePool?.CurrentValue ?? 0));
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

            character.WeaponAccuracy = Mathf.Max(0f, character.Stats.WeaponAccuracy.GetValue());
            if (character.ResourcePool == null) return;

            character.ResourcePool.Regeneration = Mathf.Max(
                0,
                Mathf.RoundToInt(character.Stats.ResourceRegeneration.GetValue()));
            character.ResourcePool.MaxValue = Mathf.Max(
                0,
                Mathf.RoundToInt(character.Stats.MaxResource.GetValue()));
            var overflowRanks = 0;
            foreach (var tree in character.SkillTreeProgress ?? new())
            {
                if (tree?.TreeId != "tree.mage.warlock") continue;
                foreach (var investment in tree.NodeInvestments ?? new())
                    if (investment?.NodeId == "node.mage.warlock.beyond_limits")
                        overflowRanks = Mathf.Clamp(investment.Ranks, 0, 2);
            }
            character.ResourcePool.ManaOverflowPercent = 15 * overflowRanks;
            character.ResourcePool.CurrentValue = Mathf.Clamp(
                character.ResourcePool.CurrentValue,
                0,
                character.ResourcePool.RecoveryLimit);
        }

        private static void RefreshResistances(CharacterInstance character)
        {
            character.Resistances ??= new ResistanceSet();
            // Capture legacy/runtime-only base data once before deriving it.
            character.BaseResistances ??= new ResistanceSet {
                BleedResistance = character.Resistances.BleedResistance,
                PoisonResistance = character.Resistances.PoisonResistance,
                FireResistance = character.Resistances.FireResistance,
                WeakeningResistance = character.Resistances.WeakeningResistance,
                SuppressionResistance = character.Resistances.SuppressionResistance,
                MoveResistance = character.Resistances.MoveResistance };
            character.Resistances.BleedResistance = ModifyResistanceSystem.CalculateResistance(character, ResistanceType.Bleed);
            character.Resistances.PoisonResistance = ModifyResistanceSystem.CalculateResistance(character, ResistanceType.Poison);
            character.Resistances.FireResistance = ModifyResistanceSystem.CalculateResistance(character, ResistanceType.Fire);
            character.Resistances.WeakeningResistance = ModifyResistanceSystem.CalculateResistance(character, ResistanceType.Weakening);
            character.Resistances.SuppressionResistance = ModifyResistanceSystem.CalculateResistance(character, ResistanceType.Suppression);
            character.Resistances.MoveResistance = ModifyResistanceSystem.CalculateResistance(character, ResistanceType.Move);
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
