using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public class DungeonDoorwayView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer markerRenderer;

        public void Bind(DungeonRoomConnection connection)
        {
            if (connection == null || !connection.HasGeometry)
            {
                return;
            }

            var firstDoorCenter = new Vector3(
                connection.FirstDoorCell.x + 0.5f,
                connection.FirstDoorCell.y + 0.5f,
                0f);

            var secondDoorCenter = new Vector3(
                connection.SecondDoorCell.x + 0.5f,
                connection.SecondDoorCell.y + 0.5f,
                0f);

            transform.position = (firstDoorCenter + secondDoorCenter) * 0.5f;

            if (markerRenderer == null)
            {
                return;
            }

            var isHorizontalConnection =
                connection.FirstDoorCell.y
                == connection.SecondDoorCell.y;

            var doorwayLength = 0.6f * connection.DoorwayWidth;

            markerRenderer.transform.localScale = isHorizontalConnection
                ? new Vector3(0.16f, doorwayLength, 1f)
                : new Vector3(doorwayLength, 0.16f, 1f);
        }
    }
}