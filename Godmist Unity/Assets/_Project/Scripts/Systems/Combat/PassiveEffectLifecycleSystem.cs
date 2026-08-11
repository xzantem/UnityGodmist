using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;

namespace Godmist.Systems.Combat
{
    public class PassiveEffectLifecycleSystem
    {
        private readonly Action<string> battleLog;
        private readonly IReadOnlyList<IPassiveEffectTurnProcessor> turnProcessors;

        public PassiveEffectLifecycleSystem(
            Action<string> battleLog = null,
            IReadOnlyList<IPassiveEffectTurnProcessor> turnProcessors = null)
        {
            this.battleLog = battleLog;
            this.turnProcessors = turnProcessors
                ?? new List<IPassiveEffectTurnProcessor>();
        }

        public bool TryApplyTimedEffect(
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

            incomingEffect.IsTimed = true;
            incomingEffect.RemainingDuration = Math.Max(
                1,
                incomingEffect.RemainingDuration);

            incomingEffect.Stacks = Math.Max(1, incomingEffect.Stacks);

            var existingEffect = FindEffect(
                character.PassiveEffects,
                incomingEffect.EffectId);

            if (existingEffect == null)
            {
                character.PassiveEffects.Add(incomingEffect);
                appliedEffect = incomingEffect;
                return true;
            }

            existingEffect.IsTimed = true;
            existingEffect.TickPhase = incomingEffect.TickPhase;

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

            AddSources(
                existingEffect.SourceEntityIds,
                incomingEffect.SourceEntityIds);

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
                || !effect.IsTimed)
            {
                return false;
            }

            effect.RemainingDuration--;

            if (effect.RemainingDuration > 0)
            {
                return false;
            }

            return character.PassiveEffects.Remove(effect);
        }

        public int RemoveExpiredEffects(CharacterInstance character)
        {
            if (character?.PassiveEffects == null)
            {
                return 0;
            }

            var removedCount = 0;

            for (var index = character.PassiveEffects.Count - 1; index >= 0; index--)
            {
                var effect = character.PassiveEffects[index];

                if (effect == null || HasExpired(effect))
                {
                    character.PassiveEffects.RemoveAt(index);
                    removedCount++;
                }
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

                character.PassiveEffects.RemoveAt(index);
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
                && (!effect.IsTimed || effect.RemainingDuration > 0);
        }

        public static bool HasExpired(PassiveEffectRuntime effect)
        {
            return effect != null
                && effect.IsTimed
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
            List<string> targetSourceIds,
            List<string> incomingSourceIds)
        {
            if (targetSourceIds == null || incomingSourceIds == null)
            {
                return;
            }

            foreach (var sourceId in incomingSourceIds)
            {
                if (string.IsNullOrWhiteSpace(sourceId)
                    || targetSourceIds.Contains(sourceId))
                {
                    continue;
                }

                targetSourceIds.Add(sourceId);
            }
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