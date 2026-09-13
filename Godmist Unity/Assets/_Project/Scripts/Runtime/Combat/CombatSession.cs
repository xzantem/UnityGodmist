using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Configuration;

namespace Godmist.Runtime.Combat
{
    [Serializable]
    public class CombatSession
    {
        public string CombatId = string.Empty;
        public CombatOutcomeState Outcome = CombatOutcomeState.Ongoing;
        public int CurrentEscapeChancePercent = GameDefinesRuntime.GetInt(
            GameDefineIds.EscapeBaseChancePercent, 60);
        public CombatRewardResult Rewards = new();
        public bool RewardsGranted;
        public bool EscapePenaltyApplied;
        public bool HasEscapePenaltyEligibleCharacters;
        public HashSet<string> EscapePenaltyEligibleCharacterIds = new();
        public bool IsDebugSession;
        [NonSerialized] public DungeonCombatContext DungeonContext;
        public bool IsSeamless;
        // Rescue eligibility is supplied by the dungeon owner, independent of UI focus.
        public bool CanReceiveReinforcements;
        public readonly Dictionary<string, BattleUnit> DepartedUnits = new();
        public readonly HashSet<BattleCellEdge> OpenExitEdges = new();
        [NonSerialized] public Func<GridPosition, bool> ExternalPositionOccupied;
        public bool IsExitPosition(GridPosition position) => IsSeamless && !Grid.IsWalkable(position);
        public bool CanMoveBetween(BattleUnit unit, GridPosition from, GridPosition to)
        {
            if (ExternalPositionOccupied?.Invoke(to) == true) return false;
            if (Grid.CanMoveBetween(from, to)) return true;
            return IsSeamless && unit?.IsPlayerSide == true && Grid.IsWalkable(from)
                && !Grid.IsBlocked(from) && IsExitPosition(to)
                && Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y) == 1
                && OpenExitEdges.Contains(new BattleCellEdge(from, to));
        }
        public DungeonTheme DungeonTheme = DungeonTheme.Story;
        public float ActionPointsPerSpeed = GameDefinesRuntime.GetFloat(
            GameDefineIds.ActionPointsPerSpeed, 0.25f);
        public BattleGrid Grid;
        public List<BattleUnit> Units = new();
        public List<ScheduledCombatEffect> ScheduledEffects = new();
        public List<SummonedBattleUnit> Summons = new();
        public HashSet<string> DefeatedEnemyInstanceIds = new();
        private readonly Dictionary<string, GridPosition> enemyDeathPositions = new();
        public List<CombatMapObject> MapObjects = new();
        public List<SummonedCombatMapObject> SummonedObjects = new();
        public List<SummonedCombatMapObject> SummonedObjectTurnOrder = new();
        [NonSerialized] public PassiveEffectCombatEventDispatcher PassiveEffectEventDispatcher;

        public List<CombatTurnParticipant> TurnParticipants = new();
        public List<BattleUnit> TurnOrder = new();
        public int ActiveTurnIndex;
        public BattleUnit ActiveUnit;
        public CombatTurnParticipant ActiveParticipant;
        public SummonedCombatMapObject ActiveSummonedObject;

        public bool IsPositionOccupied(
            GridPosition position,
            BattleUnit ignoredUnit = null)
        {
            if (ExternalPositionOccupied?.Invoke(position) == true) return true;
            foreach (var unit in Units)
            {
                if (unit == null || unit == ignoredUnit)
                {
                    continue;
                }

                if (unit.Position == position)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsPositionOccupiedByMapObject(
            GridPosition position,
            CombatMapObject ignoredObject = null)
        {
            foreach (var mapObject in MapObjects)
            {
                if (mapObject == null || mapObject == ignoredObject)
                {
                    continue;
                }

                if (mapObject.Position == position)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsPositionOccupiedByLivingUnit(GridPosition position)
        {
            foreach (var unit in Units)
            {
                if (unit?.Character == null
                    || unit.Character.LifeState == CharacterLifeState.Dead)
                {
                    continue;
                }

                if (unit.Position == position)
                {
                    return true;
                }
            }

            return false;
        }

        public bool DoesObjectBlockMovement(CombatMapObject mapObject)
        {
            if (mapObject == null || !mapObject.BlocksMovement)
            {
                return false;
            }

            return !IsPositionOccupiedByLivingUnit(mapObject.Position);
        }

        public bool DoesObjectBlockLineOfSight(CombatMapObject mapObject)
        {
            if (mapObject == null || !mapObject.BlocksLineOfSight)
            {
                return false;
            }

            return !IsPositionOccupiedByLivingUnit(mapObject.Position);
        }

        public CombatMapObject GetMapObjectAt(GridPosition position)
        {
            foreach (var mapObject in MapObjects)
            {
                if (mapObject != null && mapObject.Position == position)
                {
                    return mapObject;
                }
            }

            return null;
        }

        public CombatMapObject GetMapObject(string objectId)
        {
            foreach (var mapObject in MapObjects)
            {
                if (mapObject != null && mapObject.ObjectId == objectId)
                {
                    return mapObject;
                }
            }

            return null;
        }

        public SummonedCombatMapObject GetSummonedObject(
            string objectInstanceId)
        {
            foreach (var summonedObject in SummonedObjects)
            {
                if (summonedObject != null
                    && summonedObject.ObjectId == objectInstanceId)
                {
                    return summonedObject;
                }
            }

            return null;
        }

        public bool TryMarkEnemyDefeated(string enemyInstanceId)
        {
            if (string.IsNullOrWhiteSpace(enemyInstanceId))
            {
                return false;
            }

            return DefeatedEnemyInstanceIds.Add(enemyInstanceId);
        }

        public bool TryMarkEnemyDefeated(
            string enemyInstanceId,
            GridPosition deathPosition)
        {
            if (!TryMarkEnemyDefeated(enemyInstanceId))
            {
                return false;
            }

            enemyDeathPositions[enemyInstanceId] = deathPosition;
            return true;
        }

        public bool TryGetEnemyDeathPosition(
            string enemyInstanceId,
            out GridPosition deathPosition)
        {
            deathPosition = default;

            return !string.IsNullOrWhiteSpace(enemyInstanceId)
                && enemyDeathPositions.TryGetValue(
                    enemyInstanceId,
                    out deathPosition);
        }
    }
}
