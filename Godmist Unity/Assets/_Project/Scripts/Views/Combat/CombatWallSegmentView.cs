using Godmist.Runtime.Combat;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Godmist.Views.Combat
{
    public class CombatWallSegmentView : MonoBehaviour
    {
        [SerializeField] private float thickness = 0.14f;

        public void Bind(
            BattleCellEdge edge,
            Tilemap floorTilemap)
        {
            if (floorTilemap == null) return;

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

            var deltaX = edge.SecondCell.X - edge.FirstCell.X;

            transform.position = (firstCenter + secondCenter) * 0.5f;

            transform.rotation = Quaternion.Euler(0f, 0f, deltaX != 0 ? 0f : 90f);
            transform.localScale = new Vector3(thickness, 1f, 1f);
        }
    }
}