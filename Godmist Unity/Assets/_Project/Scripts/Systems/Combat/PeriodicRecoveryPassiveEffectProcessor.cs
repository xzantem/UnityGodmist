using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Core.Enums;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public sealed class PeriodicRecoveryPassiveEffectProcessor : IPassiveEffectTurnProcessor
    {
        private readonly CombatRecoverySystem recoverySystem;

        public PeriodicRecoveryPassiveEffectProcessor(CombatRecoverySystem recoverySystem)
        {
            this.recoverySystem = recoverySystem;
        }

        public bool CanProcess(
            PassiveEffectRuntime effect,
            PassiveEffectTickPhase tickPhase)
        {
            return effect?.GetPayload<PeriodicRecoveryPayload>() != null
                && tickPhase == PassiveEffectTickPhase.TurnEnd;
        }

        public bool ProcessTurn(
            CombatSession session,
            BattleUnit unit,
            PassiveEffectRuntime effect,
            out bool shouldExpire)
        {
            shouldExpire = false;
            var payload = effect?.GetPayload<PeriodicRecoveryPayload>();
            if (unit?.Character == null || payload == null)
            {
                return true;
            }

            if (payload.HealthPercent > 0f)
            {
                var maxHealth = Mathf.Max(0, Mathf.RoundToInt(
                    unit.Character.Stats?.MaxHealth.GetValue() ?? 0f));
                recoverySystem?.Heal(
                    unit,
                    Mathf.CeilToInt(maxHealth * payload.HealthPercent));
            }

            if (payload.ResourcePercent > 0f && unit.Character.ResourcePool != null)
            {
                recoverySystem?.RestoreResource(
                    unit,
                    Mathf.CeilToInt(
                        unit.Character.ResourcePool.MaxValue * payload.ResourcePercent));
            }

            return true;
        }
    }
}
