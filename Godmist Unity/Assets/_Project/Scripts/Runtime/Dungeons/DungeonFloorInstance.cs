using System;
using System.Collections.Generic;

namespace Godmist.Runtime.Dungeons
{
    public class DungeonFloorInstance
    {
        private readonly List<DungeonRoomInstance> rooms = new();
        private readonly List<DungeonRoomConnection> connections = new();
        private readonly List<DungeonEncounterInstance> encounters = new();

        public int FloorIndex { get; }
        public IReadOnlyList<DungeonRoomInstance> Rooms => rooms;
        public IReadOnlyList<DungeonRoomConnection> Connections => connections;
        public IReadOnlyList<DungeonEncounterInstance> Encounters => encounters;

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

        public bool TryAddConnection(
            string firstRoomId,
            string secondRoomId)
        {
            if (!TryGetRoom(firstRoomId, out _)
                || !TryGetRoom(secondRoomId, out _))
            {
                return false;
            }

            if (string.Equals(
                    firstRoomId,
                    secondRoomId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            if (connections.Exists(
                    connection => connection.Connects(
                        firstRoomId,
                        secondRoomId)))
            {
                return false;
            }

            connections.Add(
                new DungeonRoomConnection(
                    firstRoomId,
                    secondRoomId));

            return true;
        }
        
        public bool TrySetMap(DungeonFloorMap map)
        {
            if (map == null || Map != null)
            {
                return false;
            }

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
    }
}