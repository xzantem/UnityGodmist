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
        
        public BattleGrid Grid;
        public List<BattleUnit> Units = new();
        public List<CombatMapObject> MapObjects = new();

        public List<BattleUnit> TurnOrder = new();
        public int ActiveTurnIndex; 
        public BattleUnit ActiveUnit;

        public bool IsPositionOccupied(GridPosition position, BattleUnit ignoredUnit = null)
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

        public CombatMapObject GetMapObjectAt(GridPosition position)
        {
            foreach (var mapObject in MapObjects)
            {
                if (mapObject != null && mapObject.Position == position) return mapObject;
            }

            return null;
        }
        public CombatMapObject GetMapObject(string objectId)
        {
            foreach (var mapObject in MapObjects)
            {
                if (mapObject != null && mapObject.ObjectId == objectId) return mapObject;
            }

            return null;
        }
    }
}