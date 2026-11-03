using Godmist.Core.Enums;
using Godmist.Core.Utilities;
using Godmist.Runtime.Combat;

namespace Godmist.Systems.Combat
{
    /// <summary>Presentation-only outcomes. Never rolls, spends or applies a gameplay effect.</summary>
    public static class CombatFailureFeedback
    {
        public static void Effect(CombatFeedbackBus bus, BattleUnit source, BattleUnit target,
            EffectType effect, CombatEffectFailureReason reason = CombatEffectFailureReason.NotApplied,
            string effectName = null)
        {
            if (target?.Character == null || target.Character.LifeState == CharacterLifeState.Dead) return;
            bus?.Publish(new CombatFeedbackEvent(target, CombatFeedbackType.EffectFailed,
                effectName: effectName ?? UiTerms.CombatEffect(effect), source: source, failureReason: reason));
        }
    }
}
