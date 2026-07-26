namespace Godmist.Core.Enums
{
    public enum EffectConditionType
    {
        Always = 0,
        PreviousEffectSucceeded = 1,
        PreviousEffectDealtHealthDamage = 2,
        TargetHasStatus = 3,
        CasterHasStatus = 4,
        TargetHealthBelowPercent = 5,
        CasterHealthBelowPercent = 6
    }
}