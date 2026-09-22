namespace Godmist.Core.Enums
{
    public enum PassiveEffectOperationType
    {
        ModifyStat = 0,
        ModifyResistance = 1,
        ModifyGameplayVariable = 2,
        ModifySkillParameter = 3,
        ModifyResourceCost = 4,
        ModifyMovementCost = 5,
        ApplyStatus = 6,
        ModifyStatus = 7,
        DealDamage = 8,
        Heal = 9,
        RestoreResource = 10,
        GrantShield = 11,
        RedirectDamage = 12,
        ReflectDamage = 13,
        PreventDamage = 14,
        DuplicateAction = 15,
        GrantActionPoints = 16,
        ChangeHitResolution = 17,
        SetRuntimeFlag = 18,
        ModifyActionValue = 19
    }
}
