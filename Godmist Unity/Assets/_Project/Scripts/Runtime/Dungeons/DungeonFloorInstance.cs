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

        public int FloorIndex { get; }
        public IReadOnlyList<DungeonRoomInstance> Rooms => rooms;
        public IReadOnlyList<DungeonRoomConnection> Connections => connections;
        public IReadOnlyList<DungeonEncounterInstance> Encounters => encounters;
        public IReadOnlyList<DungeonObjectInstance> Objects => objects;

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
            return true;
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

        public bool TryAddObject(DungeonObjectInstance dungeonObject)
        {
            if (dungeonObject == null ||  objects.Exists(
                existing => string.Equals(existing.InstanceId, dungeonObject.InstanceId, StringComparison.Ordinal))
                || objects.Exists(existing => existing.Position == dungeonObject.Position)) return false;

            objects.Add(dungeonObject);
            return true;
        }

        public bool TryGetActiveObjectAt(Vector2Int position, out DungeonObjectInstance dungeonObject)
        {
            dungeonObject = objects.Find(existing => existing.IsActive && existing.Position == position);

            return dungeonObject != null;
        }
    }
}