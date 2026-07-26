using System.Collections.Generic;
using Godmist.Runtime.Combat;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Godmist.Views.Combat
{
    public class CombatMovementInputController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CombatSandboxBootstrap sandboxBootstrap;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private SpriteRenderer highlightPrefab;
        [SerializeField] private Transform highlightRoot;

        [Header("Grid Mapping")]
        [SerializeField] private Vector2 gridOrigin = Vector2.zero;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private float highlightZ = 0.1f;

        private readonly Dictionary<
            GridPosition,
            SpriteRenderer> activeHighlights = new();

        private Dictionary<
            GridPosition,
            List<GridPosition>> reachablePaths = new();

        private CombatSession cachedSession;
        private BattleUnit cachedActiveUnit;
        private GridPosition cachedPosition;
        private int cachedActionPoints;
        private int cachedMovementSteps;
        private bool cacheValid;

        private void Awake()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }
        }

        private void Update()
        {
            if (!HasControllableUnit())
            {
                ClearHighlights();
                cacheValid = false;
                return;
            }

            RefreshIfNeeded();

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

            if (sandboxBootstrap.TryInteractAt(destination))
            {
                cacheValid = false;
                return;
            }
            if (sandboxBootstrap.TryReviveAt(destination))
            {
                cacheValid = false;
                return;
            }

            if (!reachablePaths.TryGetValue(
                    destination,
                    out var path))
            {
                return;
            }

            if (sandboxBootstrap.TryMoveActiveUnitAlongPath(path))
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

            if (highlightPrefab == null
                || highlightRoot == null)
            {
                return;
            }

            foreach (var pair in reachablePaths)
            {
                var highlight = Instantiate(
                    highlightPrefab,
                    highlightRoot);

                highlight.transform.localPosition =
                    new Vector3(
                        gridOrigin.x
                            + pair.Key.X * cellSize,
                        gridOrigin.y
                            + pair.Key.Y * cellSize,
                        highlightZ);

                activeHighlights[pair.Key] = highlight;
            }
        }

        private void ClearHighlights()
        {
            foreach (var highlight in activeHighlights.Values)
            {
                if (highlight != null)
                {
                    Destroy(highlight.gameObject);
                }
            }

            activeHighlights.Clear();
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

        private void OnDisable()
        {
            ClearHighlights();
            cacheValid = false;
        }
    }
}
