using System.Collections.Generic;
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
        private DungeonFloorInstance floor;
        private DungeonFloorMapView map;
        private Vector3 pointer;
        private Vector2Int cell;
        private float stationarySince;
        private string shown;

        public void Bind(DungeonFloorInstance source, DungeonFloorMapView view) { floor = source; map = view; ResetHover(); }
        private void ResetHover() { tooltip?.InvalidateSource(this); shown = null; stationarySince = Time.unscaledTime; }
        private void Update()
        {
            if (floor?.Map == null || map == null || worldCamera == null || tooltip == null) return;
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
        private void OnDisable() => ResetHover();
    }
}
