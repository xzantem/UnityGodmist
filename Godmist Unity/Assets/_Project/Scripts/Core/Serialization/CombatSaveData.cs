using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
namespace Godmist.Core.Serialization
{

    [Serializable] public sealed class CombatSaveData
    {
        public int Version = 1;
        public string CombatId, EncounterId, FocusedCombatId;
        public bool IsDungeonWide, IsFloorWide;
        public int FloorIndex, EscapeChance, ActiveTurnIndex, EnemyTurnsWithoutDamage;
        public float ActionPointsPerSpeed;
        public string StallEnemyId;
        public bool StallEnemyInvisibleAtTurnStart, EnemyDamagedSinceLastTurn;
        public List<CombatUnitSaveData> Units = new();
        public List<string> ActiveUnitIds = new(), DepartedUnitIds = new();
        public List<string> TurnOrder = new(), SuspendedTurns = new();
        public List<string> DefeatedEnemyIds = new(), ExperienceGrantedEnemyIds = new();
        public List<string> EscapePenaltyEligibleIds = new();
        public bool HasEscapePenaltyEligibleCharacters;
        public List<CombatOriginSaveData> EnemySources = new();
        public List<CombatPointEntrySaveData> DeathPositions = new();
        public List<CombatObjectSaveData> Objects = new();
        public List<string> MapObjectOrder = new();
        public List<CombatScheduledSaveData> Scheduled = new();
        public List<CombatSummonSaveData> Summons = new();
        public List<CombatContactSaveData> Contact = new();
        public List<string> ContactSeenPlayers = new();
        public List<CombatIntEntrySaveData> ContactCompleted = new();
        public List<CombatStringSetSaveData> ContactStarted = new();
        public List<CombatHitSetSaveData> ActionHits = new();
    }
    [Serializable] public sealed class CombatHitSetSaveData { public string Id; public List<CombatIntEntrySaveData> Values = new(); }
    [Serializable] public sealed class CombatPositionSaveData
    {
        public int X,Y,Level,StairStep,FirstX,FirstY,FirstLevel,SecondX,SecondY,SecondLevel;
    }
    [Serializable] public sealed class CombatPointEntrySaveData { public string Id; public CombatPositionSaveData Position; }
    [Serializable] public sealed class CombatIntEntrySaveData { public string Id, OtherId; public int Value, ExtraValue; }
    [Serializable] public sealed class CombatFloatEntrySaveData { public string Id; public float Value; }
    [Serializable] public sealed class CombatStringSetSaveData { public string Id; public List<string> Values = new(); }
    [Serializable] public sealed class CombatOriginSaveData { public string UnitId, EncounterId, EnemyId; }
    [Serializable] public sealed class CombatStatSaveData { public CharacterStatType Type; public float Base, Effective; }
    [Serializable] public sealed class CombatAuraSaveData
    {
        public string AuraId, DisplayName, StatusId, CreatorId;
        public int RemainingTurns, Radius, DurationTurns, StatusDamagePerTurn;
        public bool IncludesCaster; public TargetingType TargetingType;
        public float Chance, StatusDamageMultiplier;
    }
    [Serializable] public sealed class CombatRedirectSaveData { public string TargetId, SourceEffectId, TargetCharacterId, TargetDisplayName; public float Percent; }
    [Serializable] public sealed class CombatObjectSaveData
    {
        public string Id, DefinitionId, CreatorId, SkillId; public int EffectIndex, Hits, Duration, KnifeCount;
        public CombatPositionSaveData Position; public bool IsKnife; public float ActionValue;
    }
    [Serializable] public sealed class CombatScheduledSaveData
    {
        public string CasterId, SkillId, ActionId, TargetId;
        public int EffectIndex, RemainingTurns; public bool HasTargetPosition; public CombatPositionSaveData TargetPosition;
    }
    [Serializable] public sealed class CombatSummonSaveData
    {
        public string UnitId, CreatorId, GroupId, SkillId; public int EffectIndex, Duration, CreationOrder;
    }
    [Serializable] public sealed class CombatContactSaveData
    {
        public string EnemyId, TargetId; public CombatPositionSaveData Position, SearchPosition;
        public int DirectionX, DirectionY, ForwardSteps; public bool Checked, Visible, WasVisible;
    }
    [Serializable] public sealed class CombatUnitSaveData
    {
        public int DeadKingKnightHits;
        public bool RibbedTunicProtectionUsed;
        public int PoacherArmorCooldown;
        public int PoacherArmorShieldTurns;
        public int CurrentActionPoints;
        public int MaxActionPoints;
        public int MovementStepsThisTurn;
        public bool HasInitializedCombatResources;
        public bool HasUnfinishedCombatTurn;
        public int MaxKnives;
        public int AvailableKnives;
        public float CurrentActionValue;
        public int NextTurnActionPointModifier;
        public int LastSkillResourceSpent;
        public int ResourceRegenerationBlockedTurns;
        public string DamageRedirectProtectorCharacterId;
        public float DamageRedirectPercent;
        public bool HasReacted;
        public bool IsReadyToEscape;
        public bool IsPlayerSide;
        public bool IsAiControlledSummon;
        public bool IsStationarySummon;
        public float PendingEquipmentActionValueReduction;
        public bool IsBoss;
        public int DownedTurnsRemaining;
        public string Id, DefinitionId; public bool HasOwnCharacter, HasEnt, HasKing, HasSlayer, HasBandit; public CharacterSaveData Character;
        public CombatPositionSaveData Position;
        public int CurrentHealth;
        public ResourcePoolSaveData Resource;
        public ResistanceSetSaveData BaseResistances, CurrentResistances;
        public List<CombatStatSaveData> Stats = new();
        public List<UniqueBuffType> UniqueBuffs = new();
        public List<CombatFloatEntrySaveData> Resistances = new();
        public List<CombatIntEntrySaveData> Tolerances = new();
        public List<string> ActiveToggles = new(), UsedToggles = new();
        public List<ConsumableUseType> UsedConsumables = new();
        public List<CombatAuraSaveData> Auras = new();
        public List<CombatRedirectSaveData> Redirects = new();
        public ScoutTargetStateSaveData Scout = new(); public WarriorCombatStateSaveData Warrior = new();
        public MageCombatStateSaveData Mage = new(); public PaladinCombatStateSaveData Paladin = new();
        public ForestEntCombatStateSaveData Ent; public DeadKingCombatStateSaveData King;
        public SkeletonSlayerCombatStateSaveData Slayer; public BanditLeaderCombatStateSaveData Bandit;
    }
    [Serializable] public sealed class ScoutTargetStateSaveData
    {
        public bool RunningAttackUsed;
        public int ExtraKnifeCapacity;
        public int WeakPointTurns;
        public float WeakPointCritDamage;
        public int AnalysisTurns;
        public float AnalysisPenetration;
        public int FacingX,FacingY; public List<CombatIntEntrySaveData> Anger = new(); public List<CombatFloatEntrySaveData> Knives = new();
    }
    [Serializable] public sealed class WarriorCombatStateSaveData
    {
        public int OrderTurns;
        public int ProtectionTurns;
        public bool Riposte;
        public int FreeMovementTurns;
        public int FreeStepsUsed;
        public int TraumaTurns;
        public float TraumaBonus;
        public string LastCutTarget;
        public int ConsecutiveCuts;
        public float RevengeRemainder;
        public float HealingRemainder;
        public float TrueDamageRemainder;
        public int RageIdleTurns;
        public bool RageOffensiveActionUsed;
        public bool RageHadOffensiveOpportunity;
        public bool RageControlBlocked;
        public string OrderSourceId, ProtectorSourceId;
    }
    [Serializable] public sealed class MageCombatStateSaveData
    {
        public int OffensiveTargetCount;
        public float OverflowCritChance;
        public string OverflowActionId;
        public bool HasTiringAttempts; public List<CombatIntEntrySaveData> Adaptation = new(), FireErosion = new(); public List<CombatStringSetSaveData> TiringAttempts = new();
    }
    [Serializable] public sealed class PaladinCombatStateSaveData
    {
        public int DamageThisTurn;
        public bool HealingDiscount;

    }
    [Serializable] public sealed class ForestEntCombatStateSaveData
    {
        public bool PhaseTwo;
        public bool PhaseTwoPending;
        public string ArenaRoomId;
        public int FireTurns;
        public bool Warning;
        public float ManaCarry;
        public string OriginalDefinitionId; public List<CombatPositionSaveData> Fields = new(); public List<string> Markers = new(), PeriodicHits = new(), FireHits = new(); public List<CombatIntEntrySaveData> Plague = new(); public List<CombatStringSetSaveData> ActionHits = new();
    }
    [Serializable] public sealed class DeadKingCombatStateSaveData
    {
        public bool PhaseTwo;
        public bool PhaseTwoPending;
        public string ArenaRoomId;
        public int ShadowTurns;
        public string OriginalDefinitionId; public List<CombatPositionSaveData> SafeFields = new(), DarkFields = new(); public List<string> Markers = new();
    }
    [Serializable] public sealed class SkeletonSlayerCombatStateSaveData
    {
        public bool PhaseTwo;
        public bool PhaseTwoPending;
        public int RevengeHits;
        public int DefenseTurns;
        public int DefenseDamage;
        public int NextAttackBonus;
        public int ActiveAttackBonus;
        public int HammerTurns;
        public int HammerDamage;
        public bool TaskUsedThisTurn;
        public bool AccelerateNextTurn;
        public string OriginalDefinitionId; public bool HasSelectedHammerCenter; public CombatPositionSaveData HammerCenter, SelectedHammerCenter; public List<CombatPositionSaveData> HammerFields = new(); public List<string> Markers = new();
    }
    [Serializable] public sealed class BanditLeaderCombatStateSaveData
    {
        public bool PhaseTwo;
        public bool PhasePending;
        public int DamageSinceTurn;
        public string OriginalDefinitionId; public List<string> Guards = new();
    }
}
