using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine.UI;
using Godmist.Views.Exploration;
using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public sealed class DungeonTacticalTooltip : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private UIWorkspaceTooltipView tooltip;
        [SerializeField, Min(0.1f)] private float dwellSeconds = .5f;
        [SerializeField] private ExplorationPartyController partyController;
        [SerializeField] private PartyRailMenuView obstacleMenu;
        [SerializeField] private RectTransform obstacleHealthRoot;
        [SerializeField] private Slider obstacleHealthSlider;
        [SerializeField] private TMP_Text obstacleHealthText;
        private ObstacleDungeonObjectInstance menuObstacle;
        private DungeonFloorInstance floor;
        private DungeonFloorMapView map;
        private Vector3 pointer;
        private Vector2Int cell;
        private float stationarySince;
        private string shown;

        public void Bind(DungeonFloorInstance source, DungeonFloorMapView view) { floor = source; map = view; HideObstacleUi(); ResetHover(); }
        private void ResetHover() { if (tooltip != null) tooltip.InvalidateSource(this); shown = null; stationarySince = Time.unscaledTime; }
        private void Update()
        {
            if (floor?.Map == null || map == null || worldCamera == null || tooltip == null) return;
            UpdateObstacleUi();
            if (DevConsoleView.IsOpen || UIWorkspaceInputRouter.IsPointerOverUi()
                || Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2))
            { ResetHover(); return; }
            var next = map.WorldToCell(worldCamera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x,
                Input.mousePosition.y, -worldCamera.transform.position.z)));
            if (!map.IsSurfaceVisible(new DungeonSurfacePosition(next))) { ResetHover(); return; }
            if ((pointer - Input.mousePosition).sqrMagnitude > 4f || cell != next)
            { pointer = Input.mousePosition; cell = next; ResetHover(); return; }
            if (Time.unscaledTime - stationarySince < dwellSeconds) return;
            var content = Describe(floor, cell);
            if (shown == content) return;
            tooltip.InvalidateSource(this);
            shown = content;
            if (string.IsNullOrEmpty(content)) tooltip.NotifySourceExit(this);
            else tooltip.ShowText("Informacje o polu", content, this);
        }
        private void HideObstacleUi()
        {
            if (obstacleMenu != null) obstacleMenu.Hide(); menuObstacle = null;
            if (obstacleHealthRoot != null) obstacleHealthRoot.gameObject.SetActive(false);
        }
        private bool IsVisible(ObstacleDungeonObjectInstance obstacle) => obstacle is { IsActive: true }
            && map != null && map.IsSurfaceVisible(new DungeonSurfacePosition(obstacle.Position));
        private void UpdateObstacleUi()
        {
            if (DevConsoleView.IsOpen) { HideObstacleUi(); return; }
            if (menuObstacle != null && (!IsVisible(menuObstacle) || (partyController == null || !partyController.CanOrderDestruction)))
            { if (obstacleMenu != null) obstacleMenu.Hide(); menuObstacle = null; }
            if (obstacleMenu == null || !obstacleMenu.gameObject.activeSelf) menuObstacle = null;
            var hovered = menuObstacle;
            if (!UIWorkspaceInputRouter.IsPointerOverUi())
            {
                var world = worldCamera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -worldCamera.transform.position.z));
                var at = map.WorldToCell(world);
                hovered = floor.Objects.OfType<ObstacleDungeonObjectInstance>().FirstOrDefault(o => o.Position == at && IsVisible(o));
                if (Input.GetMouseButtonDown(1) && hovered != null && obstacleMenu != null && partyController != null && partyController.CanOrderDestruction)
                {
                    var target = hovered;
                    partyController.CancelMovement();
                    menuObstacle = target;
                    obstacleMenu.Show(Input.mousePosition, new List<(string, System.Action, bool)> {
                        (target.Definition.DisplayName, null, false),
                        ("Zniszcz", () => {
                            if (!partyController.BeginDestroyObstacle(target))
                                tooltip.ShowText(target.Definition.DisplayName, "Nie można podejść do tej przeszkody.", this);
                        }, target.IsDestructible)
                    });
                }
            }
            if (hovered == null && partyController != null) hovered = partyController.DestructionTarget;
            if (obstacleHealthRoot == null || obstacleHealthSlider == null || obstacleHealthText == null) return;
            var show = hovered is { IsDestructible: true } && IsVisible(hovered);
            obstacleHealthRoot.gameObject.SetActive(show);
            if (!show) return;
            obstacleHealthSlider.maxValue = hovered.MaxHits;
            obstacleHealthSlider.SetValueWithoutNotify(hovered.RemainingHits);
            obstacleHealthText.text = $"{hovered.RemainingHits}/{hovered.MaxHits}";
            var screen = worldCamera.WorldToScreenPoint((Vector3)map.CellToWorldCenter(hovered.Position) + Vector3.up * .65f);
            var canvas = obstacleHealthRoot.GetComponentInParent<Canvas>();
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)obstacleHealthRoot.parent, screen,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var local))
                obstacleHealthRoot.localPosition = new Vector3(local.x, local.y, 0);
        }
        public static string Describe(DungeonFloorInstance floor, Vector2Int cell)
        {
            if (floor?.Map == null || !floor.Map.IsWalkable(cell)) return string.Empty;
            var lines = new List<string>();
            if (floor.Map.IsDifficultTerrain(cell)) lines.Add("Trudny teren: +1 AP za wejście. W eksploracji ruch wolniejszy o 50%.");
            foreach (var obj in floor.Objects)
            {
                if (!obj.IsActive) continue;
                if (obj is DoorDungeonObjectInstance door)
                {
                    if (door.Edge.FirstCell == cell || door.Edge.SecondCell == cell)
                        lines.Add(door.IsOpen ? "Drzwi: otwarte." : "Drzwi: zamknięte — blokują przejście.");
                    continue;
                }
                if (obj.Position != cell) continue;
                if (obj is SpikeTrapDungeonObjectInstance trap)
                    lines.Add($"{obj.Definition.DisplayName}: " + (trap.IsTriggered ? "zużyta pułapka." : "aktywna pułapka — wejście jest niebezpieczne."));
                else if (obj is ObstacleDungeonObjectInstance obstacle)
                    lines.Add($"{obj.Definition.DisplayName}: " +
                        (obstacle.BlocksMovement ? "blokuje ruch" : "nie blokuje ruchu") + ", " +
                        (obstacle.BlocksLineOfSight ? "blokuje widoczność." : "nie blokuje widoczności."));
            }
            var walls = new HashSet<DungeonWallType>();
            foreach (var edge in floor.Map.WallEdges)
                if (edge.FirstCell == cell || edge.SecondCell == cell) walls.Add(floor.Map.GetWallType(edge));
            foreach (var wall in walls)
                lines.Add(wall == DungeonWallType.Low ? "Niski murek przy polu: przejście +2 AP, tylko na istniejącą podłogę."
                    : wall == DungeonWallType.Full ? "Pełna ściana przy polu: blokuje ruch i widoczność."
                    : "Balustrada przy polu: blokuje ruch, przepuszcza widoczność.");
            return string.Join("\n", lines);
        }
        private void OnDisable() { HideObstacleUi(); ResetHover(); }
    }
}
