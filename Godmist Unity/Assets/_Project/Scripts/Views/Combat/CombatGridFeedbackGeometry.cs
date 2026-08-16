using System.Collections.Generic;
using Godmist.Runtime.Combat;

namespace Godmist.Views.Combat
{
    public enum CombatGridEdgeDirection
    {
        Bottom,
        Top,
        Left,
        Right
    }

    public readonly struct CombatGridOutlineEdge
    {
        public CombatGridOutlineEdge(GridPosition cell, CombatGridEdgeDirection direction)
        {
            Cell = cell;
            Direction = direction;
        }

        public GridPosition Cell { get; }
        public CombatGridEdgeDirection Direction { get; }
    }

    public static class CombatGridFeedbackGeometry
    {
        public static List<CombatGridOutlineEdge> BuildOutlineEdges(
            IEnumerable<GridPosition> positions)
        {
            var cells = new HashSet<GridPosition>();
            if (positions != null)
            {
                foreach (var position in positions) cells.Add(position);
            }

            var edges = new List<CombatGridOutlineEdge>();
            foreach (var cell in cells)
            {
                AddIfExposed(cell, new GridPosition(cell.X, cell.Y - 1), CombatGridEdgeDirection.Bottom, cells, edges);
                AddIfExposed(cell, new GridPosition(cell.X, cell.Y + 1), CombatGridEdgeDirection.Top, cells, edges);
                AddIfExposed(cell, new GridPosition(cell.X - 1, cell.Y), CombatGridEdgeDirection.Left, cells, edges);
                AddIfExposed(cell, new GridPosition(cell.X + 1, cell.Y), CombatGridEdgeDirection.Right, cells, edges);
            }

            return edges;
        }

        private static void AddIfExposed(
            GridPosition cell,
            GridPosition adjacent,
            CombatGridEdgeDirection direction,
            HashSet<GridPosition> cells,
            List<CombatGridOutlineEdge> edges)
        {
            if (!cells.Contains(adjacent)) edges.Add(new CombatGridOutlineEdge(cell, direction));
        }
    }
}
