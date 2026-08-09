using System.Collections.Generic;
using Godmist.Runtime.Combat;

namespace Godmist.Systems.Combat
{
    public enum CombatSpawnEdge
    {
        Left,
        Right,
        Bottom,
        Top
    }

    public class DungeonCombatSpawnResolver
    {
        public static void ResolveOpposingEdges(
            GridPosition partyAnchor,
            GridPosition enemyAnchor,
            out CombatSpawnEdge partyEdge,
            out CombatSpawnEdge enemyEdge)
        {
            var deltaX = enemyAnchor.X - partyAnchor.X;
            var deltaY = enemyAnchor.Y - partyAnchor.Y;

            if (System.Math.Abs(deltaX) >= System.Math.Abs(deltaY))
            {
                partyEdge = deltaX <= 0
                    ? CombatSpawnEdge.Right
                    : CombatSpawnEdge.Left;

                enemyEdge = partyEdge == CombatSpawnEdge.Left
                    ? CombatSpawnEdge.Right
                    : CombatSpawnEdge.Left;

                return;
            }

            partyEdge = deltaY <= 0
                ? CombatSpawnEdge.Top
                : CombatSpawnEdge.Bottom;

            enemyEdge = partyEdge == CombatSpawnEdge.Bottom
                ? CombatSpawnEdge.Top
                : CombatSpawnEdge.Bottom;
        }

        public bool TryResolveGroupPositions(
            BattleGrid grid,
            CombatSpawnEdge edge,
            int memberCount,
            ISet<GridPosition> occupiedPositions,
            out List<GridPosition> positions)
        {
            positions = new List<GridPosition>();

            if (grid == null || memberCount <= 0 || occupiedPositions == null)
            {
                return false;
            }

            var lineLength = edge == CombatSpawnEdge.Left
                || edge == CombatSpawnEdge.Right
                ? grid.Height
                : grid.Width;

            for (var memberIndex = 0; memberIndex < memberCount; memberIndex++)
            {
                var preferredLineIndex = GetPreferredLineIndex(
                    lineLength,
                    memberIndex,
                    memberCount);

                if (!TryFindFreePositionFromEdge(
                        grid,
                        edge,
                        preferredLineIndex,
                        occupiedPositions,
                        out var position))
                {
                    return false;
                }

                positions.Add(position);
                occupiedPositions.Add(position);
            }

            return true;
        }

        private static bool TryFindFreePositionFromEdge(
            BattleGrid grid,
            CombatSpawnEdge edge,
            int preferredLineIndex,
            ISet<GridPosition> occupiedPositions,
            out GridPosition position)
        {
            position = default;

            var maximumDepth = edge == CombatSpawnEdge.Left
                || edge == CombatSpawnEdge.Right
                ? grid.Width
                : grid.Height;

            var lineLength = edge == CombatSpawnEdge.Left
                || edge == CombatSpawnEdge.Right
                ? grid.Height
                : grid.Width;

            for (var depth = 0; depth < maximumDepth; depth++)
            {
                for (var offset = 0; offset < lineLength; offset++)
                {
                    var lowerIndex = preferredLineIndex - offset;

                    if (TryUseCandidate(
                            grid,
                            edge,
                            depth,
                            lowerIndex,
                            occupiedPositions,
                            out position))
                    {
                        return true;
                    }

                    if (offset == 0) continue;

                    var higherIndex = preferredLineIndex + offset;

                    if (TryUseCandidate(
                            grid,
                            edge,
                            depth,
                            higherIndex,
                            occupiedPositions,
                            out position))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static int GetPreferredLineIndex(
            int lineLength,
            int memberIndex,
            int memberCount)
        {
            if (memberCount <= 1)
            {
                return lineLength / 2;
            }

            var edgeMargin = lineLength >= 5 ? 1 : 0;
            var firstIndex = edgeMargin;
            var lastIndex = lineLength - 1 - edgeMargin;

            var normalizedIndex = memberIndex / (float)(memberCount - 1);

            return (int)System.Math.Round(
                firstIndex + (lastIndex - firstIndex) * normalizedIndex);
        }

        private static bool TryUseCandidate(
            BattleGrid grid,
            CombatSpawnEdge edge,
            int depth,
            int lineIndex,
            ISet<GridPosition> occupiedPositions,
            out GridPosition position)
        {
            position = default;

            var lineLength = edge == CombatSpawnEdge.Left
                || edge == CombatSpawnEdge.Right
                ? grid.Height
                : grid.Width;

            if (lineIndex < 0 || lineIndex >= lineLength)
            {
                return false;
            }

            var candidate = GetPositionAtEdgeDepth(
                grid,
                edge,
                depth,
                lineIndex);

            if (!grid.IsWalkable(candidate)
                || grid.IsBlocked(candidate)
                || occupiedPositions.Contains(candidate))
            {
                return false;
            }

            position = candidate;
            return true;
        }

        private static GridPosition GetPositionAtEdgeDepth(
            BattleGrid grid,
            CombatSpawnEdge edge,
            int depth,
            int lineIndex)
        {
            return edge switch
            {
                CombatSpawnEdge.Left =>
                    new GridPosition(depth, lineIndex),

                CombatSpawnEdge.Right =>
                    new GridPosition(
                        grid.Width - 1 - depth,
                        lineIndex),

                CombatSpawnEdge.Bottom =>
                    new GridPosition(lineIndex, depth),

                CombatSpawnEdge.Top =>
                    new GridPosition(
                        lineIndex,
                        grid.Height - 1 - depth),

                _ => default
            };
        }
    }
}