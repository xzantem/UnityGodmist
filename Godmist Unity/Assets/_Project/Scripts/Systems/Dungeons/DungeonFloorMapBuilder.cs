using System;
using System.Collections.Generic;
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

                foreach (var localCell in footprint.Enumerate(
                             template.SizeInCells))
                {
                    var cell = room.LayoutPosition + localCell;

                    if (!map.TryAddFloorCell(cell, room.RoomId))
                    {
                        throw new InvalidOperationException(
                            $"Komorka {cell} nalezy do wiecej niz jednego pokoju.");
                    }
                }
            }
        }

        private static HashSet<DungeonCellEdge> BuildOpenEdges(
            DungeonFloorInstance floor,
            DungeonFloorMap map)
        {
            var openEdges = new HashSet<DungeonCellEdge>();

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

                    if (!map.IsWalkable(firstDoorCell)
                        || !map.IsWalkable(secondDoorCell))
                    {
                        throw new InvalidOperationException(
                            "Drzwi wskazuja komorke poza podloga lochu.");
                    }

                    openEdges.Add(
                        new DungeonCellEdge(
                            firstDoorCell,
                            secondDoorCell));
                }
            }

            return openEdges;
        }

        private static void AddWallEdges(
            DungeonFloorMap map,
            HashSet<DungeonCellEdge> openEdges)
        {
            foreach (var cell in map.FloorCells)
            {
                foreach (var direction in CardinalDirections)
                {
                    var neighbour = cell + direction;

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

                    var edge = new DungeonCellEdge(
                        cell,
                        neighbour);

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

                foreach (var anchor in template.StructuralAnchors)
                {
                    if (anchor == null
                        || anchor.Type != DungeonStructuralAnchorType.Wall)
                    {
                        continue;
                    }

                    var firstCell = room.LayoutPosition + anchor.LocalCell;
                    var secondCell = firstCell + anchor.GetDirectionOffset();

                    if (!map.IsWalkable(firstCell)
                        || !map.IsWalkable(secondCell))
                    {
                        throw new InvalidOperationException(
                            $"Kotwica sciany w szablonie '{template.Id}' "
                            + "wskazuje pole poza pokojem.");
                    }

                    if (!map.TryGetRoomId(firstCell, out var firstRoomId)
                        || !map.TryGetRoomId(secondCell, out var secondRoomId)
                        || firstRoomId != room.RoomId
                        || secondRoomId != room.RoomId)
                    {
                        throw new InvalidOperationException(
                            $"Kotwica sciany w szablonie '{template.Id}' "
                            + "nie moze wychodzic poza granice pokoju.");
                    }

                    map.TryAddWall(firstCell, secondCell);
                }
            }
        }
    }
}
