using System.Collections.Generic;
namespace Godmist.Runtime.Combat
{
    public sealed class ForestEntCombatState
    {
        public Godmist.Data.Characters.CharacterDefinition OriginalDefinition;
        public bool PhaseTwo;
        public bool PhaseTwoPending;
        public string ArenaRoomId;
        public int FireTurns;
        public bool Warning;
        public float ManaCarry;
        public readonly HashSet<GridPosition> Fields = new();
        public readonly List<SummonedCombatMapObject> Markers = new();
        public readonly Dictionary<BattleUnit, int> Plague = new();
        public readonly HashSet<(string, BattleUnit)> ActionHits = new();
        public readonly HashSet<BattleUnit> PeriodicHits = new();
        public readonly HashSet<BattleUnit> FireHits = new();
    }
}
