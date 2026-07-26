namespace Godmist.Runtime.Combat
{
    public readonly struct CombatDamageResult
    {
        public bool Hit { get; }
        public bool IsCritical { get; }
        public int HealthDamage { get; }

        public CombatDamageResult(bool hit, bool isCritical, int healthDamage)
        {
            Hit = hit;
            IsCritical = isCritical;
            HealthDamage = healthDamage;
        }
    }
}