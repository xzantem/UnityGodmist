using System;
using System.Collections.Generic;
using Godmist.Core.Enums;

namespace Godmist.Runtime.Combat
{
    [Serializable]
    public class CombatSession
    {
        public string CombatId = string.Empty;
        public CombatOutcomeState Outcome = CombatOutcomeState.Ongoing;
        public int CurrentEscapeChancePercent = 60;
        public CombatRewardResult Rewards = new();
        public bool RewardsGranted;
        public bool EscapePenaltyApplied;
        public bool HasEscapePenaltyEligibleCharacters;
        public HashSet<string> EscapePenaltyEligibleCharacterIds = new();
        public bool IsDebugSession;
        public float ActionPointsPerSpeed = 0.25f;
        public BattleGrid Grid;
        public List<BattleUnit> Units = new();
        public List<ScheduledCombatEffect> ScheduledEffects = new();
        public List<SummonedBattleUnit> Summons = new();
        public HashSet<string> DefeatedEnemyInstanceIds = new();
        public List<CombatMapObject> MapObjects = new();
        public List<SummonedCombatMapObject> SummonedObjects = new();
        public List<SummonedCombatMapObject> SummonedObjectTurnOrder = new();

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
    }
}
