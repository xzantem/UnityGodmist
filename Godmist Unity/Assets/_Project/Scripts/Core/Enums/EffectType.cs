namespace Godmist.Core.Enums
{
    public enum EffectType
    {
        DealDamage = 0,
        Push = 1,
        ApplyStatus = 2,
        Heal = 3,
        RestoreResource = 4,
        Shield = 5,
        ModifyStat = 6,
        ModifyVar = 7,
        Pull = 8,
        Fallback = 9,
        Charge = 10,
        Teleport = 11,
        CreateObject = 12,
        Summon = 13,
        [System.Obsolete("Use ApplyUniqueBuff + ActionPointsNextTurn.")]
        LegacyModifyActionPoints = 14,
        ModifyActionValue = 15,
        DrainResource = 16,
        SacrificeHealthForResource = 17,
        ModifyResistance = 18,
        ApplyUniqueBuff = 19,
        [System.Obsolete("Use ApplyUniqueBuff + DamageRedirect.")]
        LegacyRedirectDamage = 20,
        [System.Obsolete("Use ApplyUniqueBuff + AuraStatus.")]
        LegacyApplyAura = 21,
        BlockResourceRegeneration = 22,
        [System.Obsolete("Use ApplyUniqueBuff + OnHitStatus.")]
        LegacyApplyOnHitStatus = 23,
        HealPercent = 24,
        RestoreResourcePercent = 25,
        HealOverTimePercent = 26,
        RestoreResourceOverTimePercent = 27,
        ModifyActionPointsPercent = 28,
        RemoveStatus = 29
    }

    public enum UniqueBuffType
    {
        GuaranteedCriticalHit = 0,
        GuaranteedDodge = 1,
        MaximumDamage = 2,
        ActionPointsNextTurn = 3,
        OnHitStatus = 4,
        DamageRedirect = 5,
        AuraStatus = 6
    }

    public enum ResistanceType
    {
        Bleed = 0,
        Poison = 1,
        Fire = 2,
        Weakening = 3,
        Suppression = 4,
        Move = 5
    }
}
