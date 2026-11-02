using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;

namespace Godmist.Systems.Combat
{
    // Cooperative job: all reads and writes remain on the combat owner's thread.
    // A checkpoint is one candidate/edge evaluation, never a complete search.
    public sealed class SimpleCombatAiActionJob : IDisposable
    {
        public const double DecisionTimeoutSeconds = 8;
        private readonly IEnumerator<bool> work;
        private readonly Stopwatch elapsed = Stopwatch.StartNew();
        private bool isPaused;
        private readonly double timeoutSeconds;
        private readonly Func<bool> canContinue;
        private readonly Func<bool> stateUnchanged;
        private readonly Action<string> canceled;
        public string CancelReason { get; private set; } = "disposed";
        private readonly SimpleCombatAiSystem.AiActionResult result;
        private static readonly Unity.Profiling.ProfilerMarker SliceSample = new("Godmist.AI.Slice");
        public bool IsComplete { get; private set; }
        public bool IsCanceled { get; private set; }
        public bool HasTimedOut { get; private set; }
        public bool Executed => IsComplete && !IsCanceled && result.Executed;
        public SimpleCombatAiAction Action => result.Action;
        public void SetPaused(bool paused)
        {
            isPaused = paused;
            if (paused) elapsed.Stop();
            else elapsed.Start();
        }

        internal SimpleCombatAiActionJob(IEnumerable<bool> steps,
            SimpleCombatAiSystem.AiActionResult output, Func<bool> validity, double timeoutSeconds, Func<bool> stateUnchanged = null, Action<string> canceled = null)
        {
            if (timeoutSeconds <= 0 || !double.IsFinite(timeoutSeconds)) throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
            work = steps.GetEnumerator(); result = output; canContinue = validity; this.timeoutSeconds = timeoutSeconds;
            this.stateUnchanged = stateUnchanged; this.canceled = canceled;
        }

        public void AdvanceSlice(double milliseconds = 2, int maxSteps = int.MaxValue)
        {
            if (milliseconds <= 0 || !double.IsFinite(milliseconds)) throw new ArgumentOutOfRangeException(nameof(milliseconds));
            if (maxSteps < 1) throw new ArgumentOutOfRangeException(nameof(maxSteps));
            if (IsComplete || isPaused) return;
            // Full state is checked once per frame slice, not once per graph edge.
            if (stateUnchanged?.Invoke() == false) { CancelReason="planning-state-changed"; Dispose(); return; }
            using var sample = SliceSample.Auto();
            var started = Stopwatch.GetTimestamp();
            for (var step = 0; step < maxSteps; step++)
            {
                if (!canContinue()) { CancelReason="actor-turn-or-membership-changed"; Dispose(); return; }
                if (elapsed.Elapsed.TotalSeconds >= timeoutSeconds)
                { HasTimedOut = true; CancelReason="decision-timeout"; Dispose(); return; }
                if (!work.MoveNext())
                { IsComplete = true; work.Dispose(); return; }
                if ((Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency >= milliseconds) return;
            }
        }

        public void Dispose()
        {
            if (IsComplete) return;
            IsCanceled = true; IsComplete = true; canceled?.Invoke(CancelReason);
            work.Dispose();
        }
    }

    public partial class SimpleCombatAiSystem
    {
        private sealed class AiResult<T> { public T Value; }
        internal sealed class AiActionResult
        { public bool Executed; public SimpleCombatAiAction Action; }
        private static readonly Unity.Profiling.ProfilerMarker ApplySample = new("Godmist.AI.ApplyAction");

        public SimpleCombatAiActionJob CreateNextActionJob(CombatSession session, BattleUnit unit,
            SimpleCombatAiTurnState turnState, Func<bool> canContinue = null,
            double timeoutSeconds = SimpleCombatAiActionJob.DecisionTimeoutSeconds)
        {
            var result = new AiActionResult();
            var position = unit?.Position ?? default;
            var points = unit?.CurrentActionPoints ?? 0;
            var modes = unit?.Character?.GetActiveSkills().Where(skill => skill != null).SelectMany(skill => skill.TargetModes).ToArray()
                ?? Array.Empty<Godmist.Data.Skills.SkillTargetModeData>();
            var radius = TurnPlanDepth + (modes.Length == 0 ? 1 : modes.Max(mode =>
                (mode.AbsoluteRange?.MaxRange ?? 0) + (mode.RelativeRange?.MaxRange ?? 0)));
            var globalAllies = modes.Any(mode => mode.TargetingType == TargetingType.Allies && mode.TargetType == TargetType.All);
            var stamp = session == null ? 0 : PlanningStateStamp(session, unit, radius, globalAllies);
            bool Valid() => session != null && session.Outcome == CombatOutcomeState.Ongoing
                && session.ActiveUnit == unit && unit?.Character?.LifeState == CharacterLifeState.Alive
                && unit.Position == position && unit.CurrentActionPoints == points
                && !session.MembershipChangePending && (canContinue?.Invoke() ?? true);
            return new SimpleCombatAiActionJob(ExecuteNextActionWork(session, unit, turnState, result), result, Valid, timeoutSeconds,
                () => session != null && PlanningStateStamp(session, unit, radius, globalAllies) == stamp,
                reason => { if(session?.Diagnostics?.Enabled == true) session.Diagnostics.Record("ai-cancel",session,unit,reason); });
        }

        private IEnumerable<bool> ExecuteNextActionWork(CombatSession session, BattleUnit aiUnit,
            SimpleCombatAiTurnState turnState, AiActionResult result)
        {
            if (session == null || session.Outcome != CombatOutcomeState.Ongoing
                || aiUnit?.Character?.LifeState != CharacterLifeState.Alive) yield break;
            yield return false;
            session.Contact?.Refresh(session);
            turnState ??= CreateTurnState();
            if(session.Diagnostics?.Enabled == true)
                session.Diagnostics.Record("ai-begin", session, aiUnit, "immobilized="+StatusEffectSystem.IsImmobilized(aiUnit)+";skills="
                    +string.Join(",",aiUnit.Character.GetActiveSkills().Select(skill => $"{skill.SkillId}[ap={CombatActionCostCalculator.CalculateSkillCost(aiUnit,skill)},resource={PassiveEffectCombatExecutionSystem.ResolveResourceCost(aiUnit,skill)},afford={CanAffordSkill(aiUnit,skill)}]")));
            // Threshold vengeance applies immediately, before ordinary planning.
            bool vengeance;
            Godmist.Data.Skills.SkillData vengeanceSkill = null;
            using (ApplySample.Auto()) vengeance = catacombsBossSystem?.TryExecuteVengeance(session, aiUnit,
                out vengeanceSkill) == true;
            if (vengeance)
            {
                result.Executed = true;
                result.Action = new SimpleCombatAiAction(SimpleCombatAiActionType.Skill, vengeanceSkill);
                yield break;
            }
            if (aiUnit.CurrentActionPoints <= 0 && !aiUnit.Character.GetActiveSkills().Any(skill => CanAffordSkill(aiUnit, skill))) yield break;
            var selection = new AiResult<AiSkillSelection>();
            var currentCandidates = new List<AiSkillSelection>();
            if (skillExecutionSystem != null)
                foreach (var step in FindBestSkillSelectionWork(session, aiUnit,
                             turnState.UsedZeroActionSkills, selection, currentCandidates)) yield return step;
            var plan = new AiResult<AiTurnPlanStep>();
            if (skillExecutionSystem != null)
                foreach (var step in FindTurnPlanWork(session, aiUnit, turnState, selection.Value, currentCandidates, plan)) yield return step;
            if(session.Diagnostics?.Enabled == true)
                session.Diagnostics.Record("ai-options",session,aiUnit,
                    $"candidates={currentCandidates.Count};bestImmediate={selection.Value?.Skill?.SkillId};immediateScore={selection.Value?.Score};planScore={plan.Value?.Score};wait={plan.Value?.WaitForNextTurn};plannedSteps={plan.Value?.MovementPath?.Count};"
                    +string.Join(",",currentCandidates.OrderByDescending(candidate=>candidate.Score).Take(6).Select(candidate=>$"{candidate.Skill.SkillId}:{candidate.Score:F2}")));
            if (plan.Value != null)
            {
                yield return false;
                if (plan.Value.WaitForNextTurn)
                {
                    if(session.Diagnostics?.Enabled == true) session.Diagnostics.Record("ai-wait",session,aiUnit,"chosen-flanking-or-preparation-plan; score="+plan.Value.Score);
                    yield break;
                }
                if (plan.Value.MovementPath != null)
                {
                    if (TryExecuteMovementPath(session, aiUnit, plan.Value.MovementPath, out var plannedPath))
                    {
                        result.Executed = true;
                        result.Action = new SimpleCombatAiAction(SimpleCombatAiActionType.Movement, movementPath: plannedPath);
                        yield break;
                    }
                }
                else if (plan.Value.DoorId != null)
                {
                    if (interactionSystem.TryInteractWithDoor(session, aiUnit, plan.Value.DoorId))
                    {
                        result.Executed = true; result.Action = new SimpleCombatAiAction(SimpleCombatAiActionType.Interaction);
                        yield break;
                    }
                }
                else selection.Value = plan.Value.Selection;
            }
            var momentumPath = new AiResult<List<GridPosition>>();
            var selectedSpecial = plan.Value != null || selection.Value?.Skill != null
                && (PassiveEffectCombatExecutionSystem.ResolveResourceCost(aiUnit, selection.Value.Skill) > 0
                    || HasActiveOffensivePreparation(aiUnit));
            if (!selectedSpecial)
                foreach (var step in FindMomentumRepositionWork(session, aiUnit, turnState, momentumPath)) yield return step;
            if (momentumPath.Value?.Count > 0)
            {
                turnState.MomentumRepositioned = true;
                if (TryExecuteMovementPath(session, aiUnit, momentumPath.Value, out var completed))
                {
                    result.Executed = true;
                    result.Action = new SimpleCombatAiAction(completed == null ? SimpleCombatAiActionType.Interaction
                        : SimpleCombatAiActionType.Movement, movementPath: completed);
                    yield break;
                }
            }
            if (skillExecutionSystem != null)
            {
                var rejected = new HashSet<Godmist.Data.Skills.SkillData>();
                while (selection.Value != null)
                {
                    yield return false;
                    var chosen = selection.Value;
                    var chosenCost = CombatActionCostCalculator.CalculateSkillCostForTarget(aiUnit, chosen.Skill, chosen.TargetsByModeIndex.Values.SelectMany(value => value).FirstOrDefault());
                    bool executed;
                    using (ApplySample.Auto()) executed = skillExecutionSystem.TryExecute(session, aiUnit,
                        chosen.Skill, chosen.TargetsByModeIndex, teleportDestination: null,
                        objectPlacementDestination: chosen.SummonPlacementDestination);
                    if (executed)
                    {
                        if (chosenCost == 0)
                            turnState.UsedZeroActionSkills.Add(chosen.Skill);
                        result.Executed = true;
                        result.Action = new SimpleCombatAiAction(SimpleCombatAiActionType.Skill, chosen.Skill);
                        yield break;
                    }
                    // Validation can reject a candidate not fully represented by
                    // the forecast (for example knife inventory). Re-read the
                    // actual state and try another skill once, without burning
                    // the rest of the turn or retrying the same failure forever.
                    if(session.Diagnostics?.Enabled == true) session.Diagnostics.Record("ai-rejected",session,aiUnit,"skill="+chosen.Skill.SkillId+";score="+chosen.Score);
                    rejected.Add(chosen.Skill);
                    session.Contact?.Refresh(session);
                    selection.Value = null;
                    var remaining = aiUnit.Character.GetActiveSkills().Where(skill => !rejected.Contains(skill)).ToArray();
                    foreach (var step in FindBestSkillSelectionWork(session, aiUnit,
                                 turnState.UsedZeroActionSkills, selection, planningSkills: remaining)) yield return step;
                }
            }
            yield return false;
            if (aiUnit.IsStationarySummon) yield break;
            var hasTaunter = StatusEffectSystem.TryGetTaunter(session, aiUnit, out var taunter);
            var target = hasTaunter && (session.Contact == null || session.Contact.Sees(aiUnit, taunter))
                ? taunter : FindClosestPlayerUnit(session, aiUnit);
            var path = new AiResult<List<GridPosition>>();
            var searching = target == null;
            if (searching)
            {
                if (session.Contact != null && session.Contact.TryGetSearchPosition(session, aiUnit, out var last))
                    foreach (var step in FindPathWork(session, aiUnit, last, session.IsPositionOccupied(last, aiUnit), path))
                        yield return step;
            }
            else
            {
                var options = hasTaunter ? new[] { target } : session.Units.Where(u=>IsObservedLivingOpponent(session,aiUnit,u))
                    .OrderBy(u=>GetManhattanDistance(aiUnit.Position,u.Position)).Take(6).ToArray();
                var bestCost=float.PositiveInfinity;
                foreach(var candidate in options)
                {
                    var route=new AiResult<List<GridPosition>>();
                    foreach(var step in FindMovementWork(session,aiUnit,candidate,route))yield return step;
                    if(route.Value?.Count>0)
                    {
                        var risk=session.EvaluateTrapRisk?.Invoke(aiUnit,route.Value) ?? 0f;
                        if(!float.IsFinite(risk))continue;
                        var cost=UnityEngine.Mathf.Max(0,aiUnit.CurrentActionPoints-RemainingApAfterRoute(session,aiUnit,route.Value)) + risk*10000f;
                        if(cost<bestCost){bestCost=cost;path.Value=route.Value;}
                        if(risk<=0f)break;
                    }
                    // The closest legal goal remains stable on ties.
                }
            }
            if (path.Value != null && path.Value.Count > 0)
            {
                yield return false;
                bool moved;
                List<GridPosition> completed;
                using (ApplySample.Auto()) moved = TryExecuteMovementPath(session, aiUnit, path.Value,
                    out completed, searching);
                if (moved)
                {
                    result.Executed = true;
                    result.Action = new SimpleCombatAiAction(completed == null ? SimpleCombatAiActionType.Interaction
                        : SimpleCombatAiActionType.Movement, movementPath: completed);
                    yield break;
                }
            }
            if(session.Diagnostics?.Enabled == true)
                session.Diagnostics.Record("ai-no-action",session,aiUnit,
                    $"searching={searching};target={target?.Character?.CharacterId};targetPosition=({target?.Position.X},{target?.Position.Y},{target?.Position.Level});pathSteps={path.Value?.Count ?? 0};immobilized={StatusEffectSystem.IsImmobilized(aiUnit)};reason="
                    +(target==null?"no-observed-opponent-or-search-route":StatusEffectSystem.IsImmobilized(aiUnit)?"movement-blocked-by-status":"no-legal-affordable-safe-route"));
            if (searching) evaluateOutcome?.Invoke(session);
        }

        private IEnumerable<bool> FindMovementWork(CombatSession session, BattleUnit aiUnit,
            BattleUnit target, AiResult<List<GridPosition>> result)
        {
            if (TryGetTimedPreparation(aiUnit, out var preparation, out var attacks)
                && attacks.Any(attack => CanPayPreparationFollowUp(aiUnit, preparation, attack)))
            {
                bool Ready(GridPosition position) => attacks.Any(attack => CanPayPreparationFollowUp(aiUnit, preparation, attack)
                    && CanPreparedAttackFrom(session, aiUnit, attack, target, position));
                if (Ready(aiUnit.Position)) yield break;
                foreach (var step in FindPathWork(session, aiUnit, target.Position, false, result,
                             candidate => Ready(candidate)))
                    yield return step;
                yield break;
            }
            var role = aiUnit?.Character?.Definition?.AiRole ?? CombatAiRole.Balanced;
            var desiredRange = GetPreferredCombatRange(aiUnit);
            var rangedAttacks = (role == CombatAiRole.Dps || role == CombatAiRole.Healer) && desiredRange > 1
                ? GetAvailableRangedAttacks(aiUnit) : null;
            if (rangedAttacks?.Count > 0)
            {
                if (CanUseRangedSkillAgainst(session, target, aiUnit.Position, rangedAttacks)) yield break;
                foreach (var step in FindPathWork(session, aiUnit, target.Position, false, result,
                             candidate => CanUseRangedSkillAgainst(session, target, candidate, rangedAttacks),
                             routeGoal: (candidate,route) => CanUseRangedSkillAgainst(session,target,candidate,rangedAttacks)
                                 && aiUnit.Character.GetActiveSkills().Any(skill=>CanAffordSkill(aiUnit,skill)
                                     && skill.Effects.Any(e=>e?.EffectType==EffectType.DealDamage)
                                     && CombatActionCostCalculator.CalculateSkillCost(aiUnit,skill)<=RemainingApAfterRoute(session,aiUnit,route)
                                     && CanPreparedAttackFrom(session,aiUnit,skill,target,candidate))))
                    yield return step;
                if (result.Value?.Count > 0) yield break;
                // If the shot only fits next turn, still stop at its firing position,
                // rather than walking all the way into melee.
                foreach(var step in FindPathWork(session,aiUnit,target.Position,false,result,
                    candidate=>CanUseRangedSkillAgainst(session,target,candidate,rangedAttacks)))yield return step;
                if(result.Value?.Count>0)yield break;
            }
            if (session?.Grid == null || aiUnit?.Character == null || target?.Character == null
                || aiUnit.Character.LifeState != CharacterLifeState.Alive
                || target.Character.LifeState != CharacterLifeState.Alive) yield break;
            result.Value = null;
            foreach (var step in FindPathWork(session, aiUnit, target.Position, true, result)) yield return step;
        }
    }
}
