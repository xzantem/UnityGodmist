using System.Collections.Generic;

namespace Godmist.Runtime.Dungeons
{
    public enum DungeonKnowledge { Unexplored, Remembered, Visible }

    public sealed class DungeonObjectKnowledge
    {
        public bool Active;
        // Door open / lever activated / condition satisfied / trap triggered / chest unlocked.
        public bool State;
    }

    public sealed class DungeonFogState
    {
        private readonly HashSet<DungeonTraversalPosition> discovered = new();
        private readonly HashSet<DungeonTraversalPosition> visible = new();
        public IReadOnlyCollection<DungeonTraversalPosition> Discovered => discovered;
        public IReadOnlyCollection<DungeonTraversalPosition> Visible => visible;
        public Dictionary<string, DungeonObjectKnowledge> Objects { get; } = new();
        public HashSet<string> Bodies { get; } = new();
        public HashSet<DungeonSurfaceEdge> ClosedGates { get; } = new();
        // Console overrides are transient and are not part of save data.
        public bool DebugFogDisabled { get; private set; }
        public int DebugRevision { get; private set; }
        public int Revision { get; private set; }
        public void ToggleDebugFog() { DebugFogDisabled = !DebugFogDisabled; DebugRevision++; }
        public void SetMapDiscovered(bool reveal)
        {
            discovered.Clear(); Objects.Clear(); Bodies.Clear(); ClosedGates.Clear();
            RevealLegacyMap = reveal;
            DebugRevision++;
        }
        public bool RevealLegacyMap { get; set; }
        public bool IsVisible(DungeonTraversalPosition position) => visible.Contains(position);
        public DungeonKnowledge Get(DungeonTraversalPosition position) => visible.Contains(position)
            ? DungeonKnowledge.Visible : discovered.Contains(position) ? DungeonKnowledge.Remembered : DungeonKnowledge.Unexplored;
        public bool RestoreDiscovered(DungeonFloorMap map, DungeonTraversalPosition position)
            => map != null && map.TryGetTraversalCell(position, out _) && discovered.Add(position);
        public void SetVisible(IEnumerable<DungeonTraversalPosition> positions, IEnumerable<DungeonTraversalPosition> newlyDiscovered = null)
        {
            Revision++;
            visible.Clear();
            visible.UnionWith(positions);
            discovered.UnionWith(newlyDiscovered ?? visible);
        }
    }
}
