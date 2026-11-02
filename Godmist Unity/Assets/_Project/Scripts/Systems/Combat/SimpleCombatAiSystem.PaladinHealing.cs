using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public partial class SimpleCombatAiSystem
    {
        // Same bounded horizon as the existing regeneration-status evaluation.
        private const int RecoveryForecastTurns = 2;

        private static float PendingRegeneration(BattleUnit target) => Mathf.Min(GetMissingHealth(target),
            target.Character.PassiveEffects.Where(effect => effect?.EffectId == PaladinCombatSystem.HolyRegenerationId)
                .Sum(effect => PaladinCombatSystem.ForecastRegenerationTicks(effect, RecoveryForecastTurns, Mathf.CeilToInt(GetMissingHealth(target)),
                    effect.Sources.Any(source => source.SourceCharacterId == target.Character.CharacterId) ? target : null)));

        private static float FutureHealingUtility(CombatSession session, BattleUnit caster, BattleUnit target, float amount) =>
            // Future ticks cannot rescue now and do not receive the immediate-threat/revival bonus.
            target.Character.LifeState == CharacterLifeState.Alive ? amount * (target.IsPlayerSide == caster.IsPlayerSide ? 1f : -1f) : 0f;

        private static float PaladinRecoveryUtility(CombatSession session, BattleUnit caster, BattleUnit target,
            SkillData skill, SkillEffectData effect, bool singleTarget, float? pending = null)
        {
            var forecast = PaladinCombatSystem.ForecastHealing(session, caster, target, skill, effect, singleTarget, RecoveryForecastTurns);
            var previous = pending ?? PendingRegeneration(target);
            var future = effect.EffectType == EffectType.PaladinRegeneration ? forecast.Future
                : Mathf.Min(previous, Mathf.Max(0, GetMissingHealth(target) - forecast.Immediate));
            return HealingUtility(session, caster, target, forecast.Immediate)
                + FutureHealingUtility(session, caster, target, future)
                - FutureHealingUtility(session, caster, target, previous);
        }
    }
}
