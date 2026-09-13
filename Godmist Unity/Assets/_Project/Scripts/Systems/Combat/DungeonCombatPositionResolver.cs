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
