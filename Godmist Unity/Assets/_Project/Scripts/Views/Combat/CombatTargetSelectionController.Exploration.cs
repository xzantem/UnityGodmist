using System;
using System.Collections.Generic;
using Godmist.Runtime.Combat;
using UnityEngine;

namespace Godmist.Views.Combat
{
    public partial class CombatTargetSelectionController
    {
        // Pointer hit testing only. Return the same BattleUnit/grid identity
        // used by ordinary targeting, but locate its clickable marker at feet.
        public static BattleUnit FindUnitAtFeet(IEnumerable<BattleUnit> units,
            Func<BattleUnit,Vector2?> feet,Vector2 pointer,float cellSize)
        {
            if(units==null||feet==null)return null;
            BattleUnit closest=null;var distance=float.PositiveInfinity;var half=Mathf.Max(.01f,cellSize)*.5f;
            foreach(var unit in units)
            {
                if(unit==null||feet(unit) is not Vector2 position)continue;
                var delta=pointer-position;
                if(Mathf.Abs(delta.x)>half||Mathf.Abs(delta.y)>half||delta.sqrMagnitude>=distance)continue;
                closest=unit;distance=delta.sqrMagnitude;
            }
            return closest;
        }
    }
}
