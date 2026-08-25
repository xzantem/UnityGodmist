using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Views.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Godmist.Views.Combat
{
    public class CombatTargetSelectionController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CombatSandboxBootstrap sandboxBootstrap;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private CombatGridFeedbackRenderer feedbackRenderer;
        [SerializeField] private CombatMovementInputController movementInputController;

        [Header("Grid Mapping")]
        [SerializeField] private Vector2 gridOrigin = Vector2.zero;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private Color targetHighlightColor = new(1f, 0.72f, 0.2f, 0.65f);
        [SerializeField] private Color absoluteRangeHighlightColor = new(0.25f, 0.55f, 1f, 0.25f);

        [SerializeField] private Color relativeRangeHighlightColor = new(1f, 0.3f, 0.12f, 0.42f);

        [SerializeField] private Color affectedTargetHighlightColor = new(1f, 0.45f, 0.15f, 0.8f);

        private readonly HashSet<GridPosition> absoluteRangePositions = new();

        private readonly HashSet<GridPosition> selectablePositions = new();

        private readonly HashSet<GridPosition> eligibleTargetPositions = new();

        private GridPosition? previewedRelativeCenter;

        private SkillData selectedSkill;
        private readonly Dictionary<int, GridPosition?> selectedPositionsByModeIndex = new();
        private int currentTargetModeIndex;
        private SkillEffectData selectedTeleportEffect;
        private GridPosition selectedTeleportSourcePosition;
        private bool isSelectingTeleportDestination;
        private SkillEffectData selectedCreateObjectEffect;
        private bool isSelectingObjectPlacement;
        private SkillTargetModeData CurrentTargetMode => selectedSkill.TargetModes[currentTargetModeIndex];

        public bool IsSelectingTarget => selectedSkill != null;
        public BattleUnit HoveredUnit { get; private set; }

        private void Awake()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (movementInputController == null)
            {
                movementInputController =
                    GetComponent<CombatMovementInputController>();
            }
        }

        public void BeginTargetSelection(SkillData skill)
        {
            if (skill != null && selectedSkill == skill)
            {
                CancelTargetSelection();
                return;
            }

            var session = sandboxBootstrap?.Session;

            if (skill == null || session == null || session.ActiveUnit == null
                || !session.ActiveUnit.IsPlayerSide
                || session.Outcome != CombatOutcomeState.Ongoing)
            {
                return;
            }

            if (skill.IsToggle
                && session.ActiveUnit.ActiveToggleSkillIds != null
                && session.ActiveUnit.ActiveToggleSkillIds.Contains(skill.SkillId))
            {
                sandboxBootstrap.TryExecuteSkill(
                    skill,
                    new Dictionary<int, GridPosition?>());
                return;
            }

            var isObjectPlacementOnlySkill =
                IsObjectPlacementOnlySkill(skill);

            if (!isObjectPlacementOnlySkill
                && (skill.TargetModes == null || skill.TargetModes.Count == 0))
            {
                Debug.LogWarning($"Skill '{skill.SkillId}' has no target modes.");
                return;
            }

            if (!isObjectPlacementOnlySkill)
            {
                foreach (var targetMode in skill.TargetModes)
                {
                    if (targetMode == null
                        || !IsSupportedTargetType(targetMode.TargetType))
                    {
                        Debug.LogWarning(
                            $"Skill '{skill.SkillId}' has an invalid target mode.");

                        return;
                    }
                }
            }

            if (IsSelectingTarget)
            {
                CancelTargetSelection();
            }

            selectedSkill = skill;
            currentTargetModeIndex = 0;
            selectedPositionsByModeIndex.Clear();

            if (movementInputController != null)
            {
                movementInputController.enabled = false;
            }

            BeginCurrentTargetMode();
        }
        private void BeginCurrentTargetMode()
        {
            if (selectedSkill == null)
            {
                return;
            }

            if (IsObjectPlacementOnlySkill(selectedSkill))
            {
                TryCompleteSelection();
                return;
            }

            while (currentTargetModeIndex < selectedSkill.TargetModes.Count)
            {
                var targetMode = CurrentTargetMode;

                if (targetMode.TargetType == TargetType.Caster)
                {
                    selectedPositionsByModeIndex[currentTargetModeIndex] = null;
                    currentTargetModeIndex++;
                    continue;
                }

                RebuildTargetHighlights();
                return;
            }

            TryCompleteSelection();
        }

        private void AdvanceTargetMode()
        {
            currentTargetModeIndex++;
            BeginCurrentTargetMode();
        }

        private void TryCompleteSelection()
        {
            if (TryBeginTeleportDestinationSelection())
            {
                return;
            }

            if (TryBeginObjectPlacementSelection())
            {
                return;
            }

            if (sandboxBootstrap.TryExecuteSkill(
                    selectedSkill,
                    selectedPositionsByModeIndex,
                    null,
                    null))
            {
                CancelTargetSelection();
                return;
            }

            CancelTargetSelection();
        }

        public void CancelTargetSelection()
        {
            selectedSkill = null;
            currentTargetModeIndex = 0;
            selectedPositionsByModeIndex.Clear();

            selectedTeleportEffect = null;
            selectedTeleportSourcePosition = default;
            isSelectingTeleportDestination = false;

            selectedCreateObjectEffect = null;
            isSelectingObjectPlacement = false;
            HoveredUnit = null;

            ClearAllHighlights();

            if (movementInputController != null)
            {
                movementInputController.enabled = true;
            }
        }

        private void Update()
        {
            if (DevConsoleView.IsOpen || !IsSelectingTarget)
            {
                return;
            }

            var session = sandboxBootstrap?.Session;

            if (session == null || session.ActiveUnit == null
                || !session.ActiveUnit.IsPlayerSide
                || session.Outcome != CombatOutcomeState.Ongoing)
            {
                CancelTargetSelection();
                return;
            }

            if (!isSelectingTeleportDestination
                && !isSelectingObjectPlacement)
            {
                UpdateRelativeRangePreview();
                UpdateHoveredUnit(session);
            }
            else
            {
                HoveredUnit = null;
            }

            if (Input.GetKeyDown(KeyCode.Escape)
                || Input.GetMouseButtonDown(1))
            {
                CancelTargetSelection();
                return;
            }

            if (!Input.GetMouseButtonDown(0)
                || worldCamera == null)
            {
                return;
            }

            if (EventSystem.current != null
                && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            var targetPosition = GetGridPositionUnderMouse();

            if (isSelectingTeleportDestination)
            {
                if (!selectablePositions.Contains(targetPosition))
                {
                    return;
                }

                if (sandboxBootstrap.TryExecuteSkill(
                        selectedSkill,
                        selectedPositionsByModeIndex,
                        targetPosition,
                        null))
                {
                    CancelTargetSelection();
                }

                return;
            }

            if (isSelectingObjectPlacement)
            {
                if (!selectablePositions.Contains(targetPosition))
                {
                    return;
                }

                if (sandboxBootstrap.TryExecuteSkill(
                        selectedSkill,
                        selectedPositionsByModeIndex,
                        null,
                        targetPosition))
                {
                    CancelTargetSelection();
                }

                return;
            }

            var targetMode = CurrentTargetMode;

            if (UsesGridClickOnlyAsConfirmation(targetMode))
            {
                if (session.Grid == null
                    || !session.Grid.IsWalkable(targetPosition))
                {
                    return;
                }

                selectedPositionsByModeIndex[currentTargetModeIndex] = null;
                AdvanceTargetMode();
                return;
            }

            if (!selectablePositions.Contains(targetPosition))
            {
                return;
            }

            selectedPositionsByModeIndex[currentTargetModeIndex] =
                targetPosition;

            AdvanceTargetMode();
        }

        private void RebuildTargetHighlights()
        {
            ClearAllHighlights();

            if (feedbackRenderer == null
                || selectedSkill == null)
            {
                return;
            }

            var absolutePositions =
                sandboxBootstrap.GetAbsoluteSkillRangePositions(
                    selectedSkill,
                    CurrentTargetMode);

            foreach (var position in absolutePositions)
            {
                absoluteRangePositions.Add(position);
            }

            SpawnHighlights(
                absolutePositions,
                absoluteRangeHighlightColor,
                CombatGridFeedbackLayer.SkillRange);
            feedbackRenderer.ShowFill(
                CombatGridFeedbackLayer.SkillRange,
                absolutePositions,
                WithAlpha(absoluteRangeHighlightColor, .24f));

            var selectable =
                sandboxBootstrap.GetSelectableSkillPositions(
                    selectedSkill,
                    CurrentTargetMode,
                    currentTargetModeIndex);

            foreach (var position in selectable)
            {
                selectablePositions.Add(position);
            }

            var eligibleTargets =
                sandboxBootstrap.GetEligibleSkillTargetPositions(
                    selectedSkill,
                    CurrentTargetMode,
                    currentTargetModeIndex);

            foreach (var position in eligibleTargets)
            {
                eligibleTargetPositions.Add(position);
            }

            SpawnHighlights(
                selectable,
                targetHighlightColor,
                CombatGridFeedbackLayer.SkillTarget);
            feedbackRenderer.ShowFill(
                CombatGridFeedbackLayer.SkillTarget,
                selectable,
                WithAlpha(targetHighlightColor, .28f));
        }

        private void UpdateRelativeRangePreview()
        {
            if (selectedSkill == null || !HasRelativeRangePreview())
            {
                ClearRelativeRangeHighlights();
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                ClearRelativeRangeHighlights();
                return;
            }

            var hoveredPosition = GetGridPositionUnderMouse();

            if (previewedRelativeCenter.HasValue && previewedRelativeCenter.Value == hoveredPosition) return;

            ClearRelativeRangeHighlights();
            previewedRelativeCenter = hoveredPosition;

            if (!IsRelativePreviewCenter(hoveredPosition)) return;

            var relativePositions = sandboxBootstrap.GetRelativeSkillRangePositions(selectedSkill, 
                CurrentTargetMode, hoveredPosition);

            SpawnHighlights(relativePositions, relativeRangeHighlightColor, CombatGridFeedbackLayer.RelativeRange);
            feedbackRenderer.ShowFill(
                CombatGridFeedbackLayer.RelativeRange,
                relativePositions,
                WithAlpha(relativeRangeHighlightColor, .24f));

            var affectedPositions = new List<GridPosition>();

            foreach (var target in sandboxBootstrap.GetSkillTargets(
                selectedSkill,
                CurrentTargetMode,
                currentTargetModeIndex,
                hoveredPosition)) affectedPositions.Add(target.Position);

            SpawnHighlights(affectedPositions, affectedTargetHighlightColor, CombatGridFeedbackLayer.AffectedArea);
            feedbackRenderer.ShowFill(
                CombatGridFeedbackLayer.AffectedArea,
                affectedPositions,
                WithAlpha(affectedTargetHighlightColor, .28f));
        }

        private bool HasRelativeRangePreview()
        {
            if (selectedSkill?.TargetModes == null
                || selectedSkill.TargetModes.Count == 0)
            {
                return false;
            }

            if (isSelectingTeleportDestination
                || isSelectingObjectPlacement)
            {
                return false;
            }

            return CurrentTargetMode.UseRelativeRange;
        }

        private bool TryBeginTeleportDestinationSelection()
        {
            if (selectedSkill?.Effects == null || sandboxBootstrap?.Session?.ActiveUnit == null)
            {
                return false;
            }

            foreach (var effect in selectedSkill.Effects)
            {
                if (effect?.EffectType != EffectType.Teleport)
                {
                    continue;
                }

                if (effect.TargetModeIndex < 0
                    || effect.TargetModeIndex >= selectedSkill.TargetModes.Count)
                {
                    return false;
                }

                var sourceTargetMode =
                    selectedSkill.TargetModes[effect.TargetModeIndex];

                if (sourceTargetMode.TargetType == TargetType.Caster)
                {
                    selectedTeleportSourcePosition = sandboxBootstrap.Session.ActiveUnit.Position;
                }
                else if (!selectedPositionsByModeIndex.TryGetValue(
                             effect.TargetModeIndex,
                             out var selectedPosition)
                         || !selectedPosition.HasValue)
                {
                    return false;
                }
                else
                {
                    selectedTeleportSourcePosition = selectedPosition.Value;
                }

                selectedTeleportEffect = effect;
                isSelectingTeleportDestination = true;

                ClearAllHighlights();

                var destinations = sandboxBootstrap.GetTeleportDestinationPositions(
                    selectedSkill,
                    selectedTeleportEffect,
                    selectedTeleportSourcePosition);

                foreach (var position in destinations)
                {
                    selectablePositions.Add(position);
                }

                SpawnHighlights(
                    destinations,
                    relativeRangeHighlightColor,
                    CombatGridFeedbackLayer.RelativeRange);
                feedbackRenderer.ShowFill(
                    CombatGridFeedbackLayer.RelativeRange,
                    destinations,
                    WithAlpha(relativeRangeHighlightColor, .24f));

                return true;
            }

            return false;
        }

        private bool TryBeginObjectPlacementSelection()
        {
            if (selectedSkill?.Effects == null
                || sandboxBootstrap?.Session?.ActiveUnit == null
                || !IsObjectPlacementOnlySkill(selectedSkill))
            {
                return false;
            }

            foreach (var effect in selectedSkill.Effects)
            {
                if (effect?.EffectType != EffectType.CreateObject
                    && effect?.EffectType != EffectType.Summon)
                {
                    continue;
                }

                if (effect.EffectType == EffectType.CreateObject
                    && effect.SummonedObjectDefinition == null)
                {
                    return false;
                }

                if (effect.EffectType == EffectType.Summon
                    && (effect.SummonedCharacterDefinition == null
                        || effect.SummonedClassData == null))
                {
                    return false;
                }

                selectedCreateObjectEffect = effect;
                isSelectingObjectPlacement = true;

                ClearAllHighlights();

                var placements =
                    sandboxBootstrap.GetObjectPlacementPositions(
                        selectedSkill,
                        selectedCreateObjectEffect);

                foreach (var position in placements)
                {
                    selectablePositions.Add(position);
                }

                SpawnHighlights(
                    placements,
                    relativeRangeHighlightColor,
                    CombatGridFeedbackLayer.RelativeRange);
                feedbackRenderer.ShowFill(
                    CombatGridFeedbackLayer.RelativeRange,
                    placements,
                    WithAlpha(relativeRangeHighlightColor, .24f));

                return true;
            }

            return false;
        }

        private void SpawnHighlights(
            IReadOnlyList<GridPosition> positions,
            Color color,
            CombatGridFeedbackLayer layer)
        {
            feedbackRenderer?.ShowOutline(layer, positions, color);
        }
        private GridPosition GetGridPositionUnderMouse()
        {
            var screenPosition = Input.mousePosition;
            screenPosition.z =
                Mathf.Abs(worldCamera.transform.position.z);

            var worldPosition =
                worldCamera.ScreenToWorldPoint(screenPosition);

            var gridX = Mathf.RoundToInt(
                (worldPosition.x - gridOrigin.x) / cellSize);

            var gridY = Mathf.RoundToInt(
                (worldPosition.y - gridOrigin.y) / cellSize);

            return new GridPosition(gridX, gridY);
        }

        private void UpdateHoveredUnit(CombatSession session)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                HoveredUnit = null;
                return;
            }

            var position = GetGridPositionUnderMouse();
            HoveredUnit = null;
            foreach (var unit in session.Units)
            {
                if (unit != null && unit.Position == position)
                {
                    HoveredUnit = unit;
                    return;
                }
            }
        }

        private void ClearAllHighlights()
        {
            feedbackRenderer?.ClearOutline(CombatGridFeedbackLayer.SkillRange);
            feedbackRenderer?.ClearOutline(CombatGridFeedbackLayer.SkillTarget);
            feedbackRenderer?.ClearFill(CombatGridFeedbackLayer.SkillRange);
            feedbackRenderer?.ClearFill(CombatGridFeedbackLayer.SkillTarget);
            ClearRelativeRangeHighlights();
            selectablePositions.Clear();
            absoluteRangePositions.Clear();
            eligibleTargetPositions.Clear();
        }

        private void ClearRelativeRangeHighlights()
        {
            feedbackRenderer?.ClearOutline(CombatGridFeedbackLayer.RelativeRange);
            feedbackRenderer?.ClearFill(CombatGridFeedbackLayer.RelativeRange);
            previewedRelativeCenter = null;
            feedbackRenderer?.ClearOutline(CombatGridFeedbackLayer.AffectedArea);
            feedbackRenderer?.ClearFill(CombatGridFeedbackLayer.AffectedArea);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private void OnDisable()
        {
            ClearAllHighlights();
        }

        private static bool UsesGridClickOnlyAsConfirmation(SkillTargetModeData targetMode)
        {
            return targetMode.TargetType == TargetType.AreaAroundCaster
                || targetMode.TargetType == TargetType.All;
        }

        private static bool IsObjectPlacementOnlySkill(SkillData skill)
        {
            if (skill?.Effects == null || skill.Effects.Count == 0)
            {
                return false;
            }

            foreach (var effect in skill.Effects)
            {
                if (effect?.EffectType != EffectType.CreateObject
                    && effect?.EffectType != EffectType.Summon)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsSupportedTargetType(
            TargetType targetType)
        {
            return targetType == TargetType.Caster
                || targetType == TargetType.Single
                || targetType == TargetType.All
                || targetType == TargetType.AreaAroundCaster
                || targetType == TargetType.AreaAroundTile;
        }
        private bool IsRelativePreviewCenter(GridPosition position)
        {
            var mode = CurrentTargetMode;

            if (mode.TargetType == TargetType.Single) return eligibleTargetPositions.Contains(position);

            if (mode.TargetType == TargetType.AreaAroundTile) return absoluteRangePositions.Contains(position);

            return false;
        }
    }
}
