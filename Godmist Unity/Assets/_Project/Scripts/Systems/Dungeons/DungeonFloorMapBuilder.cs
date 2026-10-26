using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Systems.Dungeons
{
    public class DungeonFloorMapBuilder
    {
        private static readonly Vector2Int[] CardinalDirections =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        public DungeonFloorMap Build(
            DungeonFloorInstance floor,
            DungeonRoomTemplateCatalog catalog)
        {
            if (floor == null)
            {
                throw new ArgumentNullException(nameof(floor));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            var templatesById = BuildTemplateLookup(catalog);
            var map = new DungeonFloorMap();

            AddRoomFloorCells(floor, templatesById, map);
            foreach (var connection in floor.Connections)
                foreach (var cell in connection.CorridorCells)
                    if (!map.TryAddFloorCell(cell, connection.SecondRoomId))
                        throw new InvalidOperationException($"Corridor overlaps room or corridor at {cell}.");
            if (catalog.HasVegetationFringe) AddVegetationFringe(map);

            var openEdges = BuildOpenEdges(floor, map);

            AddWallEdges(map, openEdges);
            AddStructuralWalls(floor, templatesById, map);
            if (catalog.HasVegetationFringe && catalog.UseRoutedConnections)
                AddForestPaths(floor, templatesById, map);

            return map;
        }

        private static void AddForestPaths(DungeonFloorInstance floor,
            Dictionary<string, DungeonRoomTemplateDefinition> templates, DungeonFloorMap map)
        {
            foreach (var connection in floor.Connections)
                foreach (var cell in connection.CorridorCells) map.AddVisualPathCell(cell);
            foreach (var room in floor.Rooms)
            {
                var template = templates[room.TemplateId];
                Vector2Int Transform(Vector2Int cell) => room.LayoutPosition +
                    DungeonRoomTransformUtility.TransformCell(cell, template.SizeInCells, room.RotationDegrees);
                var cells = template.EnumerateSurfaces().Where(s => s.Level == 0).Select(s => Transform(s.LocalCell)).ToHashSet();
                // Physical props are authored around this access network. Keep
                // their complete footprint out of the worn ground beneath them.
                foreach (var anchor in template.SemanticAnchors)
                    if (anchor.ObjectDefinition is ObstacleDungeonObjectDefinition obstacle && obstacle.BlocksMovement)
                        foreach (var cell in obstacle.EnumerateOccupiedCells(Transform(anchor.LocalCell))) cells.Remove(cell);
                var targets = floor.Connections.Where(c => c.FirstRoomId == room.RoomId || c.SecondRoomId == room.RoomId)
                    .Select(c => c.GetEntranceCell(room.RoomId) +
                        (c.SecondDoorCell.x != c.FirstDoorCell.x ? Vector2Int.up : Vector2Int.right) * (c.DoorwayWidth / 2))
                    .Where(cells.Contains).Distinct().ToList();
                if (targets.Count == 0) continue;
                var network = new HashSet<Vector2Int> { targets[0] };
                // Join entrances directly, then grow only short spurs towards
                // actual interaction targets. No compulsory room-centre hub.
                targets = targets.Skip(1).OrderByDescending(c => (c - network.First()).sqrMagnitude).ToList();
                foreach (var anchor in template.SemanticAnchors.Where(a => a.Type == DungeonSemanticAnchorType.Chest
                    || a.Type == DungeonSemanticAnchorType.Lever || a.Type == DungeonSemanticAnchorType.Condition))
                    if (cells.Contains(Transform(anchor.LocalCell))) targets.Add(Transform(anchor.LocalCell));
                foreach (var target in targets)
                {
                    var parents = new Dictionary<Vector2Int, Vector2Int> { [target] = target };
                    var queue = new Queue<Vector2Int>(); queue.Enqueue(target);
                    Vector2Int? joined = null;
                    while (queue.Count > 0)
                    {
                        var cell = queue.Dequeue();
                        if (network.Contains(cell)) { joined = cell; break; }
                        var turn = (int)((uint)(cell.x * 73856093 ^ cell.y * 19349663) % 4);
                        for (var i = 0; i < 4; i++)
                        {
                            var next = cell + CardinalDirections[(i + turn) % 4];
                            if (cells.Contains(next) && map.CanMoveBetween(cell, next, true) && parents.TryAdd(next, cell)) queue.Enqueue(next);
                        }
                    }
                    if (!joined.HasValue) throw new InvalidOperationException($"Authored props block the forest path in {room.TemplateId}.");
                    var step = joined.Value;
                    while (true) { network.Add(step); if (step == target) break; step = parents[step]; }
                }
                foreach (var cell in network) map.AddVisualPathCell(cell);
            }
        }

        private static void AddVegetationFringe(DungeonFloorMap map)
        {
            // One shallow, non-recursive fringe. Never bridge different rooms,
            // corridors or narrow islands; the added cells belong to their glade
            // and therefore participate in every grid/nav/combat query and save.
            var original = new HashSet<Vector2Int>(map.FloorCells);
            if (original.Count == 0) return;
            var minX = original.Min(c => c.x) - 1;
            var maxX = original.Max(c => c.x) + 1;
            var minY = original.Min(c => c.y) - 1;
            var maxY = original.Max(c => c.y) + 1;
            var exterior = new HashSet<Vector2Int>();
            var pending = new Queue<Vector2Int>();
            pending.Enqueue(new Vector2Int(minX, minY));
            while (pending.Count > 0)
            {
                var cell = pending.Dequeue();
                if (cell.x < minX || cell.x > maxX || cell.y < minY || cell.y > maxY
                    || original.Contains(cell) || !exterior.Add(cell)) continue;
                foreach (var direction in CardinalDirections) pending.Enqueue(cell + direction);
            }
            var candidates = original.SelectMany(c => CardinalDirections.Select(d => c + d))
                .Where(exterior.Contains).Distinct().OrderBy(c => c.y).ThenBy(c => c.x).ToArray();
            foreach (var cell in candidates)
            {
                var neighbours = CardinalDirections.Select(d => cell + d).Where(original.Contains).ToArray();
                if (neighbours.Length == 0 || neighbours.Length > 2) continue;
                var nearbyOwners = new HashSet<string>();
                for (var y = -1; y <= 1; y++)
                    for (var x = -1; x <= 1; x++)
                        if (original.Contains(cell + new Vector2Int(x, y))
                            && map.TryGetRoomId(cell + new Vector2Int(x, y), out var owner)) nearbyOwners.Add(owner);
                if (nearbyOwners.Count != 1) continue;
                map.TryAddFloorCell(cell, nearbyOwners.Single());
                // These cells are part of the playable clearing. Mature trees
                // belong beyond its final boundary, never on this walkable band.
            }
        }

        private static Dictionary<string, DungeonRoomTemplateDefinition>
            BuildTemplateLookup(DungeonRoomTemplateCatalog catalog)
        {
            var templatesById =
                new Dictionary<string, DungeonRoomTemplateDefinition>();

            foreach (var template in catalog.AllTemplates)
            {
                if (template == null
                    || string.IsNullOrWhiteSpace(template.Id))
                {
                    continue;
                }

                if (!templatesById.TryAdd(template.Id, template))
                {
                    throw new InvalidOperationException(
                        $"Powtorzony RoomTemplateId '{template.Id}'.");
                }
            }

            return templatesById;
        }

        private static void AddRoomFloorCells(
            DungeonFloorInstance floor,
            Dictionary<string, DungeonRoomTemplateDefinition> templatesById,
            DungeonFloorMap map)
        {
            foreach (var room in floor.Rooms)
            {
                if (!templatesById.TryGetValue(
                        room.TemplateId,
                        out var template))
                {
                    throw new InvalidOperationException(
                        $"Brak szablonu '{room.TemplateId}' dla pokoju '{room.RoomId}'.");
                }

                var footprint = template.Footprint ?? new DungeonRoomFootprint();
                footprint.Validate(
                    template.SizeInCells,
                    template.Id);

                template.ValidateSurfaceGeometry();
                foreach (var surface in template.EnumerateSurfaces())
                {
                    var cell = TransformSurface(surface, template, room);

                    if (!map.TryAddFloorCell(cell, room.RoomId))
                    {
                        throw new InvalidOperationException(
                            $"Komorka {cell} nalezy do wiecej niz jednego pokoju.");
                    }
                }
                foreach (var stair in template.Stairs ?? new List<DungeonRoomStairData>())
                    if (!map.TryAddStairs(TransformSurface(stair.First, template, room), TransformSurface(stair.Second, template, room),
                            stair.PathCells?.Select(cell => room.LayoutPosition + DungeonRoomTransformUtility.TransformCell(cell, template.SizeInCells, room.RotationDegrees)).ToArray()))
                        throw new InvalidOperationException($"Nieprawidlowe schody w pokoju '{room.RoomId}'.");
            }
        }

        private static DungeonSurfacePosition TransformSurface(DungeonRoomSurfaceData surface,
            DungeonRoomTemplateDefinition template, DungeonRoomInstance room)
            => new(room.LayoutPosition + DungeonRoomTransformUtility.TransformCell(surface.LocalCell,
                template.SizeInCells, room.RotationDegrees), surface.Level);

        private static HashSet<DungeonSurfaceEdge> BuildOpenEdges(
            DungeonFloorInstance floor,
            DungeonFloorMap map)
        {
            var openEdges = new HashSet<DungeonSurfaceEdge>();

            foreach (var connection in floor.Connections)
            {
                if (!connection.HasGeometry)
                {
                    throw new InvalidOperationException(
                        $"Polaczenie '{connection.FirstRoomId}' "
                        + $"-> '{connection.SecondRoomId}' nie ma geometrii.");
                }

                var normal = connection.SecondDoorCell
                             - connection.FirstDoorCell;

                var lateral = normal.x != 0
                    ? Vector2Int.up
                    : Vector2Int.right;

                for (var offset = 0;
                    offset < connection.DoorwayWidth;
                    offset++)
                {
                    var firstDoorCell = connection.FirstDoorCell
                                        + lateral * offset;

                    var secondDoorCell = connection.SecondDoorCell
                                         + lateral * offset;

                    if (!map.TryGetRoomId(new DungeonSurfacePosition(firstDoorCell, connection.DoorwayLevel), out var firstRoom)
                        || firstRoom != connection.FirstRoomId
                        || !map.TryGetRoomId(new DungeonSurfacePosition(secondDoorCell, connection.DoorwayLevel), out var secondRoom)
                        || secondRoom != connection.SecondRoomId)
                    {
                        throw new InvalidOperationException(
                            "Drzwi wymagaja powierzchni obu pokojow na tym samym poziomie na calej szerokosci.");
                    }

                    openEdges.Add(
                        new DungeonSurfaceEdge(new DungeonCellEdge(firstDoorCell, secondDoorCell), connection.DoorwayLevel));
                }
            }

            foreach (var stair in map.Stairs)
                if (map.TryGetStairPath(stair, out var path))
                {
                    openEdges.Add(new DungeonSurfaceEdge(new DungeonCellEdge(path[0], path[1]), stair.First.Level));
                    openEdges.Add(new DungeonSurfaceEdge(new DungeonCellEdge(path[path.Count - 2], path[path.Count - 1]), stair.Second.Level));
                }
            return openEdges;
        }

        private static void AddWallEdges(
            DungeonFloorMap map,
            HashSet<DungeonSurfaceEdge> openEdges)
        {
            foreach (var cell in map.Surfaces)
            {
                foreach (var direction in CardinalDirections)
                {
                    var neighbour = new DungeonSurfacePosition(cell.Cell + direction, cell.Level);

                    if (openEdges.Contains(new DungeonSurfaceEdge(cell, neighbour))) continue;
                    if (!map.IsWalkable(neighbour))
                    {
                        map.TryAddWall(cell, neighbour);
                        continue;
                    }

                    map.TryGetRoomId(cell, out var roomId);
                    map.TryGetRoomId(neighbour, out var neighbourRoomId);

                    if (roomId == neighbourRoomId)
                    {
                        continue;
                    }

                    var edge = new DungeonSurfaceEdge(cell, neighbour);

                    if (openEdges.Contains(edge))
                    {
                        continue;
                    }

                    map.TryAddWall(cell, neighbour);
                }
            }
        }

        private static void AddStructuralWalls(
            DungeonFloorInstance floor,
            Dictionary<string, DungeonRoomTemplateDefinition> templatesById,
            DungeonFloorMap map)
        {
            foreach (var room in floor.Rooms)
            {
                if (!templatesById.TryGetValue(
                        room.TemplateId,
                        out var template))
                {
                    throw new InvalidOperationException(
                        $"Brak szablonu '{room.TemplateId}' dla pokoju '{room.RoomId}'.");
                }

                // Difficult terrain is currently disabled in playable dungeons.
                // Low walls remain authored traversal edges with their own cost.

                foreach (var anchor in template.StructuralAnchors)
                {
                    if (anchor == null
                        || anchor.Type != DungeonStructuralAnchorType.Wall)
                    {
                        continue;
                    }

                    var firstCell = room.LayoutPosition
                                    + DungeonRoomTransformUtility.TransformCell(
                                        anchor.LocalCell,
                                        template.SizeInCells,
                                        room.RotationDegrees);
                    var secondCell = firstCell
                                     + DungeonRoomTransformUtility.GetDirectionOffset(
                                         DungeonRoomTransformUtility.TransformDirection(
                                             anchor.Direction,
                                             room.RotationDegrees));

                    var firstSurface = new DungeonSurfacePosition(firstCell, anchor.Level);
                    if (!map.IsWalkable(firstSurface))
                    {
                        throw new InvalidOperationException(
                            $"Kotwica sciany w szablonie '{template.Id}' "
                            + "wskazuje pole poza pokojem.");
                    }

                    if (!map.TryGetRoomId(firstSurface, out var firstRoomId)
                        || firstRoomId != room.RoomId)
                    {
                        throw new InvalidOperationException(
                            $"Kotwica sciany w szablonie '{template.Id}' "
                            + "nie moze wychodzic poza granice pokoju.");
                    }

                    if (!Enum.IsDefined(typeof(DungeonWallType), anchor.WallType))
                        throw new InvalidOperationException("Invalid wall type in room template.");
                    var edge = new DungeonSurfaceEdge(new DungeonCellEdge(firstCell, secondCell), anchor.Level);
                    if (map.SurfaceWallTypes.TryGetValue(edge, out var existing) && existing != anchor.WallType)
                        throw new InvalidOperationException("Conflicting wall types on shared room edge.");
                    map.SetWallType(edge, anchor.WallType);
                }
            }
        }
    }
}

namespace Godmist.Systems.Dungeons
{
    public static class DungeonPassageGeometry
    {
        public static bool TryFindCommonLevel(DungeonRoomTemplateDefinition first,
            DungeonRoomTemplateDefinition second, int firstRotation, int secondRotation,
            Vector2Int firstCell, Vector2Int secondCell, int width, Vector2Int lateral,
            DungeonEdgeDirection firstDirection, DungeonEdgeDirection secondDirection, out int level)
        {
            level = 0;
            if (first == null || second == null || width < 1) return false;
            first.ValidateSurfaceGeometry();
            second.ValidateSurfaceGeometry();
            // Stable ordering, no new RNG draw for legacy templates.
            foreach (var candidate in first.EnumerateSurfaces().Select(s => s.Level).Distinct().OrderBy(l => l))
            {
                var matches = true;
                for (var offset = 0; offset < width; offset++)
                    if (!DungeonRoomTransformUtility.AllowsTransformedBoundaryEdge(first, firstCell + lateral * offset,
                            firstDirection, firstRotation, candidate)
                        || !DungeonRoomTransformUtility.AllowsTransformedBoundaryEdge(second, secondCell + lateral * offset,
                            secondDirection, secondRotation, candidate))
                    { matches = false; break; }
                if (matches) { level = candidate; return true; }
            }
            return false;
        }
    }
}
