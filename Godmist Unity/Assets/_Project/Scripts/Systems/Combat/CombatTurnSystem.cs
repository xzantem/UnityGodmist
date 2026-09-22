using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Configuration;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;

namespace Godmist.Systems.Combat
{
    public class CombatTurnSystem
    {
        public static float BaseActionValue => GameDefinesRuntime.GetFloat(
            GameDefineIds.BaseActionValue, 10000f);

        private readonly Action<string> battleLog;
        private readonly Action<BattleUnit> onUnitTurnBegan;
        private readonly Action<BattleUnit> onUnitTurnEnded;
        private readonly Action<SummonedCombatMapObject> onSummonedObjectTurnBegan;
        private readonly Action<SummonedCombatMapObject> onSummonedObjectTurnEnded;
        private readonly Action<BattleUnit> onUnitDied;
        private readonly PassiveEffectCombatEventDispatcher passiveEffectEventDispatcher;

        public CombatTurnSystem(
            Action<string> battleLog = null,
            Action<BattleUnit> onUnitTurnBegan = null,
            Action<BattleUnit> onUnitTurnEnded = null,
            Action<SummonedCombatMapObject> onSummonedObjectTurnBegan = null,
            Action<SummonedCombatMapObject> onSummonedObjectTurnEnded = null,
            Action<BattleUnit> onUnitDied = null,
            PassiveEffectCombatEventDispatcher passiveEffectEventDispatcher = null)
        {
            this.battleLog = battleLog;
            this.onUnitTurnBegan = onUnitTurnBegan;
            this.onUnitTurnEnded = onUnitTurnEnded;
            this.onSummonedObjectTurnBegan = onSummonedObjectTurnBegan;
            this.onSummonedObjectTurnEnded = onSummonedObjectTurnEnded;
            this.onUnitDied = onUnitDied;
            this.passiveEffectEventDispatcher = passiveEffectEventDispatcher;
        }

        public void InitializeTurnOrder(CombatSession session)
        {
            if (session == null)
            {
                return;
            }

            session.TurnParticipants ??= new();
            session.SummonedObjects ??= new();

            session.TurnParticipants = session.Units
                .Where(IsScheduledUnit)
                .Select(unit => new CombatTurnParticipant(unit))
                .Concat(session.SummonedObjects
                    .Where(IsScheduledSummonedObject)
                    .Select(summonedObject => new CombatTurnParticipant(summonedObject)))
                .ToList();

            foreach (var participant in session.TurnParticipants)
            {
                participant.CurrentActionValue = session.IsSeamless
                    ? Math.Max(participant.CurrentActionValue, CalculateBaseActionValue(participant))
                    : CalculateBaseActionValue(participant);
            }

            ClearActiveParticipant(session);
            SyncLegacyState(session);
            AdvanceTurn(session);
        }

        /// <summary>
        /// Projects the next unit turn events without changing the combat session.
        /// A unit can appear more than once when its speed earns it multiple turns
        /// inside the requested preview window.
        /// </summary>
        public static IReadOnlyList<BattleUnit> GetUpcomingUnitTurns(
            CombatSession session,
            int maxTurnCount)
        {
            var upcomingUnits = new List<BattleUnit>();
            if (session?.TurnParticipants == null || maxTurnCount <= 0)
            {
                return upcomingUnits;
            }

            var previewParticipants = session.TurnParticipants
                .Where(IsScheduledParticipant)
                .Select((participant, index) => new TurnPreviewParticipant(
                    participant,
                    participant.CurrentActionValue,
                    index))
                .ToList();

            var simulatedEventCount = 0;
            var maxSimulatedEventCount = maxTurnCount
                * Math.Max(1, previewParticipants.Count)
                * 4;
            while (upcomingUnits.Count < maxTurnCount
                && previewParticipants.Count > 0
                && simulatedEventCount < maxSimulatedEventCount)
            {
                simulatedEventCount++;
                var nextParticipant = previewParticipants
                    .OrderBy(participant => participant.ActionValue)
                    .ThenBy(participant => participant.InitialOrder)
                    .First();

                var elapsedActionValue = Math.Max(0f, nextParticipant.ActionValue);
                foreach (var participant in previewParticipants)
                {
                    participant.ActionValue = UnityEngine.Mathf.Max(
                        0f,
                        participant.ActionValue - elapsedActionValue);
                }

                if (nextParticipant.Participant.IsUnit)
                {
                    upcomingUnits.Add(nextParticipant.Participant.Unit);
                }

                // This mirrors EndActiveTurn: the selected participant receives its
                // next AV cost, while all other AV values remain in the projected state.
                nextParticipant.ActionValue = CalculateBaseActionValue(
                    nextParticipant.Participant);
            }

            return upcomingUnits;
        }

        public void EndActiveTurn(CombatSession session)
        {
            if (session == null
                || session.Outcome != CombatOutcomeState.Ongoing)
            {
                return;
            }

            EnsureActiveParticipant(session);
            var participant = session.ActiveParticipant;

            if (participant == null)
            {
                return;
            }

            if (participant.IsUnit)
            {
                if (!IsAlive(participant.Unit))
                {
                    ClearActiveParticipant(session);
                    AdvanceTurn(session);
                    return;
                }

                battleLog?.Invoke(
                    $"{participant.Unit.Character.Definition.DisplayName} konczy ture");

                onUnitTurnEnded?.Invoke(participant.Unit);
                (passiveEffectEventDispatcher ?? session.PassiveEffectEventDispatcher)
                    ?.Publish(
                        PassiveEffectTriggerType.OnTurnEnd,
                        new PassiveEffectCombatContext(
                            session: session,
                            source: participant.Unit,
                            target: participant.Unit,
                            turnNumber: session.ActiveTurnIndex));
                participant.CurrentActionValue =
                    CalculateBaseActionValue(participant);
            }
            else if (participant.IsSummonedObject)
            {
                battleLog?.Invoke(
                    $"{participant.SummonedObject.Definition?.DisplayName ?? participant.SummonedObject.ObjectId} " +
                    "konczy ture");

                onSummonedObjectTurnEnded?.Invoke(participant.SummonedObject);

                if (IsScheduledSummonedObject(participant.SummonedObject))
                {
                    participant.CurrentActionValue =
                        CalculateBaseActionValue(participant);
                }
            }

            ClearActiveParticipant(session);
            AdvanceTurn(session);
        }

        public void AdvanceTurn(CombatSession session)
        {
            if (session == null
                || session.Outcome != CombatOutcomeState.Ongoing)
            {
                return;
            }

            EnsureParticipants(session);
            RemoveExpiredParticipants(session);

            if (session.TurnParticipants.Count == 0)
            {
                ClearActiveParticipant(session);
                return;
            }

            while (session.TurnParticipants.Count > 0)
            {
                var nextParticipant = FindNextScheduledParticipant(session);

                if (nextParticipant == null)
                {
                    ClearActiveParticipant(session);
                    return;
                }

                var elapsedActionValue = Math.Max(
                    0f,
                    nextParticipant.CurrentActionValue);

                ReduceActionValues(session, elapsedActionValue);
                nextParticipant.CurrentActionValue = 0f;

                if (nextParticipant.IsUnit
                    && nextParticipant.Unit.Character.LifeState == CharacterLifeState.Downed)
                {
                    var wasRemoved = ProcessDownedTurn(
                        session,
                        nextParticipant.Unit);

                    if (wasRemoved)
                    {
                        RemoveExpiredParticipants(session);
                        continue;
                    }

                    nextParticipant.CurrentActionValue =
                        CalculateBaseActionValue(nextParticipant);

                    SortTurnParticipants(session);
                    continue;
                }

                if (nextParticipant.IsUnit && !IsAlive(nextParticipant.Unit))
                {
                    RemoveExpiredParticipants(session);
                    continue;
                }

                if (nextParticipant.IsSummonedObject
                    && !IsScheduledSummonedObject(nextParticipant.SummonedObject))
                {
                    RemoveExpiredParticipants(session);
                    continue;
                }

                session.ActiveParticipant = nextParticipant;
                SyncLegacyState(session);
                SortTurnParticipants(session);
                if (session.SuspendedTurns.Remove(nextParticipant)) session.ResumeTurn?.Invoke(nextParticipant);
                else BeginActiveTurn(session, nextParticipant);
                return;
            }

            ClearActiveParticipant(session);
        }

        public void BeginActiveTurn(CombatSession session, BattleUnit unit)
        {
            if (session == null || !IsAlive(unit))
            {
                return;
            }

            BeginActiveTurn(session, new CombatTurnParticipant(unit));
        }

        private void BeginActiveTurn(
            CombatSession session,
            CombatTurnParticipant participant)
        {
            if (participant.IsUnit)
            {
                var unit = participant.Unit;

                battleLog?.Invoke(
                    $"Tura: {unit.Character.Definition.DisplayName}");

                RefreshMaxActionPoints(
                    session,
                    unit,
                    refillCurrentActionPoints: true);

                unit.HasReacted = false;
                unit.MovementStepsThisTurn = 0;
                unit.ToggleSkillIdsUsedThisTurn?.Clear();
                unit.ConsumableUseTypesUsedThisTurn?.Clear();
                onUnitTurnBegan?.Invoke(unit);
                (passiveEffectEventDispatcher ?? session.PassiveEffectEventDispatcher)
                    ?.Publish(
                        PassiveEffectTriggerType.OnTurnStart,
                        new PassiveEffectCombatContext(
                            session: session,
                            source: unit,
                            target: unit,
                            turnNumber: session.ActiveTurnIndex));
                return;
            }

            if (!participant.IsSummonedObject)
            {
                return;
            }

            battleLog?.Invoke(
                $"Tura: {participant.SummonedObject.Definition?.DisplayName ?? participant.SummonedObject.ObjectId}");

            onSummonedObjectTurnBegan?.Invoke(
                participant.SummonedObject);
        }

        public int CalculateMaxActionPoints(
            CharacterInstance character,
            float actionPointsPerSpeed)
        {
            if (character == null)
            {
                return 0;
            }

            var speed = character.Stats.Speed.GetValue();

            return UnityEngine.Mathf.Max(
                0,
                UnityEngine.Mathf.RoundToInt(
                    speed * actionPointsPerSpeed));
        }

        public static float CalculateBaseActionValue(BattleUnit unit)
        {
            if (unit?.Character?.Stats?.Speed == null)
            {
                return 0f;
            }

            var speed = UnityEngine.Mathf.Max(0.01f,
                unit.Character.Stats.Speed.GetValue());
            return BaseActionValue / speed;
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

            WarriorCombatSystem.Refresh(unit);
            var speed = unit.Character.Stats.Speed.GetValue();
            var maxActionPoints = UnityEngine.Mathf.Max(
                0,
                UnityEngine.Mathf.RoundToInt(
                    speed * session.ActionPointsPerSpeed));

            unit.MaxActionPoints = maxActionPoints;
            var nextTurnBonus = refillCurrentActionPoints
                ? GetActiveActionPointModifier(unit)
                : 0;
            unit.CurrentActionPoints = refillCurrentActionPoints
                ? UnityEngine.Mathf.Max(0, maxActionPoints + nextTurnBonus)
                : UnityEngine.Mathf.Min(
                    unit.CurrentActionPoints,
                    maxActionPoints);

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

        public static void ReorderParticipants(CombatSession session)
        {
            if (session?.TurnParticipants == null) return;
            SortTurnParticipants(session);
        }

        private static int GetActiveActionPointModifier(BattleUnit unit)
        {
            if (unit?.Character?.PassiveEffects == null) return 0;

            var total = 0;
            foreach (var effect in unit.Character.PassiveEffects)
            {
                if (PassiveEffectLifecycleSystem.IsActive(effect))
                {
                    total += effect
                        .GetPayload<UniqueCombatBuffPayload>()
                        ?.ActionPointModifier ?? 0;
                }
            }
            return total;
        }

        private static CombatTurnParticipant FindNextScheduledParticipant(
            CombatSession session)
        {
            return session.TurnParticipants
                .Where(IsScheduledParticipant)
                .OrderBy(participant => participant.CurrentActionValue)
                .FirstOrDefault();
        }

        private static void ReduceActionValues(
            CombatSession session,
            float amount)
        {
            foreach (var participant in session.TurnParticipants)
            {
                if (!IsScheduledParticipant(participant))
                {
                    continue;
                }

                participant.CurrentActionValue = UnityEngine.Mathf.Max(
                    0f,
                    participant.CurrentActionValue - amount);
            }
        }

        private static void SortTurnParticipants(CombatSession session)
        {
            session.TurnParticipants = session.TurnParticipants
                .Where(IsScheduledParticipant)
                .OrderBy(participant => participant.CurrentActionValue)
                .ToList();

            SyncLegacyState(session);
        }

        private static void RemoveExpiredParticipants(CombatSession session)
        {
            session.TurnParticipants.RemoveAll(
                participant => !IsScheduledParticipant(participant));

            SyncLegacyState(session);
        }

        private static float CalculateBaseActionValue(
            CombatTurnParticipant participant)
        {
            if (participant.IsUnit)
            {
                return CalculateBaseActionValue(participant.Unit);
            }

            var speed = participant.SummonedObject.Definition.TurnSpeed;

            speed = UnityEngine.Mathf.Max(0.01f, speed);
            return BaseActionValue / speed;
        }

        private static bool IsScheduledParticipant(
            CombatTurnParticipant participant)
        {
            return participant != null
                && ((participant.IsUnit && IsScheduledUnit(participant.Unit))
                    || (participant.IsSummonedObject
                        && IsScheduledSummonedObject(participant.SummonedObject)));
        }

        private static bool IsScheduledUnit(BattleUnit unit)
        {
            return unit?.Character != null
                && unit.Character.LifeState != CharacterLifeState.Dead;
        }

        private static bool IsScheduledSummonedObject(
            SummonedCombatMapObject summonedObject)
        {
            return summonedObject != null
                && summonedObject.Definition != null
                && !summonedObject.IsExpired();
        }

        private static bool IsAlive(BattleUnit unit)
        {
            return unit?.Character != null
                && unit.Character.LifeState == CharacterLifeState.Alive;
        }

        private sealed class TurnPreviewParticipant
        {
            public CombatTurnParticipant Participant { get; }
            public int InitialOrder { get; }
            public float ActionValue { get; set; }

            public TurnPreviewParticipant(
                CombatTurnParticipant participant,
                float actionValue,
                int initialOrder)
            {
                Participant = participant;
                ActionValue = actionValue;
                InitialOrder = initialOrder;
            }
        }

        private bool ProcessDownedTurn(
            CombatSession session,
            BattleUnit unit)
        {
            unit.DownedTurnsRemaining = Math.Max(
                0,
                unit.DownedTurnsRemaining - 1);

            battleLog?.Invoke(
                $"{unit.Character.Definition.DisplayName} " +
                "nie wykonuje tury jako powalony. " +
                $"Pozostale tury: {unit.DownedTurnsRemaining}");

            if (unit.DownedTurnsRemaining > 0)
            {
                return false;
            }

            unit.Character.CurrentHealth = 0;
            unit.Character.LifeState = CharacterLifeState.Dead;

            onUnitDied?.Invoke(unit);

            battleLog?.Invoke(
                $"{unit.Character.Definition.DisplayName} " +
                "ginie po uplywie czasu na podniesienie.");

            session.Units.Remove(unit);
            session.TurnParticipants.RemoveAll(
                participant => participant?.Unit == unit);

            return true;
        }

        private static void EnsureParticipants(CombatSession session)
        {
            session.TurnParticipants ??= new();
            session.TurnOrder ??= new();
            session.SummonedObjects ??= new();

            if (session.TurnParticipants.Count > 0)
            {
                return;
            }

            session.TurnParticipants = session.TurnOrder
                .Where(IsScheduledUnit)
                .Select(unit => new CombatTurnParticipant(unit))
                .Concat(session.SummonedObjects
                    .Where(IsScheduledSummonedObject)
                    .Select(item => new CombatTurnParticipant(item)))
                .ToList();

            foreach (var participant in session.TurnParticipants)
            {
                if (participant.CurrentActionValue <= 0f)
                {
                    participant.CurrentActionValue =
                        CalculateBaseActionValue(participant);
                }
            }

            EnsureActiveParticipant(session);
        }

        private static void EnsureActiveParticipant(CombatSession session)
        {
            if (session.ActiveParticipant != null)
            {
                return;
            }

            if (session.ActiveUnit != null)
            {
                session.ActiveParticipant = session.TurnParticipants
                    .FirstOrDefault(participant =>
                        participant?.Unit == session.ActiveUnit);
            }

            if (session.ActiveParticipant == null
                && session.ActiveSummonedObject != null)
            {
                session.ActiveParticipant = session.TurnParticipants
                    .FirstOrDefault(participant =>
                        participant?.SummonedObject
                            == session.ActiveSummonedObject);
            }
        }

        private static void ClearActiveParticipant(CombatSession session)
        {
            session.ActiveParticipant = null;
            session.ActiveUnit = null;
            session.ActiveSummonedObject = null;
            session.ActiveTurnIndex = -1;
        }

        private static void SyncLegacyState(CombatSession session)
        {
            session.TurnOrder = session.TurnParticipants
                .Where(participant => participant?.Unit != null)
                .Select(participant => participant.Unit)
                .ToList();

            session.ActiveUnit = session.ActiveParticipant?.Unit;
            session.ActiveSummonedObject =
                session.ActiveParticipant?.SummonedObject;
            session.ActiveTurnIndex = session.ActiveUnit == null
                ? -1
                : session.TurnOrder.IndexOf(session.ActiveUnit);
        }
    }
}
