using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Runtime.Combat;
using Godmist.Views.Exploration;
using Godmist.Systems.Combat;

namespace Godmist.Views.Dungeons
{
    public sealed partial class DungeonCombatCoordinator
    {
        private void RestoreSavedCombats()
        {
            if (!game.HasCombat) return;
            foreach(var session in game.Combats)
            {
                var restored=new Dictionary<BattleUnit,ExplorationCharacterView>();
                foreach(var unit in session.Units)
                {
                    if(session.Summons.Any(s=>s.Unit==unit))continue;
                    ExplorationCharacterView view;
                    if(unit.IsPlayerSide)
                    {
                        view=party.ActiveMemberViews.Single(v=>v.CharacterId==unit.Character.CharacterId);
                        retainedPlayers[unit.Character.CharacterId]=unit;
                    }
                    else
                    {
                        var source=session.EnemySources[unit.Character.CharacterId];
                        view=encounters.Triggers.Single(t=>t.Encounter.InstanceId==source.EncounterId)
                            .EnemyViews.Single(v=>v.EnemyInstanceId==source.EnemyId);
                    }
                    restored.Add(unit,view);
                }
                foreach(var unit in session.DepartedUnits.Values.Where(u=>u.IsPlayerSide))
                    retainedPlayers[unit.Character.CharacterId]=unit;
                if(!BindSession(session,restored,driver=>driver.ResumeSavedPlayerTurn(),restoring:true))
                    throw new InvalidOperationException("Nie udało się powiązać zapisanej walki: "+session.CombatId);
            }
            foreach(var trigger in encounters.Triggers)
                if(game.Combats.Any(c=>c.EnemySources.Values.Any(s=>s.EncounterId==trigger.Encounter.InstanceId)))
                    trigger.MarkTriggered();
            party.SeparateForCombat();
        }
    }
}
