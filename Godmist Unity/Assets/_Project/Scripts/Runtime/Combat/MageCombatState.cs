using System.Collections.Generic;
namespace Godmist.Runtime.Combat
{
    public sealed class MageCombatState
    {
        public int OffensiveTargetCount;
        public float OverflowCritChance;
        public string OverflowActionId;
        // Lazily allocated per combat participant; never stored on a character/save.
        public Dictionary<string, HashSet<string>> TiringAttempts;
        public readonly Dictionary<string, MageAdaptation> Adaptation = new();
        public readonly Dictionary<string, int> FireErosion = new();
    }
    public sealed class MageAdaptation
    {
        public int RemainingHits;
        public int Reduction;
    }
}
