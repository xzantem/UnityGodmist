using Godmist.Runtime.Combat;
namespace Godmist.Runtime.Combat
{
    // Encounter-only counters and links; never mutate shared definitions.
    public sealed class WarriorCombatState
    {
        public CombatSession Session;
        public BattleUnit OrderSource;
        public int OrderTurns;
        public BattleUnit ProtectorSource;
        public int ProtectionTurns;
        public bool Riposte;
        public int FreeMovementTurns;
        public int FreeStepsUsed;
        public int TraumaTurns;
        public float TraumaBonus;
        public string LastCutTarget;
        public int ConsecutiveCuts;
        public float RevengeRemainder;
        public float HealingRemainder;
        public float TrueDamageRemainder;
    }
}
