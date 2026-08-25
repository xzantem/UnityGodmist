using System;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public class DamageOverTimePassiveEffectProcessor : IPassiveEffectTurnProcessor
    {
        private readonly CombatDamageSystem damageSystem;

        public DamageOverTimePassiveEffectProcessor(CombatDamageSystem damageSystem)
        {
            this.damageSystem = damageSystem ?? new CombatDamageSystem();
        }

        public bool CanProcess(PassiveEffectRuntime effect, PassiveEffectTickPhase tickPhase)
        {
            return effect != null
                && tickPhase == PassiveEffectTickPhase.TurnStart
                && IsDamageOverTimeStatus(effect.EffectId);
        }

        public bool ProcessTurn(
            CombatSession session,
            BattleUnit unit,
            PassiveEffectRuntime effect,
            out bool shouldExpire)
        {
            shouldExpire = false;

            if (session == null || unit?.Character == null || effect == null
                || unit.Character.LifeState != CharacterLifeState.Alive)
            {
                return false;
            }

            var remainingTurns = Mathf.Max(1, effect.RemainingDuration);
            var rawDamage = Mathf.CeilToInt(effect.TotalDamage / (float)remainingTurns);

            effect.TotalDamage = Mathf.Max(0, effect.TotalDamage - rawDamage);

            var resistance = GetResistance(unit, effect.EffectId);
            var finalDamage = Mathf.Max(
                0,
                Mathf.RoundToInt(rawDamage * CalculateDamageMultiplier(resistance)));

            damageSystem.ApplyDamage(
                session,
                source: null,
                target: unit,
                requestedDamage: finalDamage,
                damageType: GetDamageType(effect.EffectId));

            shouldExpire = effect.TotalDamage <= 0;

            return unit.Character.LifeState == CharacterLifeState.Alive;
        }

        private static bool IsDamageOverTimeStatus(string statusId)
        {
            return string.Equals(statusId, StatusEffectIds.Bleed,
                       StringComparison.OrdinalIgnoreCase)
                || string.Equals(statusId, StatusEffectIds.Poison,
                       StringComparison.OrdinalIgnoreCase)
                || string.Equals(statusId, StatusEffectIds.Burn,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static DamageType GetDamageType(string statusId)
        {
            if (string.Equals(statusId, StatusEffectIds.Poison,
                    StringComparison.OrdinalIgnoreCase))
            {
                return DamageType.Poison;
            }

            if (string.Equals(statusId, StatusEffectIds.Burn,
                    StringComparison.OrdinalIgnoreCase))
            {
                return DamageType.Burn;
            }

            return DamageType.Bleed;
        }

        private static float GetResistance(
            BattleUnit unit,
            string statusId)
        {
            var resistances = unit?.Character?.Resistances;

            if (resistances == null)
            {
                return 0f;
            }

            if (string.Equals(statusId, StatusEffectIds.Poison,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Mathf.Clamp01(resistances.PoisonResistance + GetModifier(unit, ResistanceType.Poison));
            }

            if (string.Equals(statusId, StatusEffectIds.Burn,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Mathf.Clamp01(resistances.FireResistance + GetModifier(unit, ResistanceType.Fire));
            }

            return Mathf.Clamp01(resistances.BleedResistance + GetModifier(unit, ResistanceType.Bleed));
        }

        private static float GetModifier(BattleUnit unit, ResistanceType type)
        {
            return unit?.ResistanceModifiers != null
                && unit.ResistanceModifiers.TryGetValue(type, out var value)
                ? value : 0f;
        }

        private static float CalculateDamageMultiplier(float resistance)
        {
            resistance = Mathf.Clamp01(resistance);

            if (resistance <= 0.5f)
            {
                return 2f - 2f * resistance;
            }

            return 1.5f - resistance;
        }
    }
}
