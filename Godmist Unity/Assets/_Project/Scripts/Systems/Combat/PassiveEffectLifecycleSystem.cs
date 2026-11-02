using System;
using System.Collections.Generic;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Effects;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Characters;

namespace Godmist.Systems.Combat
{
    public class PassiveEffectLifecycleSystem
    {
        private readonly Action<string> battleLog;
        private readonly IReadOnlyList<IPassiveEffectTurnProcessor> turnProcessors;
        private readonly CharacterStatPipelineSystem statPipeline;

        public PassiveEffectLifecycleSystem(
            Action<string> battleLog = null,
            IReadOnlyList<IPassiveEffectTurnProcessor> turnProcessors = null,
            CharacterStatPipelineSystem statPipeline = null)
        {
            this.battleLog = battleLog;
            this.turnProcessors = turnProcessors
                ?? new List<IPassiveEffectTurnProcessor>();

            this.statPipeline = statPipeline ?? new CharacterStatPipelineSystem();
        }

        public bool TryApplyTimedEffect(
            CharacterInstance character,
            PassiveEffectRuntime incomingEffect,
            PassiveEffectStackingMode stackingMode,
            out PassiveEffectRuntime appliedEffect)
        {
            appliedEffect = null;

            if (incomingEffect == null)
            {
                return false;
            }

            incomingEffect.Lifetime = PassiveEffectLifetime.Timed;
            incomingEffect.RemainingDuration = Math.Max(1, incomingEffect.RemainingDuration);

            return TryApplyEffect(
                character,
                incomingEffect,
                stackingMode,
                out appliedEffect);
        }

        public bool TryApplyCombatScopedEffect(
            CharacterInstance character,
            PassiveEffectRuntime incomingEffect,
            PassiveEffectStackingMode stackingMode,
            out PassiveEffectRuntime appliedEffect)
        {
            appliedEffect = null;

            if (incomingEffect == null)
            {
                return false;
            }

            incomingEffect.Lifetime = PassiveEffectLifetime.CombatScoped;
            incomingEffect.RemainingDuration = 0;
            incomingEffect.TickPhase = PassiveEffectTickPhase.None;

            return TryApplyEffect(
                character,
                incomingEffect,
                stackingMode,
                out appliedEffect);
        }

        public bool TryApplyPersistentEffect(
            CharacterInstance character,
            PassiveEffectRuntime incomingEffect,
            PassiveEffectStackingMode stackingMode,
            out PassiveEffectRuntime appliedEffect)
        {
            appliedEffect = null;

            if (incomingEffect == null)
            {
                return false;
            }

            incomingEffect.Lifetime = PassiveEffectLifetime.Persistent;
            incomingEffect.RemainingDuration = 0;
            incomingEffect.TickPhase = PassiveEffectTickPhase.None;

            return TryApplyEffect(
                character,
                incomingEffect,
                stackingMode,
                out appliedEffect);
        }

        public bool TryApplyEffectForExecution(
            CharacterInstance character,
            PassiveEffectRuntime incomingEffect,
            PassiveEffectStackingMode stackingMode)
        {
            if (incomingEffect?.Lifetime == PassiveEffectLifetime.Timed)
            {
                return TryApplyTimedEffect(
                    character,
                    incomingEffect,
                    stackingMode,
                    out _);
            }

            return TryApplyCombatScopedEffect(
                character,
                incomingEffect,
                stackingMode,
                out _);
        }

        private bool TryApplyEffect(
            CharacterInstance character,
            PassiveEffectRuntime incomingEffect,
            PassiveEffectStackingMode stackingMode,
            out PassiveEffectRuntime appliedEffect)
        {
            appliedEffect = null;

            if (character?.PassiveEffects == null
                || incomingEffect == null
                || string.IsNullOrWhiteSpace(incomingEffect.EffectId))
            {
                return false;
            }

            StatusEffectSystem.NormalizeStatus(incomingEffect);
            incomingEffect.Stacks = Math.Max(1, incomingEffect.Stacks);

            // Fire generations must retain independent lifetimes, damage and first-tick state.
            var existingEffect = incomingEffect.GetStateValue("mage.burn.generation") != null ? null : FindEffect(
                character.PassiveEffects,
                incomingEffect.EffectId);

            if (existingEffect == null)
            {
                character.PassiveEffects.Add(incomingEffect);
                if (HasStatModifiers(incomingEffect)) RefreshStats(character);

                appliedEffect = incomingEffect;
                return true;
            }

            existingEffect.Lifetime = incomingEffect.Lifetime;
            existingEffect.TickPhase = incomingEffect.TickPhase;
            if (!string.IsNullOrWhiteSpace(incomingEffect.DisplayName))
            {
                existingEffect.DisplayName = incomingEffect.DisplayName;
            }
            var existingEffectHadStatModifiers = HasStatModifiers(existingEffect);

            switch (stackingMode)
            {
                case PassiveEffectStackingMode.ReplaceDurationAndAddTotalDamage:
                    existingEffect.RemainingDuration =
                        incomingEffect.RemainingDuration;
                    MergeDamageOverTimePayload(
                        existingEffect,
                        incomingEffect,
                        add: true);
                    existingEffect.Stacks = 1;
                    break;

                case PassiveEffectStackingMode.ReplaceDuration:
                    existingEffect.RemainingDuration =
                        incomingEffect.RemainingDuration;
                    ReplaceSleepHealingPayload(existingEffect, incomingEffect);
                    break;

                case PassiveEffectStackingMode.ReplaceDurationAndValues:
                    existingEffect.RemainingDuration =
                        incomingEffect.RemainingDuration;

                    existingEffect.Stacks = incomingEffect.Stacks;
                    existingEffect.Sources = CopySources(incomingEffect.Sources);
                    existingEffect.Payloads = CopyPayloads(incomingEffect.Payloads);
                    break;

                default:
                    existingEffect.RemainingDuration = Math.Max(
                        existingEffect.RemainingDuration,
                        incomingEffect.RemainingDuration);

                    existingEffect.Stacks = Math.Max(
                        existingEffect.Stacks,
                        incomingEffect.Stacks);

                    MergeDamageOverTimePayload(
                        existingEffect,
                        incomingEffect,
                        add: false);
                    break;
            }

            if (stackingMode != PassiveEffectStackingMode.ReplaceDurationAndValues)
            {
                AddSources(existingEffect.Sources, incomingEffect.Sources);
            }

            if (existingEffectHadStatModifiers || HasStatModifiers(existingEffect))
                RefreshStats(character);

            appliedEffect = existingEffect;
            return true;
        }

        private static void MergeDamageOverTimePayload(
            PassiveEffectRuntime existingEffect,
            PassiveEffectRuntime incomingEffect,
            bool add)
        {
            var incoming = incomingEffect.GetPayload<DamageOverTimePayload>();
            if (incoming == null) return;

            var existing = existingEffect.GetPayload<DamageOverTimePayload>();
            if (existing == null)
            {
                existingEffect.SetPayload(new DamageOverTimePayload
                {
                    RemainingTotalDamage = incoming.RemainingTotalDamage,
                    DamagePerTurn = incoming.DamagePerTurn
                });
                return;
            }

            existing.RemainingTotalDamage = add
                ? AddSafely(
                    existing.RemainingTotalDamage,
                    incoming.RemainingTotalDamage)
                : Math.Max(
                    existing.RemainingTotalDamage,
                    incoming.RemainingTotalDamage);
            if (incoming.DamagePerTurn > 0)
            {
                existing.DamagePerTurn = incoming.DamagePerTurn;
            }
        }

        private static void ReplaceSleepHealingPayload(
            PassiveEffectRuntime existingEffect,
            PassiveEffectRuntime incomingEffect)
        {
            var incoming = incomingEffect.GetPayload<SleepHealingPayload>();
            if (incoming == null) return;

            existingEffect.SetPayload(new SleepHealingPayload
            {
                MaxHealthPercent = incoming.MaxHealthPercent
            });
        }

        private static List<PassiveEffectPayloadRuntime> CopyPayloads(List<PassiveEffectPayloadRuntime> payloads)
        {
            var result = new List<PassiveEffectPayloadRuntime>();
            foreach (var payload in payloads ?? new List<PassiveEffectPayloadRuntime>())
                if (payload != null) result.Add(PassiveEffectPayloadCodec.Clone(payload));
            return result;
        }

        public bool ProcessTurnStart(CombatSession session, BattleUnit unit)
        {
            return ProcessPhase(
                session,
                unit,
                PassiveEffectTickPhase.TurnStart);
        }

        public bool ProcessTurnEnd(BattleUnit unit)
        {
            return ProcessPhase(
                null,
                unit,
                PassiveEffectTickPhase.TurnEnd);
        }

        // A skipped downed turn advances clocks, not processors or combat triggers.
        // Consume the missed DoT budget as well, so revival cannot concentrate it
        // into the remaining ticks. Persistent/combat-scoped effects stay intact.
        public void ProcessDownedTurn(BattleUnit unit)
        {
            if (unit?.Character?.LifeState != CharacterLifeState.Downed
                || unit.Character.PassiveEffects == null) return;

            var character = unit.Character;
            RemoveExpiredEffects(character);
            foreach (var effect in new List<PassiveEffectRuntime>(character.PassiveEffects))
            {
                if (!IsActive(effect) || effect.Lifetime != PassiveEffectLifetime.Timed) continue;

                var damage = effect.GetPayload<DamageOverTimePayload>();
                if (damage != null)
                {
                    var missedTick = (int)Math.Ceiling(damage.RemainingTotalDamage
                        / (double)Math.Max(1, effect.RemainingDuration));
                    damage.RemainingTotalDamage = Math.Max(0, damage.RemainingTotalDamage - missedTick);
                }

                if (AdvanceDurationAndRemoveIfExpired(character, effect)) LogExpiration(unit, effect);
            }
        }

        public IReadOnlyList<PassiveEffectRuntime> GetEffectsForTick(
            CharacterInstance character,
            PassiveEffectTickPhase tickPhase)
        {
            var effectsForTick = new List<PassiveEffectRuntime>();

            if (character?.PassiveEffects == null
                || tickPhase == PassiveEffectTickPhase.None)
            {
                return effectsForTick;
            }

            foreach (var effect in character.PassiveEffects)
            {
                if (!IsActive(effect) || effect.TickPhase != tickPhase)
                {
                    continue;
                }

                effectsForTick.Add(effect);
            }

            return effectsForTick;
        }

        public bool AdvanceDurationAndRemoveIfExpired(
            CharacterInstance character,
            PassiveEffectRuntime effect)
        {
            if (character?.PassiveEffects == null
                || effect == null
                || effect.Lifetime != PassiveEffectLifetime.Timed)
            {
                return false;
            }

            effect.RemainingDuration--;

            if (effect.RemainingDuration > 0)
            {
                return false;
            }

            var wasRemoved = character.PassiveEffects.Remove(effect);

            if (wasRemoved && HasStatModifiers(effect))
            {
                RefreshStats(character);
            }

            return wasRemoved;
        }

        public int RemoveExpiredEffects(CharacterInstance character)
        {
            if (character?.PassiveEffects == null)
            {
                return 0;
            }

            var removedCount = 0;

            var removedStatModifiers = false;

            for (var index = character.PassiveEffects.Count - 1; index >= 0; index--)
            {
                var effect = character.PassiveEffects[index];

                if (effect == null || HasExpired(effect))
                {
                    removedStatModifiers |= HasStatModifiers(effect);
                    character.PassiveEffects.RemoveAt(index);
                    removedCount++;
                }
            }

            if (removedStatModifiers)
            {
                RefreshStats(character);
            }

            return removedCount;
        }

        public bool RemoveEffect(CharacterInstance character, string effectId)
        {
            if (character?.PassiveEffects == null
                || string.IsNullOrWhiteSpace(effectId))
            {
                return false;
            }

            for (var index = character.PassiveEffects.Count - 1; index >= 0; index--)
            {
                var effect = character.PassiveEffects[index];

                if (effect == null)
                {
                    character.PassiveEffects.RemoveAt(index);
                    continue;
                }

                if (!string.Equals(
                        effect.EffectId,
                        effectId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var removedEffectHadStatModifiers = HasStatModifiers(effect);
                character.PassiveEffects.RemoveAt(index);
                if (removedEffectHadStatModifiers)
                    RefreshStats(character);
                return true;
            }

            return false;
        }

        public static bool HasActiveEffect(
            CharacterInstance character,
            string effectId)
        {
            if (character?.PassiveEffects == null
                || string.IsNullOrWhiteSpace(effectId))
            {
                return false;
            }

            foreach (var effect in character.PassiveEffects)
            {
                if (IsActive(effect)
                    && string.Equals(
                        effect.EffectId,
                        effectId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsActive(PassiveEffectRuntime effect)
        {
            return effect != null
                && (effect.Lifetime != PassiveEffectLifetime.Timed
                    || effect.RemainingDuration > 0);
        }
        public static bool HasExpired(PassiveEffectRuntime effect)
        {
            return effect != null
                && effect.Lifetime == PassiveEffectLifetime.Timed
                && effect.RemainingDuration <= 0;
        }

        private bool ProcessPhase(
            CombatSession session,
            BattleUnit unit,
            PassiveEffectTickPhase tickPhase)
        {
            if (unit?.Character == null
                || unit.Character.LifeState != CharacterLifeState.Alive)
            {
                return false;
            }

            RemoveExpiredEffects(unit.Character);

            if (StatusEffectSystem.IsParalyzed(unit))
            {
                if (tickPhase == PassiveEffectTickPhase.TurnEnd)
                {
                    AdvanceParalysisDuration(unit);
                }

                return true;
            }

            var effects = GetEffectsForTick(unit.Character, tickPhase);

            foreach (var effect in effects)
            {
                var shouldContinue = true;
                var shouldExpire = false;

                var processor = FindProcessor(effect, tickPhase);

                if (processor != null)
                {
                    shouldContinue = processor.ProcessTurn(
                        session,
                        unit,
                        effect,
                        out shouldExpire);
                }

                var expiredByDuration =
                    AdvanceDurationAndRemoveIfExpired(unit.Character, effect);

                if (shouldExpire && unit.Character.PassiveEffects.Remove(effect))
                {
                    LogExpiration(unit, effect);
                }
                else if (expiredByDuration)
                {
                    LogExpiration(unit, effect);
                }

                if (!shouldContinue)
                {
                    return false;
                }
            }

            return true;
        }

        private void AdvanceParalysisDuration(BattleUnit unit)
        {
            var paralysis = unit.Character.PassiveEffects.Find(effect =>
                IsActive(effect)
                && string.Equals(
                    effect.EffectId,
                    StatusEffectIds.Paralysis,
                    StringComparison.OrdinalIgnoreCase));

            if (paralysis == null)
            {
                return;
            }

            if (AdvanceDurationAndRemoveIfExpired(unit.Character, paralysis))
            {
                LogExpiration(unit, paralysis);
            }
        }

        private IPassiveEffectTurnProcessor FindProcessor(
            PassiveEffectRuntime effect,
            PassiveEffectTickPhase tickPhase)
        {
            foreach (var processor in turnProcessors)
            {
                if (processor != null && processor.CanProcess(effect, tickPhase))
                {
                    return processor;
                }
            }

            return null;
        }

        public int RemoveCombatScopedEffects(CombatSession session)
        {
            if (session?.Units == null)
            {
                return 0;
            }

            var removedCount = 0;

            foreach (var unit in session.Units)
            {
                if (unit?.Character == null)
                {
                    continue;
                }

                removedCount += RemoveCombatScopedEffects(unit.Character);
            }

            return removedCount;
        }

        public int RemoveCombatScopedEffects(CharacterInstance character)
        {
            if (character?.PassiveEffects == null)
            {
                return 0;
            }

            var removedCount = 0;

            var removedStatModifiers = false;

            for (var index = character.PassiveEffects.Count - 1; index >= 0; index--)
            {
                var effect = character.PassiveEffects[index];

                if (effect == null || effect.Lifetime != PassiveEffectLifetime.CombatScoped)
                {
                    continue;
                }
                removedStatModifiers |= HasStatModifiers(effect);
                character.PassiveEffects.RemoveAt(index);
                removedCount++;
            }

            if (removedStatModifiers)
            {
                RefreshStats(character);
            }

            return removedCount;
        }

        private static List<PassiveEffectSourceRuntime> CopySources(
            List<PassiveEffectSourceRuntime> sources)
        {
            var copiedSources = new List<PassiveEffectSourceRuntime>();

            if (sources == null)
            {
                return copiedSources;
            }

            foreach (var source in sources)
            {
                if (source == null)
                {
                    continue;
                }

                copiedSources.Add(new PassiveEffectSourceRuntime
                {
                    SourceType = source.SourceType,
                    SourceId = source.SourceId,
                    SourceCharacterId = source.SourceCharacterId
                });
            }

            return copiedSources;
        }

        private static PassiveEffectRuntime FindEffect(
            List<PassiveEffectRuntime> effects,
            string effectId)
        {
            foreach (var effect in effects)
            {
                if (effect != null
                    && string.Equals(
                        effect.EffectId,
                        effectId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return effect;
                }
            }

            return null;
        }

        private static int AddSafely(int first, int second)
        {
            if (second > int.MaxValue - first)
            {
                return int.MaxValue;
            }

            return first + second;
        }

        private static void AddSources(
            List<PassiveEffectSourceRuntime> targetSources,
            List<PassiveEffectSourceRuntime> incomingSources)
        {
            if (targetSources == null || incomingSources == null)
            {
                return;
            }

            foreach (var incomingSource in incomingSources)
            {
                if (incomingSource == null || HasSource(targetSources, incomingSource))
                {
                    continue;
                }

                targetSources.Add(new PassiveEffectSourceRuntime
                {
                    SourceType = incomingSource.SourceType,
                    SourceId = incomingSource.SourceId,
                    SourceCharacterId = incomingSource.SourceCharacterId
                });
            }
        }

        private static bool HasSource(
            List<PassiveEffectSourceRuntime> sources,
            PassiveEffectSourceRuntime candidate)
        {
            foreach (var source in sources)
            {
                if (source == null)
                {
                    continue;
                }

                if (source.SourceType == candidate.SourceType
                    && string.Equals(
                        source.SourceId,
                        candidate.SourceId,
                        StringComparison.OrdinalIgnoreCase)
                    && string.Equals(
                        source.SourceCharacterId,
                        candidate.SourceCharacterId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasStatModifiers(PassiveEffectRuntime effect)
        {
            return effect?.GetPayload<StatModifiersPayload>()?.Modifiers?.Count > 0
                || effect?.GetPayload<ResistanceModifierPayload>() != null;
        }

        private void RefreshStats(CharacterInstance character)
        {
            statPipeline?.Refresh(character);
        }

        private void LogExpiration(
            BattleUnit unit,
            PassiveEffectRuntime effect)
        {
            var displayName = unit?.Character?.Definition?.DisplayName
                ?? unit?.Character?.CharacterId
                ?? "jednostki";

            battleLog?.Invoke(
                $"Efekt {effect.EffectId} wygasa u {displayName}.");
        }
    }
}
