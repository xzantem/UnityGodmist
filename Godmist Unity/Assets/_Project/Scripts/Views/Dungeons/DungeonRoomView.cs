using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using TMPro;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public class DungeonRoomView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer backgroundRenderer;
        [SerializeField] private TextMeshPro label;
        [SerializeField] private LineRenderer wallOutlineRenderer;

        [Header("Colors")]
        [SerializeField] private Color startColor = new(0.25f, 0.8f, 0.45f);
        [SerializeField] private Color normalColor = new(0.35f, 0.55f, 0.8f);
        [SerializeField] private Color optionalColor = new(0.7f, 0.5f, 0.85f);
        [SerializeField] private Color corridorColor = new(0.45f, 0.45f, 0.5f);
        [SerializeField] private Color finalColor = new(0.9f, 0.6f, 0.25f);

        public string RoomId { get; private set; } = string.Empty;

        public Bounds WorldBounds
        {
            get
            {
                return backgroundRenderer != null
                    ? backgroundRenderer.bounds
                    : new Bounds(transform.position, Vector3.zero);
            }
        }

        public void Bind(
            DungeonRoomInstance room,
            DungeonRoomTemplateDefinition template)
        {
            if (room == null || template == null) return;

            RoomId = room.RoomId;

            var roomSize = template.SizeInCells;
            RebuildWallOutline(roomSize);

            transform.position = new Vector3(
                room.LayoutPosition.x + roomSize.x * 0.5f,
                room.LayoutPosition.y + roomSize.y * 0.5f,
                0f);

            if (backgroundRenderer != null)
            {
                backgroundRenderer.transform.localScale = new Vector3(
                    roomSize.x,
                    roomSize.y,
                    1f);

                backgroundRenderer.color = GetColor(room.RoomType);
            }

            if (label != null)
            {
                var templateName = string.IsNullOrWhiteSpace(
                    template.DisplayName)
                    ? template.Id
                    : template.DisplayName;

                label.text = $"{room.RoomType}\n{templateName}";
            }
        }

        private Color GetColor(DungeonRoomType roomType)
        {
            return roomType switch
            {
                DungeonRoomType.Start => startColor,
                DungeonRoomType.Normal => normalColor,
                DungeonRoomType.Optional => optionalColor,
                DungeonRoomType.Corridor => corridorColor,
                DungeonRoomType.Final => finalColor,
                _ => Color.white
            };
        }

        private void RebuildWallOutline(Vector2Int roomSize)
        {
            if (wallOutlineRenderer == null)
            {
                return;
            }

            var halfWidth = roomSize.x * 0.5f;
            var halfHeight = roomSize.y * 0.5f;

            wallOutlineRenderer.useWorldSpace = false;
            wallOutlineRenderer.loop = true;
            wallOutlineRenderer.positionCount = 4;

            wallOutlineRenderer.SetPosition(
                0,
                new Vector3(-halfWidth, -halfHeight, 0f));

            wallOutlineRenderer.SetPosition(
                1,
                new Vector3(-halfWidth, halfHeight, 0f));

            wallOutlineRenderer.SetPosition(
                2,
                new Vector3(halfWidth, halfHeight, 0f));

            wallOutlineRenderer.SetPosition(
                3,
                new Vector3(halfWidth, -halfHeight, 0f));
        }
    }
}