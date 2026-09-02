namespace Godmist.Core.Enums
{
    public enum PassiveEffectConditionType
    {
        Always = 0,
        TargetHasStatus = 1,
        CasterHasStatus = 2,
        TargetHealthBelowPercent = 3,
        CasterHealthBelowPercent = 4,
        TargetHealthAbovePercent = 5,
        CasterHealthAbovePercent = 6,
        TargetHasTag = 7,
        DungeonTheme = 8,
        AttackType = 9,
        AttackPosition = 10,
        Distance = 11,
        CasterHasBuff = 12,
        TargetHasBuff = 13,
        Skill = 14,
        Resource = 15,
        StackCount = 16,
        Turn = 17,
        ReferencedEffectSucceeded = 18,
        ReferencedEffectCrit = 19,
        ReferencedEffectDodged = 20
    }
}
