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
        private readonly Action<BattleUnit> onUnitDownedTurn;
        private readonly PassiveEffectCombatEventDispatcher passiveEffectEventDispatcher;
        private readonly ResourceRuleSystem resourceRuleSystem;

        public CombatTurnSystem(
            Action<string> battleLog = null,
            Action<BattleUnit> onUnitTurnBegan = null,
            Action<BattleUnit> onUnitTurnEnded = null,
            Action<SummonedCombatMapObject> onSummonedObjectTurnBegan = null,
            Action<SummonedCombatMapObject> onSummonedObjectTurnEnded = null,
            Action<BattleUnit> onUnitDied = null,
            PassiveEffectCombatEventDispatcher passiveEffectEventDispatcher = null,
            Action<BattleUnit> onUnitDownedTurn = null,
            ResourceRuleSystem resourceRuleSystem = null)
        {
            this.battleLog = battleLog;
            this.onUnitTurnBegan = onUnitTurnBegan;
            this.onUnitTurnEnded = onUnitTurnEnded;
            this.onSummonedObjectTurnBegan = onSummonedObjectTurnBegan;
            this.onSummonedObjectTurnEnded = onSummonedObjectTurnEnded;
            this.onUnitDied = onUnitDied;
            this.passiveEffectEventDispatcher = passiveEffectEventDispatcher;
            this.resourceRuleSystem = resourceRuleSystem ?? new ResourceRuleSystem(battleLog);
            this.onUnitDownedTurn = onUnitDownedTurn
                ?? new Action<BattleUnit>(new StatusEffectSystem(battleLog).ProcessDownedTurn);
        }

        public void InitializeTurnOrder(CombatSession session, BattleUnit openingActor = null,
            IReadOnlyDictionary<BattleUnit, float> openingDetection = null)
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

            // Initial delay is baked into remaining AV once; joins/merges need no special status.
            if (openingActor != null && session.Units.Contains(openingActor) && openingActor.IsPlayerControlled)
            {
                foreach (var participant in session.TurnParticipants)
                {
                    if (participant.Unit == openingActor) participant.CurrentActionValue = 0f;
                    else if (participant.Unit?.IsPlayerSide == false && openingDetection != null
                        && openingDetection.TryGetValue(participant.Unit, out var detection))
                        participant.CurrentActionValue += CalculateBaseActionValue(participant.Unit)
                            * (1f - UnityEngine.Mathf.Clamp01(float.IsFinite(detection) ? detection : 1f));
                }
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
            return ProjectUpcomingTurns(session, maxTurnCount, includeObjects: false)
                .Select(participant => participant.Unit)
                .ToList();
        }

        /// <summary>Projects turns for units and AV objects without changing the session.</summary>
        public static IReadOnlyList<CombatTurnParticipant> GetUpcomingTurns(
            CombatSession session,
            int maxTurnCount)
        {
            return ProjectUpcomingTurns(session, maxTurnCount, includeObjects: true);
        }

        private static IReadOnlyList<CombatTurnParticipant> ProjectUpcomingTurns(
            CombatSession session,
            int maxTurnCount,
            bool includeObjects)
        {
            var upcoming = new List<CombatTurnParticipant>();
            if (session?.TurnParticipants == null || maxTurnCount <= 0)
            {
                return upcoming;
            }

            var previewParticipants = session.TurnParticipants
                .Where(IsScheduledParticipant)
                .Select((participant, index) => new TurnPreviewParticipant(
                    participant,
                    participant.CurrentActionValue,
                    index))
                .ToList();

            var simulatedEventCount = 0;
            var slayerAcceleration = new HashSet<BattleUnit>(session.Units.Where(u=>u?.SkeletonSlayerState?.AccelerateNextTurn==true));
            var projectedObjectTurns = new Dictionary<SummonedCombatMapObject, int>();
            var maxSimulatedEventCount = maxTurnCount
                * Math.Max(1, previewParticipants.Count)
                * 4;
            while (upcoming.Count < maxTurnCount
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

                if (includeObjects || nextParticipant.Participant.IsUnit)
                {
                    upcoming.Add(nextParticipant.Participant);
                }

                if (nextParticipant.Participant.IsSummonedObject)
                {
                    var summonedObject = nextParticipant.Participant.SummonedObject;
                    projectedObjectTurns.TryGetValue(summonedObject, out var turns);
                    projectedObjectTurns[summonedObject] = ++turns;
                    if (turns >= summonedObject.RemainingDurationTurns)
                    {
                        previewParticipants.Remove(nextParticipant);
                        continue;
                    }
                }

                // This mirrors EndActiveTurn: the selected participant receives its
                // next AV cost, while all other AV values remain in the projected state.
                nextParticipant.ActionValue = CalculateBaseActionValue(
                    nextParticipant.Participant);
                if(nextParticipant.Participant.IsUnit && slayerAcceleration.Remove(nextParticipant.Participant.Unit))
                    nextParticipant.ActionValue=0f;
            }

            return upcoming;
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
                    participant.Unit.HasUnfinishedCombatTurn = false;
                    ClearActiveParticipant(session);
                    AdvanceTurn(session);
                    return;
                }

                battleLog?.Invoke(
                    $"{participant.Unit.Character.Definition.DisplayName} konczy ture");

                // Use the unspent AP before end-of-turn effects expire or change stats/blocks.
                resourceRuleSystem.ProcessTurnEnd(participant.Unit, session);
                onUnitTurnEnded?.Invoke(participant.Unit);
                (passiveEffectEventDispatcher ?? session.PassiveEffectEventDispatcher)
                    ?.Publish(
                        PassiveEffectTriggerType.OnTurnEnd,
                        new PassiveEffectCombatContext(
                            session: session,
                            source: participant.Unit,
                            target: participant.Unit,
                            turnNumber: session.ActiveTurnIndex));
                CombatStallWarningSystem.EndUnitTurn(session, participant.Unit, battleLog);
                participant.Unit.HasUnfinishedCombatTurn = false;
                participant.CurrentActionValue = UnityEngine.Mathf.Max(0,
                    CalculateBaseActionValue(participant) - participant.Unit.PendingEquipmentActionValueReduction);
                participant.CurrentActionValue = SkeletonSlayerBossSystem.ApplyTurnDelay(participant.Unit,participant.CurrentActionValue,true);
                participant.Unit.PendingEquipmentActionValueReduction = 0;
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
                    nextParticipant.Unit.HasUnfinishedCombatTurn = false;
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
                var suspended = session.SuspendedTurns.Remove(nextParticipant);
                if (suspended || nextParticipant.Unit?.HasUnfinishedCombatTurn == true) session.ResumeTurn?.Invoke(nextParticipant);
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
            CombatStallWarningSystem.Synchronize(session);
            if (participant.IsUnit)
            {
                var unit = participant.Unit;
                BeginEntryTurn(session, unit, onUnitTurnBegan);
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

        // The entry action and its eventual initiative slot share exactly one start.
        // Entry supplies effect processing only; ordinary BeginActiveTurn also schedules presentation/input.
        public bool BeginEntryTurn(CombatSession session, BattleUnit unit, Action<BattleUnit> startEffects)
        {
            if (session == null || !IsAlive(unit) || !session.Units.Contains(unit)) return false;
            if (unit.HasUnfinishedCombatTurn) return true;
            unit.HasUnfinishedCombatTurn = true;
            unit.RibbedTunicProtectionUsed = false;
            UniqueEquipmentCombatRules.BeginTurn(unit);
            CombatStallWarningSystem.BeginUnitTurn(session, unit);
            battleLog?.Invoke($"Tura: {unit.Character.Definition.DisplayName}");
            RefreshMaxActionPoints(session, unit, refillCurrentActionPoints: true);
            unit.HasReacted = false;
            unit.MovementStepsThisTurn = 0;
            unit.ToggleSkillIdsUsedThisTurn?.Clear();
            unit.ConsumableUseTypesUsedThisTurn?.Clear();
            startEffects?.Invoke(unit);
            (passiveEffectEventDispatcher ?? session.PassiveEffectEventDispatcher)?.Publish(
                PassiveEffectTriggerType.OnTurnStart,
                new PassiveEffectCombatContext(session: session, source: unit, target: unit, turnNumber: session.ActiveTurnIndex));
            return IsAlive(unit) && session.Units.Contains(unit) && session.Outcome == CombatOutcomeState.Ongoing;
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
                    speed * actionPointsPerSpeed * ActionPointUnits.PerPoint));
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
                    speed * session.ActionPointsPerSpeed * ActionPointUnits.PerPoint * (1 + EquipmentEffectSystem.Sum(unit.Character, GalduryteEffectType.ActionPointsPercent))));

            unit.MaxActionPoints = maxActionPoints;
            var nextTurnBonus = refillCurrentActionPoints
                ? GetActiveActionPointModifier(unit)
                : 0;
            unit.CurrentActionPoints = refillCurrentActionPoints
                ? UnityEngine.Mathf.Max(0, UnityEngine.Mathf.RoundToInt(maxActionPoints * SkeletonSlayerBossSystem.RefillMultiplier(unit)) + nextTurnBonus)
                : UnityEngine.Mathf.Min(
                    unit.CurrentActionPoints,
                    UnityEngine.Mathf.RoundToInt(maxActionPoints * SkeletonSlayerBossSystem.RefillMultiplier(unit)));

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
                    total += ActionPointUnits.FromPoints(effect
                        .GetPayload<UniqueCombatBuffPayload>()
                        ?.ActionPointModifier ?? 0);
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
            onUnitDownedTurn(unit);
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
