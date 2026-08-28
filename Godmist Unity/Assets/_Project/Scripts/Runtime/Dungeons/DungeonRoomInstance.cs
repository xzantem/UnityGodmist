using System;
using Godmist.Core.Enums;
using UnityEngine;

namespace Godmist.Runtime.Dungeons
{
    public class DungeonRoomInstance
    {
        public string RoomId { get; }
        public DungeonRoomType RoomType { get; }
        public string TemplateId { get; }
        public Vector2Int LayoutPosition { get; }
        public bool IsRequiredProgressionRoom { get; }
        public bool IsProgressionRoomCompleted { get; private set; }
        public bool IsVisited { get; private set; }

        public DungeonRoomInstance(
            string roomId,
            DungeonRoomType roomType,
            string templateId = null,
            Vector2Int layoutPosition = default,
            bool isRequiredProgressionRoom = false)
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
