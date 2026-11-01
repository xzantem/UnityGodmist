using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using UnityEngine;

namespace Godmist.Views.Combat
{
    public partial class CombatTargetSelectionController
    {
        private bool skillApproachRunning;
        public bool IsApproachingSkillTarget => skillApproachRunning;
        private int skillApproachGeneration;
        private GridPosition? approachPreviewTarget;
        private GridPosition approachPreviewOrigin;
        private int approachPreviewAp;
        private float nextApproachPreview;
        private List<GridPosition> approachPreviewPath;
        private int approachPreviewCost;
        private bool ownsMovementPreview;
        public int ApproachPreviewCost => isActiveAndEnabled && approachPreviewPath != null
            && sandboxBootstrap?.IsOpeningPreview != true ? approachPreviewCost : 0;

        private void ClearSkillApproachPreview()
        {
            approachPreviewTarget = null; approachPreviewPath = null;
            HideSkillApproachPreview();
        }

        private void HideSkillApproachPreview()
        {
            if (ownsMovementPreview) feedbackRenderer?.ClearMovementPreview();
            ownsMovementPreview = false;
        }

        private void InvalidateSkillApproach()
        {
            if (skillApproachRunning) sandboxBootstrap?.StopOpeningSkillApproach();
            skillApproachGeneration++; skillApproachRunning = false;
            ClearSkillApproachPreview();
        }

        private bool CanApproach(GridPosition target)
        {
            if (!IsSelectingTarget || selectedConsumable != null || isSelectingTeleportDestination || isSelectingObjectPlacement
                || currentTargetModeIndex >= selectedSkill.TargetModes.Count || sandboxBootstrap?.Session?.ActiveUnit == null
                || feedbackRenderer?.IsPositionVisible?.Invoke(target) == false) return false;
            var type = CurrentTargetMode.TargetType;
            return type is TargetType.Single or TargetType.Chain or TargetType.MultipleChosen or TargetType.AreaAroundTile;
        }

        private bool PlanApproach(GridPosition target, out List<GridPosition> path, out int cost)
            => QueryApproach(target, out path, out cost) && path.Count > 0;

        private bool QueryApproach(GridPosition target, out List<GridPosition> path, out int cost)
        {
            path = null; cost = 0;
            var previewRotations = new Dictionary<int, int>(rotationsByModeIndex)
                { [currentTargetModeIndex] = directionalRotationQuarterTurns };
            return CanApproach(target) && sandboxBootstrap.TryPlanSkillApproach(selectedSkill,
                currentTargetModeIndex, target, selectedPositionsByModeIndex, previewRotations,
                chosenPositionsByModeIndex, feedbackRenderer?.IsPositionVisible, out path, out cost);
        }

        private void RefreshSkillApproachPreview()
        {
            var actor = sandboxBootstrap?.Session?.ActiveUnit;
            if (!HoveredPosition.HasValue || !CanApproach(HoveredPosition.Value)
                || selectablePositions.Contains(HoveredPosition.Value)) { ClearSkillApproachPreview(); return; }
            var target = HoveredPosition.Value;
            if (approachPreviewTarget != target || approachPreviewOrigin != actor.Position
                || approachPreviewAp != actor.CurrentActionPoints || Time.unscaledTime >= nextApproachPreview)
            {
                nextApproachPreview = Time.unscaledTime + .2f;
                approachPreviewTarget = target; approachPreviewOrigin = actor.Position; approachPreviewAp = actor.CurrentActionPoints;
                if (!PlanApproach(target, out approachPreviewPath, out approachPreviewCost)) approachPreviewPath = null;
            }
            if (approachPreviewPath == null) { HideSkillApproachPreview(); return; }
            var danger = (sandboxBootstrap.Session.EvaluateTrapRisk?.Invoke(actor, approachPreviewPath) ?? 0f) > 0;
            feedbackRenderer.ShowMovementPreview(approachPreviewPath, actor.Position,
                sandboxBootstrap.IsOpeningPreview ? 0 : approachPreviewCost,
                danger ? HighlightStyle.Hostile : HighlightStyle.Selection, danger,
                showCost: !sandboxBootstrap.IsOpeningPreview);
            ownsMovementPreview = true;
        }

        private bool TryBeginSkillApproach(GridPosition target)
        {
            if (!PlanApproach(target, out var path, out _)) return false;
            var session = sandboxBootstrap.Session; var actor = session.ActiveUnit;
            var tracksUnit = CurrentTargetMode.TargetType is TargetType.Single or TargetType.Chain or TargetType.MultipleChosen;
            var targetUnit = tracksUnit ? session.Units.FirstOrDefault(u => u != actor && u.Position == target && sandboxBootstrap.IsUnitVisible(u)) : null;
            var skill = selectedSkill;
            if (!sandboxBootstrap.TryMoveActiveUnitAlongPath(path)) return false;
            skillApproachRunning = true;
            var generation = ++skillApproachGeneration;
            HoveredUnit = null; HoveredPosition = null;
            ClearAllHighlights();
            StartCoroutine(FinishSkillApproach(session, actor, targetUnit, target, path[path.Count - 1], skill, generation));
            return true;
        }

        private IEnumerator FinishSkillApproach(CombatSession session, BattleUnit actor, BattleUnit targetUnit,
            GridPosition target, GridPosition destination, Godmist.Data.Skills.SkillData skill, int generation)
        {
            var nextTargetRefresh = 0f;
            while (true)
            {
                if (generation != skillApproachGeneration || selectedSkill != skill || !isActiveAndEnabled) yield break;
                var moving = sandboxBootstrap.IsPlayerMovementPresentationInProgress;
                // Exploration routes may be replaced while moving. In combat, wait
                // for already-paid movement presentation before planning another leg.
                if (!moving || sandboxBootstrap.IsOpeningPreview && Time.unscaledTime >= nextTargetRefresh)
                {
                    nextTargetRefresh = Time.unscaledTime + .1f;
                    sandboxBootstrap.SynchronizeSkillApproachArrival();
                    if (sandboxBootstrap.Session != session || session.ActiveUnit != actor
                        || actor.Character.LifeState != CharacterLifeState.Alive || session.Outcome != CombatOutcomeState.Ongoing
                        || targetUnit != null && (!session.Units.Contains(targetUnit) || !sandboxBootstrap.IsUnitVisible(targetUnit)))
                    { CancelTargetSelection(); yield break; }
                    if (targetUnit != null && targetUnit.Position != target)
                    {
                        target = targetUnit.Position;
                        if (!QueryApproach(target, out var updatedPath, out _))
                        { CancelTargetSelection(); yield break; }
                        if (updatedPath.Count == 0)
                        {
                            sandboxBootstrap.StopOpeningSkillApproach();
                            destination = actor.Position;
                            break;
                        }
                        if (!sandboxBootstrap.TryMoveActiveUnitAlongPath(updatedPath))
                        { CancelTargetSelection(); yield break; }
                        destination = updatedPath[updatedPath.Count - 1];
                        moving = sandboxBootstrap.IsPlayerMovementPresentationInProgress;
                    }
                }
                if (!moving) break;
                yield return null;
            }
            if (generation != skillApproachGeneration || selectedSkill != skill || !isActiveAndEnabled) yield break;
            skillApproachRunning = false;
            sandboxBootstrap.SynchronizeSkillApproachArrival();
            if (sandboxBootstrap.Session != session || session.ActiveUnit != actor || actor.Position != destination
                || actor.Character.LifeState != CharacterLifeState.Alive || session.Outcome != CombatOutcomeState.Ongoing
                || targetUnit != null && (!session.Units.Contains(targetUnit) || !sandboxBootstrap.IsUnitVisible(targetUnit)))
            { CancelTargetSelection(); yield break; }
            if (targetUnit != null) target = targetUnit.Position;
            RebuildTargetHighlights();
            if (!selectablePositions.Contains(target)) { CancelTargetSelection(); yield break; }
            ConfirmTargetAt(target);
        }
    }
}
