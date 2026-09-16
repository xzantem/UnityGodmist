using System;
using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Serialization;
using Godmist.Data.Dungeons;
using Godmist.Data.World;
using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Systems.SaveLoad
{
    // Stores generated geometry and identities; mutable object state stays in the existing deltas.
    public static class DungeonLayoutSnapshot
    {
        public const int CurrentVersion = 3;

        public static List<DungeonFloorLayoutSaveData> Capture(DungeonInstance dungeon)
        {
            var result = new List<DungeonFloorLayoutSaveData>();
            foreach (var floor in dungeon.Floors)
            {
                var data = new DungeonFloorLayoutSaveData { FloorIndex = floor.FloorIndex, HasMap = floor.Map != null };
                foreach (var room in floor.Rooms)
                    data.Rooms.Add(new DungeonRoomLayoutSaveData
                    {
                        Id = room.RoomId, TemplateId = room.TemplateId, Type = (int)room.RoomType,
                        X = room.LayoutPosition.x, Y = room.LayoutPosition.y, Rotation = room.RotationDegrees,
                        Required = room.IsRequiredProgressionRoom, Completed = room.IsProgressionRoomCompleted
                    });
                foreach (var connection in floor.Connections)
                    data.Connections.Add(new DungeonConnectionLayoutSaveData
                    {
                        FirstRoomId = connection.FirstRoomId, SecondRoomId = connection.SecondRoomId,
                        HasGeometry = connection.HasGeometry, FirstX = connection.FirstDoorCell.x,
                        FirstY = connection.FirstDoorCell.y, SecondX = connection.SecondDoorCell.x,
                        SecondY = connection.SecondDoorCell.y, Width = connection.DoorwayWidth, Level = connection.DoorwayLevel,
                        HasGate = connection.HasProgressionGate, GateId = connection.ProgressionGateId,
                        ConditionId = connection.ProgressionConditionId, BranchRoomId = connection.RequiredBranchRoomId,
                        RequiredSources = new List<string>(connection.RequiredProgressionSourceIds)
                    });
                if (floor.Map != null)
                {
                    foreach (var surface in floor.Map.Surfaces)
                    {
                        var cell = surface.Cell;
                        floor.Map.TryGetRoomId(surface, out var roomId);
                        data.Cells.Add(new DungeonCellSaveData
                        { X = cell.x, Y = cell.y, Level = surface.Level, RoomId = roomId,
                            Difficult = surface.Level == 0 && floor.Map.IsDifficultTerrain(cell) });
                    }
                    foreach (var stair in floor.Map.Stairs)
                    {
                        var stairData = new DungeonStairSaveData
                        {
                            FirstX = stair.First.Cell.x, FirstY = stair.First.Cell.y, FirstLevel = stair.First.Level,
                            SecondX = stair.Second.Cell.x, SecondY = stair.Second.Cell.y, SecondLevel = stair.Second.Level
                        };
                        if (floor.Map.TryGetStairPath(stair, out var path))
                            foreach (var cell in path) stairData.PathCells.Add(new DungeonStairPathCellSaveData { X = cell.x, Y = cell.y });
                        data.Stairs.Add(stairData);
                    }
                    data.Stairs.Sort((a, b) =>
                    {
                        var first = new DungeonSurfacePosition(new Vector2Int(a.FirstX, a.FirstY), a.FirstLevel)
                            .CompareTo(new DungeonSurfacePosition(new Vector2Int(b.FirstX, b.FirstY), b.FirstLevel));
                        return first != 0 ? first : new DungeonSurfacePosition(new Vector2Int(a.SecondX, a.SecondY), a.SecondLevel)
                            .CompareTo(new DungeonSurfacePosition(new Vector2Int(b.SecondX, b.SecondY), b.SecondLevel));
                    });
                    foreach (var edge in floor.Map.SurfaceWallEdges)
                        data.Walls.Add(new DungeonWallSaveData
                        {
                            FirstX = edge.FirstCell.x, FirstY = edge.FirstCell.y,
                            SecondX = edge.SecondCell.x, SecondY = edge.SecondCell.y,
                            Type = (int)floor.Map.GetWallType(edge), Level = edge.Level
                        });
                    data.Cells.Sort((a, b) => a.Level != b.Level ? a.Level.CompareTo(b.Level)
                        : a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
                    data.Walls.Sort((a, b) =>
                    {
                        var comparison = a.Level.CompareTo(b.Level);
                        if (comparison == 0) comparison = a.FirstX.CompareTo(b.FirstX);
                        if (comparison == 0) comparison = a.FirstY.CompareTo(b.FirstY);
                        if (comparison == 0) comparison = a.SecondX.CompareTo(b.SecondX);
                        return comparison == 0 ? a.SecondY.CompareTo(b.SecondY) : comparison;
                    });
                }
                foreach (var obj in floor.Objects)
                    data.Objects.Add(new DungeonObjectLayoutSaveData
                    {
                        Id = obj.InstanceId, DefinitionId = obj.Definition.Id, Type = (int)obj.ObjectType,
                        X = obj.Position.x, Y = obj.Position.y, Required = obj.IsProgressionRequirement,
                        DoorSecondX = obj is DoorDungeonObjectInstance door ? door.Edge.SecondCell.x : 0,
                        DoorSecondY = obj is DoorDungeonObjectInstance doorY ? doorY.Edge.SecondCell.y : 0,
                        ActivationGroupId = (obj as SpikeTrapDungeonObjectInstance)?.ActivationGroupId,
                        ConditionId = obj is LeverDungeonObjectInstance lever ? lever.ProgressionConditionId
                            : (obj as ConditionDungeonObjectInstance)?.ProgressionConditionId
                    });
                foreach (var encounter in floor.Encounters)
                {
                    var entry = new DungeonEncounterLayoutSaveData
                    {
                        Id = encounter.InstanceId, DefinitionId = encounter.Definition.Id, RoomId = encounter.RoomId,
                        X = encounter.TriggerArea.x, Y = encounter.TriggerArea.y,
                        Width = encounter.TriggerArea.width, Height = encounter.TriggerArea.height,
                        PositionX = encounter.ExplorationPosition.x, PositionY = encounter.ExplorationPosition.y,
                        HasRetreat = encounter.HasRetreatPosition,
                        RetreatX = encounter.RetreatPosition.x, RetreatY = encounter.RetreatPosition.y
                    };
                    foreach (var pair in encounter.EnemyExplorationPositions)
                        entry.EnemyPositions.Add(new DungeonEnemyPositionSaveData { Id = pair.Key, X = pair.Value.x, Y = pair.Value.y });
                    entry.StationaryHomes = encounter.StationaryEnemyHomes.OrderBy(p => p.Key, StringComparer.Ordinal)
                        .Select(p => new DungeonEnemyPositionSaveData { Id = p.Key, X = p.Value.x, Y = p.Value.y }).ToList();
                    entry.EnemyPositions.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
                    data.Encounters.Add(entry);
                }
                result.Add(data);
            }
            return result;
        }

        public static bool TryRestore(DungeonSaveData data, DungeonDefinition definition,
            GameContentCatalog catalog, out DungeonInstance dungeon)
        {
            dungeon = null;
            if (data == null || (data.LayoutVersion < 1 || data.LayoutVersion > CurrentVersion)
                || data.Layout == null || data.Layout.Count == 0) return false;
            var candidate = new DungeonInstance(definition, data.Seed, data.DungeonLevel);
            var objectIds = new HashSet<string>(StringComparer.Ordinal);
            var encounterIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var saved in data.Layout)
            {
                if (saved == null || saved.FloorIndex < 0 || saved.Rooms == null || saved.Connections == null
                    || saved.Cells == null || saved.Walls == null || saved.Objects == null || saved.Encounters == null) return false;
                if (data.LayoutVersion >= 2 && saved.Stairs == null) return false;
                if (data.LayoutVersion == 1 && saved.Stairs != null && saved.Stairs.Count != 0) return false;
                var floor = new DungeonFloorInstance(saved.FloorIndex);
                if (!candidate.TryAddFloor(floor)) return false;
                foreach (var room in saved.Rooms)
                {
                    if (room == null || string.IsNullOrWhiteSpace(room.Id)
                        || !Enum.IsDefined(typeof(DungeonRoomType), room.Type)
                        || (room.Rotation != 0 && room.Rotation != 90 && room.Rotation != 180 && room.Rotation != 270)) return false;
                    var restored = new DungeonRoomInstance(room.Id, (DungeonRoomType)room.Type, room.TemplateId,
                        new Vector2Int(room.X, room.Y), room.Required, room.Rotation);
                    if (!restored.TryRestoreProgressionRoomCompleted(room.Completed) || !floor.TryAddRoom(restored)) return false;
                }
                foreach (var connection in saved.Connections)
                {
                    if (connection == null || (data.LayoutVersion == 1 && connection.Level != 0) || !floor.TryAddConnection(connection.FirstRoomId, connection.SecondRoomId)) return false;
                    var restored = floor.Connections[floor.Connections.Count - 1];
                    if (connection.HasGeometry && !restored.TrySetDirectPassage(
                        new DungeonSurfacePosition(new Vector2Int(connection.FirstX, connection.FirstY), connection.Level),
                        new DungeonSurfacePosition(new Vector2Int(connection.SecondX, connection.SecondY), connection.Level), connection.Width)) return false;
                    if (connection.HasGate)
                    {
                        if (!connection.HasGeometry || !floor.TryGetRoom(connection.BranchRoomId, out _)
                            || !restored.TrySetProgressionGate(connection.GateId, connection.ConditionId, connection.BranchRoomId)
                            || connection.RequiredSources == null) return false;
                        if (connection.RequiredSources.Count > 0 && !restored.TrySetProgressionRequirementSources(connection.RequiredSources)) return false;
                    }
                }
                if (saved.HasMap)
                {
                    var map = new DungeonFloorMap();
                    foreach (var cell in saved.Cells)
                    {
                        if (cell == null || !floor.TryGetRoom(cell.RoomId, out _)
                            || (data.LayoutVersion == 1 && cell.Level != 0)
                            || (cell.Level != 0 && cell.Difficult)
                            || !map.TryAddFloorCell(new DungeonSurfacePosition(new Vector2Int(cell.X, cell.Y), cell.Level), cell.RoomId)) return false;
                        if (cell.Difficult) map.SetDifficultTerrain(new Vector2Int(cell.X, cell.Y));
                    }
                    if (saved.Stairs != null)
                        foreach (var stair in saved.Stairs)
                        {
                            if (stair == null || (data.LayoutVersion >= 3 && stair.PathCells == null)
                                || (data.LayoutVersion < 3 && stair.PathCells?.Count > 0)) return false;
                            var path = new List<Vector2Int>();
                            if (stair.PathCells != null)
                                foreach (var cell in stair.PathCells)
                                {
                                    if (cell == null) return false;
                                    path.Add(new Vector2Int(cell.X, cell.Y));
                                }
                            if (!map.TryAddStairs(
                                new DungeonSurfacePosition(new Vector2Int(stair.FirstX, stair.FirstY), stair.FirstLevel),
                                new DungeonSurfacePosition(new Vector2Int(stair.SecondX, stair.SecondY), stair.SecondLevel), path)) return false;
                        }
                    var walls = new HashSet<DungeonSurfaceEdge>();
                    foreach (var wall in saved.Walls)
                    {
                        if (wall == null || (data.LayoutVersion == 1 && wall.Level != 0) || !Enum.IsDefined(typeof(DungeonWallType), wall.Type)) return false;
                        var first = new Vector2Int(wall.FirstX, wall.FirstY);
                        var second = new Vector2Int(wall.SecondX, wall.SecondY);
                        if (Math.Abs((long)first.x - second.x) + Math.Abs((long)first.y - second.y) != 1) return false;
                        var edge = new DungeonSurfaceEdge(new DungeonCellEdge(first, second), wall.Level);
                        if (!walls.Add(edge) || !map.SetWallType(edge, (DungeonWallType)wall.Type)) return false;
                    }
                    foreach (var connection in floor.Connections)
                    {
                        if (!connection.HasGeometry) continue;
                        var lateral = connection.FirstDoorCell.x != connection.SecondDoorCell.x ? Vector2Int.up : Vector2Int.right;
                        for (var offset = 0; offset < connection.DoorwayWidth; offset++)
                            if (!map.IsWalkable(new DungeonSurfacePosition(connection.FirstDoorCell + lateral * offset, connection.DoorwayLevel))
                                || !map.IsWalkable(new DungeonSurfacePosition(connection.SecondDoorCell + lateral * offset, connection.DoorwayLevel))) return false;
                    }
                    if (!floor.TrySetMap(map)) return false;
                }
                else if (saved.Cells.Count != 0 || saved.Walls.Count != 0 || saved.Stairs?.Count > 0) return false;
                foreach (var obj in saved.Objects)
                {
                    if (obj == null || string.IsNullOrWhiteSpace(obj.Id) || !objectIds.Add(obj.Id)
                        || !catalog.TryGetDungeonObject(obj.DefinitionId, out var objectDefinition)
                        || (int)objectDefinition.ObjectType != obj.Type) return false;
                    var position = new Vector2Int(obj.X, obj.Y);
                    if (floor.Map != null && !floor.Map.IsWalkable(position)) return false;
                    DungeonObjectInstance restored;
                    switch (objectDefinition)
                    {
                        case DoorDungeonObjectDefinition door:
                            var other = new Vector2Int(obj.DoorSecondX, obj.DoorSecondY);
                            if (Math.Abs((long)position.x - other.x) + Math.Abs((long)position.y - other.y) != 1) return false;
                            restored = new DoorDungeonObjectInstance(obj.Id, door, new DungeonCellEdge(position, other));
                            break;
                        case ChestDungeonObjectDefinition chest:
                            restored = new ChestDungeonObjectInstance(obj.Id, chest, position, null);
                            break;
                        case SpikeTrapDungeonObjectDefinition trap:
                            restored = new SpikeTrapDungeonObjectInstance(obj.Id, trap, position, obj.ActivationGroupId);
                            break;
                        case ObstacleDungeonObjectDefinition obstacle:
                            restored = new ObstacleDungeonObjectInstance(obj.Id, obstacle, position);
                            break;
                        case LeverDungeonObjectDefinition lever:
                            var leverInstance = new LeverDungeonObjectInstance(obj.Id, lever, position, obj.Required);
                            if (!string.IsNullOrEmpty(obj.ConditionId)) leverInstance.TryBindCondition(obj.ConditionId);
                            restored = leverInstance;
                            break;
                        case ConditionDungeonObjectDefinition condition:
                            var conditionInstance = new ConditionDungeonObjectInstance(obj.Id, condition, position, obj.Required);
                            if (!string.IsNullOrEmpty(obj.ConditionId)) conditionInstance.TryBindCondition(obj.ConditionId);
                            restored = conditionInstance;
                            break;
                        default: return false;
                    }
                    if (!floor.TryAddObject(restored)) return false;
                }
                foreach (var encounter in saved.Encounters)
                {
                    if (encounter == null || string.IsNullOrWhiteSpace(encounter.Id) || !encounterIds.Add(encounter.Id)
                        || !floor.TryGetRoom(encounter.RoomId, out _) || encounter.Width <= 0 || encounter.Height <= 0
                        || encounter.EnemyPositions == null) return false;
                    // A definition can be absent from the generation pool but retained in the global catalog.
                    var encounterDefinition = definition.EncounterDefinitions.Find(e => e != null && e.Id == encounter.DefinitionId);
                    if (encounterDefinition == null && definition.BossEncounterDefinition?.Id == encounter.DefinitionId)
                        encounterDefinition = definition.BossEncounterDefinition;
                    if (encounterDefinition == null && !catalog.TryGetEncounter(encounter.DefinitionId, out encounterDefinition)) return false;
                    var restored = new DungeonEncounterInstance(encounter.Id, encounterDefinition, encounter.RoomId,
                        new RectInt(encounter.X, encounter.Y, encounter.Width, encounter.Height));
                    restored.TrySetExplorationPosition(new Vector2Int(encounter.PositionX, encounter.PositionY));
                    if (encounter.HasRetreat) restored.TrySetRetreatPosition(new Vector2Int(encounter.RetreatX, encounter.RetreatY));
                    var enemyIds = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var enemy in encounter.EnemyPositions)
                        if (enemy == null || string.IsNullOrWhiteSpace(enemy.Id) || !enemyIds.Add(enemy.Id)
                            || !restored.TrySetEnemyExplorationPosition(enemy.Id, new Vector2Int(enemy.X, enemy.Y))) return false;
                    if (encounter.StationaryHomes != null)
                        foreach (var home in encounter.StationaryHomes)
                            if (home == null || string.IsNullOrWhiteSpace(home.Id)
                                || floor.Map == null || !floor.Map.FloorCells.Contains(new Vector2Int(home.X, home.Y))
                                || !restored.StationaryEnemyHomes.TryAdd(home.Id, new Vector2Int(home.X, home.Y))) return false;
                    if (!floor.TryAddEncounter(restored)) return false;
                }
            }
            if (!HasCompleteState(data, candidate)) return false;
            candidate.MarkContentPlaced();
            dungeon = candidate;
            return true;
        }

        private static bool HasCompleteState(DungeonSaveData data, DungeonInstance dungeon)
        {
            if (data.FloorDeltas == null || data.EncounterDeltas == null || data.FloorDeltas.Count != dungeon.Floors.Count) return false;
            var floors = new HashSet<int>();
            var encounters = new HashSet<string>(StringComparer.Ordinal);
            foreach (var floor in dungeon.Floors)
                foreach (var encounter in floor.Encounters) encounters.Add(encounter.InstanceId);
            foreach (var delta in data.FloorDeltas)
            {
                if (delta == null || !floors.Add(delta.FloorIndex) || !dungeon.TryGetFloor(delta.FloorIndex, out var floor)
                    || delta.ObjectDeltas == null || delta.ProgressionGateDeltas == null
                    || delta.ObjectDeltas.Count != floor.Objects.Count) return false;
                var objects = new HashSet<string>(StringComparer.Ordinal);
                foreach (var obj in delta.ObjectDeltas)
                    if (obj == null || !objects.Add(obj.ObjectInstanceId) || !floor.TryGetObject(obj.ObjectInstanceId, out var restored)
                        || (restored is ChestDungeonObjectInstance && !obj.HasChestLootState)) return false;
                var gates = new HashSet<string>(StringComparer.Ordinal);
                foreach (var connection in floor.Connections)
                    if (connection.HasProgressionGate && !gates.Add(connection.ProgressionGateId)) return false;
                foreach (var gate in delta.ProgressionGateDeltas)
                    if (gate == null || !gates.Remove(gate.GateId)) return false;
                if (gates.Count != 0) return false;
            }
            foreach (var encounter in data.EncounterDeltas)
                if (encounter == null || !encounters.Remove(encounter.EncounterInstanceId)) return false;
            return encounters.Count == 0;
        }
    }
}
