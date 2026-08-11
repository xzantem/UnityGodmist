using System;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public class BleedPassiveEffectProcessor : IPassiveEffectTurnProcessor
    {
        public const string BleedId = StatusEffectIds.Bleed;

        private readonly CombatDamageSystem damageSystem;

        public BleedPassiveEffectProcessor(CombatDamageSystem damageSystem)
        {
            this.damageSystem = damageSystem ?? new CombatDamageSystem();
        }

        public bool CanProcess(
            PassiveEffectRuntime effect,
            PassiveEffectTickPhase tickPhase)
        {
            return effect != null
                && tickPhase == PassiveEffectTickPhase.TurnStart
                && string.Equals(
                    effect.EffectId,
                    BleedId,
                    StringComparison.OrdinalIgnoreCase);
        }

        public bool ProcessTurn(
            CombatSession session,
            BattleUnit unit,
            PassiveEffectRuntime effect,
            out bool shouldExpire)
        {
            shouldExpire = false;

            if (session == null
                || unit?.Character == null
                || unit.Character.LifeState != CharacterLifeState.Alive
                || effect == null)
            {
                return false;
            }

            var remainingTurns = Mathf.Max(1, effect.RemainingDuration);
            var rawDamage = Mathf.CeilToInt(
                effect.TotalDamage / (float)remainingTurns);

            effect.TotalDamage = Mathf.Max(0, effect.TotalDamage - rawDamage);

            var resistance = Mathf.Clamp01(
                unit.Character.Resistances?.BleedResistance ?? 0f);

            var multiplier = CalculateDamageMultiplier(resistance);
            var finalDamage = Mathf.Max(
                0,
                Mathf.RoundToInt(rawDamage * multiplier));

            damageSystem.ApplyDamage(
                session,
                null,
                unit,
                finalDamage,
                DamageType.Bleed);

            shouldExpire = effect.TotalDamage <= 0;

            return unit.Character.LifeState == CharacterLifeState.Alive;
        }

        private static float CalculateDamageMultiplier(float resistance)
        {
            if (resistance <= 0.5f)
            {
                return 2f - 2f * resistance;
            }

            return 1.5f - resistance;
        }
    }
}