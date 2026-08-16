using System;
using System.Collections.Generic;

namespace Godmist.Core.Serialization
{
    [Serializable]
    public class DungeonSaveData
    {
        public string DungeonId = string.Empty;
        public int Seed;
        public int CurrentFloorIndex;
        public bool IsCompleted;

        public bool HasPlayerPosition;
        public float PlayerPositionX;
        public float PlayerPositionY;

        public List<string> ClearedEncounterIds = new();
        public List<DungeonFloorDeltaSaveData> FloorDeltas = new();
        public List<DungeonEncounterDeltaSaveData> EncounterDeltas = new();
    }

    [Serializable]
    public class DungeonFloorDeltaSaveData
    {
        public int FloorIndex;
        public List<string> VisitedRoomIds = new();
        public List<DungeonObjectDeltaSaveData> ObjectDeltas = new();
    }

    [Serializable]
    public class DungeonObjectDeltaSaveData
    {
        public string ObjectInstanceId = string.Empty;
        public bool IsActive;
        public bool IsDoor;
        public bool IsOpen;
    }
    
    [Serializable]
    public class DungeonEncounterDeltaSaveData
    {
        public string EncounterInstanceId = string.Empty;
        public List<string> DefeatedEnemyInstanceIds = new();
    }
}
