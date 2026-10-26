using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Dungeons;
using UnityEngine;
using UnityEngine.AI;
using Unity.Profiling;

namespace Godmist.Views.Exploration
{
    // Owned by one party controller. The shadow XZ surface is query-only;
    // the existing 2D mover remains responsible for physics and traversal state.
    public sealed class DungeonNavigationMesh : IDisposable
    {
        private static readonly ProfilerMarker BakeSample = new("Godmist.Navigation.Bake");
        private NavMeshData data;
        private NavMeshDataInstance instance;
        private NavMeshQueryFilter filter;
        private readonly NavMeshPath query = new();
        private float plane;
        private AsyncOperation pendingBake;
        private List<NavMeshBuildSource> pendingSources;
        private long bakeStarted;
        private readonly SourceGeometry ownedGeometry = new();
        public bool IsBuilding => pendingBake != null && !pendingBake.isDone;
        public bool IsReady
        {
            get
            {
                if (pendingBake != null && pendingBake.isDone)
                {
                    NativeBakeMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - bakeStarted) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                    pendingBake = null;
                    pendingSources = null;
                    if (data != null) instance = NavMesh.AddNavMeshData(data, new Vector3(0, plane, 0), Quaternion.identity);
                }
                return instance.valid;
            }
        }
        public int PhysicalBoundsSignature { get; private set; }
        public int BuildSourceCount { get; private set; }
        public double GeometryBuildMilliseconds { get; private set; }
        public double NativeBakeMilliseconds { get; private set; }
        public double[] PreparationPhaseMilliseconds { get; } = new double[4];
        public int StaticGeometryBuildCount { get; private set; }
        public Bounds BuildBounds { get; private set; }

        public static int BoundsSignature(IEnumerable<Bounds> walls)
        {
            unchecked
            {
                var signature = 0;
                foreach (var wall in walls) signature += wall.GetHashCode();
                return signature;
            }
        }

        public bool Build(DungeonFloorInstance floor, DungeonFloorMapView view,
            int level, float radius, IEnumerable<Bounds> physicalWalls, bool background = false, SourceGeometry geometry = null)
        {
            using var sample = BakeSample.Auto();
            Dispose();
            var geometryStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            var phaseStarted = geometryStarted;
            void Phase(int index)
            {
                var now = System.Diagnostics.Stopwatch.GetTimestamp();
                PreparationPhaseMilliseconds[index] = (now - phaseStarted) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                phaseStarted = now;
            }
            // The input can perform a physics overlap query. Capture once for
            // both the signature and geometry instead of enumerating it twice.
            var physical = physicalWalls?.ToArray();
            PhysicalBoundsSignature = physical == null ? 0 : BoundsSignature(physical);
            Phase(0);
            if (floor?.Map == null || view == null || NavMesh.GetSettingsCount() == 0) return false;
            // Instance identity separates simultaneously loaded floors/controllers,
            // without global registration or changes to project agent settings.
            plane = -10000f - Mathf.Abs(view.GetInstanceID() % 1000) * 16f - level * 4f;
            var sources = new List<NavMeshBuildSource>();
            var x = view.CellToWorldCenter(Vector2Int.right) - view.CellToWorldCenter(Vector2Int.zero);
            var y = view.CellToWorldCenter(Vector2Int.up) - view.CellToWorldCenter(Vector2Int.zero);
            var size = new Vector2(x.magnitude, y.magnitude);
            if (size.x <= 0 || size.y <= 0) return false;
            geometry ??= ownedGeometry;
            // Bake near zero: far-away per-controller planes quantize the
            // .025m voxels differently as Unity instance IDs change. Apply the
            // isolation offset only when registering the completed local data.
            if (!geometry.AppendTo(floor.Map, view, level, 0, sources, out var bounds)) return false;
            StaticGeometryBuildCount = geometry.BuildCount;
            Phase(1);
            void Block(Bounds b)
                => sources.Add(Box(new Bounds(new Vector3(b.center.x, 1, b.center.y),
                    new Vector3(b.size.x, 2, b.size.y)), 1));
            var thickness = Mathf.Max(.05f, view.WallHalfThickness * 2);
            Bounds Edge(DungeonSurfaceEdge edge)
            {
                var a = view.CellToWorldCenter(edge.FirstCell);
                var b = view.CellToWorldCenter(edge.SecondCell);
                return new Bounds((a + b) * .5f, edge.FirstCell.x == edge.SecondCell.x
                    ? new Vector3(size.x, thickness, 0) : new Vector3(thickness, size.y, 0));
            }
            foreach (var edge in floor.Map.SurfaceClosedProgressionGateEdges)
                if (edge.Level == level) Block(Edge(edge));
            // These explicit edge sets contain every dynamic reason for two
            // adjacent floor cells to be disconnected. No floor-wide neighbour scan.
            foreach (var edge in floor.Map.SurfaceClosedDoorEdges)
                if (edge.Level == level && floor.Map.IsWalkable(edge.First) && floor.Map.IsWalkable(edge.Second)) Block(Edge(edge));
            if (physical != null) foreach (var b in physical) Block(b);
            if (level == 0)
                foreach (var obj in floor.Objects)
                {
                    if (obj is ObstacleDungeonObjectInstance obstacle && obstacle.IsActive && obstacle.BlocksMovement)
                    {
                        // Rubble keeps its one-cell tactical occupancy, but its
                        // Catacombs exploration collider is only half a cell
                        // high. NavigationBlockerBounds supplies that physical
                        // collider below; adding the logical cell here would
                        // erase the free upper half again.
                        if (!obstacle.UsesPhysicalExplorationFootprint)
                            foreach (var occupiedCell in obstacle.EnumerateOccupiedCells())
                                Block(new Bounds(view.CellToWorldCenter(occupiedCell), new Vector3(size.x, size.y, 0)));
                    }
                    if (obj is DoorDungeonObjectInstance door && door.IsActive && !door.IsOpen)
                        Block(Edge(new DungeonSurfaceEdge(door.Edge)));
                }
            Phase(2);
            var settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = radius + (view.UsesVegetationPresentation ? .02f : .10f);
            settings.agentHeight = 1;
            settings.agentClimb = .05f;
            settings.agentSlope = 0;
            settings.overrideVoxelSize = true;
            settings.voxelSize = .025f;
            settings.overrideTileSize = true;
            settings.tileSize = 128;
            settings.minRegionArea = 0;
            bounds.Expand(new Vector3(2, 4, 2));
            BuildSourceCount = sources.Count; BuildBounds = bounds;
            GeometryBuildMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - geometryStarted) * 1000d / System.Diagnostics.Stopwatch.Frequency;
            bakeStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            NativeBakeMilliseconds = 0;
            filter = new NavMeshQueryFilter { agentTypeID = settings.agentTypeID, areaMask = 1 };
            if (background)
            {
                // No stale mesh remains queryable while a gate closes. The
                // party uses its validated logical routes until this completes.
                data = new NavMeshData(settings.agentTypeID) { hideFlags = HideFlags.DontSave };
                pendingSources = sources;
                pendingBake = NavMeshBuilder.UpdateNavMeshDataAsync(data, settings, pendingSources, bounds);
                Phase(3);
                return IsReady;
            }
            data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            NativeBakeMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - bakeStarted) * 1000d / System.Diagnostics.Stopwatch.Frequency;
            if (data == null) return false;
            data.hideFlags = HideFlags.DontSave;
            instance = NavMesh.AddNavMeshData(data, new Vector3(0, plane, 0), Quaternion.identity);
            return IsReady;
        }

        // Controller-owned and shared by agents of different radii on one level.
        // Numeric sources survive gate changes without per-wall hierarchy queries.
        public sealed class SourceGeometry
        {
            private DungeonFloorMap map;
            private DungeonFloorMapView view;
            private int revision, level;
            private float plane, thickness;
            private Vector2 origin, right, up;
            private Bounds bounds;
            private bool hasFloor;
            private readonly List<NavMeshBuildSource> sources = new();
            public int BuildCount { get; private set; }

            internal bool AppendTo(DungeonFloorMap current, DungeonFloorMapView presentation, int currentLevel,
                float currentPlane, List<NavMeshBuildSource> destination, out Bounds buildBounds)
            {
                var o = presentation.CellToWorldCenter(Vector2Int.zero);
                var r = presentation.CellToWorldCenter(Vector2Int.right);
                var u = presentation.CellToWorldCenter(Vector2Int.up);
                var t = Mathf.Max(.05f, presentation.WallHalfThickness * 2);
                if (!ReferenceEquals(map,current) || view != presentation || revision != current.StaticGeometryRevision
                    || level != currentLevel || plane != currentPlane || origin != o || right != r || up != u || thickness != t)
                {
                    map=current; view=presentation; revision=current.StaticGeometryRevision; level=currentLevel;
                    plane=currentPlane; origin=o; right=r; up=u; thickness=t;
                    sources.Clear(); hasFloor=false; BuildCount++;
                    var size = new Vector2((r-o).magnitude,(u-o).magnitude);
                    foreach (var rectangle in FloorRectangles(current,level))
                    {
                        var first=view.CellToWorldCenter(rectangle.min);
                        var last=view.CellToWorldCenter(rectangle.max-Vector2Int.one);
                        var center=(first+last)*.5f;
                        var b=new Bounds(new Vector3(center.x,plane-.05f,center.y),
                            new Vector3(size.x*rectangle.width,.1f,size.y*rectangle.height));
                        sources.Add(Box(b,0));
                        if (!hasFloor) { bounds=b;hasFloor=true; } else bounds.Encapsulate(b);
                    }
                    foreach (var edge in current.SurfaceWallEdges)
                    {
                        if (edge.Level!=level || current.GetWallType(edge)==Godmist.Core.Enums.DungeonWallType.TreeTrunk) continue;
                        var a=view.CellToWorldCenter(edge.FirstCell);var b=view.CellToWorldCenter(edge.SecondCell);
                        var center=(a+b)*.5f;
                        sources.Add(Box(new Bounds(new Vector3(center.x,plane+1,center.y),
                            edge.FirstCell.x==edge.SecondCell.x?new Vector3(size.x,2,thickness):new Vector3(thickness,2,size.y)),1));
                    }
                }
                buildBounds=bounds;
                destination.AddRange(sources);
                return hasFloor;
            }
        }

        private static IEnumerable<RectInt> FloorRectangles(DungeonFloorMap map, int level)
        {
            var rectangles = new List<RectInt>();
            var previous = new Dictionary<(int X, int Width), int>();
            var previousY = int.MinValue;
            foreach (var row in map.Surfaces.Where(s => s.Level == level).Select(s => s.Cell)
                .GroupBy(c => c.y).OrderBy(g => g.Key))
            {
                if (row.Key != previousY + 1) previous.Clear();
                var current = new Dictionary<(int X, int Width), int>();
                var columns = row.Select(c => c.x).OrderBy(x => x).ToArray();
                for (var i = 0; i < columns.Length; i++)
                {
                    var start = columns[i];
                    var end = start;
                    while (i + 1 < columns.Length && columns[i + 1] == end + 1) end = columns[++i];
                    var key = (start, end - start + 1);
                    if (previous.TryGetValue(key, out var index))
                    {
                        var rectangle = rectangles[index]; rectangle.height++;
                        rectangles[index] = rectangle; current[key] = index;
                    }
                    else
                    {
                        current[key] = rectangles.Count;
                        rectangles.Add(new RectInt(start, row.Key, key.Item2, 1));
                    }
                }
                previous = current; previousY = row.Key;
            }
            return rectangles;
        }

        public bool TryFindPath(Vector2 start, Vector2 target, Vector2 colliderOffset,
            Func<Vector2, Vector2, bool> isSafeSegment, out List<Vector2> points)
        {
            points = new List<Vector2>();
            if (!IsReady) return false;
            Vector3 ToNav(Vector2 p) => new Vector3(p.x + colliderOffset.x, plane, p.y + colliderOffset.y);
            Vector2 FromNav(Vector3 p) => new Vector2(p.x, p.z) - colliderOffset;
            if (!NavMesh.SamplePosition(ToNav(start), out var from, .35f, filter)
                || !NavMesh.SamplePosition(ToNav(target), out var to, .35f, filter)
                || !NavMesh.CalculatePath(from.position, to.position, filter, query)
                || query.status != NavMeshPathStatus.PathComplete) return false;
            if (isSafeSegment(start, target))
            {
                points.Add(target);
                return true;
            }
            var previous = start;
            foreach (var corner in query.corners)
            {
                var point = FromNav(corner);
                if ((point - previous).sqrMagnitude < .000001f) continue;
                if (!isSafeSegment(previous, point)) { points.Clear(); return false; }
                points.Add(point); previous = point;
            }
            // Sampling may only add a safe connector; it never silently changes
            // the click/follower goal or teleports an actor out of a wall.
            if (!isSafeSegment(previous, target)) { points.Clear(); return false; }
            if ((previous - target).sqrMagnitude > .000001f || points.Count == 0) points.Add(target);
            else points[points.Count - 1] = target;
            return true;
        }

        private static NavMeshBuildSource Box(Bounds b, int area)
        {
            // Translation only: constructing this directly avoids thousands
            // of native TRS calls when submitting static terrain blockers.
            var matrix=Matrix4x4.identity;
            matrix.m03=b.center.x;matrix.m13=b.center.y;matrix.m23=b.center.z;
            return new NavMeshBuildSource { shape=NavMeshBuildSourceShape.Box,transform=matrix,size=b.size,area=area };
        }

        public void Dispose()
        {
            if (pendingBake != null && data != null) NavMeshBuilder.Cancel(data);
            pendingBake = null;
            pendingSources = null;
            if (instance.valid) instance.Remove();
            instance = default;
            if (data != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(data);
                else UnityEngine.Object.DestroyImmediate(data);
            }
            data = null;
        }
    }
}
