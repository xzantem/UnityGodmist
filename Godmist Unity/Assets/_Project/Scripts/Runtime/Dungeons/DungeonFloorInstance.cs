using System;
using System.Collections.Generic;
using UnityEngine;

namespace Godmist.Runtime.Dungeons
{
    public class DungeonFloorInstance
    {
        private readonly List<DungeonRoomInstance> rooms = new();
        private readonly List<DungeonRoomConnection> connections = new();
        private readonly List<DungeonEncounterInstance> encounters = new();
        private readonly List<DungeonObjectInstance> objects = new();
        private readonly List<DungeonEnemyBodyInstance> enemyBodies = new();

        public int FloorIndex { get; }
        public IReadOnlyList<DungeonRoomInstance> Rooms => rooms;
        public IReadOnlyList<DungeonRoomConnection> Connections => connections;
        public IReadOnlyList<DungeonEncounterInstance> Encounters => encounters;
        public IReadOnlyList<DungeonObjectInstance> Objects => objects;
        public IReadOnlyList<DungeonEnemyBodyInstance> EnemyBodies => enemyBodies;

        public DungeonFloorMap Map { get; private set; }

        public DungeonFloorInstance(int floorIndex)
        {
            if (floorIndex < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(floorIndex),
                    "FloorIndex nie moze byc ujemne.");
            }

            FloorIndex = floorIndex;
        }

        public bool TryAddRoom(DungeonRoomInstance room)
        {
            if (room == null || TryGetRoom(room.RoomId, out _)) return false;

            rooms.Add(room);
            return true;
        }

        public bool TryGetRoom(
            string roomId,
            out DungeonRoomInstance room)
        {
            room = null;

            if (string.IsNullOrWhiteSpace(roomId)) return false;

            foreach (var candidate in rooms)
            {
                if (!string.Equals(
                        candidate.RoomId,
                        roomId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                room = candidate;
                return true;
            }

            return false;
        }

        public bool TryAddConnection(string firstRoomId, string secondRoomId)
        {
            if (!TryGetRoom(firstRoomId, out _) || !TryGetRoom(secondRoomId, out _)) return false;

            if (string.Equals(firstRoomId, secondRoomId, StringComparison.Ordinal)) return false;
            if (connections.Exists(connection => connection.Connects(firstRoomId, secondRoomId))) return false;

            connections.Add(new DungeonRoomConnection(firstRoomId, secondRoomId));

            return true;
        }
        
        public bool TrySetMap(DungeonFloorMap map)
        {
            if (map == null || Map != null) return false;

            Map = map;

            foreach (var connection in connections)
            {
                if (!connection.HasProgressionGate
                    || TryApplyProgressionGateState(connection))
                {
                    continue;
                }

                Map = null;
                return false;
            }

            return true;
        }

        public bool TrySatisfyProgressionCondition(string conditionId)
        {
            return TrySatisfyProgressionCondition(conditionId, null);
        }

        public bool TrySatisfyProgressionCondition(
            string conditionId,
            string sourceId)
        {
            if (string.IsNullOrWhiteSpace(conditionId) || Map == null)
            {
                return false;
            }

            var changed = false;

            foreach (var connection in connections)
            {
                if (!connection.TrySatisfyProgressionCondition(
                        conditionId,
                        sourceId))
                {
                    continue;
                }

                if (!TryApplyProgressionGateState(connection))
                {
                    return false;
                }

                if (connection.IsProgressionGateUnlocked
                    && TryGetRoom(
                        connection.RequiredBranchRoomId,
                        out var requiredRoom))
                {
                    requiredRoom.TryMarkProgressionRoomCompleted();
                }

                changed = true;
            }

            return changed;
        }

        public bool TryTriggerSpikeTrap(
            string objectInstanceId,
            out List<SpikeTrapDungeonObjectInstance> triggeredTraps)
        {
            triggeredTraps = new List<SpikeTrapDungeonObjectInstance>();

            if (!TryGetObject(objectInstanceId, out var source)
                || source is not SpikeTrapDungeonObjectInstance sourceTrap
                || sourceTrap.IsTriggered)
            {
                return false;
            }

            // Activation groups are local to one instantiated room. Two rooms
            // may come from the same template and therefore legitimately reuse
            // the same serialized group id.
            if (Map == null
                || !Map.TryGetRoomId(sourceTrap.Position, out var sourceRoomId))
            {
                return false;
            }

            foreach (var dungeonObject in objects)
            {
                if (dungeonObject is not SpikeTrapDungeonObjectInstance trap
                    || trap.IsTriggered)
                {
                    continue;
                }

                if (!Map.TryGetRoomId(trap.Position, out var trapRoomId)
                    || !string.Equals(
                        sourceRoomId,
                        trapRoomId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(sourceTrap.ActivationGroupId)
                    ? !ReferenceEquals(trap, sourceTrap)
                    : !string.Equals(
                        sourceTrap.ActivationGroupId,
                        trap.ActivationGroupId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (trap.TryTrigger())
                {
                    triggeredTraps.Add(trap);
                }
            }

            return triggeredTraps.Count > 0;
        }

        public bool TryRestoreProgressionGateState(
            string gateId,
            bool isUnlocked)
        {
            if (string.IsNullOrWhiteSpace(gateId) || Map == null)
            {
                return false;
            }

            foreach (var connection in connections)
            {
                if (!connection.HasProgressionGate
                    || !string.Equals(
                        connection.ProgressionGateId,
                        gateId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (!connection.TryRestoreProgressionGateUnlocked(isUnlocked))
                {
                    return false;
                }

                if (!isUnlocked)
                {
                    foreach (var dungeonObject in objects)
                    {
                        var sourceId = dungeonObject?.InstanceId;
                        var conditionId = dungeonObject switch
                        {
                            LeverDungeonObjectInstance lever
                                when lever.IsActivated
                                => lever.ProgressionConditionId,
                            ConditionDungeonObjectInstance condition
                                when condition.IsSatisfied
                                => condition.ProgressionConditionId,
                            _ => string.Empty
                        };

                        if (!string.Equals(
                                conditionId,
                                connection.ProgressionConditionId,
                                StringComparison.Ordinal))
                        {
                            continue;
                        }

                        connection.TrySatisfyProgressionCondition(
                            conditionId,
                            sourceId);
                    }
                }

                if (!TryApplyProgressionGateState(connection))
                {
                    return false;
                }

                return TryGetRoom(
                        connection.RequiredBranchRoomId,
                        out var requiredRoom)
                    && requiredRoom.TryRestoreProgressionRoomCompleted(
                        connection.IsProgressionGateUnlocked);
            }

            return false;
        }

        public bool TryAddEncounter(DungeonEncounterInstance encounter) 
        {
            if (encounter == null || !TryGetRoom(encounter.RoomId, out _) 
                || encounters.Exists(existing =>  string.Equals
                    (existing.InstanceId, encounter.InstanceId, StringComparison.Ordinal))) return false;

            encounters.Add(encounter);
            return true;
        }

        public bool TryGetEncounter(
            string encounterId,
            out DungeonEncounterInstance encounter)
        {
            encounter = null;

            if (string.IsNullOrWhiteSpace(encounterId)) return false;

            foreach (var candidate in encounters)
            {
                if (candidate == null
                    || !string.Equals(
                        candidate.InstanceId,
                        encounterId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                encounter = candidate;
                return true;
            }

            return false;
        }

        public bool TryAddEnemyBody(DungeonEnemyBodyInstance body)
        {
            if (body == null
                || enemyBodies.Exists(existing =>
                    existing != null
                    && string.Equals(
                        existing.InstanceId,
                        body.InstanceId,
                        StringComparison.Ordinal)))
            {
                return false;
            }

            enemyBodies.Add(body);
            return true;
        }

        public bool TryGetEnemyBody(
            string bodyInstanceId,
            out DungeonEnemyBodyInstance body)
        {
            body = null;

            if (string.IsNullOrWhiteSpace(bodyInstanceId)) return false;

            foreach (var candidate in enemyBodies)
            {
                if (candidate == null
                    || !string.Equals(
                        candidate.InstanceId,
                        bodyInstanceId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                body = candidate;
                return true;
            }

            return false;
        }

        public bool TryRemoveEnemyBody(string bodyInstanceId)
        {
            if (string.IsNullOrWhiteSpace(bodyInstanceId)) return false;

            var index = enemyBodies.FindIndex(candidate =>
                candidate != null
                && string.Equals(
                    candidate.InstanceId,
                    bodyInstanceId,
                    StringComparison.Ordinal));

            if (index < 0) return false;

            enemyBodies.RemoveAt(index);
            return true;
        }

        public bool TryRestoreEncounterDefeatedEnemyInstanceIds(
            string encounterInstanceId,
            IEnumerable<string> defeatedEnemyInstanceIds)
        {
            return TryGetEncounter(encounterInstanceId, out var encounter)
                && encounter.TryRestoreDefeatedEnemyInstanceIds(
                    defeatedEnemyInstanceIds);
        }

        public bool TryGetObject(
            string objectInstanceId,
            out DungeonObjectInstance dungeonObject)
        {
            dungeonObject = null;

            if (string.IsNullOrWhiteSpace(objectInstanceId))
            {
                return false;
            }

            foreach (var candidate in objects)
            {
                if (candidate == null
                    || !string.Equals(
                        candidate.InstanceId,
                        objectInstanceId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                dungeonObject = candidate;
                return true;
            }

            return false;
        }

        public bool TryAddObject(DungeonObjectInstance dungeonObject)
        {
            if (dungeonObject == null
                || objects.Exists(existing =>
                    existing != null
                    && string.Equals(
                        existing.InstanceId,
                        dungeonObject.InstanceId,
                        StringComparison.Ordinal)))
            {
                return false;
            }

            if (dungeonObject is DoorDungeonObjectInstance door)
            {
                if (objects.Exists(existing =>
                        existing is DoorDungeonObjectInstance existingDoor
                        && existingDoor.Edge.Equals(door.Edge)))
                {
                    return false;
                }

                if (Map == null || !Map.TrySetDoorState(door.Edge, door.IsOpen))
                {
                    return false;
                }

                objects.Add(door);
                return true;
            }

            if (objects.Exists(existing =>
                    existing is not DoorDungeonObjectInstance
                    && existing.Position == dungeonObject.Position))
            {
                return false;
            }

            objects.Add(dungeonObject);
            return true;
        }
        
        public bool TrySetDoorOpen(
            string doorInstanceId,
            bool isOpen)
        {
            if (string.IsNullOrWhiteSpace(doorInstanceId)
                || Map == null)
            {
                return false;
            }

            foreach (var dungeonObject in objects)
            {
                if (dungeonObject is not DoorDungeonObjectInstance door
                    || !string.Equals(
                        door.InstanceId,
                        doorInstanceId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (!door.TrySetOpen(isOpen))
                {
                    return false;
                }

                return Map.TrySetDoorState(door.Edge, door.IsOpen);
            }

            return false;
        }

        public bool TryRestoreRoomVisited(
            string roomId,
            bool isVisited)
        {
            if (!TryGetRoom(roomId, out var room))
            {
                return false;
            }

            return room.TryRestoreVisited(isVisited);
        }

        public bool TryRestoreObjectState(
            string objectInstanceId,
            bool isActive,
            bool isDoor,
            bool isOpen,
            bool isTriggered = false,
            bool isActivated = false,
            bool isSatisfied = false)
        {
            if (!TryGetObject(objectInstanceId, out var dungeonObject))
            {
                return false;
            }

            if (dungeonObject is DoorDungeonObjectInstance door)
            {
                if (!isDoor || Map == null)
                {
                    return false;
                }

                door.TryRestoreIsActive(isActive);
                door.TryRestoreOpen(isOpen);

                return Map.TrySetDoorState(
                    door.Edge,
                    !door.IsActive || door.IsOpen);
            }

            if (isDoor)
            {
                return false;
            }

            if (!dungeonObject.TryRestoreIsActive(isActive))
            {
                return false;
            }

            return dungeonObject switch
            {
                SpikeTrapDungeonObjectInstance trap =>
                    trap.TryRestoreTriggered(isTriggered),
                LeverDungeonObjectInstance lever =>
                    lever.TryRestoreActivated(isActivated),
                ConditionDungeonObjectInstance condition =>
                    condition.TryRestoreSatisfied(isSatisfied),
                _ => true
            };
        }

        public bool TryGetActiveObjectAt(Vector2Int position, out DungeonObjectInstance dungeonObject)
        {
            dungeonObject = objects.Find(existing => existing.IsActive && existing.Position == position);

            return dungeonObject != null;
        }

        private bool TryApplyProgressionGateState(
            DungeonRoomConnection connection)
        {
            if (Map == null
                || connection == null
                || !connection.HasGeometry)
            {
                return false;
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
                var edge = new DungeonCellEdge(
                    connection.FirstDoorCell + lateral * offset,
                    connection.SecondDoorCell + lateral * offset);

                if (!Map.TrySetProgressionGateState(
                        edge,
                        connection.IsProgressionGateUnlocked))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
