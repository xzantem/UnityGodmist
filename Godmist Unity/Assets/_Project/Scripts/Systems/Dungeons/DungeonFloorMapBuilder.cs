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

            var openEdges = BuildOpenEdges(floor, map);

            AddWallEdges(map, openEdges);
            AddStructuralWalls(floor, templatesById, map);

            return map;
        }

        private static Dictionary<string, DungeonRoomTemplateDefinition>
            BuildTemplateLookup(DungeonRoomTemplateCatalog catalog)
        {
            var templatesById =
                new Dictionary<string, DungeonRoomTemplateDefinition>();

            foreach (var template in catalog.Templates)
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

                foreach (var localCell in template.DifficultTerrainCells)
                    map.SetDifficultTerrain(room.LayoutPosition + DungeonRoomTransformUtility.TransformCell(
                        localCell, template.SizeInCells, room.RotationDegrees));

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
