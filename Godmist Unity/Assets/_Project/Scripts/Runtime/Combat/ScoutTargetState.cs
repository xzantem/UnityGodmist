using System.Collections.Generic;
namespace Godmist.Runtime.Combat
{
    public sealed class EmbeddedScoutKnife
    {
        public BattleUnit Owner;
        public float BleedMultiplier;
    }
    public sealed class PendingScoutKnives
    {
        public BattleUnit Owner;
        public GridPosition DropPosition;
        public int Count;
    }
    // Owned by the target's BattleUnit, never by the persistent character or a static cache.
    public sealed class ScoutTargetState
    {
        public readonly Dictionary<BattleUnit, int> AngerBySource = new();
        public readonly List<EmbeddedScoutKnife> Knives = new();
        public UnityEngine.Vector2Int Facing;
        public bool RunningAttackUsed;
        public int ExtraKnifeCapacity;
        public int WeakPointTurns;
        public float WeakPointCritDamage;
        public int AnalysisTurns;
        public float AnalysisPenetration;
    }
}
