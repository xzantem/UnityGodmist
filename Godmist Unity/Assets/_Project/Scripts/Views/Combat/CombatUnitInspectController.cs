using Godmist.Runtime.Combat;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Godmist.Views.Combat
{
    [DefaultExecutionOrder(-100)]
    public partial class CombatUnitInspectController : MonoBehaviour
    {
        public System.Func<GridPosition, bool> IsPositionVisible { get; set; }
        public System.Func<Vector3, GridPosition?> ResolvePointerPosition { get; set; }

        public void SetGridMapping(Vector2 origin, float size)
        {
            gridOrigin = origin;
            cellSize = Mathf.Max(0.01f, size);
        }
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
        public BattleUnit InspectedUnit => floatingInspectWindow != null && floatingInspectWindow.IsVisible ? selectedUnit : null;
        public BattleUnit HoveredUnit { get; private set; }

        private void Update() => UpdateWorldInteraction();

        private void ShowUnit(BattleUnit unit)
        {
            var parent = workspaceManager != null ? workspaceManager.WorkspaceRoot
                : worldTooltip != null ? worldTooltip.transform.parent : null;
            if (floatingInspectWindow == null) floatingInspectWindow = CombatUnitInspectWindowView.Create(
                parent,
                inspectWindowPrefab);
            if (floatingInspectWindow != null)
            {
                if (floatingInspectWindow.BoundUnit != unit
                    || !floatingInspectWindow.IsVisible)
                {
                    floatingInspectWindow.Show(
                        unit,
                        worldTooltip);
                }
                else
                {
                    floatingInspectWindow.Refresh();
                }

                if (inspectPanel != null) inspectPanel.Hide();
                return;
            }

            if (inspectPanel != null) inspectPanel.Show(unit);
        }

        private void OnDisable()
        {
            ClearWorldInteraction();
            selectedUnit = null;
            HoveredUnit = null;
            HideInspect();
        }

        private void HideInspect()
        {
            if (inspectPanel != null) inspectPanel.Hide();
            if (floatingInspectWindow != null) floatingInspectWindow.Hide();
        }

        private BattleUnit FindUnitAtPosition(
            CombatSession session,
            GridPosition position)
        {
            foreach (var unit in session.Units)
            {
                if (unit != null && unit.Character != null && unit.Character.LifeState != Godmist.Core.Enums.CharacterLifeState.Dead && unit.Position == position
                    && IsPositionVisible?.Invoke(position) != false)
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
            return session != null && TryGetGridPositionUnderMouse(out var position)
                ? FindUnitAtPosition(session, position) : null;
        }

        private bool TryGetGridPositionUnderMouse(out GridPosition position)
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

            var resolved = ResolvePointerPosition != null
                ? ResolvePointerPosition(worldPosition) : new GridPosition(gridX, gridY);
            position = resolved.GetValueOrDefault();
            return resolved.HasValue;
        }
    }
}
