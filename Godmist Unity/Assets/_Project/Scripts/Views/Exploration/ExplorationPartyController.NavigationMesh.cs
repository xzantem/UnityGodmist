using System.Collections.Generic;
using System.Linq;
using Godmist.Runtime.Dungeons;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Godmist.Views.Exploration
{
    public partial class ExplorationPartyController
    {
        private readonly Dictionary<(int Level, float Radius), DungeonNavigationMesh> navigationMeshes = new();
        private readonly Dictionary<ExplorationCharacterView, MeshRoute> meshRoutes = new();
        private int meshRevision;
        private bool hasMeshRevision;
        private bool deferNavigationBuilds;
        private readonly Dictionary<int,DungeonNavigationMesh.SourceGeometry> navigationSourceGeometry = new();
        private readonly Dictionary<int,List<Collider2D>> externalNavigationColliders = new();
        private int externalNavigationGeometryRevision = -1;
        private Vector2 externalNavigationOrigin, externalNavigationRight, externalNavigationUp;
        private Bounds externalNavigationBounds;
        public int NavigationExternalCollectionCount { get; private set; }

        private DungeonNavigationMesh GetNavigationMesh(ExplorationCharacterView actor, int level)
        {
            var revision = CurrentTraversalNavigationRevision();
            if (!hasMeshRevision || meshRevision != revision)
            {
                if (hasMeshRevision) deferNavigationBuilds = true;
                foreach (var oldMesh in navigationMeshes.Values) oldMesh.Dispose();
                navigationMeshes.Clear(); meshRevision = revision; hasMeshRevision = true;
            }
            var key = (level, actor.MovementRadius);
            if (!navigationMeshes.TryGetValue(key, out var mesh))
            {
                mesh = new DungeonNavigationMesh(); navigationMeshes.Add(key, mesh);
                Physics2D.SyncTransforms();
                if (!navigationSourceGeometry.TryGetValue(level,out var geometry))
                    navigationSourceGeometry.Add(level,geometry=new DungeonNavigationMesh.SourceGeometry());
                mesh.Build(traversalFloor, traversalMapView, level, actor.MovementRadius,
                    NavigationBlockerBounds(actor, level), background: deferNavigationBuilds, geometry: geometry);
            }
            return mesh;
        }

        private void PrepareNavigationMeshes()
        {
            if (traversalFloor?.Map == null || traversalMapView == null) return;
            // Pay the initial bake during floor setup, not the first follower
            // FixedUpdate. Queries reuse the mesh until topology changes.
            foreach (var actor in viewsById.Values)
                if (actor != null && !actor.DungeonPosition.IsOnStairs)
                    GetNavigationMesh(actor, actor.DungeonPosition.Surface.Level);
            deferNavigationBuilds = hasMeshRevision;
        }
        private sealed class MeshRoute
        {
            public readonly Queue<Vector2> Points;
            public readonly DungeonTraversalPosition Target;
            public readonly int Revision;
            public readonly List<DungeonTraversalPosition> LogicalPath;
            public MeshRoute(List<Vector2> points, DungeonTraversalPosition target, int revision,
                List<DungeonTraversalPosition> logicalPath)
            { Points = new Queue<Vector2>(points); Target = target; Revision = revision; LogicalPath = logicalPath; }
        }

        private void DisposeNavigationMeshes()
        {
            foreach (var mesh in navigationMeshes.Values) mesh.Dispose();
            navigationMeshes.Clear(); meshRoutes.Clear(); hasMeshRevision = false; deferNavigationBuilds = false;
            navigationSourceGeometry.Clear(); externalNavigationColliders.Clear();
            externalNavigationGeometryRevision=-1;
        }

        // A complete native route is authoritative. Failure may need authored
        // transitions (stairs, low-wall crossing or opening a closed door).
        private bool TryPlanMeshRoute(ExplorationCharacterView actor, DungeonTraversalPosition target,
            Vector2? targetWorld, out List<DungeonTraversalPosition> path, out bool handled)
        {
            using var sample = MeshPlanSample.Auto();
            path = null; handled = false;
            if (actor == null || traversalFloor?.Map == null || traversalMapView == null) return false;
            meshRoutes.Remove(actor);
            var start = actor.DungeonPosition;
            if (start.IsOnStairs || target.IsOnStairs || start.Surface.Level != target.Surface.Level
                // A narrow trunk does not block its entire logical cell.
                // Keep native routing when the actor stands beside its roots.
                || !CanEnterTraversalAtWorld(actor, start, actor.Position)
                || (!targetWorld.HasValue
                    ? !CanEnterTraversal(actor, target)
                    : !CanEnterTraversalAtWorld(actor, target, targetWorld.Value))) return false;
            var revision = CurrentTraversalNavigationRevision();
            var key = (start.Surface.Level, actor.MovementRadius);
            var mesh = GetNavigationMesh(actor, key.Item1);
            if (!mesh.IsReady) return false;
            var goal = targetWorld ?? TraversalAnchorWorld(actor, target.Surface.Cell);
            bool Safe(Vector2 from, Vector2 to)
            {
                var a = new DungeonTraversalPosition(new DungeonSurfacePosition(traversalMapView.WorldToCell(from), key.Item1));
                var b = new DungeonTraversalPosition(new DungeonSurfacePosition(traversalMapView.WorldToCell(to), key.Item1));
                return CanNavigateDirectly(actor, a, from, b, to);
            }
            var found = mesh.TryFindPath(actor.Position, goal, actor.MovementColliderCenterOffset, Safe, out var points);
            if (!found)
            {
                // Props can move without changing the authored floor revision.
                // Only a failed query needs this physical geometry comparison.
                var blockers = CollectNavigationBlockerBounds(actor, key.Item1, refreshExternal: true).ToList();
                if (DungeonNavigationMesh.BoundsSignature(blockers) != mesh.PhysicalBoundsSignature)
                {
                    mesh.Build(traversalFloor, traversalMapView, key.Item1, key.Item2, blockers, background: true,
                        geometry: navigationSourceGeometry[key.Item1]);
                    return false;
                }
            }
            if (found)
            {
                // The logical queue retains route ownership and target identity;
                // execution consumes the exact native corners, not cell centres.
                path = new List<DungeonTraversalPosition> { start };
                foreach (var point in points)
                    path.Add(new DungeonTraversalPosition(new DungeonSurfacePosition(traversalMapView.WorldToCell(point), key.Item1)));
                path[path.Count - 1] = target;
                meshRoutes[actor] = new MeshRoute(points, target, revision, path);
                handled = true;
                return true;
            }
            handled = !traversalFloor.Map.Stairs.Any()
                && !traversalFloor.Map.SurfaceWallTypes.Values.Any(t => t == Godmist.Core.Enums.DungeonWallType.Low)
                && !navigationDoors.Values.Any(d => d.IsActive && !d.IsOpen
                    && d.InteractionMode != Godmist.Core.Enums.DoorInteractionMode.LeverOnly);
            path = new List<DungeonTraversalPosition>();
            return false;
        }

        private IEnumerable<Bounds> NavigationBlockerBounds(ExplorationCharacterView actor, int level)
            => CollectNavigationBlockerBounds(actor,level,false);

        private IEnumerable<Bounds> CollectNavigationBlockerBounds(ExplorationCharacterView actor, int level, bool refreshExternal)
        {
            foreach (var b in traversalMapView.CachedNavigationWallBounds(level, refreshExternal)) yield return b;
            // Ground movement also collides with props outside the generated
            // wall hierarchy. Collect once per bake, not once per search node.
            if (level != 0) yield break;
            var origin=traversalMapView.CellToWorldCenter(Vector2Int.zero);
            var right=traversalMapView.CellToWorldCenter(Vector2Int.right);
            var up=traversalMapView.CellToWorldCenter(Vector2Int.up);
            if (externalNavigationGeometryRevision!=traversalFloor.Map.StaticGeometryRevision
                || externalNavigationOrigin!=origin || externalNavigationRight!=right || externalNavigationUp!=up)
            {
                externalNavigationColliders.Clear();
                externalNavigationGeometryRevision=traversalFloor.Map.StaticGeometryRevision;
                externalNavigationOrigin=origin;externalNavigationRight=right;externalNavigationUp=up;
                var any=false;
                foreach(var s in traversalFloor.Map.Surfaces)
                {
                    if(s.Level!=0)continue;
                    var b=new Bounds(traversalMapView.CellToWorldCenter(s.Cell),Vector3.one*2);
                    if(!any){externalNavigationBounds=b;any=true;}else externalNavigationBounds.Encapsulate(b);
                }
                if(!any)yield break;
            }
            var layer=actor.gameObject.layer;
            if(refreshExternal || !externalNavigationColliders.TryGetValue(layer,out _))
            {
                var hits=new List<Collider2D>();
                var contact=new ContactFilter2D { useTriggers=false };
                contact.SetLayerMask(Physics2D.GetLayerCollisionMask(layer));
                gameObject.scene.GetPhysicsScene2D().OverlapBox(externalNavigationBounds.center,externalNavigationBounds.size,0,contact,hits);
                NavigationExternalCollectionCount++;
                // The map's own indexed colliders were already collected above.
                // Cache only props, never actors or duplicate wall hierarchies.
                hits.RemoveAll(hit=>hit==null || traversalMapView.OwnsNavigationCollider(hit)
                    || hit.GetComponentInParent<ExplorationCharacterView>()!=null);
                externalNavigationColliders[layer]=hits;
            }
            foreach (var hit in externalNavigationColliders[layer])
                if (hit != null && hit.enabled && !hit.isTrigger
                    && hit.gameObject.activeInHierarchy)
                    yield return hit.bounds;
        }

        private bool AdvanceMeshRoute(ExplorationCharacterView actor, Queue<DungeonTraversalPosition> logicalRoute,
            MeshRoute route, bool canReplan = true)
        {
            if (route.Revision != CurrentTraversalNavigationRevision())
            {
                if (!canReplan) return false;
                var goal = route.Points.Count > 0 ? route.Points.Last() : actor.Position;
                if (!TryPlanMeshRoute(actor, route.Target, goal, out var replanned, out _)) return false;
                logicalRoute.Clear();
                foreach (var step in replanned.Skip(1)) logicalRoute.Enqueue(step);
                return AdvanceMeshRoute(actor, logicalRoute, meshRoutes[actor], false);
            }
            while (route.Points.Count > 0)
            {
                var waypoint = route.Points.Peek();
                if (Vector2.Distance(actor.Position, waypoint) <= .03f)
                { route.Points.Dequeue(); continue; }
                var next = new DungeonTraversalPosition(new DungeonSurfacePosition(
                    traversalMapView.WorldToCell(waypoint), route.Target.Surface.Level));
                if (!CanNavigateDirectly(actor, actor.DungeonPosition, actor.Position, next, waypoint))
                {
                    if (!canReplan) return false;
                    var goal = route.Points.Last();
                    if (!TryPlanMeshRoute(actor, route.Target, goal, out var replanned, out _)) return false;
                    logicalRoute.Clear();
                    foreach (var step in replanned.Skip(1)) logicalRoute.Enqueue(step);
                    return AdvanceMeshRoute(actor, logicalRoute, meshRoutes[actor], false);
                }
                var steering = next;
                if (actor.UsesLayeredTraversal && next != actor.DungeonPosition
                    && !traversalFloor.Map.GetTraversalNeighbours(actor.DungeonPosition, CanOpenNavigationDoor).Contains(next)
                    && !TryGetImmediateTraversalStep(actor, next, out steering)) return false;
                actor.PreferredTraversalDestination = steering;
                actor.MoveTowards(waypoint, movementSpeed * Time.fixedDeltaTime, 0);
                return true;
            }
            logicalRoute.Clear(); meshRoutes.Remove(actor); actor.PreferredTraversalDestination = null;
            return true;
        }
    }
}
