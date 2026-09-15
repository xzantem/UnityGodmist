using System.Collections.Generic;
namespace Godmist.Runtime.Dungeons
{
    public sealed class DungeonPatrolState
    {
        public List<DungeonTraversalPosition> Route { get; } = new();
        public HashSet<int> StopIndices { get; } = new();
        public int NextPoint;
        public float WaitRemaining;
        public int Stops;
        public string ObservedCharacterId;
        public void Arrive(string enemyKey)
        {
            var shouldPause = StopIndices.Contains(NextPoint);
            NextPoint = (NextPoint + 1) % Route.Count;
            if (!shouldPause) { WaitRemaining = 0; return; }
            // Deterministic pauses without a process-global RNG or save/load reroll.
            uint hash = 2166136261;
            foreach (var c in enemyKey) hash = unchecked((hash ^ c) * 16777619);
            hash = unchecked((hash ^ (uint)Stops++) * 16777619);
            WaitRemaining = 2f + (hash % 1001) / 1000f;
        }
    }
}
