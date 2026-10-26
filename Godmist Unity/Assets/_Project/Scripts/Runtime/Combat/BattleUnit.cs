using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Constants;
using Godmist.Data.Configuration;
using Godmist.Runtime.Characters;
using UnityEngine;

namespace Godmist.Runtime.Combat
{
    public static class ActionPointUnits
    {
        public const int PerPoint = 100;
        public static int FromPoints(float points) => Mathf.RoundToInt(points * PerPoint);
        public static float ToPoints(int units) => units / (float)PerPoint;
        public static string Format(int units) => ToPoints(units).ToString("0.##");
        public static int PipCount(int units) => Mathf.CeilToInt(Mathf.Max(0, units) / (float)PerPoint);
        public static float PipFill(int units, int index) => Mathf.Clamp01(ToPoints(units) - index);
    }

    [Serializable]
    public class BattleUnit
    {
        [NonSerialized] public ForestEntCombatState ForestEntState;
        [NonSerialized] public DeadKingCombatState DeadKingState;
        [NonSerialized] public SkeletonSlayerCombatState SkeletonSlayerState;
        [NonSerialized] public BanditLeaderCombatState BanditLeaderState;
        public int DeadKingKnightHits;
        [NonSerialized] public bool RibbedTunicProtectionUsed;
        [NonSerialized] public int PoacherArmorCooldown, PoacherArmorShieldTurns;
        public CharacterInstance Character;
        public readonly ScoutTargetState ScoutState = new();
        public readonly WarriorCombatState WarriorState = new();
        public readonly MageCombatState MageState = new();
        public readonly PaladinCombatState PaladinState = new();
        public GridPosition Position;

        // AP amounts are stored in hundredths. Speed multipliers and authored flat costs remain in whole AP.
        public int CurrentActionPoints;
        public int MaxActionPoints;
        public int MovementStepsThisTurn;
        public bool HasInitializedCombatResources;
        // Survives departure/merge; cleared when the turn is consumed or the combat finishes.
        [NonSerialized] public bool HasUnfinishedCombatTurn;
        public int MaxKnives;
        public int AvailableKnives;
        public float CurrentActionValue;

        // Combat-scoped state. It intentionally lives on the battle participant rather
        // than on CharacterInstance, so it cannot leak into exploration/save state.
        public int NextTurnActionPointModifier;
        public int LastSkillResourceSpent;
        public int ResourceRegenerationBlockedTurns;
        public string DamageRedirectProtectorCharacterId = string.Empty;
        public float DamageRedirectPercent;
        public List<UniqueBuffType> UniqueBuffs = new();
        public Dictionary<ResistanceType, float> ResistanceModifiers = new();
        public List<CombatAuraRuntime> ActiveAuras = new();
        public List<CombatDamageRedirectRuntime> OutgoingDamageRedirects = new();
        public HashSet<string> ActiveToggleSkillIds = new();
        public HashSet<string> ToggleSkillIdsUsedThisTurn = new();
        public HashSet<ConsumableUseType> ConsumableUseTypesUsedThisTurn = new();
        public Dictionary<string, StatusToleranceRuntime> StatusTolerances = new();

        public bool HasReacted;
        public bool IsReadyToEscape;
        public bool IsPlayerSide;
        public bool IsAiControlledSummon;
        public bool IsStationarySummon;
        public float PendingEquipmentActionValueReduction;
        public bool IsPlayerControlled => IsPlayerSide && !IsAiControlledSummon;
        public bool IsBoss;
        public int DownedTurnsRemaining;

        // Suppression statuses share one combat-local adaptation pool.
        private static string GetToleranceKey(string statusId) =>
            StatusEffectIds.UsesSuppressionResistance(statusId)
                ? "suppression" : statusId?.ToLowerInvariant();

        public void AddStatusTolerance(string statusId, int statusDurationTurns = 1)
        {
            if (string.IsNullOrWhiteSpace(statusId)) return;
            var isSuppression = StatusEffectIds.UsesSuppressionResistance(statusId);
            statusId = GetToleranceKey(statusId);
            StatusTolerances ??= new Dictionary<string, StatusToleranceRuntime>();
            if (!StatusTolerances.TryGetValue(statusId, out var tolerance)
                || tolerance == null || tolerance.RemainingTurns <= 0)
            {
                tolerance = new StatusToleranceRuntime { RemainingTurns = 2 };
                StatusTolerances[statusId] = tolerance;
            }

            tolerance.Stacks = Math.Max(1, tolerance.Stacks + 1);
            // The two-turn base is granted once; later CC adds only its own duration.
            tolerance.RemainingTurns = isSuppression
                ? tolerance.RemainingTurns + Math.Max(1, statusDurationTurns)
                : Math.Max(tolerance.RemainingTurns, 2);
        }

        public float GetStatusTolerance(string statusId)
        {
            statusId = GetToleranceKey(statusId);
            return string.IsNullOrWhiteSpace(statusId)
                || StatusTolerances == null
                || !StatusTolerances.TryGetValue(statusId, out var tolerance)
                || tolerance == null
                || tolerance.RemainingTurns <= 0
                ? 0f
                : Mathf.Clamp01(tolerance.Stacks * GameDefinesRuntime.GetFloat(
                    GameDefineIds.StatusTolerancePerStack, .25f));
        }

        public void AdvanceStatusTolerances()
        {
            if (StatusTolerances == null) return;
            var expired = new List<string>();
            foreach (var pair in StatusTolerances)
            {
                if (pair.Value == null || --pair.Value.RemainingTurns <= 0)
                    expired.Add(pair.Key);
            }

            foreach (var statusId in expired)
                StatusTolerances.Remove(statusId);
        }

        public static int DownedTurnLimit => GameDefinesRuntime.GetInt(
            GameDefineIds.DownedTurnLimit, 3);
    }

    [Serializable]
    public class CombatAuraRuntime
    {
        public string AuraId = string.Empty;
        public string DisplayName = string.Empty;
        public string StatusId = string.Empty;
        public BattleUnit Creator;
        public int RemainingTurns;
        public int Radius;
        public bool IncludesCaster;
        public TargetingType TargetingType;
        public float Chance = 1f;
        public int DurationTurns = 1;
        public float StatusDamageMultiplier;
        public int StatusDamagePerTurn;
    }

    [Serializable]
    public sealed class StatusToleranceRuntime
    {
        public int Stacks;
        public int RemainingTurns;
    }

    [Serializable]
    public class CombatDamageRedirectRuntime
    {
        [NonSerialized] public BattleUnit Target;
        public string SourceEffectId = string.Empty;
        public string TargetCharacterId = string.Empty;
        public string TargetDisplayName = string.Empty;
        public float RedirectPercent;
    }
}
