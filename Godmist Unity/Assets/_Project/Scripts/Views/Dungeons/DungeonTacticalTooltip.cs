using Godmist.Core.Utilities;
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
        [SerializeField] private Godmist.Views.Combat.CombatUnitInspectController unitInteractions;
        private ObstacleDungeonObjectInstance menuObstacle;
        private DungeonFloorInstance floor;
        private DungeonFloorMapView map;
        private IReadOnlyList<DungeonObjectView> objectViews;
        private Vector3 pointer;
        private Vector2Int cell;
        private float stationarySince;
        private string shown;
        private bool groundPress, groundDragged, groundMenu;
        private bool pointerOverObstacle;
        private int displayedRemainingHits = -1, displayedMaxHits = -1;
        private Vector2 groundPressScreen;

        public void Bind(DungeonFloorInstance source, DungeonFloorMapView view,
            IReadOnlyList<DungeonObjectView> views = null)
        {
            floor = source; map = view; objectViews = views; HideObstacleUi(); ResetHover();
            tooltip?.PrepareRichUnitContent();
        }
        private void ResetHover() { if (tooltip != null) tooltip.InvalidateSource(this); shown = null; stationarySince = Time.unscaledTime; }
        private void Update()
        {
            if (floor?.Map == null || map == null || worldCamera == null || tooltip == null) return;
            if (UIWorkspaceTooltipView.IsPointerOverVisibleTooltip(Godmist.Views.UI.ControllerInput.PointerPosition))
            {
                groundPress = false;
                return;
            }
            UpdateGroundMenu();
            if (unitInteractions != null && unitInteractions.HasWorldUnitHover) { HideObstacleUi(); ResetHover(); return; }
            UpdateObstacleUi();
            if (pointerOverObstacle) { ResetHover(); return; }
            // Ground cells do not have a hover tooltip. Keep obstacle inspection
            // and the explicit camp context menu above.
            ResetHover();
        }
        private void HideObstacleUi()
        {
            groundMenu = false;
            if (obstacleMenu != null) obstacleMenu.Hide(); menuObstacle = null;
            if (obstacleHealthRoot != null) obstacleHealthRoot.gameObject.SetActive(false);
        }
        private void UpdateGroundMenu()
        {
            if ((Input.GetKeyDown(KeyCode.Escape) || Godmist.Views.UI.ControllerInput.Cancel)) { groundPress = false; if (groundMenu) HideObstacleUi(); return; }
            if (partyController == null || !partyController.CanAcceptExplorationInput
                || partyController.Session.HasCombat || obstacleMenu == null)
            {
                groundPress = false;
                if (groundMenu) HideObstacleUi();
                return;
            }
            if ((Input.GetMouseButtonDown(1) || Godmist.Views.UI.ControllerInput.Context))
            {
                groundPress = false;
                if (UIWorkspaceInputRouter.IsPointerOverUi() || unitInteractions != null && unitInteractions.HasWorldUnitHover) return;
                var world = worldCamera.ScreenToWorldPoint(Godmist.Views.UI.ControllerInput.PointerPosition);
                var at = map.WorldToCell(world);
                if (!floor.Map.IsWalkable(at) || !map.IsSurfaceVisible(new DungeonSurfacePosition(at))
                    || floor.Objects.Any(o => o.IsActive
                        && (o is ObstacleDungeonObjectInstance obstacle
                            ? obstacle.OccupiesCell(at)
                            : o.Position == at))
                    || DungeonEnemyBodyView.IsPointerOverBody(world)) return;
                groundPress = true; groundDragged = false; groundPressScreen = Godmist.Views.UI.ControllerInput.PointerPosition;
            }
            if (!groundPress) return;
            if (((Vector2)Godmist.Views.UI.ControllerInput.PointerPosition - groundPressScreen).sqrMagnitude
                > ExplorationCameraFollow.RightDragThreshold * ExplorationCameraFollow.RightDragThreshold) groundDragged = true;
            if (!Input.GetMouseButtonUp(1)) return;
            groundPress = false;
            if (groundDragged || UIWorkspaceInputRouter.IsPointerOverUi()
                || unitInteractions != null && unitInteractions.HasWorldUnitHover) return;
            partyController.CancelMovement();
            var allowed = partyController.CanCamp(out var reason);
            menuObstacle = null; groundMenu = true;
            var actions = new List<(string, System.Action, bool)> {
                ("Rozbij obóz", TryCampFromMenu, allowed)
            };
            obstacleMenu.Show(Godmist.Views.UI.ControllerInput.PointerPosition, actions, allowed ? "Obóz" : reason);
        }
        private void TryCampFromMenu()
        {
            groundMenu = false;
            ResetHover();
            var failure = partyController.TryCamp();
            if (string.IsNullOrEmpty(failure)) return;
            groundMenu = true;
            obstacleMenu.Show(Godmist.Views.UI.ControllerInput.PointerPosition,
                new List<(string, System.Action, bool)> { (failure, null, false) }, "Obóz");
        }
        private bool IsVisible(ObstacleDungeonObjectInstance obstacle)
        {
            if (obstacle is not { IsActive: true } || map == null) return false;
            var size = obstacle.OccupiedSizeInCells;
            for (var x = 0; x < size.x; x++)
            for (var y = 0; y < size.y; y++)
                if (map.IsSurfaceVisible(new DungeonSurfacePosition(obstacle.Position + new Vector2Int(x, y)))) return true;
            return false;
        }

        private ObstacleDungeonObjectInstance FindHoveredObstacle(Vector2Int at)
        {
            for (var index = 0; index < floor.Objects.Count; index++)
                if (floor.Objects[index] is ObstacleDungeonObjectInstance obstacle
                    && obstacle.OccupiesCell(at) && IsVisible(obstacle)) return obstacle;
            return null;
        }
        private void UpdateObstacleUi()
        {
            pointerOverObstacle = false;
            if (DevConsoleView.IsOpen) { HideObstacleUi(); return; }
            if (menuObstacle != null && (!IsVisible(menuObstacle) || (partyController == null || !partyController.CanOrderDestruction)))
            { if (obstacleMenu != null) obstacleMenu.Hide(); menuObstacle = null; }
            if (obstacleMenu == null || !obstacleMenu.gameObject.activeSelf) menuObstacle = null;
            var hovered = menuObstacle;
            if (!UIWorkspaceInputRouter.IsPointerOverUi())
            {
                var world = worldCamera.ScreenToWorldPoint(new Vector3(Godmist.Views.UI.ControllerInput.PointerPosition.x, Godmist.Views.UI.ControllerInput.PointerPosition.y, -worldCamera.transform.position.z));
                var at = map.WorldToCell(world);
                hovered = FindHoveredObstacle(at);
                pointerOverObstacle = hovered != null;
                if ((Input.GetMouseButtonDown(1) || Godmist.Views.UI.ControllerInput.Context) && hovered != null && obstacleMenu != null && partyController != null && partyController.CanOrderDestruction)
                {
                    var target = hovered;
                    groundMenu = false;
                    partyController.CancelMovement();
                    menuObstacle = target;
                    obstacleMenu.Show(Godmist.Views.UI.ControllerInput.PointerPosition, new List<(string, System.Action, bool)> {
                        (target.Definition.DisplayName, null, false),
                        ("Zniszcz", () => {
                            if (!partyController.BeginDestroyObstacle(target))
                                tooltip.ShowText(target.Definition.DisplayName, "Nie można podejść do tej przeszkody.", this);
                        }, target.IsDestructible)
                    });
                }
            }
            if (hovered == null && partyController != null) hovered = partyController.DestructionTarget;
            RefreshObstacleHealth(hovered);
        }

        private void ConfigureObstacleHealthPresentation()
        {
            // This world label needs readable dimensions independently of zoom.
            // Fit the existing Gothic frame and keep the fill inside its aperture.
            obstacleHealthRoot.sizeDelta = new Vector2(112, 24);
            obstacleHealthRoot.pivot = new Vector2(.5f, 0);
            CombatBarPresentation.FitFrame(obstacleHealthSlider);
            var area = obstacleHealthSlider.fillRect?.parent as RectTransform;
            if (area != null && area != obstacleHealthRoot)
            {
                area.anchorMin = Vector2.zero;
                area.anchorMax = Vector2.one;
                area.offsetMin = new Vector2(7, 4);
                area.offsetMax = new Vector2(-7, -4);
                if (area.GetComponent<RectMask2D>() == null) area.gameObject.AddComponent<RectMask2D>();
                var fill = obstacleHealthSlider.fillRect;
                fill.offsetMin = fill.offsetMax = Vector2.zero;
                if (fill.TryGetComponent<Image>(out var fillImage)) fillImage.type = Image.Type.Simple;
            }
            var background = obstacleHealthRoot.Find("Background")?.GetComponent<Image>();
            if (background != null)
            {
                background.rectTransform.offsetMin = new Vector2(7, 4);
                background.rectTransform.offsetMax = new Vector2(-7, -4);
                background.color = new Color(.07f, .07f, .08f, 1);
            }
            obstacleHealthText.enableAutoSizing = false;
            obstacleHealthText.fontSize = 16;
            obstacleHealthText.color = Color.white;
            obstacleHealthText.alignment = TextAlignmentOptions.Center;
            obstacleHealthText.transform.SetAsLastSibling();
        }

        private void RefreshObstacleHealth(ObstacleDungeonObjectInstance hovered)
        {
            if (obstacleHealthRoot == null || obstacleHealthSlider == null || obstacleHealthText == null) return;
            var show = hovered is { IsDestructible: true } && IsVisible(hovered);
            var wasActive = obstacleHealthRoot.gameObject.activeSelf;
            obstacleHealthRoot.gameObject.SetActive(show);
            if (!show) return;
            if (!wasActive) ConfigureObstacleHealthPresentation();
            obstacleHealthSlider.maxValue = hovered.MaxHits;
            obstacleHealthSlider.SetValueWithoutNotify(hovered.RemainingHits);
            if (displayedRemainingHits != hovered.RemainingHits || displayedMaxHits != hovered.MaxHits)
            {
                displayedRemainingHits = hovered.RemainingHits;
                displayedMaxHits = hovered.MaxHits;
                obstacleHealthText.text = $"{displayedRemainingHits}/{displayedMaxHits}";
            }
            var anchor = (Vector3)map.CellToWorldCenter(hovered.Position) + Vector3.up * .65f;
            if (objectViews != null)
                foreach (var view in objectViews)
                    if (view is DungeonObstacleView obstacleView && view.InstanceId == hovered.InstanceId
                        && obstacleView.TryGetPresentationBounds(out var bounds))
                    {
                        anchor = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
                        break;
                    }
            var screen = worldCamera.WorldToScreenPoint(anchor);
            var canvas = obstacleHealthRoot.GetComponentInParent<Canvas>();
            if (canvas == null || screen.z <= 0) { obstacleHealthRoot.gameObject.SetActive(false); return; }
            screen.y += 8f;
            var rect = worldCamera.pixelRect;
            var halfWidth = obstacleHealthRoot.rect.width * canvas.scaleFactor * .5f;
            screen.x = Mathf.Clamp(screen.x, rect.xMin + halfWidth + 4, rect.xMax - halfWidth - 4);
            screen.y = Mathf.Clamp(screen.y, rect.yMin + 4,
                rect.yMax - obstacleHealthRoot.rect.height * canvas.scaleFactor - 4);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)obstacleHealthRoot.parent, screen,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var local))
                obstacleHealthRoot.localPosition = new Vector3(local.x, local.y, 0);
        }
        public static string Describe(DungeonFloorInstance floor, Vector2Int cell)
        {
            if (floor?.Map == null || !floor.Map.IsWalkable(cell)) return string.Empty;
            for (var index = 0; index < floor.Objects.Count; index++)
                if (floor.Objects[index] is ObstacleDungeonObjectInstance obstacleAtCell && obstacleAtCell.OccupiesCell(cell))
                    return string.Empty;
            var lines = new List<string>();
            if (floor.Map.IsDifficultTerrain(cell)) lines.Add($"Trudny teren: +1 {UiTerms.ActionPointsShort} za wejście. W eksploracji ruch wolniejszy o 50%.");
            foreach (var obj in floor.Objects)
            {
                if (!obj.IsActive) continue;
                if (obj is DoorDungeonObjectInstance door)
                {
                    if (door.Edge.FirstCell == cell || door.Edge.SecondCell == cell)
                        lines.Add(door.IsOpen ? "Drzwi: otwarte." : "Drzwi: zamknięte — blokują przejście.");
                    continue;
                }
                if (obj is ObstacleDungeonObjectInstance obstacleCell
                    ? !obstacleCell.OccupiesCell(cell)
                    : obj.Position != cell) continue;
                if (obj is SpikeTrapDungeonObjectInstance trap)
                    lines.Add($"{obj.Definition.DisplayName}: " + (trap.IsTriggered ? "zużyta pułapka." : "aktywna pułapka — wejście jest niebezpieczne."));
            }
            var walls = new HashSet<DungeonWallType>();
            foreach (var edge in floor.Map.WallEdges)
                if (edge.FirstCell == cell || edge.SecondCell == cell) walls.Add(floor.Map.GetWallType(edge));
            foreach (var wall in walls)
                lines.Add(wall == DungeonWallType.Low ? $"Niski murek przy polu: przejście +2 {UiTerms.ActionPointsShort}, tylko na istniejącą podłogę."
                    : wall == DungeonWallType.Full ? "Pełna ściana przy polu: blokuje ruch i widoczność."
                    : "Balustrada przy polu: blokuje ruch, przepuszcza widoczność.");
            return string.Join("\n", lines);
        }
        private void OnDisable() { groundPress = false; HideObstacleUi(); ResetHover(); }
    }
}
