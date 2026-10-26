using System.Collections.Generic;
using Godmist.Data.Characters;
namespace Godmist.Runtime.Combat
{
    public sealed class DeadKingCombatState
    {
        public CharacterDefinition OriginalDefinition;
        public bool PhaseTwo;
        public bool PhaseTwoPending;
        public string ArenaRoomId;
        public int ShadowTurns;
        public HashSet<GridPosition> SafeFields = new();
        public HashSet<GridPosition> DarkFields = new();
        public List<SummonedCombatMapObject> Markers = new();
    }
}
