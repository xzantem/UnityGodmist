using System;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Configuration;
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

        public static int CalculateEffectiveDamage(
            BattleUnit unit,
            string statusId,
            int rawDamage)
        {
            if (rawDamage <= 0) return 0;
            if (unit?.Character == null) return rawDamage;

            var resistance = GetResistance(unit, statusId);
            return Mathf.Max(
                0,
                Mathf.RoundToInt(rawDamage * CalculateDamageMultiplier(resistance)));
        }

        public bool CanProcess(PassiveEffectRuntime effect, PassiveEffectTickPhase tickPhase)
        {
            return effect != null
                && tickPhase == PassiveEffectTickPhase.TurnStart
                && effect.GetPayload<DamageOverTimePayload>() != null
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

            var payload = effect.GetPayload<DamageOverTimePayload>();
            if (payload == null) return true;

            var remainingTurns = Mathf.Max(1, effect.RemainingDuration);
            var rawDamage = Mathf.CeilToInt(
                payload.RemainingTotalDamage / (float)remainingTurns);

            payload.RemainingTotalDamage = Mathf.Max(
                0,
                payload.RemainingTotalDamage - rawDamage);

            var finalDamage = CalculateEffectiveDamage(unit, effect.EffectId, rawDamage);

            damageSystem.ApplyDamage(
                session,
                source: null,
                target: unit,
                requestedDamage: finalDamage,
                damageType: GetDamageType(effect.EffectId));

            shouldExpire = payload.RemainingTotalDamage <= 0;

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

            if (string.Equals(statusId, StatusEffectIds.Poison,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Mathf.Clamp01((resistances?.PoisonResistance ?? 0f)
                    + GetModifier(unit, ResistanceType.Poison));
            }

            if (string.Equals(statusId, StatusEffectIds.Burn,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Mathf.Clamp01((resistances?.FireResistance ?? 0f)
                    + GetModifier(unit, ResistanceType.Fire));
            }

            return Mathf.Clamp01((resistances?.BleedResistance ?? 0f)
                + GetModifier(unit, ResistanceType.Bleed));
        }

        private static float GetModifier(BattleUnit unit, ResistanceType type)
        {
            return ModifyResistanceSystem.GetModifier(unit, type);
        }

        public static float CalculateDamageMultiplier(float resistance)
        {
            resistance = Mathf.Clamp01(resistance);
            var halfPoint = GameDefinesRuntime.GetFloat(
                GameDefineIds.DotResistanceHalfPoint, .5f);
            var lowMultiplier = GameDefinesRuntime.GetFloat(
                GameDefineIds.DotResistanceLowMultiplier, 2f);
            var lowSlope = GameDefinesRuntime.GetFloat(
                GameDefineIds.DotResistanceLowSlope, 2f);
            var highBase = GameDefinesRuntime.GetFloat(
                GameDefineIds.DotResistanceHighBase, 1.5f);
            var highSlope = GameDefinesRuntime.GetFloat(
                GameDefineIds.DotResistanceHighSlope, 1f);

            if (resistance <= halfPoint)
            {
                return lowMultiplier - lowSlope * resistance;
            }

            return highBase - highSlope * resistance;
        }
    }
}
