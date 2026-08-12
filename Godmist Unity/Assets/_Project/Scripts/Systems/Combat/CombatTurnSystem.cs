using System;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;

namespace Godmist.Systems.Combat
{
    public class CombatTurnSystem
    {
        public const float BaseActionValue = 10000f;

        private readonly Action<string> battleLog;
        private readonly Action<BattleUnit> onUnitTurnBegan;
        private readonly Action<BattleUnit> onUnitTurnEnded;

        public CombatTurnSystem(
            Action<string> battleLog = null,
            Action<BattleUnit> onUnitTurnBegan = null,
            Action<BattleUnit> onUnitTurnEnded = null)
        {
            this.battleLog = battleLog;
            this.onUnitTurnBegan = onUnitTurnBegan;
            this.onUnitTurnEnded = onUnitTurnEnded;
        }

        public void InitializeTurnOrder(CombatSession session)
        {
            if (session == null)
            {
                return;
            }

            session.TurnOrder = session.Units
                .Where(IsScheduledUnit)
                .ToList();

            foreach (var unit in session.TurnOrder)
            {
                unit.CurrentActionValue =
                    CalculateBaseActionValue(unit);
            }

            session.ActiveUnit = null;
            session.ActiveTurnIndex = -1;

            AdvanceTurn(session);
        }

        public void EndActiveTurn(CombatSession session)
        {
            if (session?.ActiveUnit == null || session.Outcome != CombatOutcomeState.Ongoing) return;

            if (!IsAlive(session.ActiveUnit))
            {
                AdvanceTurn(session);
                return;
            }

            battleLog?.Invoke($"{session.ActiveUnit.Character.Definition.DisplayName} konczy ture");

            onUnitTurnEnded?.Invoke(session.ActiveUnit);

            session.ActiveUnit.CurrentActionValue = CalculateBaseActionValue(session.ActiveUnit);

            session.ActiveUnit = null;

            AdvanceTurn(session);
        }

        public void AdvanceTurn(CombatSession session)
        {
            if (session == null
                || session.Outcome
                    != CombatOutcomeState.Ongoing)
            {
                return;
            }

            RemoveDeadUnitsFromTurnOrder(session);

            if (session.TurnOrder.Count == 0)
            {
                session.ActiveUnit = null;
                session.ActiveTurnIndex = -1;
                return;
            }

            while (session.TurnOrder.Count > 0)
            {
                var nextUnit =
                    FindNextScheduledUnit(session);

                if (nextUnit == null)
                {
                    session.ActiveUnit = null;
                    session.ActiveTurnIndex = -1;
                    return;
                }

                var elapsedActionValue =
                    Math.Max(0f, nextUnit.CurrentActionValue);

                ReduceActionValues(
                    session,
                    elapsedActionValue);

                nextUnit.CurrentActionValue = 0f;

                if (nextUnit.Character.LifeState
                    == CharacterLifeState.Downed)
                {
                    var wasRemoved =
                        ProcessDownedTurn(session, nextUnit);

                    if (wasRemoved)
                    {
                        RemoveDeadUnitsFromTurnOrder(
                            session);

                        if (session.TurnOrder.Count == 0)
                        {
                            session.ActiveUnit = null;
                            session.ActiveTurnIndex = -1;
                            return;
                        }

                        continue;
                    }

                    nextUnit.CurrentActionValue =
                        CalculateBaseActionValue(nextUnit);

                    SortTurnOrderByActionValue(session);
                    continue;
                }

                if (!IsAlive(nextUnit))
                {
                    RemoveDeadUnitsFromTurnOrder(session);
                    continue;
                }

                session.ActiveUnit = nextUnit;

                SortTurnOrderByActionValue(session);

                BeginActiveTurn(
                    session,
                    nextUnit);

                return;
            }

            session.ActiveUnit = null;
            session.ActiveTurnIndex = -1;
        }

        public void BeginActiveTurn(
            CombatSession session,
            BattleUnit unit)
        {
            if (session == null
                || !IsAlive(unit))
            {
                return;
            }

            battleLog?.Invoke(
                $"Tura: {unit.Character.Definition.DisplayName}");

            RefreshMaxActionPoints(session, unit, refillCurrentActionPoints: true);

            unit.HasReacted = false;
            unit.MovementStepsThisTurn = 0;

            onUnitTurnBegan?.Invoke(unit);
        }

        public int CalculateMaxActionPoints(
            CharacterInstance character,
            float actionPointsPerSpeed)
        {
            if (character == null)
            {
                return 0;
            }

            var speed =
                character.Stats.Speed.GetValue();

            return UnityEngine.Mathf.Max(
                0,
                UnityEngine.Mathf.RoundToInt(
                    speed * actionPointsPerSpeed));
        }

        public static void RefreshMaxActionPoints(
            CombatSession session,
            BattleUnit unit,
            bool refillCurrentActionPoints = false)
        {
            if (session == null || unit?.Character?.Stats?.Speed == null)
            {
                return;
            }

            var speed = unit.Character.Stats.Speed.GetValue();
            var maxActionPoints = UnityEngine.Mathf.Max(
                0,
                UnityEngine.Mathf.RoundToInt(
                    speed * session.ActionPointsPerSpeed));

            unit.MaxActionPoints = maxActionPoints;
            unit.CurrentActionPoints = refillCurrentActionPoints
                ? maxActionPoints
                : UnityEngine.Mathf.Min(unit.CurrentActionPoints, maxActionPoints);
        }

        public static void RefreshMaxActionPoints(
            CombatSession session,
            bool refillActiveUnit = false)
        {
            if (session?.Units == null)
            {
                return;
            }

            foreach (var unit in session.Units)
            {
                RefreshMaxActionPoints(
                    session,
                    unit,
                    refillActiveUnit && unit == session.ActiveUnit);
            }
        }

        private static BattleUnit FindNextScheduledUnit(
            CombatSession session)
        {
            return session.TurnOrder
                .Where(IsScheduledUnit)
                .OrderBy(unit => unit.CurrentActionValue)
                .FirstOrDefault();
        }

        private static void ReduceActionValues(
            CombatSession session,
            float amount)
        {
            foreach (var unit in session.TurnOrder)
            {
                if (!IsScheduledUnit(unit))
                {
                    continue;
                }

                unit.CurrentActionValue =
                    UnityEngine.Mathf.Max(
                        0f,
                        unit.CurrentActionValue - amount);
            }
        }

        private static void SortTurnOrderByActionValue(
            CombatSession session)
        {
            session.TurnOrder = session.TurnOrder
                .Where(IsScheduledUnit)
                .OrderBy(unit => unit.CurrentActionValue)
                .ToList();

            session.ActiveTurnIndex =
                session.ActiveUnit == null
                    ? -1
                    : session.TurnOrder.IndexOf(
                        session.ActiveUnit);
        }

        private static void RemoveDeadUnitsFromTurnOrder(
            CombatSession session)
        {
            session.TurnOrder.RemoveAll(
                unit => unit == null
                    || unit.Character == null
                    || unit.Character.LifeState
                        == CharacterLifeState.Dead);

            session.ActiveTurnIndex =
                session.ActiveUnit == null
                    ? -1
                    : session.TurnOrder.IndexOf(
                        session.ActiveUnit);
        }

        private static float CalculateBaseActionValue(
            BattleUnit unit)
        {
            var speed =
                unit.Character.Stats.Speed.GetValue();

            speed = UnityEngine.Mathf.Max(
                0.01f,
                speed);

            return BaseActionValue / speed;
        }

        private static bool IsScheduledUnit(
            BattleUnit unit)
        {
            return unit?.Character != null
                && unit.Character.LifeState
                    != CharacterLifeState.Dead;
        }

        private static bool IsAlive(
            BattleUnit unit)
        {
            return unit?.Character != null
                && unit.Character.LifeState
                    == CharacterLifeState.Alive;
        }

        private bool ProcessDownedTurn(
            CombatSession session,
            BattleUnit unit)
        {
            unit.DownedTurnsRemaining =
                Math.Max(
                    0,
                    unit.DownedTurnsRemaining - 1);

            battleLog?.Invoke(
                $"{unit.Character.Definition.DisplayName} " +
                "nie wykonuje tury jako powalony. " +
                $"Pozostale tury: " +
                $"{unit.DownedTurnsRemaining}");

            if (unit.DownedTurnsRemaining > 0)
            {
                return false;
            }

            unit.Character.CurrentHealth = 0;
            unit.Character.LifeState =
                CharacterLifeState.Dead;

            battleLog?.Invoke(
                $"{unit.Character.Definition.DisplayName} " +
                "ginie po uplywie czasu na podniesienie.");

            session.Units.Remove(unit);
            session.TurnOrder.Remove(unit);

            return true;
        }
    }
}
