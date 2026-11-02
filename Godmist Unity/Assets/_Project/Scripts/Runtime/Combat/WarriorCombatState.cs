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
        public int? AccuracyRageSnapshot;
        public string AccuracySnapshotSkill;
        public string FencingTarget;
        public int FencingStacks;
        public long DeathWishPreventedDamage;
        // Combat-local grace: 0 after offense, 1 free idle turn, 2 recurring decay.
        public int RageIdleTurns;
        public bool ArmoredInitialized, RenewalActive;
        public double MitigatedBank, AttackBank, VampirismRemainder;
        public int ArmoredTurn, ReviveCooldown;
        public int DisorientationUntil;
        public float DisorientationCap;
        public readonly System.Collections.Generic.HashSet<string> AdaptedSkills = new();
        public readonly System.Collections.Generic.HashSet<string> PendingAdaptedSkills = new();
        public void CopyArmoredFrom(WarriorCombatState state)
        {
            ArmoredInitialized=state.ArmoredInitialized;RenewalActive=state.RenewalActive;
            MitigatedBank=state.MitigatedBank;AttackBank=0;VampirismRemainder=state.VampirismRemainder;
            ArmoredTurn=state.ArmoredTurn;ReviveCooldown=state.ReviveCooldown;
            DisorientationUntil=state.DisorientationUntil;DisorientationCap=state.DisorientationCap;
            AdaptedSkills.UnionWith(state.AdaptedSkills);PendingAdaptedSkills.UnionWith(state.PendingAdaptedSkills);
        }
        public int ArmoredStateHash()
        {
            var h=new System.HashCode();h.Add(ArmoredInitialized);h.Add(RenewalActive);h.Add(MitigatedBank);
            h.Add(VampirismRemainder);h.Add(ArmoredTurn);
            h.Add(ReviveCooldown);h.Add(DisorientationUntil);h.Add(DisorientationCap);
            int a=0,b=0;foreach(var id in AdaptedSkills)a^=id.GetHashCode();foreach(var id in PendingAdaptedSkills)b^=id.GetHashCode();
            h.Add(a);h.Add(b);return h.ToHashCode();
        }
        public bool RageOffensiveActionUsed, RageHadOffensiveOpportunity, RageControlBlocked;
    }
}
