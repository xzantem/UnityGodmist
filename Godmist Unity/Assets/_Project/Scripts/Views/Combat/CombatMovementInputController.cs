using System.Collections.Generic;
using Godmist.Runtime.Combat;
using Godmist.Views.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Godmist.Views.Combat
{
    public class CombatMovementInputController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CombatSandboxBootstrap sandboxBootstrap;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private CombatGridFeedbackRenderer feedbackRenderer;

        [Header("Grid Mapping")]
        [SerializeField] private Vector2 gridOrigin = Vector2.zero;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private Color movementRangeColor = new(1f, .72f, .2f, .85f);
        [SerializeField] private Color movementPreviewColor = new(1f, .84f, .38f, 1f);

        private Dictionary<
            GridPosition,
            List<GridPosition>> reachablePaths = new();

        private CombatSession cachedSession;
        private BattleUnit cachedActiveUnit;
        private GridPosition cachedPosition;
        private int cachedActionPoints;
        private int cachedMovementSteps;
        private bool cacheValid;
        private GridPosition? previewedDestination;
        private int previewedPathCost;

        private void Awake()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }
        }

        private void Update()
        {
            if (DevConsoleView.IsOpen)
            {
                ClearHighlights();
                cacheValid = false;
                return;
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
                }

                return;
            }

            if (sandboxBootstrap.TryInteractAt(destination))
            {
                cacheValid = false;
            }
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

            RebuildHighlights();

            cachedSession = session;
            cachedActiveUnit = activeUnit;
            cachedPosition = activeUnit.Position;
            cachedActionPoints =
                activeUnit.CurrentActionPoints;
            cachedMovementSteps =
                activeUnit.MovementStepsThisTurn;
            cacheValid = true;
        }

        private void RebuildHighlights()
        {
            ClearHighlights();
            feedbackRenderer?.ShowOutline(
                CombatGridFeedbackLayer.MovementRange,
                reachablePaths.Keys,
                movementRangeColor);
            feedbackRenderer?.ShowFill(
                CombatGridFeedbackLayer.MovementRange,
                reachablePaths.Keys,
                WithAlpha(movementRangeColor, .24f));
        }

        private void ClearHighlights()
        {
            feedbackRenderer?.ClearOutline(CombatGridFeedbackLayer.MovementRange);
            feedbackRenderer?.ClearFill(CombatGridFeedbackLayer.MovementRange);
            feedbackRenderer?.ClearMovementPreview();
            previewedDestination = null;
            previewedPathCost = 0;
        }

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
            if (previewedDestination == hoveredPosition && previewedPathCost == pathCost) return;

            feedbackRenderer.ShowMovementPreview(
                path,
                cachedActiveUnit.Position,
                pathCost,
                movementPreviewColor);
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
