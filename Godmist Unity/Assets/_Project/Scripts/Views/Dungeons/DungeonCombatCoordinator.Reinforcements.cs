using System.Collections.Generic;
using System.Linq;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using Godmist.Systems.Dungeons;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public sealed partial class DungeonCombatCoordinator
    {
        private bool TrySpawnReinforcements(CombatSession session)
        {
            if (game?.ActiveDungeon == null || !game.Combats.Contains(session)
                || session.MembershipChangePending || encounters == null) return false;
            var driver = combatHost.GetDriver(session);
            if (driver == null) return false;
            var occupied = new HashSet<Vector2Int>(party.ActiveMemberViews.Where(v => v != null && v.isActiveAndEnabled)
                .Select(v => map.WorldToCell(v.Position)));
            occupied.UnionWith(encounters.EnemyViews.Where(v => v != null && v.isActiveAndEnabled).Select(v => map.WorldToCell(v.Position)));
            var wave = floor.Encounters.Count(e => e.IsReinforcement);
            var random = new System.Random(unchecked(game.ActiveDungeon.Seed + 7919 * (wave + 1) + 397 * floor.FloorIndex));
            if (!DungeonReinforcementSystem.TryPlan(game.ActiveDungeon, floor, session, occupied, random, out var encounter)) return false;
            var trigger = encounters.SpawnAdditionalEncounter(encounter, floor, party, game.ActiveDungeon.DungeonLevel);
            if (trigger == null) return false;
            var added = new List<BattleUnit>();
            foreach (var view in trigger.EnemyViews)
            {
                var position = DungeonBattleGridFactory.ToBattlePosition(session.DungeonContext, map.WorldToCell(view.Position));
                var unit = sessionFactory.CreateJoiningUnit(view.RuntimeCharacter, position, false);
                if (!driver.TryJoin(unit, position)) { Rollback(); return false; }
                added.Add(unit);
            }
            if (added.Count != encounter.Enemies.Count || !floor.TryAddEncounter(encounter)) { Rollback(); return false; }
            var emptyRewards = new CombatRewardResult();
            game.ActiveDungeon.TryStorePreRolledRewards(encounter.InstanceId, emptyRewards);
            for (var i = 0; i < added.Count; i++)
            {
                var unit = added[i]; var view = trigger.EnemyViews[i];
                DungeonCombatMembershipSystem.RegisterEnemy(session, encounter.InstanceId, view.EnemyInstanceId, emptyRewards, unit);
                ConfigureView(view, "enemy:" + encounter.InstanceId + ":" + view.EnemyInstanceId);
                bindings.Add(unit, view); bindingSessions.Add(unit, session);
                unitViews.Borrow(session, unit, view);
            }
            trigger.SetCombatCoordinator(this);
            trigger.MarkTriggered();
            InvalidateFovPreview();
            UpdateFog(true);
            return true;

            void Rollback()
            {
                foreach (var unit in added)
                { session.Units.Remove(unit); session.TurnParticipants.RemoveAll(p => p.Unit == unit); }
                CombatTurnSystem.ReorderParticipants(session);
                encounters.RemoveAdditionalEncounter(trigger);
            }
        }
    }
}
