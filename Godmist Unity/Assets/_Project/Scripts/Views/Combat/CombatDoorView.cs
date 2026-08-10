using Godmist.Runtime.Combat;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Godmist.Views.Combat
{
    public class CombatDoorView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private float thickness = 0.16f;
        [SerializeField] private Color closedColor = new(0.45f, 0.23f, 0.1f, 1f);
        [SerializeField] private Color openColor = new(0.3f, 0.7f, 0.45f, 0.35f);

        private CombatSandboxBootstrap sandboxBootstrap;
        private string objectId;

        public void Initialize(
            CombatSandboxBootstrap nextSandboxBootstrap,
            DoorCombatMapObject door,
            Tilemap floorTilemap)
        {
            if (nextSandboxBootstrap == null
                || door == null
                || floorTilemap == null)
            {
                return;
            }

            sandboxBootstrap = nextSandboxBootstrap;
            objectId = door.ObjectId;

            ConfigureEdgeVisual(door.Edge, floorTilemap);

            sandboxBootstrap.CombatMapObjectChanged += HandleCombatMapObjectChanged;

            Refresh();
        }

        private void OnDestroy()
        {
            if (sandboxBootstrap != null)
            {
                sandboxBootstrap.CombatMapObjectChanged -= HandleCombatMapObjectChanged;
            }
        }

        private void HandleCombatMapObjectChanged(CombatMapObject changedObject)
        {
            if (changedObject == null || changedObject.ObjectId != objectId)
            {
                return;
            }

            Refresh();
        }

        private void ConfigureEdgeVisual(
            BattleCellEdge edge,
            Tilemap floorTilemap)
        {
            var firstCenter = floorTilemap.GetCellCenterWorld(
                new Vector3Int(
                    edge.FirstCell.X,
                    edge.FirstCell.Y,
                    0));

            var secondCenter = floorTilemap.GetCellCenterWorld(
                new Vector3Int(
                    edge.SecondCell.X,
                    edge.SecondCell.Y,
                    0));

            transform.position = (firstCenter + secondCenter) * 0.5f;

            if (targetRenderer == null)
            {
                return;
            }

            var deltaX = edge.SecondCell.X - edge.FirstCell.X;

            targetRenderer.transform.localScale = deltaX != 0
                ? new Vector3(thickness, 1f, 1f)
                : new Vector3(1f, thickness, 1f);
        }

        private void Refresh()
        {
            if (sandboxBootstrap == null || targetRenderer == null)
            {
                return;
            }

            var door = sandboxBootstrap.GetCombatMapObject(objectId)
                       as DoorCombatMapObject;

            targetRenderer.enabled = door != null;

            if (door == null)
            {
                return;
            }

            targetRenderer.color = door.IsOpen
                ? openColor
                : closedColor;
        }
    }
}