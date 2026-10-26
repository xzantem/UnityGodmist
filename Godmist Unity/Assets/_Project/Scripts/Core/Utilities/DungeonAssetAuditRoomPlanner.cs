using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;

namespace Godmist.Core.Utilities
{
    /// <summary>
    /// Pure deterministic placement planner for the Catacombs asset audit
    /// fixture. Runtime code supplies the authored floor cells and blocked
    /// structural edges, then consumes this exact plan for object/enemy views.
    /// </summary>
    public static class DungeonAssetAuditRoomPlanner
    {
        public readonly struct Cell : IEquatable<Cell>
        {
            public int X { get; }
            public int Y { get; }

            public Cell(int x, int y)
            {
                X = x;
                Y = y;
            }

            public bool Equals(Cell other) => X == other.X && Y == other.Y;
            public override bool Equals(object obj) => obj is Cell other && Equals(other);
            public override int GetHashCode() => unchecked((X * 397) ^ Y);
            public override string ToString() => $"({X},{Y})";
        }

        public readonly struct Edge : IEquatable<Edge>
        {
            public Cell First { get; }
            public Cell Second { get; }

            public Edge(Cell first, Cell second)
            {
                if (Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y) != 1)
                    throw new ArgumentException("An audit edge must join adjacent cells.");

                if (first.X < second.X || first.X == second.X && first.Y <= second.Y)
                {
                    First = first;
                    Second = second;
                }
                else
                {
                    First = second;
                    Second = first;
                }
            }

            public bool Equals(Edge other) => First.Equals(other.First) && Second.Equals(other.Second);
            public override bool Equals(object obj) => obj is Edge other && Equals(other);
            public override int GetHashCode() => unchecked((First.GetHashCode() * 397) ^ Second.GetHashCode());
            public override string ToString() => $"{First}-{Second}";
        }

        public readonly struct SurfaceEdge : IEquatable<SurfaceEdge>
        {
            public Edge Edge { get; }
            public int Level { get; }
            public DungeonWallType WallType { get; }

            public SurfaceEdge(Cell first, Cell second, int level, DungeonWallType wallType)
            {
                Edge = new Edge(first, second);
                Level = level;
                WallType = wallType;
            }

            public bool Equals(SurfaceEdge other)
                => Edge.Equals(other.Edge) && Level == other.Level && WallType == other.WallType;

            public override bool Equals(object obj) => obj is SurfaceEdge other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Edge, Level, WallType);
        }

        public sealed class Plan
        {
            public IReadOnlyList<Edge> DoorEdges { get; }
            public IReadOnlyList<Cell> ObjectCells { get; }
            public IReadOnlyList<int> ObjectFootprintHeights { get; }
            public IReadOnlyList<Cell> EnemyCells { get; }
            public IReadOnlyCollection<Cell> NavigationCells { get; }
            public IReadOnlyList<Cell> ElevatedCells { get; }
            public IReadOnlyList<Cell> StairPath { get; }
            public IReadOnlyList<SurfaceEdge> GroundStructuralEdges { get; }
            public IReadOnlyList<SurfaceEdge> ElevatedStructuralEdges { get; }
            public Cell DaggerCell { get; }

            public Plan(
                IReadOnlyList<Edge> doorEdges,
                IReadOnlyList<Cell> objectCells,
                IReadOnlyList<int> objectFootprintHeights,
                IReadOnlyList<Cell> enemyCells,
                IReadOnlyCollection<Cell> navigationCells,
                IReadOnlyList<Cell> elevatedCells,
                IReadOnlyList<Cell> stairPath,
                IReadOnlyList<SurfaceEdge> groundStructuralEdges,
                IReadOnlyList<SurfaceEdge> elevatedStructuralEdges,
                Cell daggerCell)
            {
                DoorEdges = doorEdges;
                ObjectCells = objectCells;
                ObjectFootprintHeights = objectFootprintHeights;
                EnemyCells = enemyCells;
                NavigationCells = navigationCells;
                ElevatedCells = elevatedCells;
                StairPath = stairPath;
                GroundStructuralEdges = groundStructuralEdges;
                ElevatedStructuralEdges = elevatedStructuralEdges;
                DaggerCell = daggerCell;
            }
        }

        public static Plan Build(
            IEnumerable<Cell> floorCells,
            IEnumerable<Edge> blockedEdges,
            Cell spawnCell,
            int seed,
            int objectCount,
            IReadOnlyList<int> enemyMinimumChebyshevDistances,
            IReadOnlyList<int> objectFootprintHeights = null)
        {
            if (floorCells == null) throw new ArgumentNullException(nameof(floorCells));
            if (blockedEdges == null) throw new ArgumentNullException(nameof(blockedEdges));
            if (objectCount < 0) throw new ArgumentOutOfRangeException(nameof(objectCount));
            if (enemyMinimumChebyshevDistances == null)
                throw new ArgumentNullException(nameof(enemyMinimumChebyshevDistances));
            if (objectFootprintHeights != null
                && objectFootprintHeights.Count != objectCount)
            {
                throw new ArgumentException(
                    "Object footprint metadata must match objectCount.",
                    nameof(objectFootprintHeights));
            }

            var floor = new HashSet<Cell>(floorCells);
            if (!floor.Contains(spawnCell))
                throw new InvalidOperationException("Audit spawn cell must belong to the authored floor.");

            var blocked = new HashSet<Edge>(blockedEdges);
            var gallery = floor
                .Where(cell => cell.X >= 2 && cell.X <= 12 && cell.Y >= 2 && cell.Y <= 8)
                .OrderBy(cell => cell.Y)
                .ThenBy(cell => cell.X)
                .ToList();
            var elevation = BuildElevationPlan(floor);
            var navigation = new HashSet<Cell>(floor.Where(cell => cell.Y == 3 || cell.X == 2))
            {
                spawnCell,
                new Cell(7, 5)
            };
            foreach (var cell in elevation.ElevatedCells)
                navigation.Add(cell);
            foreach (var cell in elevation.StairPath)
                navigation.Add(cell);
            var occupied = new HashSet<Cell>(navigation);
            var floorSet = new HashSet<Cell>(floor);
            foreach (var structuralEdge in elevation.GroundStructuralEdges)
            {
                occupied.Add(structuralEdge.Edge.First);
                occupied.Add(structuralEdge.Edge.Second);
                blocked.Add(structuralEdge.Edge);
            }
            var doors = new List<Edge>(2);
            var horizontalDoor = SelectDoorEdge(
                gallery,
                blocked,
                occupied,
                vertical: false,
                seed);
            doors.Add(horizontalDoor);
            occupied.Add(horizontalDoor.First);
            occupied.Add(horizontalDoor.Second);

            var verticalDoor = SelectDoorEdge(
                gallery,
                blocked,
                occupied,
                vertical: true,
                seed + 17);
            doors.Add(verticalDoor);
            occupied.Add(verticalDoor.First);
            occupied.Add(verticalDoor.Second);

            var orderedObjects = Rotate(gallery, seed);
            var objects = new List<Cell>(objectCount);
            var footprints = new List<int>(objectCount);
            var objectIndex = 0;
            while (objects.Count < objectCount)
            {
                var footprintHeight = objectFootprintHeights == null
                    ? 1
                    : Math.Max(1, objectFootprintHeights[objects.Count]);
                objects.Add(NextCell(
                    orderedObjects,
                    floorSet,
                    occupied,
                    footprintHeight,
                    ref objectIndex));
                footprints.Add(footprintHeight);
            }

            var orderedEnemies = Rotate(gallery, seed + 7919);
            var enemies = new List<Cell>(enemyMinimumChebyshevDistances.Count);
            var objectAnchors = new List<Cell>(objects.Count + doors.Count * 2);
            for (var i = 0; i < objects.Count; i++)
            {
                objectAnchors.Add(objects[i]);
                for (var offset = 1; offset < footprints[i]; offset++)
                    objectAnchors.Add(new Cell(objects[i].X, objects[i].Y + offset));
            }
            foreach (var door in doors)
            {
                objectAnchors.Add(door.First);
                objectAnchors.Add(door.Second);
            }

            if (!TryPlaceEnemies(
                    orderedEnemies,
                    occupied,
                    objectAnchors,
                    enemyMinimumChebyshevDistances,
                    0,
                    enemies))
            {
                throw new InvalidOperationException(
                    "No audit gallery cell satisfies the requested enemy spacing.");
            }

            return new Plan(
                doors,
                objects,
                footprints,
                enemies,
                navigation,
                elevation.ElevatedCells,
                elevation.StairPath,
                elevation.GroundStructuralEdges,
                elevation.StructuralEdges,
                elevation.DaggerCell);
        }

        private readonly struct ElevationPlan
        {
            public ElevationPlan(
                IReadOnlyList<Cell> elevatedCells,
                IReadOnlyList<Cell> stairPath,
                IReadOnlyList<SurfaceEdge> groundStructuralEdges,
                IReadOnlyList<SurfaceEdge> structuralEdges,
                Cell daggerCell)
            {
                ElevatedCells = elevatedCells;
                StairPath = stairPath;
                GroundStructuralEdges = groundStructuralEdges;
                StructuralEdges = structuralEdges;
                DaggerCell = daggerCell;
            }

            public IReadOnlyList<Cell> ElevatedCells { get; }
            public IReadOnlyList<Cell> StairPath { get; }
            public IReadOnlyList<SurfaceEdge> GroundStructuralEdges { get; }
            public IReadOnlyList<SurfaceEdge> StructuralEdges { get; }
            public Cell DaggerCell { get; }
        }

        private static ElevationPlan BuildElevationPlan(ISet<Cell> floor)
        {
            var elevated = new[]
            {
                new Cell(8, 9), new Cell(9, 9), new Cell(10, 9), new Cell(11, 9)
            };
            if (elevated.Any(cell => !floor.Contains(cell)))
                throw new InvalidOperationException(
                    "Catacombs audit balcony requires the production room's north floor row.");

            var stairPath = new[]
            {
                new Cell(8, 5), new Cell(8, 6), new Cell(8, 7), new Cell(8, 8), new Cell(8, 9)
            };
            for (var i = 0; i < stairPath.Length; i++)
            {
                if (!floor.Contains(stairPath[i]))
                    throw new InvalidOperationException("Catacombs audit stairs leave the authored floor.");
                if (i > 0 && Math.Abs(stairPath[i].X - stairPath[i - 1].X)
                    + Math.Abs(stairPath[i].Y - stairPath[i - 1].Y) != 1)
                {
                    throw new InvalidOperationException("Catacombs audit stairs contain a non-cardinal step.");
                }
            }

            var groundStructuralEdges = new[]
            {
                new SurfaceEdge(new Cell(3, 4), new Cell(4, 4), 0, DungeonWallType.Low),
                new SurfaceEdge(new Cell(3, 4), new Cell(3, 5), 0, DungeonWallType.Low)
            };
            var structuralEdges = new[]
            {
                new SurfaceEdge(new Cell(8, 9), new Cell(7, 9), 1, DungeonWallType.Full),
                new SurfaceEdge(new Cell(8, 9), new Cell(8, 10), 1, DungeonWallType.Railing),
                new SurfaceEdge(new Cell(9, 9), new Cell(9, 10), 1, DungeonWallType.Railing),
                new SurfaceEdge(new Cell(10, 9), new Cell(10, 10), 1, DungeonWallType.Railing),
                new SurfaceEdge(new Cell(11, 9), new Cell(11, 10), 1, DungeonWallType.Railing),
                new SurfaceEdge(new Cell(11, 9), new Cell(12, 9), 1, DungeonWallType.Low)
            };
            var dagger = new Cell(3, 9);
            if (!floor.Contains(dagger))
                throw new InvalidOperationException("Catacombs audit dagger cell leaves the authored floor.");

            return new ElevationPlan(
                elevated,
                stairPath,
                groundStructuralEdges,
                structuralEdges,
                dagger);
        }

        private static Edge SelectDoorEdge(
            IReadOnlyList<Cell> gallery,
            ISet<Edge> blocked,
            ISet<Cell> occupied,
            bool vertical,
            int seed)
        {
            var deltaX = vertical ? 0 : 1;
            var deltaY = vertical ? 1 : 0;
            var candidates = new List<Edge>();
            foreach (var first in gallery)
            {
                var second = new Cell(first.X + deltaX, first.Y + deltaY);
                if (!gallery.Contains(second)) continue;
                var edge = new Edge(first, second);
                if (blocked.Contains(edge) || occupied.Contains(first) || occupied.Contains(second)) continue;
                candidates.Add(edge);
            }

            if (candidates.Count == 0)
                throw new InvalidOperationException("No free audit door edge exists.");
            return candidates[PositiveModulo(seed, candidates.Count)];
        }

        private static Cell NextCell(
            IReadOnlyList<Cell> cells,
            ISet<Cell> floor,
            ISet<Cell> occupied,
            int footprintHeight,
            ref int index)
        {
            while (index < cells.Count)
            {
                var cell = cells[index++];
                var footprint = Enumerable.Range(0, footprintHeight)
                    .Select(offset => new Cell(cell.X, cell.Y + offset))
                    .ToArray();
                if (footprint.Any(candidate => !floor.Contains(candidate)
                    || occupied.Contains(candidate))) continue;
                foreach (var candidate in footprint) occupied.Add(candidate);
                return cell;
            }

            throw new InvalidOperationException("No free audit gallery cell exists for the requested footprint.");
        }

        private static bool TryPlaceEnemies(
            IReadOnlyList<Cell> cells,
            ISet<Cell> occupied,
            IReadOnlyList<Cell> objectAnchors,
            IReadOnlyList<int> minimumDistances,
            int enemyIndex,
            IList<Cell> enemies)
        {
            if (enemyIndex >= minimumDistances.Count)
                return true;

            var minimumDistance = Math.Max(1, minimumDistances[enemyIndex]);
            for (var candidateIndex = 0; candidateIndex < cells.Count; candidateIndex++)
            {
                var candidate = cells[candidateIndex];
                if (occupied.Contains(candidate)) continue;

                if (enemyIndex == 0
                    && objectAnchors.Any(anchor =>
                        ChebyshevDistance(anchor, candidate) < minimumDistance))
                {
                    continue;
                }

                var tooClose = false;
                for (var existingIndex = 0; existingIndex < enemies.Count; existingIndex++)
                {
                    var requiredDistance = Math.Max(
                        minimumDistance,
                        Math.Max(1, minimumDistances[existingIndex]));
                    if (ChebyshevDistance(enemies[existingIndex], candidate) < requiredDistance)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;
                occupied.Add(candidate);
                enemies.Add(candidate);
                if (TryPlaceEnemies(
                        cells,
                        occupied,
                        objectAnchors,
                        minimumDistances,
                        enemyIndex + 1,
                        enemies))
                {
                    return true;
                }

                enemies.RemoveAt(enemies.Count - 1);
                occupied.Remove(candidate);
            }

            return false;
        }

        private static List<Cell> Rotate(IReadOnlyList<Cell> cells, int seed)
        {
            if (cells.Count == 0) return new List<Cell>();
            var offset = PositiveModulo(seed, cells.Count);
            return cells.Skip(offset).Concat(cells.Take(offset)).ToList();
        }

        private static int PositiveModulo(int value, int modulus)
        {
            var remainder = value == int.MinValue
                ? (long)int.MaxValue + 1L
                : Math.Abs((long)value);
            return (int)(remainder % modulus);
        }

        private static int ChebyshevDistance(Cell first, Cell second)
            => Math.Max(Math.Abs(first.X - second.X), Math.Abs(first.Y - second.Y));
    }
}
