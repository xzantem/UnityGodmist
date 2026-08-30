using System;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using UnityEngine;

namespace Godmist.Runtime.Dungeons
{
    public class DungeonRoomInstance
    {
        public string RoomId { get; }
        public DungeonRoomType RoomType { get; }
        public string TemplateId { get; }
        public Vector2Int LayoutPosition { get; }
        public int RotationDegrees { get; }
        public bool IsRequiredProgressionRoom { get; }
        public bool IsProgressionRoomCompleted { get; private set; }
        public bool IsVisited { get; private set; }

        public DungeonRoomInstance(
            string roomId,
            DungeonRoomType roomType,
            string templateId = null,
            Vector2Int layoutPosition = default,
            bool isRequiredProgressionRoom = false,
            int rotationDegrees = 0)
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                throw new ArgumentException(
                    "RoomId musi byc niepuste.",
                    nameof(roomId));
            }

            RoomId = roomId;
            RoomType = roomType;
            TemplateId = templateId ?? string.Empty;
            LayoutPosition = layoutPosition;
            IsRequiredProgressionRoom = isRequiredProgressionRoom;
            RotationDegrees = DungeonRoomTransformUtility.NormalizeRotation(
                rotationDegrees);
        }

        public bool TryMarkVisited()
        {
            if (IsVisited) return false;

            IsVisited = true;
            return true;
        }

        public bool TryMarkProgressionRoomCompleted()
        {
            if (!IsRequiredProgressionRoom || IsProgressionRoomCompleted)
            {
                return false;
            }

            IsProgressionRoomCompleted = true;
            return true;
        }

        public bool TryRestoreProgressionRoomCompleted(bool isCompleted)
        {
            if (!IsRequiredProgressionRoom && isCompleted)
            {
                return false;
            }

            IsProgressionRoomCompleted = isCompleted;
            return true;
        }

        public bool TryRestoreVisited(bool isVisited)
        {
            IsVisited = isVisited;
            return true;
        }
    }
}
