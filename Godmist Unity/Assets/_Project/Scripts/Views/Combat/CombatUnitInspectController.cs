using Godmist.Runtime.Combat;
using Godmist.Views.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Godmist.Views.Combat
{
    public class CombatUnitInspectController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CombatSandboxBootstrap sandboxBootstrap;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private CombatTargetSelectionController
            targetSelectionController;
        [SerializeField] private CombatUnitInspectPanel inspectPanel;
        [SerializeField] private CombatUnitViewSpawner unitViewSpawner;

        [Header("Grid Mapping")]
        [SerializeField] private Vector2 gridOrigin = Vector2.zero;
        [SerializeField] private float cellSize = 1f;

        private BattleUnit selectedUnit;

        private void Awake()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (unitViewSpawner == null)
            {
                unitViewSpawner = sandboxBootstrap != null
                    ? sandboxBootstrap.GetComponent<CombatUnitViewSpawner>()
                    : GetComponent<CombatUnitViewSpawner>();
            }
        }

        private void Update()
        {
            if (DevConsoleView.IsOpen)
            {
                return;
            }

            if (inspectPanel == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                selectedUnit = null;
                inspectPanel.Hide();
                RefreshNameLabels(null);
                return;
            }

            if (targetSelectionController != null
                && targetSelectionController.IsSelectingTarget)
            {
                var hoveredUnit = targetSelectionController.HoveredUnit;
                if (hoveredUnit != null)
                {
                    inspectPanel.Show(hoveredUnit);
                }
                else
                {
                    inspectPanel.Hide();
                }

                RefreshNameLabels(hoveredUnit);
                return;
            }

            var hoveredUnitOutsideTargeting = GetUnitUnderMouse();
            RefreshNameLabels(hoveredUnitOutsideTargeting);

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

            var session = sandboxBootstrap?.Session;

            if (session == null)
            {
                inspectPanel.Hide();
                return;
            }

            var position = GetGridPositionUnderMouse();
            var unit = FindUnitAtPosition(session, position);

            if (unit == null)
            {
                selectedUnit = null;
                inspectPanel.Hide();
                RefreshNameLabels(null);
                return;
            }

            selectedUnit = unit;
            inspectPanel.Show(unit);
            RefreshNameLabels(hoveredUnitOutsideTargeting);
        }

        private BattleUnit FindUnitAtPosition(
            CombatSession session,
            GridPosition position)
        {
            foreach (var unit in session.Units)
            {
                if (unit != null && unit.Position == position)
                {
                    return unit;
                }
            }

            return null;
        }

        private BattleUnit GetUnitUnderMouse()
        {
            if (worldCamera == null
                || (EventSystem.current != null
                    && EventSystem.current.IsPointerOverGameObject()))
            {
                return null;
            }

            var session = sandboxBootstrap?.Session;
            return session == null
                ? null
                : FindUnitAtPosition(session, GetGridPositionUnderMouse());
        }

        private void RefreshNameLabels(BattleUnit hoveredUnit)
        {
            unitViewSpawner?.ShowNameLabelsFor(hoveredUnit, selectedUnit);
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
    }
}
