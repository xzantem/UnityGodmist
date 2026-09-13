using System.Collections.Generic;
using Godmist.Runtime.Combat;

namespace Godmist.Systems.Combat
{
    public static class DungeonCombatPositionResolver
    {
        private static readonly GridPosition[] Directions =
        {
            new(0, 1), new(1, 0), new(0, -1), new(-1, 0)
        };

        // Reserve every original cell before placing collisions, so a displaced unit
        // cannot steal another participant's valid exploration position.
        public static bool TryResolveGroup(BattleGrid grid, IReadOnlyList<GridPosition> preferred,
            ISet<GridPosition> reserved, out List<GridPosition> positions)
        {
            positions = null;
            if (grid == null || preferred == null || reserved == null) return false;
            var occupied = new HashSet<GridPosition>(reserved);
            var resolved = new List<GridPosition>(preferred.Count);
            var displaced = new List<int>();
            for (var i = 0; i < preferred.Count; i++)
            {
                var cell = preferred[i];
                if (!grid.IsWalkable(cell) || grid.IsBlocked(cell)) return false;
                resolved.Add(cell);
                if (!occupied.Add(cell)) displaced.Add(i);
            }
            foreach (var i in displaced)
            {
                if (!TryResolve(grid, preferred[i], occupied, out var cell)) return false;
                resolved[i] = cell;
                occupied.Add(cell);
            }
            positions = resolved;
            return true;
        }

        // Search only the connected floor component; never snap through walls or doors.
        public static bool TryResolve(BattleGrid grid, GridPosition preferred,
            ISet<GridPosition> occupied, out GridPosition result)
        {
            result = default;
            if (grid == null || !grid.IsWalkable(preferred) || grid.IsBlocked(preferred)) return false;
            var queue = new Queue<GridPosition>();
            var visited = new HashSet<GridPosition> { preferred };
            queue.Enqueue(preferred);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!occupied.Contains(current)) { result = current; return true; }
                foreach (var direction in Directions)
                {
                    var next = new GridPosition(current.X + direction.X, current.Y + direction.Y);
                    if (grid.CanMoveBetween(current, next) && visited.Add(next)) queue.Enqueue(next);
                }
            }
            return false;
        }
    }
}
