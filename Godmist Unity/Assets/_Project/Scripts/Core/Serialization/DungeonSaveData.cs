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
        // Zero means the legacy seed-only format. Never fall back for a damaged snapshot.
        public int LayoutVersion;
        public List<DungeonFloorLayoutSaveData> Layout = new();
        public int CurrentFloorIndex;
        public int HighestReachedFloorIndex = -1;
        public int GuildProgressCreditedFloorCount;
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
        public List<DungeonPatrolSaveData> Patrols;
        public DungeonFogSaveData Fog;
        public int FloorIndex;
        public List<string> VisitedRoomIds = new();
        public List<DungeonActorPositionSaveData> ActorPositions = new();
        public List<DungeonActorFacingSaveData> ActorFacings = new();
        public List<DungeonEnemyAwarenessSaveData> EnemyAwareness = new();
        public List<DungeonObjectDeltaSaveData> ObjectDeltas = new();
        public List<DungeonProgressionGateDeltaSaveData> ProgressionGateDeltas = new();
        public List<DungeonEnemyBodySaveData> EnemyBodyDeltas = new();
    }

    [Serializable]
    public class DungeonPatrolSaveData
    {
        public string EnemyKey;
        public List<int> StopIndices; // null in legacy per-cell patrol saves
        public List<DungeonActorPositionSaveData> Route = new();
        public int NextPoint, Stops;
        public float WaitRemaining;
        public string ObservedCharacterId;
    }

    [Serializable]
    public class DungeonFogSaveData
    {
        public List<string> Bodies = new();
        public List<DungeonFogGateSaveData> ClosedGates = new();
        public List<DungeonActorPositionSaveData> Discovered = new();
        public List<DungeonObjectKnowledgeSaveData> Objects = new();
    }

    [Serializable]
    public class DungeonFogGateSaveData
    {
        public int X, Y, NextX, NextY, Level;
    }

    [Serializable]
    public class DungeonObjectKnowledgeSaveData
    {
        public string Id;
        public bool Active;
        public bool State;
    }

    [Serializable]
    public class DungeonEnemyAwarenessSaveData
    {
        public string EnemyKey;
        public float Progress;
    }

    [Serializable]
    public class DungeonActorFacingSaveData
    {
        public string ActorId;
        public float X;
        public float Y;
    }

    [Serializable]
    public class DungeonActorPositionSaveData
    {
        public string ActorId;
        public float OffsetX, OffsetY;
        public bool IsOnStairs;
        public int X, Y, Level, StairStep;
        public int FirstX, FirstY, FirstLevel, SecondX, SecondY, SecondLevel;
    }

    [Serializable]
    public class DungeonFloorLayoutSaveData
    {
        public int FloorIndex;
        public bool HasMap;
        public List<DungeonRoomLayoutSaveData> Rooms = new();
        public List<DungeonConnectionLayoutSaveData> Connections = new();
        public List<DungeonCellSaveData> Cells = new();
        public List<DungeonStairSaveData> Stairs = new();
        public List<DungeonWallSaveData> Walls = new();
        public List<DungeonObjectLayoutSaveData> Objects = new();
        public List<DungeonEncounterLayoutSaveData> Encounters = new();
    }

    [Serializable]
    public class DungeonRoomLayoutSaveData
    {
        public string Id, TemplateId;
        public int Type, X, Y, Rotation;
        public bool Required, Completed;
    }

    [Serializable]
    public class DungeonConnectionLayoutSaveData
    {
        public string FirstRoomId, SecondRoomId;
        public bool HasGeometry, HasGate;
        public int FirstX, FirstY, SecondX, SecondY, Width;
        public int Level;
        public string GateId, ConditionId, BranchRoomId;
        public List<string> RequiredSources = new();
    }

    [Serializable]
    public class DungeonCellSaveData
    {
        public int X, Y;
        public int Level;
        public string RoomId;
        public bool Difficult;
    }

    [Serializable]
    public class DungeonStairSaveData
    {
        public int FirstX, FirstY, FirstLevel;
        public int SecondX, SecondY, SecondLevel;
        public List<DungeonStairPathCellSaveData> PathCells = new();
    }

    [Serializable]
    public class DungeonStairPathCellSaveData
    {
        public int X, Y;
    }

    [Serializable]
    public class DungeonWallSaveData
    {
        public int FirstX, FirstY, SecondX, SecondY, Type;
        public int Level;
    }

    [Serializable]
    public class DungeonObjectLayoutSaveData
    {
        public string Id, DefinitionId;
        public int Type, X, Y, DoorSecondX, DoorSecondY;
        public bool Required;
        public string ConditionId, ActivationGroupId;
    }

    [Serializable]
    public class DungeonEncounterLayoutSaveData
    {
        public string Id, DefinitionId, RoomId;
        public int X, Y, Width, Height;
        public int PositionX, PositionY, RetreatX, RetreatY;
        public bool HasRetreat;
        public List<DungeonEnemyPositionSaveData> EnemyPositions = new();
        public List<DungeonEnemyPositionSaveData> StationaryHomes;
    }

    [Serializable]
    public class DungeonEnemyPositionSaveData
    {
        public string Id;
        public int X, Y;
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
        public bool HasObstacleDurability, ObstacleDestructible;
        public int ObstacleMaxHits, ObstacleRemainingHits;
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
        public bool HasPreparedRewards;
        public List<DungeonEnemyRewardSaveData> Rewards = new();
        public List<string> DefeatedEnemyInstanceIds = new();
        public List<string> GeneratedEnemyBodyEnemyInstanceIds = new();
    }

    [Serializable]
    public class DungeonEnemyRewardSaveData
    {
        // Empty id denotes the legacy aggregate reward.
        public string EnemyId;
        public int Experience, Gold;
        public List<string> ItemIds = new();
    }
}
