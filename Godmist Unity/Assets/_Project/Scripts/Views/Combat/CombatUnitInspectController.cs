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

        [Header("Grid Mapping")]
        [SerializeField] private Vector2 gridOrigin = Vector2.zero;
        [SerializeField] private float cellSize = 1f;

        private void Awake()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }
        }

        private void Update()
        {
            if (DevConsoleView.IsOpen) return;
            
            if (inspectPanel == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                inspectPanel.Hide();
                return;
            }

            if (targetSelectionController != null
                && targetSelectionController.IsSelectingTarget)
            {
                inspectPanel.Hide();
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
                inspectPanel.Hide();
                return;
            }

            var position = GetGridPositionUnderMouse();
            var unit = FindUnitAtPosition(session, position);

            if (unit == null)
            {
                inspectPanel.Hide();
                return;
            }

            inspectPanel.Show(unit);
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