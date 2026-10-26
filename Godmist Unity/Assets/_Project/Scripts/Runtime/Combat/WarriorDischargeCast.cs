namespace Godmist.Runtime.Combat
{
    // Action-local snapshot; never persisted or shared with another cast.
    public sealed class WarriorDischargeCast
    {
        public readonly float MinAttack, MaxAttack, CritChance;
        public readonly int RageSpent;
        public bool Refunded { get; private set; }
        public WarriorDischargeCast(float minAttack, float maxAttack, float critChance, int rageSpent)
        { MinAttack = minAttack; MaxAttack = maxAttack; CritChance = critChance; RageSpent = rageSpent; }
        public bool TryMarkRefunded()
        {
            if (Refunded) return false;
            Refunded = true;
            return true;
        }
    }
}
