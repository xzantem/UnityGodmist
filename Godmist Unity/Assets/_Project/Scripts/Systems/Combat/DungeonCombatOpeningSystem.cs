using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;

namespace Godmist.Systems.Combat
{
    public static class DungeonCombatOpeningSystem
    {
        // The scene owner supplies the ordinary room alarm and combat visibility rules.
        public static HashSet<BattleUnit> CollectParticipants(CombatSession session, BattleUnit actor,
            IEnumerable<BattleUnit> directTargets, BattleUnit anchor,
            Func<BattleUnit, IEnumerable<BattleUnit>> roomAlarm,
            Func<BattleUnit, BattleUnit, bool> canObserve)
        {
            var result = new HashSet<BattleUnit>();
            if (session == null || actor?.IsPlayerControlled != true || !session.Units.Contains(actor)) return result;
            result.Add(actor);
            foreach (var target in directTargets.Append(anchor).Where(u => u != null && session.Units.Contains(u)
                && u.Character?.LifeState == CharacterLifeState.Alive)) result.Add(target);
            foreach (var enemy in result.Where(u => !u.IsPlayerSide).ToArray())
                foreach (var peer in roomAlarm(enemy))
                    if (session.Units.Contains(peer) && !peer.IsPlayerSide
                        && peer.Character?.LifeState == CharacterLifeState.Alive) result.Add(peer);
            foreach (var ally in session.Units.Where(u => u.IsPlayerSide && u.Character?.LifeState == CharacterLifeState.Alive))
                if (result.Any(enemy => !enemy.IsPlayerSide && canObserve(enemy, ally))) result.Add(ally);
            return result;
        }
    }
}
