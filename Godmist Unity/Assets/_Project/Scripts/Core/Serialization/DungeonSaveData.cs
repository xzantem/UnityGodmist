using System;
using System.Collections.Generic;

namespace Godmist.Core.Serialization
{
    [Serializable]
    public class DungeonSaveData
    {
        public string DungeonId = string.Empty;
        public int Seed;
        public int DungeonLevel = 1;
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
        public List<DungeonProgressionGateDeltaSaveData> ProgressionGateDeltas = new();
        public List<DungeonEnemyBodySaveData> EnemyBodyDeltas = new();
    }

    [Serializable]
    public class DungeonProgressionGateDeltaSaveData
    {
        public string GateId = string.Empty;
        public bool IsUnlocked;
    }

    [Serializable]
    public class DungeonEnemyBodySaveData
    {
        public string BodyInstanceId = string.Empty;
        public string EnemyInstanceId = string.Empty;
        public int PositionX;
        public int PositionY;
        public int Gold;
        public List<string> ItemInstanceIds = new();
    }

    [Serializable]
    public class DungeonObjectDeltaSaveData
    {
        public string ObjectInstanceId = string.Empty;
        public bool IsActive;
        public bool IsChest;
        public bool IsUnlocked;
        public bool HasChestLootState;
        public List<string> ChestItemInstanceIds = new();
        public bool IsDoor;
        public bool IsOpen;
        public bool IsTriggered;
        public bool IsActivated;
        public bool IsSatisfied;
    }
    
    [Serializable]
    public class DungeonEncounterDeltaSaveData
    {
        public string EncounterInstanceId = string.Empty;
        public List<string> DefeatedEnemyInstanceIds = new();
        public List<string> GeneratedEnemyBodyEnemyInstanceIds = new();
    }
}
