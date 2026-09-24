using Godmist.Core.Enums;

namespace Godmist.Runtime.Combat
{
    public enum CombatFeedbackType
    {
        Damage,
        Heal,
        ShieldGain,
        ShieldLoss,
        ResourceGain,
        ResourceSpend,
        ActionPointSpend,
        Critical,
        Miss,
        StatusApplied,
        BuffApplied,
        DebuffApplied,
        ProcTriggered,
        AuraApplied,
        DamageRedirected,
        ResourceRegenerationBlocked,
        TurnSkipped,
        SkillUsed
    }

    public readonly struct CombatFeedbackEvent
    {
        public CombatFeedbackEvent(
            BattleUnit target,
            CombatFeedbackType type,
            int amount = 0,
            DamageType? damageType = null,
            ResourceType? resourceType = null,
            GridPosition? position = null,
            string effectName = null)
        {
            Target = target;
            Type = type;
            Amount = amount;
            DamageType = damageType;
            ResourceType = resourceType;
            Position = position ?? target?.Position ?? default;
            EffectName = effectName ?? string.Empty;
        }

        public BattleUnit Target { get; }
        public CombatFeedbackType Type { get; }
        public int Amount { get; }
        public DamageType? DamageType { get; }
        public ResourceType? ResourceType { get; }
        public GridPosition Position { get; }
        public string EffectName { get; }
    }
}
