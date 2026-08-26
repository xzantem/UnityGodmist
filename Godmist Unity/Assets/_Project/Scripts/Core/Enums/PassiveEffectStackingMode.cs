namespace Godmist.Core.Enums
{
    public enum PassiveEffectPayloadType
    {
        DamageOverTime,
        SleepHealing,
        UniqueCombatBuff,
        ResourceRegenerationBlock,
        DamageRedirect,
        OnHitStatus,
        Counterattack,
        StatModifiers,
        VariableModifiers
    }

    public enum PassiveEffectStackingMode
    {
        MaxDurationAndValues = 0,
        ReplaceDuration = 1,
        ReplaceDurationAndAddTotalDamage = 2,
        ReplaceDurationAndValues = 3
    }
}
