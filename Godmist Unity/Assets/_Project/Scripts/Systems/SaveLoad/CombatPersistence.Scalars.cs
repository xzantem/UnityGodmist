using System;
using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Serialization;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Data.World;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Systems.Combat;
using UnityEngine;
namespace Godmist.Systems.SaveLoad
{
    public static partial class CombatPersistence
    {
        static CombatUnitSaveData CaptureScalars(BattleUnit source) => new()
        {
            DeadKingKnightHits = source.DeadKingKnightHits,
            RibbedTunicProtectionUsed = source.RibbedTunicProtectionUsed,
            PoacherArmorCooldown = source.PoacherArmorCooldown,
            PoacherArmorShieldTurns = source.PoacherArmorShieldTurns,
            CurrentActionPoints = source.CurrentActionPoints,
            MaxActionPoints = source.MaxActionPoints,
            MovementStepsThisTurn = source.MovementStepsThisTurn,
            HasInitializedCombatResources = source.HasInitializedCombatResources,
            HasUnfinishedCombatTurn = source.HasUnfinishedCombatTurn,
            MaxKnives = source.MaxKnives,
            AvailableKnives = source.AvailableKnives,
            CurrentActionValue = source.CurrentActionValue,
            NextTurnActionPointModifier = source.NextTurnActionPointModifier,
            LastSkillResourceSpent = source.LastSkillResourceSpent,
            ResourceRegenerationBlockedTurns = source.ResourceRegenerationBlockedTurns,
            DamageRedirectProtectorCharacterId = source.DamageRedirectProtectorCharacterId,
            DamageRedirectPercent = source.DamageRedirectPercent,
            HasReacted = source.HasReacted,
            IsReadyToEscape = source.IsReadyToEscape,
            IsPlayerSide = source.IsPlayerSide,
            IsAiControlledSummon = source.IsAiControlledSummon,
            IsStationarySummon = source.IsStationarySummon,
            PendingEquipmentActionValueReduction = source.PendingEquipmentActionValueReduction,
            IsBoss = source.IsBoss,
            DownedTurnsRemaining = source.DownedTurnsRemaining,
        };
        static void RestoreScalars(CombatUnitSaveData source, BattleUnit target)
        {
            target.DeadKingKnightHits = source.DeadKingKnightHits;
            target.RibbedTunicProtectionUsed = source.RibbedTunicProtectionUsed;
            target.PoacherArmorCooldown = source.PoacherArmorCooldown;
            target.PoacherArmorShieldTurns = source.PoacherArmorShieldTurns;
            target.CurrentActionPoints = source.CurrentActionPoints;
            target.MaxActionPoints = source.MaxActionPoints;
            target.MovementStepsThisTurn = source.MovementStepsThisTurn;
            target.HasInitializedCombatResources = source.HasInitializedCombatResources;
            target.HasUnfinishedCombatTurn = source.HasUnfinishedCombatTurn;
            target.MaxKnives = source.MaxKnives;
            target.AvailableKnives = source.AvailableKnives;
            target.CurrentActionValue = source.CurrentActionValue;
            target.NextTurnActionPointModifier = source.NextTurnActionPointModifier;
            target.LastSkillResourceSpent = source.LastSkillResourceSpent;
            target.ResourceRegenerationBlockedTurns = source.ResourceRegenerationBlockedTurns;
            target.DamageRedirectProtectorCharacterId = source.DamageRedirectProtectorCharacterId;
            target.DamageRedirectPercent = source.DamageRedirectPercent;
            target.HasReacted = source.HasReacted;
            target.IsReadyToEscape = source.IsReadyToEscape;
            target.IsPlayerSide = source.IsPlayerSide;
            target.IsAiControlledSummon = source.IsAiControlledSummon;
            target.IsStationarySummon = source.IsStationarySummon;
            target.PendingEquipmentActionValueReduction = source.PendingEquipmentActionValueReduction;
            target.IsBoss = source.IsBoss;
            target.DownedTurnsRemaining = source.DownedTurnsRemaining;
        }
        static ScoutTargetStateSaveData CaptureScalars(ScoutTargetState source) => new()
        {
            RunningAttackUsed = source.RunningAttackUsed,
            ExtraKnifeCapacity = source.ExtraKnifeCapacity,
            WeakPointTurns = source.WeakPointTurns,
            WeakPointCritDamage = source.WeakPointCritDamage,
            AnalysisTurns = source.AnalysisTurns,
            AnalysisPenetration = source.AnalysisPenetration,
        };
        static void RestoreScalars(ScoutTargetStateSaveData source, ScoutTargetState target)
        {
            target.RunningAttackUsed = source.RunningAttackUsed;
            target.ExtraKnifeCapacity = source.ExtraKnifeCapacity;
            target.WeakPointTurns = source.WeakPointTurns;
            target.WeakPointCritDamage = source.WeakPointCritDamage;
            target.AnalysisTurns = source.AnalysisTurns;
            target.AnalysisPenetration = source.AnalysisPenetration;
        }
        static WarriorCombatStateSaveData CaptureScalars(WarriorCombatState source) => new()
        {
            OrderTurns = source.OrderTurns,
            ProtectionTurns = source.ProtectionTurns,
            Riposte = source.Riposte,
            FreeMovementTurns = source.FreeMovementTurns,
            FreeStepsUsed = source.FreeStepsUsed,
            TraumaTurns = source.TraumaTurns,
            TraumaBonus = source.TraumaBonus,
            LastCutTarget = source.LastCutTarget,
            ConsecutiveCuts = source.ConsecutiveCuts,
            RevengeRemainder = source.RevengeRemainder,
            HealingRemainder = source.HealingRemainder,
            TrueDamageRemainder = source.TrueDamageRemainder,
            RageIdleTurns = source.RageIdleTurns,
            RageOffensiveActionUsed = source.RageOffensiveActionUsed,
            RageHadOffensiveOpportunity = source.RageHadOffensiveOpportunity,
            RageControlBlocked = source.RageControlBlocked,
        };
        static void RestoreScalars(WarriorCombatStateSaveData source, WarriorCombatState target)
        {
            target.OrderTurns = source.OrderTurns;
            target.ProtectionTurns = source.ProtectionTurns;
            target.Riposte = source.Riposte;
            target.FreeMovementTurns = source.FreeMovementTurns;
            target.FreeStepsUsed = source.FreeStepsUsed;
            target.TraumaTurns = source.TraumaTurns;
            target.TraumaBonus = source.TraumaBonus;
            target.LastCutTarget = source.LastCutTarget;
            target.ConsecutiveCuts = source.ConsecutiveCuts;
            target.RevengeRemainder = source.RevengeRemainder;
            target.HealingRemainder = source.HealingRemainder;
            target.TrueDamageRemainder = source.TrueDamageRemainder;
            target.RageIdleTurns = source.RageIdleTurns;
            target.RageOffensiveActionUsed = source.RageOffensiveActionUsed;
            target.RageHadOffensiveOpportunity = source.RageHadOffensiveOpportunity;
            target.RageControlBlocked = source.RageControlBlocked;
        }
        static MageCombatStateSaveData CaptureScalars(MageCombatState source) => new()
        {
            OffensiveTargetCount = source.OffensiveTargetCount,
            OverflowCritChance = source.OverflowCritChance,
            OverflowActionId = source.OverflowActionId,
        };
        static void RestoreScalars(MageCombatStateSaveData source, MageCombatState target)
        {
            target.OffensiveTargetCount = source.OffensiveTargetCount;
            target.OverflowCritChance = source.OverflowCritChance;
            target.OverflowActionId = source.OverflowActionId;
        }
        static PaladinCombatStateSaveData CaptureScalars(PaladinCombatState source) => new()
        {
            DamageThisTurn = source.DamageThisTurn,
            HealingDiscount = source.HealingDiscount,
        };
        static void RestoreScalars(PaladinCombatStateSaveData source, PaladinCombatState target)
        {
            target.DamageThisTurn = source.DamageThisTurn;
            target.HealingDiscount = source.HealingDiscount;
        }
        static ForestEntCombatStateSaveData CaptureScalars(ForestEntCombatState source) => new()
        {
            PhaseTwo = source.PhaseTwo,
            PhaseTwoPending = source.PhaseTwoPending,
            ArenaRoomId = source.ArenaRoomId,
            FireTurns = source.FireTurns,
            Warning = source.Warning,
            ManaCarry = source.ManaCarry,
        };
        static void RestoreScalars(ForestEntCombatStateSaveData source, ForestEntCombatState target)
        {
            target.PhaseTwo = source.PhaseTwo;
            target.PhaseTwoPending = source.PhaseTwoPending;
            target.ArenaRoomId = source.ArenaRoomId;
            target.FireTurns = source.FireTurns;
            target.Warning = source.Warning;
            target.ManaCarry = source.ManaCarry;
        }
        static DeadKingCombatStateSaveData CaptureScalars(DeadKingCombatState source) => new()
        {
            PhaseTwo = source.PhaseTwo,
            PhaseTwoPending = source.PhaseTwoPending,
            ArenaRoomId = source.ArenaRoomId,
            ShadowTurns = source.ShadowTurns,
        };
        static void RestoreScalars(DeadKingCombatStateSaveData source, DeadKingCombatState target)
        {
            target.PhaseTwo = source.PhaseTwo;
            target.PhaseTwoPending = source.PhaseTwoPending;
            target.ArenaRoomId = source.ArenaRoomId;
            target.ShadowTurns = source.ShadowTurns;
        }
        static SkeletonSlayerCombatStateSaveData CaptureScalars(SkeletonSlayerCombatState source) => new()
        {
            PhaseTwo = source.PhaseTwo,
            PhaseTwoPending = source.PhaseTwoPending,
            RevengeHits = source.RevengeHits,
            DefenseTurns = source.DefenseTurns,
            DefenseDamage = source.DefenseDamage,
            NextAttackBonus = source.NextAttackBonus,
            ActiveAttackBonus = source.ActiveAttackBonus,
            HammerTurns = source.HammerTurns,
            HammerDamage = source.HammerDamage,
            TaskUsedThisTurn = source.TaskUsedThisTurn,
            AccelerateNextTurn = source.AccelerateNextTurn,
        };
        static void RestoreScalars(SkeletonSlayerCombatStateSaveData source, SkeletonSlayerCombatState target)
        {
            target.PhaseTwo = source.PhaseTwo;
            target.PhaseTwoPending = source.PhaseTwoPending;
            target.RevengeHits = source.RevengeHits;
            target.DefenseTurns = source.DefenseTurns;
            target.DefenseDamage = source.DefenseDamage;
            target.NextAttackBonus = source.NextAttackBonus;
            target.ActiveAttackBonus = source.ActiveAttackBonus;
            target.HammerTurns = source.HammerTurns;
            target.HammerDamage = source.HammerDamage;
            target.TaskUsedThisTurn = source.TaskUsedThisTurn;
            target.AccelerateNextTurn = source.AccelerateNextTurn;
        }
        static BanditLeaderCombatStateSaveData CaptureScalars(BanditLeaderCombatState source) => new()
        {
            PhaseTwo = source.PhaseTwo,
            PhasePending = source.PhasePending,
            DamageSinceTurn = source.DamageSinceTurn,
        };
        static void RestoreScalars(BanditLeaderCombatStateSaveData source, BanditLeaderCombatState target)
        {
            target.PhaseTwo = source.PhaseTwo;
            target.PhasePending = source.PhasePending;
            target.DamageSinceTurn = source.DamageSinceTurn;
        }
    }
}
