using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Godmist.Views.Combat
{
    public class CombatTargetSelectionController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CombatSandboxBootstrap sandboxBootstrap;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private SpriteRenderer targetHighlightPrefab;
        [SerializeField] private Transform targetHighlightRoot;
        [SerializeField] private CombatMovementInputController movementInputController;

        [Header("Grid Mapping")]
        [SerializeField] private Vector2 gridOrigin = Vector2.zero;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private float highlightZ = -0.2f;
        [SerializeField] private Color targetHighlightColor = new(1f, 0.72f, 0.2f, 0.65f);
        [SerializeField] private Color absoluteRangeHighlightColor = new(0.25f, 0.55f, 1f, 0.25f);

        [SerializeField] private Color relativeRangeHighlightColor = new(1f, 0.3f, 0.12f, 0.42f);

        [SerializeField] private Color affectedTargetHighlightColor = new(1f, 0.45f, 0.15f, 0.8f);

        private readonly List<SpriteRenderer> affectedTargetHighlights = new();

        private readonly HashSet<GridPosition> absoluteRangePositions = new();

        private readonly List<SpriteRenderer> absoluteRangeHighlights = new();

        private readonly List<SpriteRenderer> selectableTargetHighlights = new();

        private readonly List<SpriteRenderer> relativeRangeHighlights = new();

        private readonly HashSet<GridPosition> selectablePositions = new();

        private readonly HashSet<GridPosition> eligibleTargetPositions = new();

        private GridPosition? previewedRelativeCenter;

        private SkillData selectedSkill;
        private readonly Dictionary<int, GridPosition?> selectedPositionsByModeIndex = new();
        private int currentTargetModeIndex;
        private SkillTargetModeData CurrentTargetMode => selectedSkill.TargetModes[currentTargetModeIndex];

        public bool IsSelectingTarget => selectedSkill != null;

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

            if (skill == null || session == null || session.ActiveUnit == null || !session.ActiveUnit.IsPlayerSide
                || session.Outcome != CombatOutcomeState.Ongoing) return;

            if (skill.TargetModes == null || skill.TargetModes.Count == 0)
            {
                Debug.LogWarning($"Skill '{skill.SkillId}' nie ma trybow celu.");
                return;
            }

            foreach (var targetMode in skill.TargetModes)
            {
                if (targetMode == null || !IsSupportedTargetType(targetMode.TargetType))
                {
                    Debug.LogWarning($"Skill '{skill.SkillId}' ma niepoprawny tryb celu.");
                    return;
                }
            }

            if (IsSelectingTarget) CancelTargetSelection();

            selectedSkill = skill;
            currentTargetModeIndex = 0;
            selectedPositionsByModeIndex.Clear();

            if (movementInputController != null) movementInputController.enabled = false;

            BeginCurrentTargetMode();
        }
        private void BeginCurrentTargetMode()
        {
            while (selectedSkill != null && currentTargetModeIndex < selectedSkill.TargetModes.Count)
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
            if (sandboxBootstrap.TryExecuteSkill(selectedSkill, selectedPositionsByModeIndex))
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

            ClearAllHighlights();

            if (movementInputController != null) movementInputController.enabled = true;
        }

        private void Update()
        {
            if (!IsSelectingTarget)
            {
                return;
            }

            var session = sandboxBootstrap?.Session;

            if (session == null || session.ActiveUnit == null || !session.ActiveUnit.IsPlayerSide 
                || session.Outcome != CombatOutcomeState.Ongoing)
            {
                CancelTargetSelection();
                return;
            }

            UpdateRelativeRangePreview();

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                CancelTargetSelection();
                return;
            }

            if (!Input.GetMouseButtonDown(0) || worldCamera == null) return;

            if (EventSystem.current != null  && EventSystem.current.IsPointerOverGameObject()) return;

            var targetPosition = GetGridPositionUnderMouse();

            var targetMode = CurrentTargetMode;

            if (UsesGridClickOnlyAsConfirmation(targetMode))
            {
                if (session.Grid == null || !session.Grid.IsWithinBounds(targetPosition)) return;

                selectedPositionsByModeIndex[currentTargetModeIndex] = null;
                AdvanceTargetMode();
                return;
            }

            if (!selectablePositions.Contains(targetPosition)) return;

            selectedPositionsByModeIndex[currentTargetModeIndex] = targetPosition;
            AdvanceTargetMode();
        }

        private void RebuildTargetHighlights()
        {
            ClearAllHighlights();

            if (targetHighlightPrefab == null || targetHighlightRoot == null || selectedSkill == null) return;

            var absolutePositions = sandboxBootstrap.GetAbsoluteSkillRangePositions(selectedSkill, CurrentTargetMode);

            foreach (var position in absolutePositions) absoluteRangePositions.Add(position);

            SpawnHighlights(absolutePositions, absoluteRangeHighlightColor, absoluteRangeHighlights);

            var selectable = sandboxBootstrap.GetSelectableSkillPositions(
                selectedSkill,
                CurrentTargetMode,
                currentTargetModeIndex);

            foreach (var position in selectable) selectablePositions.Add(position);

            var eligibleTargets = sandboxBootstrap.GetEligibleSkillTargetPositions(
                selectedSkill,
                CurrentTargetMode,
                currentTargetModeIndex);

            foreach (var position in eligibleTargets) eligibleTargetPositions.Add(position);

            SpawnHighlights(eligibleTargets, targetHighlightColor, selectableTargetHighlights);
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

            SpawnHighlights(relativePositions, relativeRangeHighlightColor, relativeRangeHighlights);

            var affectedPositions = new List<GridPosition>();

            foreach (var target in sandboxBootstrap.GetSkillTargets(
                selectedSkill,
                CurrentTargetMode,
                currentTargetModeIndex,
                hoveredPosition)) affectedPositions.Add(target.Position);

            SpawnHighlights(affectedPositions, affectedTargetHighlightColor, affectedTargetHighlights);
        }

        private bool HasRelativeRangePreview()
        {
            if (selectedSkill.TargetModes == null || selectedSkill.TargetModes.Count == 0) return false;

            var mode = CurrentTargetMode;

            return mode.UseRelativeRange;
        }

        private void SpawnHighlights(
            IReadOnlyList<GridPosition> positions,
            Color color,
            List<SpriteRenderer> destination)
        {
            foreach (var position in positions)
            {
                var highlight = Instantiate(
                    targetHighlightPrefab,
                    targetHighlightRoot);

                highlight.transform.localPosition =
                    new Vector3(
                        gridOrigin.x + position.X * cellSize,
                        gridOrigin.y + position.Y * cellSize,
                        highlightZ);

                highlight.color = color;
                destination.Add(highlight);
            }
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

        private void ClearAllHighlights()
        {
            ClearHighlightList(absoluteRangeHighlights);
            ClearHighlightList(selectableTargetHighlights);
            ClearRelativeRangeHighlights();
            selectablePositions.Clear();
            absoluteRangePositions.Clear();
            eligibleTargetPositions.Clear();
        }

        private void ClearRelativeRangeHighlights()
        {
            ClearHighlightList(relativeRangeHighlights);
            previewedRelativeCenter = null;
            ClearHighlightList(affectedTargetHighlights);
        }

        private void ClearHighlightList(
            List<SpriteRenderer> highlights)
        {
            foreach (var highlight in highlights)
            {
                if (highlight != null)
                {
                    Destroy(highlight.gameObject);
                }
            }

            highlights.Clear();
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
