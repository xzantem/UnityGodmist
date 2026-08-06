using Godmist.Runtime.Dungeons;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Godmist.Views.Dungeons
{
    public class DungeonWallSegmentView : MonoBehaviour
    {
        [SerializeField] private float thickness = 0.14f;

        public void Bind(
            DungeonCellEdge edge,
            Tilemap floorTilemap)
        {
            if (floorTilemap == null)
            {
                return;
            }

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

            if (delta.x != 0)
            {
                transform.localScale = new Vector3(
                    thickness,
                    1f,
                    1f);

                return;
            }

            transform.localScale = new Vector3(
                1f,
                thickness,
                1f);
        }
    }
}