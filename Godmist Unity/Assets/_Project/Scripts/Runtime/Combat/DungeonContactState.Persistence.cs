using System;
using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Serialization;
using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Runtime.Combat
{
    public sealed partial class DungeonContactState
    {
        public IEnumerable<BattleUnit> ReferencedUnits => memory.Keys.SelectMany(p=>new[]{p.Enemy,p.Target})
            .Concat(completed.Keys).Concat(completed.Values.SelectMany(p=>p.Keys))
            .Concat(started.Keys).Concat(started.Values.SelectMany(p=>p)).Concat(seenPlayers).Distinct();

        public void CaptureSave(CombatSaveData data)
        {
            foreach(var p in memory)
            {
                var s=p.Value;
                data.Contact.Add(new CombatContactSaveData {EnemyId=p.Key.Enemy.Character.CharacterId,
                    TargetId=p.Key.Target.Character.CharacterId,Position=SavePoint(s.Position),SearchPosition=SavePoint(s.SearchPosition),
                    DirectionX=s.Direction.x,DirectionY=s.Direction.y,ForwardSteps=s.ForwardSteps,Checked=s.Checked,
                    Visible=visible.Contains(p.Key),WasVisible=previousVisible.Contains(p.Key)});
            }
            foreach(var target in completed)foreach(var enemy in target.Value)
                data.ContactCompleted.Add(new() {Id=target.Key.Character.CharacterId,OtherId=enemy.Key.Character.CharacterId,Value=enemy.Value});
            foreach(var enemy in started)data.ContactStarted.Add(new()
                {Id=enemy.Key.Character.CharacterId,Values=enemy.Value.Select(p=>p.Character.CharacterId).ToList()});
            data.ContactSeenPlayers=seenPlayers.Select(p=>p.Character.CharacterId).ToList();
        }

        public void RestoreSave(CombatSaveData data,Func<string,BattleUnit> unit)
        {
            memory.Clear();previousVisible.Clear();visible.Clear();completed.Clear();started.Clear();seenPlayers.Clear();
            foreach(var p in data.Contact)
            {
                var key=(unit(p.EnemyId),unit(p.TargetId));
                memory.Add(key,new Observation(LoadPoint(p.Position)) {SearchPosition=LoadPoint(p.SearchPosition),
                    Direction=new(p.DirectionX,p.DirectionY),ForwardSteps=p.ForwardSteps,Checked=p.Checked});
                if(p.Visible)visible.Add(key);if(p.WasVisible)previousVisible.Add(key);
            }
            foreach(var p in data.ContactCompleted)
            {
                var target=unit(p.Id);if(!completed.TryGetValue(target,out var turns))completed.Add(target,turns=new());
                turns.Add(unit(p.OtherId),p.Value);
            }
            foreach(var p in data.ContactStarted)started.Add(unit(p.Id),new HashSet<BattleUnit>(p.Values.Select(unit)));
            seenPlayers.UnionWith(data.ContactSeenPlayers.Select(unit));
        }

        static CombatPositionSaveData SavePoint(GridPosition position) => position.CaptureSave();
        static GridPosition LoadPoint(CombatPositionSaveData position) => GridPosition.RestoreSave(position);
    }
}
