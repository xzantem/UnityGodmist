using System.Collections.Generic;
using Godmist.Runtime.Combat;
using Godmist.Views.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Godmist.Views.Combat
{
    public class CombatMovementInputController : MonoBehaviour
    {
        public void SetGridMapping(Vector2 origin, float size)
        {
            gridOrigin = origin;
            cellSize = Mathf.Max(0.01f, size);
        }
        [Header("References")]
        [SerializeField] private CombatSandboxBootstrap sandboxBootstrap;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private CombatGridFeedbackRenderer feedbackRenderer;
        [SerializeField] private CombatUnitInspectController inspectController;

        [Header("Grid Mapping")]
        [SerializeField] private Vector2 gridOrigin = Vector2.zero;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private Color movementRangeColor = new(1f, .72f, .2f, .85f);
        [SerializeField] private Color movementPreviewColor = new(1f, .84f, .38f, 1f);
        [SerializeField] private Color inspectedMovementRangeColor =
            new(.85f, .3f, 1f, .85f);

        private Dictionary<
            GridPosition,
            List<GridPosition>> reachablePaths = new();

        [System.NonSerialized] private CombatSession cachedSession;
        private BattleUnit cachedActiveUnit;
        private GridPosition cachedPosition;
        private int cachedActionPoints;
        private int cachedMovementSteps;
        private bool cacheValid;
        private GridPosition? previewedDestination;
        private int previewedPathCost;
        public int PreviewCost => isActiveAndEnabled && previewedDestination.HasValue ? previewedPathCost : 0;
        private BattleUnit cachedInspectedUnit;
        private GridPosition cachedInspectedPosition;
        private int cachedInspectedMaxActionPoints;
        private bool inspectedRangeVisible;

        public bool ConsumedPrimaryClickThisFrame { get; private set; }

        private void Awake()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (inspectController == null)
            {
                inspectController = Object.FindAnyObjectByType<CombatUnitInspectController>();
            }
        }

        private void Update()
        {
            ConsumedPrimaryClickThisFrame = false;
            if (sandboxBootstrap?.Session?.IsSeamless == true) cacheValid = false;

            if (DevConsoleView.IsOpen)
            {
                ClearHighlights();
                cacheValid = false;
                return;
            }

            if (sandboxBootstrap?.IsPlayerMovementPresentationInProgress == true)
            {
                ClearHighlights();
                cacheValid = false;
                return;
            }

            if (TryShowInspectedUnitMovementRange())
            {
                return;
            }

            if (ClearInspectedMovementRange())
            {
                cacheValid = false;
            }

            if (!HasControllableUnit())
            {
                ClearHighlights();
                cacheValid = false;
                return;
            }

            RefreshIfNeeded();
            RefreshMovementPreview();

            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            if (EventSystem.current != null
                && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            var destination = GetGridPositionUnderMouse();

            if (sandboxBootstrap.TryReviveAt(destination))
            {
                cacheValid = false;
                return;
            }

            var forceInteraction = Input.GetKey(KeyCode.LeftControl)
                                || Input.GetKey(KeyCode.RightControl);

            if (forceInteraction
                && sandboxBootstrap.TryInteractAt(destination))
            {
                cacheValid = false;
                return;
            }

            if (reachablePaths.TryGetValue(destination, out var path))
            {
                if (sandboxBootstrap.TryMoveActiveUnitAlongPath(path))
                {
                    cacheValid = false;
                    ConsumedPrimaryClickThisFrame = true;
                }

                return;
            }

            if (sandboxBootstrap.TryInteractAt(destination))
            {
                cacheValid = false;
                ConsumedPrimaryClickThisFrame = true;
            }
        }

        public bool IsReachableDestination(GridPosition position)
        {
            if (!HasControllableUnit()) return false;

            return sandboxBootstrap
                .GetActiveUnitMovementPaths()
                .ContainsKey(position);
        }

        private bool HasControllableUnit()
        {
            var session = sandboxBootstrap?.Session;

            return session != null
                && session.ActiveUnit != null
                && session.ActiveUnit.IsPlayerSide
                && session.Outcome
                    == Godmist.Core.Enums.CombatOutcomeState.Ongoing;
        }

        private void RefreshIfNeeded()
        {
            var session = sandboxBootstrap.Session;
            var activeUnit = session.ActiveUnit;

            if (cacheValid
                && cachedSession == session
                && cachedActiveUnit == activeUnit
                && cachedPosition == activeUnit.Position
                && cachedActionPoints
                    == activeUnit.CurrentActionPoints
                && cachedMovementSteps
                    == activeUnit.MovementStepsThisTurn)
            {
                return;
            }

            reachablePaths =
                sandboxBootstrap.GetActiveUnitMovementPaths();

            RebuildHighlights(session.Grid);

            cachedSession = session;
            cachedActiveUnit = activeUnit;
            cachedPosition = activeUnit.Position;
            cachedActionPoints =
                activeUnit.CurrentActionPoints;
            cachedMovementSteps =
                activeUnit.MovementStepsThisTurn;
            cacheValid = true;
        }

        [SerializeField, Range(0f, 1f)] private float movementFillAlpha = .025f;
        [SerializeField, Range(0f, 1f)] private float difficultTerrainFillAlpha = .1f;

        private void RebuildHighlights(BattleGrid grid)
        {
            ClearHighlights();
            feedbackRenderer?.ShowOutline(
                CombatGridFeedbackLayer.MovementRange,
                reachablePaths.Keys,
                movementRangeColor,
                cell => ShadeForSurcharge(movementRangeColor, grid.GetTerrainMovementSurcharge(cell)));
            feedbackRenderer?.ShowFill(
                CombatGridFeedbackLayer.MovementRange,
                reachablePaths.Keys,
                WithAlpha(movementRangeColor, movementFillAlpha),
                cell => ShadeForSurcharge(WithAlpha(movementRangeColor, grid.IsDifficultTerrain(cell) ? difficultTerrainFillAlpha : movementFillAlpha), grid.GetTerrainMovementSurcharge(cell)));
            feedbackRenderer?.ShowOutline(CombatGridFeedbackLayer.ActiveMovementUnit,
                new[] { sandboxBootstrap.Session.ActiveUnit.Position }, Color.Lerp(movementRangeColor, Color.white, .35f));
        }

        private void ClearHighlights()
        {
            feedbackRenderer?.ClearOutline(CombatGridFeedbackLayer.MovementRange);
            feedbackRenderer?.ClearFill(CombatGridFeedbackLayer.MovementRange);
            feedbackRenderer?.ClearOutline(CombatGridFeedbackLayer.ActiveMovementUnit);
            ClearInspectedMovementRange();
            feedbackRenderer?.ClearMovementPreview();
            previewedDestination = null;
            previewedPathCost = 0;
        }

        private bool TryShowInspectedUnitMovementRange()
        {
            var isInspectingMovement = Input.GetKey(KeyCode.LeftControl)
                || Input.GetKey(KeyCode.RightControl);
            var inspectedUnit = inspectController?.HoveredUnit;
            if (!isInspectingMovement
                || inspectedUnit == null
                || sandboxBootstrap?.Session == null)
            {
                return false;
            }

            feedbackRenderer?.ClearOutline(CombatGridFeedbackLayer.MovementRange);
            feedbackRenderer?.ClearFill(CombatGridFeedbackLayer.MovementRange);
            feedbackRenderer?.ClearMovementPreview();
            previewedDestination = null;
            previewedPathCost = 0;
            cacheValid = false;

            if (inspectedRangeVisible
                && cachedInspectedUnit == inspectedUnit
                && cachedInspectedPosition == inspectedUnit.Position
                && cachedInspectedMaxActionPoints == inspectedUnit.MaxActionPoints)
            {
                return true;
            }

            var inspectedPaths = sandboxBootstrap
                .GetUnitNextTurnMovementPaths(inspectedUnit);
            feedbackRenderer?.ShowOutline(
                CombatGridFeedbackLayer.InspectedMovementRange,
                inspectedPaths.Keys,
                inspectedMovementRangeColor,
                cell => ShadeForSurcharge(inspectedMovementRangeColor,
                    sandboxBootstrap.Session.Grid.GetTerrainMovementSurcharge(cell)));
            feedbackRenderer?.ShowFill(
                CombatGridFeedbackLayer.InspectedMovementRange,
                inspectedPaths.Keys,
                WithAlpha(inspectedMovementRangeColor, movementFillAlpha),
                cell => ShadeForSurcharge(WithAlpha(inspectedMovementRangeColor, sandboxBootstrap.Session.Grid.IsDifficultTerrain(cell) ? difficultTerrainFillAlpha : movementFillAlpha),
                    sandboxBootstrap.Session.Grid.GetTerrainMovementSurcharge(cell)));

            cachedInspectedUnit = inspectedUnit;
            cachedInspectedPosition = inspectedUnit.Position;
            cachedInspectedMaxActionPoints = inspectedUnit.MaxActionPoints;
            inspectedRangeVisible = true;
            return true;
        }

        private bool ClearInspectedMovementRange()
        {
            if (!inspectedRangeVisible) return false;
            feedbackRenderer?.ClearOutline(CombatGridFeedbackLayer.InspectedMovementRange);
            feedbackRenderer?.ClearFill(CombatGridFeedbackLayer.InspectedMovementRange);
            cachedInspectedUnit = null;
            inspectedRangeVisible = false;
            return true;
        }

        [SerializeField] private Color trapPreviewColor = new(1f, .2f, .1f, .85f);
        [SerializeField] private Color surchargeGradientEndColor = new(.65f, .27f, .09f, 1f);
        [SerializeField, Min(1)] private int surchargeGradientMaximum = 3;
        private bool previewedTrapDanger;
        private void RefreshMovementPreview()
        {
            if (feedbackRenderer == null
                || EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                feedbackRenderer?.ClearMovementPreview();
                previewedDestination = null;
                return;
            }

            var hoveredPosition = GetGridPositionUnderMouse();
            if (!reachablePaths.TryGetValue(hoveredPosition, out var path))
            {
                feedbackRenderer.ClearMovementPreview();
                previewedDestination = null;
                return;
            }

            var pathCost = sandboxBootstrap.GetActiveUnitMovementPathCost(path);
            var danger = (sandboxBootstrap.Session.EvaluateTrapRisk?.Invoke(cachedActiveUnit, path) ?? 0f) > 0f;
            if (previewedDestination == hoveredPosition && previewedPathCost == pathCost && previewedTrapDanger == danger) return;
            previewedTrapDanger = danger;

            feedbackRenderer.ShowMovementPreview(
                path,
                cachedActiveUnit.Position,
                pathCost,
                danger ? trapPreviewColor : movementPreviewColor, danger,
                (from, to) => ShadeForSurcharge(movementPreviewColor, cachedSession.Grid.GetMovementSurcharge(from, to)));
            previewedDestination = hoveredPosition;
            previewedPathCost = pathCost;
        }

        private GridPosition GetGridPositionUnderMouse()
        {
            var screenPosition = Input.mousePosition;
            screenPosition.z =
                Mathf.Abs(worldCamera.transform.position.z);

            var worldPosition =
                worldCamera.ScreenToWorldPoint(screenPosition);

            var gridX = Mathf.RoundToInt(
                (worldPosition.x - gridOrigin.x)
                / cellSize);

            var gridY = Mathf.RoundToInt(
                (worldPosition.y - gridOrigin.y)
                / cellSize);

            return new GridPosition(gridX, gridY);
        }

        private Color ShadeForSurcharge(Color baseColor, int surcharge)
        {
            var amount = Mathf.Clamp01((float)surcharge / Mathf.Max(1, surchargeGradientMaximum));
            var color = Color.Lerp(baseColor, surchargeGradientEndColor, amount);
            color.a = baseColor.a;
            return color;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private void OnDisable()
        {
            ClearHighlights();
            cacheValid = false;
        }
    }
}
