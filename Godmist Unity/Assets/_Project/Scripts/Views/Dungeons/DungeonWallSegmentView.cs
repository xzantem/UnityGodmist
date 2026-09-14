using Godmist.Runtime.Dungeons;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Godmist.Views.Dungeons
{
    public class DungeonWallSegmentView : MonoBehaviour
    {
        [SerializeField] private Collider2D movementCollider;
        [SerializeField] private float thickness = 0.14f;

        public void Bind(
            DungeonCellEdge edge,
            Tilemap floorTilemap, bool blocksMovement = true)
        {
            if (floorTilemap == null)
            {
                return;
            }

            if (movementCollider != null) movementCollider.enabled = blocksMovement;
            var firstCenter = floorTilemap.GetCellCenterWorld(
                new Vector3Int(
                    edge.FirstCell.x,
                    edge.FirstCell.y,
                    0));

            var secondCenter = floorTilemap.GetCellCenterWorld(
                new Vector3Int(
                    edge.SecondCell.x,
                    edge.SecondCell.y,
                    0));

            var delta = edge.SecondCell - edge.FirstCell;

            transform.position = (firstCenter + secondCenter) * 0.5f;

            transform.rotation = Quaternion.Euler(0f, 0f, delta.x != 0 ? 0f : 90f);
            transform.localScale = new Vector3(thickness, 1f, 1f);
        }
    }
}