using System;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;

namespace Godmist.Systems.Combat
{
    // The scene owner supplies spawning; headless/sandbox sessions can still show the warning alone.
    public static class CombatStallWarningSystem
    {
        public const int ReinforcementTurnThreshold = 3;
        public const string Warning = "Uwaga: ostatni przeciwnik nie otrzymał obrażeń przez 2 tury. Posiłki za 1 turę przeciwnika, jeśli nadal nie otrzyma obrażeń!";

        public static BattleUnit Synchronize(CombatSession session)
        {
            if (session == null) return null;
            BattleUnit remaining = null;
            if (session.Outcome == CombatOutcomeState.Ongoing)
                foreach (var unit in session.Units)
                {
                    if (unit == null || unit.IsPlayerSide || unit.Character?.LifeState != CharacterLifeState.Alive) continue;
                    if (remaining != null) { remaining = null; break; }
                    remaining = unit;
                }
            if (remaining == null || remaining != session.StallTrackedEnemy)
            {
                session.StallTrackedEnemy = remaining;
                session.EnemyTurnsWithoutDamage = 0;
                session.EnemyDamagedSinceLastTurn = false;
                session.StallEnemyInvisibleAtTurnStart = false;
            }
            return remaining;
        }

        public static void RecordDamage(CombatSession session, BattleUnit target, int damage)
        {
            if (Synchronize(session) == null || target == null || target.IsPlayerSide || damage <= 0) return;
            session.EnemyTurnsWithoutDamage = 0;
            session.EnemyDamagedSinceLastTurn = true;
        }

        public static void EndUnitTurn(CombatSession session, BattleUnit unit, Action<string> battleLog)
        {
            var enemy = Synchronize(session);
            if (enemy == null || enemy != unit) return;
            // An untargetable last enemy cannot be damaged by ordinary actions.
            // Its own invisibility must not turn the anti-stall safeguard against the player.
            if (session.StallEnemyInvisibleAtTurnStart || StatusEffectSystem.IsInvisible(enemy))
            {
                session.EnemyTurnsWithoutDamage = 0;
                session.EnemyDamagedSinceLastTurn = false;
                session.StallEnemyInvisibleAtTurnStart = false;
                return;
            }
            session.StallEnemyInvisibleAtTurnStart = false;
            if (session.EnemyDamagedSinceLastTurn)
                session.EnemyTurnsWithoutDamage = 0;
            else if (session.EnemyTurnsWithoutDamage < ReinforcementTurnThreshold)
            {
                session.EnemyTurnsWithoutDamage++;
                if (session.EnemyTurnsWithoutDamage == ReinforcementTurnThreshold - 1)
                    battleLog?.Invoke(Warning);
            }
            session.EnemyDamagedSinceLastTurn = false;
            if (session.EnemyTurnsWithoutDamage >= ReinforcementTurnThreshold
                && session.TrySpawnReinforcements?.Invoke() == true)
            {
                Synchronize(session);
                battleLog?.Invoke("Nadeszły posiłki!");
            }
        }

        public static void BeginUnitTurn(CombatSession session, BattleUnit unit)
        {
            if (Synchronize(session) == unit)
                session.StallEnemyInvisibleAtTurnStart = StatusEffectSystem.IsInvisible(unit);
        }
    }
}
