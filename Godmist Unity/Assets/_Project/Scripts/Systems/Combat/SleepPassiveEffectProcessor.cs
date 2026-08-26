using System;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public class SleepPassiveEffectProcessor : IPassiveEffectTurnProcessor
    {
        private readonly CombatRecoverySystem recoverySystem;

        public SleepPassiveEffectProcessor(
            CombatRecoverySystem recoverySystem)
        {
            this.recoverySystem = recoverySystem;
        }

        public bool CanProcess(
            PassiveEffectRuntime effect,
            PassiveEffectTickPhase tickPhase)
        {
            return effect != null
                && tickPhase == PassiveEffectTickPhase.TurnEnd
                && string.Equals(
                    effect.EffectId,
                    StatusEffectIds.Sleep,
                    StringComparison.OrdinalIgnoreCase);
        }

        public bool ProcessTurn(
            CombatSession session,
            BattleUnit unit,
            PassiveEffectRuntime effect,
            out bool shouldExpire)
        {
            shouldExpire = false;

            if (unit?.Character?.Stats == null || effect == null)
            {
                return true;
            }

            var payload = effect.GetPayload<SleepHealingPayload>();
            if (payload == null || payload.MaxHealthPercent <= 0f)
            {
                return true;
            }

            var maxHealth = unit.Character.Stats.MaxHealth.GetValue();
            var healing = Mathf.CeilToInt(
                Mathf.Max(0f, maxHealth) * payload.MaxHealthPercent);

            recoverySystem?.Heal(unit, healing);

            return true;
        }
    }
}
