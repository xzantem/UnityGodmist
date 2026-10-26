using System.Collections.Generic;
using System.Text;
using Godmist.Runtime.Combat;
using Godmist.Core.Enums;
using Godmist.Core.Constants;
using Godmist.Core.Types;
using Godmist.Data.Items;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Items;
using Godmist.Systems.Combat;
using UnityEngine;

namespace Godmist.Systems.Items
{
    public sealed class ConsumableService
    {
        private readonly InventoryService inventoryService = new();
        private readonly PassiveEffectLifecycleSystem passiveEffectLifecycle = new();

        public bool TryUseInExploration(CharacterInstance character, ConsumableItemInstance item,
            out ExplorationConsumableEffect effect)
        {
            return TryUseInExploration(
                character,
                item,
                out effect,
                returnTransition: null);
        }

        public bool TryUseInExploration(
            CharacterInstance character,
            ConsumableItemInstance item,
            out ExplorationConsumableEffect effect,
            System.Func<bool> returnTransition,
            bool passiveTicksActive = true)
        {
            effect = ExplorationConsumableEffect.None;
            var definition = item?.ConsumableDefinition;
            if (character?.Inventory?.Items == null
                || definition == null
                || !character.Inventory.Items.Contains(item)
                || !item.HasCharges
                || definition.IsCombatOnly
                || item.IsFood && character.LifeState != CharacterLifeState.Alive)
            {
                return false;
            }
            var applied = false;
            var runtimeEffects = item.RuntimeEffects ?? new System.Collections.Generic.List<ConsumableEffectRuntime>();
            if (runtimeEffects.Count == 0)
            {
                runtimeEffects = new System.Collections.Generic.List<ConsumableEffectRuntime>(
                    ConsumableRuntimeRules.GetImpliedUtilityEffects(definition));
            }
            var processed = new System.Collections.Generic.HashSet<ConsumableEffectRuntime>();
            for (var runtimeIndex = 0; runtimeIndex < runtimeEffects.Count; runtimeIndex++)
            {
                var runtimeEffect = runtimeEffects[runtimeIndex];
                if (runtimeEffect == null || !processed.Add(runtimeEffect)) continue;
                var rawValue = 0f;
                var occurrenceCount = 0;
                var durationTurns = 0;
                foreach (var candidate in runtimeEffects)
                {
                    if (!ConsumableRuntimeRules.CanCombineEffects(runtimeEffect, candidate)) continue;
                    processed.Add(candidate);
                    rawValue += Mathf.Max(0f, candidate.Value);
                    occurrenceCount++;
                    durationTurns = Mathf.Max(durationTurns, candidate.DurationTurns);
                }
                var combinedEffect = new ConsumableEffectRuntime
                {
                    EffectType = runtimeEffect.EffectType,
                    Value = rawValue,
                    DurationTurns = durationTurns,
                    Chance = runtimeEffect.Chance,
                    TargetModeIndex = runtimeEffect.TargetModeIndex,
                    StatusId = runtimeEffect.StatusId,
                    ResistanceType = runtimeEffect.ResistanceType,
                    TargetType = runtimeEffect.TargetType,
                    TargetingType = runtimeEffect.TargetingType
                };
                if (!TryApplyExplorationEffect(
                        character,
                        combinedEffect,
                        occurrenceCount,
                        item.CatalystEffectMultiplier,
                        item.CatalystDurationModifier,
                        definition.UseType,
                        passiveTicksActive,
                        runtimeIndex,
                        item.InstanceId,
                        out var appliedEffect)) continue;
                effect = effect == ExplorationConsumableEffect.None
                    ? appliedEffect
                    : effect;
                applied = true;
            }
            if (!applied) { effect = ExplorationConsumableEffect.None; return false; }

            // Keep the source name after the last potion charge leaves the inventory.
            var sourceId = $"consumable.{item.InstanceId}";
            foreach (var active in character.PassiveEffects)
            {
                if (active?.EffectId == null || !active.EffectId.StartsWith(sourceId + ".", System.StringComparison.Ordinal)) continue;
                active.DisplayName = item.DisplayName;
                if (active.Sources == null || active.Sources.Count == 0)
                    active.Sources = new System.Collections.Generic.List<PassiveEffectSourceRuntime>
                    {
                        new() { SourceType = PassiveEffectSourceType.Consumable,
                            SourceId = sourceId, SourceCharacterId = character.CharacterId }
                    };
            }

            // Returning to Arungard is a two-phase use: the scene transition must
            // be accepted before the one-use scroll is consumed. Direct service
            // callers can inspect the effect and confirm it themselves; the UI
            // supplies the transition callback for the atomic flow.
            if (effect == ExplorationConsumableEffect.ReturnToArungard)
            {
                if (returnTransition == null)
                {
                    return true;
                }

                if (!returnTransition())
                {
                    effect = ExplorationConsumableEffect.None;
                    return false;
                }
            }

            return ConsumeCharge(character, item);
        }


        /// <summary>Carried consumables are shared by player participants of this combat.</summary>
        public static IEnumerable<ConsumableItemInstance> GetCombatItems(
            CombatSession session)
        {
            if (session?.Units == null) yield break;
            var seen = new HashSet<ConsumableItemInstance>();
            foreach (var unit in session.Units)
            {
                if (unit?.IsPlayerSide != true || unit.Character?.Inventory?.Items == null) continue;
                foreach (var item in unit.Character.Inventory.Items)
                    if (item is ConsumableItemInstance consumable && IsCombatUsable(consumable) && seen.Add(consumable))
                        yield return consumable;
            }
        }

        public static CharacterInstance FindCombatOwner(CombatSession session,
            ConsumableItemInstance item)
        {
            if (session?.Units == null || item == null) return null;
            foreach (var unit in session.Units)
                if (unit?.IsPlayerSide == true && unit.Character?.Inventory?.Items?.Contains(item) == true)
                    return unit.Character;
            return null;
        }

        public static bool IsCombatUsable(ConsumableItemInstance item)
        {
            if (item?.ConsumableDefinition == null || !item.HasCharges || item.IsFood) return false;
            if (item.RuntimeEffects == null || item.RuntimeEffects.Count == 0)
                return ConsumableRuntimeRules.TryGetImpliedUtilityEffect(item.ConsumableDefinition, out var implied)
                    && implied.EffectType != ConsumableRuntimeEffectType.ReturnToArungard;
            foreach (var effect in item.RuntimeEffects)
                if (effect != null && effect.EffectType != ConsumableRuntimeEffectType.ReturnToArungard) return true;
            return false;
        }

        public static string CombatInventorySignature(CombatSession session)
        {
            var result = new StringBuilder();
            if (session?.Units == null) return string.Empty;
            foreach (var unit in session.Units)
            {
                if (unit?.IsPlayerSide != true || unit.Character?.Inventory?.Items == null) continue;
                result.Append('|').Append(unit.Character.CharacterId);
                foreach (var item in unit.Character.Inventory.Items)
                    if (item is ConsumableItemInstance consumable)
                        result.Append(':').Append(consumable.InstanceId).Append('=').Append(consumable.CurrentCharges);
            }
            return result.ToString();
        }

        public static bool CanUseInCombat(CombatSession session,
            BattleUnit actor, ConsumableItemInstance item) =>
            session?.Outcome == CombatOutcomeState.Ongoing && actor != null && actor == session.ActiveUnit
            && actor.IsPlayerControlled && session.Units?.Contains(actor) == true
            && actor.Character?.LifeState == CharacterLifeState.Alive
            && IsCombatUsable(item) && FindCombatOwner(session, item) != null
            && !actor.ConsumableUseTypesUsedThisTurn.Contains(item.ConsumableDefinition.UseType)
            && actor.CurrentActionPoints >= CombatActionCostCalculator.CalculatePercentageCost(
                actor.MaxActionPoints, ConsumableRuntimeActionBuilder.ActionCostPercent);

        /// <summary>Revalidate before effects; charge the real carrier only after a successful action.</summary>
        public bool TryExecuteInCombat(CombatSession session,
            BattleUnit actor, ConsumableItemInstance item, System.Func<bool> execute)
        {
            if (execute == null || !CanUseInCombat(session, actor, item)) return false;
            var owner = FindCombatOwner(session, item);
            if (!execute()) return false;
            if (!TrySpendCharge(owner, item)) return false;
            // The action may end combat or change the active turn. Its original caster owns the limit.
            actor.ConsumableUseTypesUsedThisTurn.Add(item.ConsumableDefinition.UseType);
            return true;
        }

        public bool TrySpendCharge(CharacterInstance character, ConsumableItemInstance item)
        {
            if (character?.Inventory?.Items == null || item == null || !item.HasCharges || !character.Inventory.Items.Contains(item)) return false;
            item.CurrentCharges--;
            return item.CurrentCharges > 0 || inventoryService.TryRemoveItem(character.Inventory, item.InstanceId, out _);
        }

        private bool ConsumeCharge(CharacterInstance character, ConsumableItemInstance item)
        {
            if (character?.Inventory?.Items == null || item == null) return false;

            item.CurrentCharges = Mathf.Max(0, item.CurrentCharges - 1);
            return item.CurrentCharges > 0
                || inventoryService.TryRemoveItem(
                    character.Inventory,
                    item.InstanceId,
                    out _);
        }

        private bool TryApplyExplorationEffect(
            CharacterInstance character,
            ConsumableEffectRuntime runtimeEffect,
            int occurrenceCount,
            float catalystEffectMultiplier,
            int catalystDurationModifier,
            ConsumableUseType useType,
            bool passiveTicksActive,
            int runtimeIndex,
            string sourceItemId,
            out ExplorationConsumableEffect result)
        {
            result = ExplorationConsumableEffect.None;
            var effectiveValue = runtimeEffect.Value
                * ConsumableRuntimeRules.GetDuplicateMultiplier(occurrenceCount)
                * Mathf.Max(0f, catalystEffectMultiplier);
            switch (runtimeEffect.EffectType)
            {
                case ConsumableRuntimeEffectType.RestoreHealth:
                    var maxHealth = Mathf.Max(0, Mathf.RoundToInt(character.Stats?.MaxHealth.GetValue() ?? 0f));
                    if (character.CurrentHealth >= maxHealth) return false;
                    character.CurrentHealth = Mathf.Min(maxHealth, character.CurrentHealth + Mathf.CeilToInt(maxHealth * effectiveValue));
                    result = ExplorationConsumableEffect.RestoreHealthPercent;
                    return true;
                case ConsumableRuntimeEffectType.RestoreResource:
                    var previousResource = character.ResourcePool?.CurrentValue ?? 0;
                    if (character.ResourcePool == null
                        || character.ResourcePool.Restore(
                            Mathf.CeilToInt(character.ResourcePool.MaxValue * effectiveValue)) <= 0)
                        return false;
                    Godmist.Systems.Combat.MomentumResourceRule.RefreshSpeedAfterChange(character, previousResource);
                    result = ExplorationConsumableEffect.RestoreResourcePercent;
                    return true;
                case ConsumableRuntimeEffectType.RegenHealth:
                case ConsumableRuntimeEffectType.RegenResource:
                    // The city has no exploration tick runner. Resolve the same
                    // total there immediately so using a potion never wastes it.
                    if (!passiveTicksActive)
                    {
                        if (runtimeEffect.EffectType == ConsumableRuntimeEffectType.RegenHealth)
                        {
                            var maximum = Mathf.Max(0, Mathf.RoundToInt(character.Stats?.MaxHealth.GetValue() ?? 0f));
                            if (character.CurrentHealth >= maximum) return false;
                            character.CurrentHealth = Mathf.Min(maximum,
                                character.CurrentHealth + Mathf.CeilToInt(maximum * effectiveValue));
                            result = ExplorationConsumableEffect.RestoreHealthPercent;
                            return true;
                        }
                        var previousPeriodicResource = character.ResourcePool?.CurrentValue ?? 0;
                        if (character.ResourcePool == null || character.ResourcePool.Restore(
                            Mathf.CeilToInt(character.ResourcePool.MaxValue * effectiveValue)) <= 0)
                            return false;
                        Godmist.Systems.Combat.MomentumResourceRule.RefreshSpeedAfterChange(character, previousPeriodicResource);
                        result = ExplorationConsumableEffect.RestoreResourcePercent;
                        return true;
                    }
                    var duration = Mathf.Max(1, ConsumableRuntimeRules.GetDuration(
                        useType, runtimeEffect.EffectType, runtimeEffect.DurationTurns)
                        + catalystDurationModifier);
                    var recovery = new PassiveEffectRuntime(
                        $"consumable.{sourceItemId}.periodic_recovery.{runtimeIndex}",
                        new System.Collections.Generic.List<PassiveEffectSourceRuntime>(),
                        1, duration, 0, PassiveEffectLifetime.Timed,
                        PassiveEffectTickPhase.TurnEnd)
                    {
                        DisplayName = "Regeneracja"
                    };
                    recovery.SetPayload(new PeriodicRecoveryPayload
                    {
                        HealthPercent = runtimeEffect.EffectType == ConsumableRuntimeEffectType.RegenHealth
                            ? effectiveValue / duration : 0f,
                        ResourcePercent = runtimeEffect.EffectType == ConsumableRuntimeEffectType.RegenResource
                            ? effectiveValue / duration : 0f
                    });
                    if (!passiveEffectLifecycle.TryApplyTimedEffect(character, recovery,
                        PassiveEffectStackingMode.ReplaceDurationAndValues, out _)) return false;
                    result = ExplorationConsumableEffect.ApplyModifier;
                    return true;
                case ConsumableRuntimeEffectType.RemoveStatus:
                    if (string.IsNullOrWhiteSpace(runtimeEffect.StatusId)
                        || !passiveEffectLifecycle.RemoveEffect(character, runtimeEffect.StatusId))
                        return false;
                    result = runtimeEffect.StatusId switch
                    {
                        StatusEffectIds.Bleed => ExplorationConsumableEffect.RemoveBleed,
                        StatusEffectIds.Poison => ExplorationConsumableEffect.RemovePoison,
                        StatusEffectIds.Burn => ExplorationConsumableEffect.RemoveBurn,
                        _ => ExplorationConsumableEffect.None
                    };
                    return result != ExplorationConsumableEffect.None;
                case ConsumableRuntimeEffectType.ReturnToArungard:
                    result = ExplorationConsumableEffect.ReturnToArungard;
                    return true;
                case ConsumableRuntimeEffectType.ModifyAllResistances:
                    var allApplied = false;
                    var resistanceIndex = 0;
                    foreach (ResistanceType resistanceType in System.Enum.GetValues(typeof(ResistanceType)))
                    {
                        var typedEffect = new ConsumableEffectRuntime
                        {
                            EffectType = ConsumableRuntimeEffectType.ModifyResistance,
                            Value = runtimeEffect.Value,
                            DurationTurns = runtimeEffect.DurationTurns,
                            ResistanceType = resistanceType
                        };
                        allApplied |= TryApplyExplorationModifier(
                            character, typedEffect, occurrenceCount, catalystEffectMultiplier,
                            catalystDurationModifier, useType,
                            runtimeIndex * 10 + resistanceIndex++, sourceItemId);
                    }
                    if (!allApplied) return false;
                    result = ExplorationConsumableEffect.ApplyModifier;
                    return true;
                default:
                    if (!TryApplyExplorationModifier(
                        character,
                        runtimeEffect,
                        occurrenceCount,
                        catalystEffectMultiplier,
                        catalystDurationModifier,
                        useType,
                        runtimeIndex,
                        sourceItemId)) return false;
                    result = ExplorationConsumableEffect.ApplyModifier;
                    return true;
            }
        }

        private bool TryApplyExplorationModifier(
            CharacterInstance character,
            ConsumableEffectRuntime runtimeEffect,
            int occurrenceCount,
            float catalystEffectMultiplier,
            int catalystDurationModifier,
            ConsumableUseType useType,
            int runtimeIndex,
            string sourceItemId)
        {
            if (character == null || runtimeEffect == null) return false;
            var value = runtimeEffect.Value
                * ConsumableRuntimeRules.GetDuplicateMultiplier(occurrenceCount)
                * Mathf.Max(0f, catalystEffectMultiplier);
            var baseDuration = ConsumableRuntimeRules.GetDuration(
                useType, runtimeEffect.EffectType, runtimeEffect.DurationTurns);
            var duration = Mathf.Max(1, baseDuration + catalystDurationModifier);
            value *= ConsumableRuntimeRules.GetCondensationMultiplier(
                runtimeEffect.EffectType, baseDuration, duration);
            var effect = new PassiveEffectRuntime(
                $"consumable.{sourceItemId}.{runtimeEffect.EffectType}.{runtimeIndex}",
                new System.Collections.Generic.List<PassiveEffectSourceRuntime>(),
                1,
                duration,
                0,
                PassiveEffectLifetime.Timed,
                PassiveEffectTickPhase.None)
            {
                DisplayName = runtimeEffect.EffectType.ToString()
            };

            switch (runtimeEffect.EffectType)
            {
                case ConsumableRuntimeEffectType.ModifyMaxResource:
                case ConsumableRuntimeEffectType.ModifyPhysicalDefense:
                case ConsumableRuntimeEffectType.ModifyMagicalDefense:
                case ConsumableRuntimeEffectType.ModifyDefense:
                case ConsumableRuntimeEffectType.ModifySpeed:
                case ConsumableRuntimeEffectType.ModifyCritChance:
                case ConsumableRuntimeEffectType.ModifyDodge:
                    effect.SetPayload(new StatModifiersPayload
                    {
                        Modifiers = new System.Collections.Generic.List<StatModifier>
                        {
                            new()
                            {
                                StatType = runtimeEffect.EffectType switch
                                {
                                    ConsumableRuntimeEffectType.ModifyMaxResource => CharacterStatType.MaxResource,
                                    ConsumableRuntimeEffectType.ModifyPhysicalDefense => CharacterStatType.PhysicalDefense,
                                    ConsumableRuntimeEffectType.ModifyMagicalDefense => CharacterStatType.MagicalDefense,
                                    ConsumableRuntimeEffectType.ModifyDefense => CharacterStatType.PhysicalDefense,
                                    ConsumableRuntimeEffectType.ModifySpeed => CharacterStatType.Speed,
                                    ConsumableRuntimeEffectType.ModifyCritChance => CharacterStatType.CritChance,
                                    _ => CharacterStatType.Dodge
                                },
                                Operation = runtimeEffect.EffectType == ConsumableRuntimeEffectType.ModifyDodge
                                    ? StatModifierOperation.FlatAbsolute
                                    : StatModifierOperation.Percent,
                                Stacking = StatModifierStacking.Additive,
                                Value = value
                            }
                        }
                    });
                    if (runtimeEffect.EffectType == ConsumableRuntimeEffectType.ModifyDefense)
                    {
                        effect.GetPayload<StatModifiersPayload>().Modifiers.Add(new StatModifier
                        {
                            StatType = CharacterStatType.MagicalDefense,
                            Operation = StatModifierOperation.Percent,
                            Stacking = StatModifierStacking.Additive,
                            Value = value
                        });
                    }
                    break;
                case ConsumableRuntimeEffectType.ModifyDamage:
                    effect.SetPayload(new VariableModifiersPayload
                    {
                        Modifiers = new System.Collections.Generic.List<GameplayVariableModifier>
                        {
                            new()
                            {
                                VariableType = GameplayVariableType.DamageDealtPercent,
                                Stacking = StatModifierStacking.Additive,
                                Value = value
                            }
                        }
                    });
                    break;
                case ConsumableRuntimeEffectType.ModifyResistance:
                    effect.SetPayload(new ResistanceModifierPayload
                    {
                        ResistanceType = runtimeEffect.ResistanceType,
                        Value = value
                    });
                    break;
                default:
                    return false;
            }

            return passiveEffectLifecycle.TryApplyTimedEffect(
                character,
                effect,
                PassiveEffectStackingMode.ReplaceDurationAndValues,
                out _);
        }

    }
}
