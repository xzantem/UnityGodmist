using Godmist.Runtime.Combat;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
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
        [SerializeField] private CombatMovementInputController
            movementInputController;
        [SerializeField] private CombatUnitInspectWindowView inspectWindowPrefab;

        [Header("Grid Mapping")]
        [SerializeField] private Vector2 gridOrigin = Vector2.zero;
        [SerializeField] private float cellSize = 1f;

        private BattleUnit selectedUnit;
        private CombatUnitInspectWindowView floatingInspectWindow;
        public BattleUnit HoveredUnit { get; private set; }

        private void Awake()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (targetSelectionController == null)
            {
                targetSelectionController =
                    GetComponent<CombatTargetSelectionController>();
            }

            if (unitViewSpawner == null)
            {
                unitViewSpawner = sandboxBootstrap != null
                    ? sandboxBootstrap.GetComponent<CombatUnitViewSpawner>()
                    : GetComponent<CombatUnitViewSpawner>();
            }

            if (movementInputController == null)
            {
                movementInputController =
                    Object.FindAnyObjectByType<CombatMovementInputController>();
            }
        }

        private void Update()
        {
            if (DevConsoleView.IsOpen)
            {
                HoveredUnit = null;
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                selectedUnit = null;
                HideInspect();
                return;
            }

            var hoveredUnitOutsideTargeting = GetUnitUnderMouse();
            HoveredUnit = hoveredUnitOutsideTargeting;

            if (targetSelectionController != null
                && (targetSelectionController.IsSelectingTarget
                    || targetSelectionController.ConsumedPrimaryClickThisFrame))
            {
                return;
            }

            if (movementInputController != null
                && movementInputController.ConsumedPrimaryClickThisFrame)
            {
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

            var session = sandboxBootstrap?.Session;

            if (session == null)
            {
                HideInspect();
                return;
            }

            var position = GetGridPositionUnderMouse();

            // Movement and inspection read the same primary click. If the
            // click targets a reachable destination, it is a movement command,
            // not a request to inspect the unit after it arrives there.
            if (movementInputController != null
                && movementInputController.IsReachableDestination(position))
            {
                return;
            }

            var unit = FindUnitAtPosition(session, position);

            if (unit == null)
            {
                selectedUnit = null;
                HideInspect();
                return;
            }

            selectedUnit = unit;
            ShowUnit(unit);

            if (floatingInspectWindow != null)
            {
                floatingInspectWindow.Refresh();
            }
        }

        private void ShowUnit(BattleUnit unit)
        {
            var parent = UIWorkspaceTooltipView.ActiveInstance?.transform.parent
                ?? UIWorkspaceWindowManager.Instance?.WorkspaceRoot;
            floatingInspectWindow ??= CombatUnitInspectWindowView.Create(
                parent,
                inspectWindowPrefab);
            if (floatingInspectWindow != null)
            {
                if (floatingInspectWindow.BoundUnit != unit
                    || !floatingInspectWindow.IsVisible)
                {
                    floatingInspectWindow.Show(
                        unit,
                        UIWorkspaceTooltipView.ActiveInstance);
                }
                else
                {
                    floatingInspectWindow.Refresh();
                }

                inspectPanel?.Hide();
                return;
            }

            inspectPanel?.Show(unit);
        }

        private void HideInspect()
        {
            inspectPanel?.Hide();
            floatingInspectWindow?.Hide();
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
