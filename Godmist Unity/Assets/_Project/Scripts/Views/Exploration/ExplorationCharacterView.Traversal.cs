using System;
using System.Linq;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Combat;
using UnityEngine;

namespace Godmist.Views.Exploration
{
    public partial class ExplorationCharacterView
    {
        private DungeonFloorInstance terrainFloor;
        private DungeonCombatContext combatContext;
        private DungeonTraversalPosition? localTraversalPosition;
        public bool UsesLayeredTraversal => terrainMap?.HasElevatedSurfaces == true && terrainView != null;
        public DungeonTraversalPosition? PreferredTraversalDestination { get; set; }
        public Func<DungeonTraversalPosition, bool> CanEnterTraversal { get; set; }
        private string traversalActorKey;
        private string TraversalActorId => traversalActorKey ?? (string.IsNullOrEmpty(EnemyInstanceId) ? "player:" + CharacterId : "enemy:" + EnemyInstanceId);

        public DungeonTraversalPosition DungeonPosition
        {
            get
            {
                if (CombatUnit != null && combatContext != null)
                    return DungeonBattleGridFactory.ToDungeonTraversalPosition(combatContext, CombatUnit.Position);
                if (UsesLayeredTraversal && localTraversalPosition.HasValue) return localTraversalPosition.Value;
                return new DungeonTraversalPosition(new DungeonSurfacePosition(terrainView != null ? terrainView.WorldToCell(Position) : Vector2Int.FloorToInt(Position)));
            }
        }

        public int PresentationLevel
        {
            get
            {
                var position = DungeonPosition;
                return terrainMap != null && terrainMap.TryGetTraversalHeight(position, out var height)
                    ? Mathf.CeilToInt(height) : 0;
            }
        }

        private void StoreTraversalPosition(DungeonTraversalPosition position)
        {
            localTraversalPosition = position;
            if (terrainFloor != null && !string.IsNullOrWhiteSpace(TraversalActorId))
                terrainFloor.TrySetActorPosition(TraversalActorId, position);
        }

        private void RestoreTraversalPosition()
        {
            localTraversalPosition = null;
            if (terrainMap == null || terrainView == null
                || !UsesLayeredTraversal && !TraversalActorId.StartsWith("enemy:", StringComparison.Ordinal)) return;
            if (terrainFloor != null && !string.IsNullOrWhiteSpace(TraversalActorId)
                && terrainFloor.TryGetActorPosition(TraversalActorId, out var saved))
            {
                var savedOffset = terrainFloor.GetActorOffset(TraversalActorId);
                StoreTraversalPosition(saved);
                terrainFloor.SetActorOffset(TraversalActorId, savedOffset);
                terrainMap.TryGetTraversalCell(saved, out var cell);
                transform.position = terrainView.CellToWorldCenter(cell) + savedOffset;
                if (body != null) body.position = transform.position;
            }
            else
            {
                var current = DungeonPosition;
                if (terrainMap.TryGetTraversalCell(current, out _)) StoreTraversalPosition(current);
            }
            if (UsesLayeredTraversal)
            {
                if (body != null) body.simulated = false;
                if (movementCollider != null) movementCollider.enabled = false;
            }
        }


        private void MoveLayeredDirection(Vector2 offset)
        {
            if (IsCrossingLowWall || offset.sqrMagnitude <= .000001f) return;
            if (!DungeonPosition.IsOnStairs && DungeonPosition.Surface.Level == 0
                && terrainMap.IsDifficultTerrain(DungeonPosition.Surface.Cell)) offset *= .5f;
            var start = Position;
            // Bound each collision query to neighbouring cells without quantizing the world position.
            var size = Vector2.Distance(terrainView.CellToWorldCenter(Vector2Int.zero), terrainView.CellToWorldCenter(Vector2Int.right));
            var count = Mathf.Max(1, Mathf.CeilToInt(offset.magnitude / (size * .2f)));
            var delta = offset / count;
            for (var i = 0; i < count; i++)
            {
                MoveFreeAxis(new Vector2(delta.x, 0));
                if (IsCrossingLowWall) break;
                MoveFreeAxis(new Vector2(0, delta.y));
                if (IsCrossingLowWall) break;
            }
            if (Position == start) return;
            if (body != null) body.position = Position;
            lastMovementTime = Time.time; SetFacingDirection(Position - start); PlayRunAnimation();
            StoreTraversalPosition(DungeonPosition);
            terrainMap.TryGetTraversalCell(DungeonPosition, out var cell);
            terrainFloor?.SetActorOffset(TraversalActorId, Position - (Vector2)terrainView.CellToWorldCenter(cell));
        }

        private void MoveFreeAxis(Vector2 delta)
        {
            if (delta.sqrMagnitude == 0) return;
            var target = Position + delta;
            var cell = terrainView.WorldToCell(target);
            var current = DungeonPosition;
            if (!terrainMap.TryGetTraversalCell(current, out var previous)) return;
            if (!FitsTraversalCollider(current, target)) return;
            if (cell == previous) { transform.position = target; return; }
            DungeonTraversalPosition? selected = null;
            foreach (var next in terrainMap.GetTraversalNeighbours(current))
            {
                if (!terrainMap.TryGetTraversalCell(next, out var nextCell) || nextCell != cell) continue;
                if (!selected.HasValue) selected = next;
                if (PreferredTraversalDestination == next) { selected = next; break; }
                // Authored stair landings choose the stair independent of the camera layer.
                if (next.IsOnStairs || (!current.IsOnStairs && next.Surface.Level != current.Surface.Level)) selected = next;
            }
            if (!selected.HasValue || CanEnterTraversal?.Invoke(selected.Value) == false) return;
            var landing = !current.IsOnStairs ? current.Surface : !selected.Value.IsOnStairs ? selected.Value.Surface : (DungeonSurfacePosition?)null;
            if (landing.HasValue && terrainMap.GetWallType(new DungeonSurfaceEdge(new DungeonCellEdge(previous, cell), landing.Value.Level)) == Godmist.Core.Enums.DungeonWallType.Low)
            {
                crossingStart = Position; crossingEnd = terrainView.CellToWorldCenter(cell); crossingElapsed = 0;
                restoreBody = false; restoreCollider = false; IsCrossingLowWall = true;
                StoreTraversalPosition(selected.Value);
                return;
            }
            StoreTraversalPosition(selected.Value);
            transform.position = target;
        }

        private static readonly Vector2Int[] collisionDirections = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        private readonly System.Collections.Generic.List<DungeonTraversalPosition> collisionNodes = new();
        private readonly System.Collections.Generic.List<DungeonTraversalPosition> collisionNeighbours = new();
        private bool FitsTraversalCollider(DungeonTraversalPosition current, Vector2 target)
        {
            // The exploration prefab uses a circle. Read its authored footprint even while
            // its physics component is disabled: stacked levels share world X/Y.
            if (movementCollider is not CircleCollider2D circle) return true;
            var scale = circle.transform.lossyScale;
            var radius = circle.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            var origin = (Vector2)circle.transform.TransformPoint(circle.offset);
            var proposed = origin + target - Position;
            collisionNodes.Clear(); collisionNodes.Add(current);
            foreach (var neighbour in terrainMap.GetTraversalNeighbours(current))
                if (current.IsOnStairs || (!neighbour.IsOnStairs && neighbour.Surface.Level == current.Surface.Level)) collisionNodes.Add(neighbour);
            foreach (var node in collisionNodes)
            {
                terrainMap.TryGetTraversalCell(node, out var at);
                collisionNeighbours.Clear(); collisionNeighbours.AddRange(terrainMap.GetTraversalNeighbours(node));
                foreach (var direction in collisionDirections)
                {
                    var beyond = at + direction;
                    var open = false;
                    foreach (var neighbour in collisionNeighbours)
                        if (terrainMap.TryGetTraversalCell(neighbour, out var c) && c == beyond && CanEnterTraversal?.Invoke(neighbour) != false)
                        { open = true; break; }
                    if (open) continue;
                    var center = terrainView.CellToWorldCenter(at);
                    var across = terrainView.CellToWorldCenter(beyond);
                    var tangent = new Vector2(-direction.y, direction.x);
                    var halfSize = Vector2.Distance(center, across) * .5f;
                    var middle = (center + across) * .5f;
                    var a = middle - tangent * halfSize;
                    var b = middle + tangent * halfSize;
                    var padding = radius + terrainView.WallHalfThickness;
                    float Distance(Vector2 p) => Vector2.Distance(p, a + (b - a) * Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude));
                    if (Distance(proposed) < padding && Distance(proposed) < Distance(origin) - .00001f) return false;
                }
            }
            return true;
        }
    }
}
