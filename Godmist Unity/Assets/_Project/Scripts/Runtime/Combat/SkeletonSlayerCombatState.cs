using System.Collections.Generic;
using Godmist.Data.Characters;

namespace Godmist.Runtime.Combat
{
    /// <summary>Encounter-local state; retained only when rebinding the same combat.</summary>
    public sealed class SkeletonSlayerCombatState
    {
        public CharacterDefinition OriginalDefinition;
        public bool PhaseTwo, PhaseTwoPending;
        public int RevengeHits;
        public int DefenseTurns, DefenseDamage, NextAttackBonus, ActiveAttackBonus;
        public int HammerTurns, HammerDamage;
        public bool TaskUsedThisTurn, AccelerateNextTurn;
        public GridPosition HammerCenter;
        public GridPosition? SelectedHammerCenter;
        public HashSet<GridPosition> HammerFields = new();
        public List<SummonedCombatMapObject> Markers = new();
    }
}
