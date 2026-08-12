using System;
using System.Collections.Generic;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
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

            incomingEffect.Stacks = Math.Max(1, incomingEffect.Stacks);

            var existingEffect = FindEffect(
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
            var existingEffectHadStatModifiers = HasStatModifiers(existingEffect);

            switch (stackingMode)
            {
                case PassiveEffectStackingMode.ReplaceDurationAndAddTotalDamage:
                    existingEffect.RemainingDuration =
                        incomingEffect.RemainingDuration;
                    existingEffect.TotalDamage = AddSafely(
                        existingEffect.TotalDamage,
                        incomingEffect.TotalDamage);
                    existingEffect.Stacks = 1;
                    break;

                case PassiveEffectStackingMode.ReplaceDuration:
                    existingEffect.RemainingDuration =
                        incomingEffect.RemainingDuration;
                    existingEffect.StatusHealPercent =
                        incomingEffect.StatusHealPercent;
                    break;

                case PassiveEffectStackingMode.ReplaceDurationAndValues:
                    existingEffect.RemainingDuration =
                        incomingEffect.RemainingDuration;

                    existingEffect.Stacks = incomingEffect.Stacks;
                    existingEffect.TotalDamage = incomingEffect.TotalDamage;
                    existingEffect.Sources = CopySources(incomingEffect.Sources);
                    existingEffect.StatModifiers = CopyStatModifiers(incomingEffect.StatModifiers);
                    existingEffect.VariableModifiers = CopyVariableModifiers(incomingEffect.VariableModifiers);
                    break;

                default:
                    existingEffect.RemainingDuration = Math.Max(
                        existingEffect.RemainingDuration,
                        incomingEffect.RemainingDuration);

                    existingEffect.Stacks = Math.Max(
                        existingEffect.Stacks,
                        incomingEffect.Stacks);

                    existingEffect.TotalDamage = Math.Max(
                        existingEffect.TotalDamage,
                        incomingEffect.TotalDamage);
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

        private static List<StatModifier> CopyStatModifiers(
            List<StatModifier> modifiers)
        {
            var copiedModifiers = new List<StatModifier>();

            if (modifiers == null)
            {
                return copiedModifiers;
            }

            foreach (var modifier in modifiers)
            {
                if (modifier == null)
                {
                    continue;
                }

                copiedModifiers.Add(new StatModifier
                {
                    StatType = modifier.StatType,
                    Operation = modifier.Operation,
                    Stacking = modifier.Stacking,
                    Value = modifier.Value
                });
            }

            return copiedModifiers;
        }

        private static List<GameplayVariableModifier> CopyVariableModifiers(
            List<GameplayVariableModifier> modifiers)
        {
            var copiedModifiers = new List<GameplayVariableModifier>();

            if (modifiers == null)
            {
                return copiedModifiers;
            }

            foreach (var modifier in modifiers)
            {
                if (modifier == null)
                {
                    continue;
                }

                copiedModifiers.Add(new GameplayVariableModifier
                {
                    VariableType = modifier.VariableType,
                    Stacking = modifier.Stacking,
                    Value = modifier.Value
                });
            }

            return copiedModifiers;
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
            return effect?.StatModifiers != null
                && effect.StatModifiers.Count > 0;
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
