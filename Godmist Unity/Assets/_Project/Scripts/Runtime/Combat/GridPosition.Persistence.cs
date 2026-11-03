using System;
using Godmist.Core.Serialization;
using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Runtime.Combat
{
    public partial struct GridPosition
    {
        public CombatPositionSaveData CaptureSave() => new()
        {
            X=X,Y=Y,Level=Level,StairStep=StairStep,
            FirstX=Stair.First.Cell.x,FirstY=Stair.First.Cell.y,FirstLevel=Stair.First.Level,
            SecondX=Stair.Second.Cell.x,SecondY=Stair.Second.Cell.y,SecondLevel=Stair.Second.Level
        };

        public static GridPosition RestoreSave(CombatPositionSaveData data)
        {
            if(data==null||data.StairStep<0||data.Level<0)
                throw new InvalidOperationException("Nieprawidłowa pozycja uczestnika walki.");
            var result=new GridPosition(data.X,data.Y,data.Level) {StairStep=data.StairStep};
            if(data.StairStep>0)result.Stair=new DungeonStairConnection(
                new DungeonSurfacePosition(new Vector2Int(data.FirstX,data.FirstY),data.FirstLevel),
                new DungeonSurfacePosition(new Vector2Int(data.SecondX,data.SecondY),data.SecondLevel));
            return result;
        }
    }
}
