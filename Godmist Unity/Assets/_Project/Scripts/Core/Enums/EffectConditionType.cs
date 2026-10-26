public enum EffectConditionType
{
    Always = 0,
    ReferencedEffectSucceeded = 1,
    ReferencedEffectDealtHealthDamage = 2,
    TargetHasStatus = 3,
    CasterHasStatus = 4,
    TargetHealthBelowPercent = 5,
        CasterHealthBelowPercent = 6,
        MinimumAdjacentAllies = 7,
        MinimumResourceSpent = 8,
        TargetHasActiveShield = 9,
        AnyOpponentHasStatus = 10,
        NoOpponentHasStatus = 11
}
