using System;
using Godmist.Core.Enums;

namespace Godmist.Runtime.Dungeons
{
    public class DungeonRoomInstance
    {
        public string RoomId { get; }
        public DungeonRoomType RoomType { get; }
        public string TemplateId { get; }
        public bool IsVisited { get; private set; }

        public DungeonRoomInstance(
            string roomId,
            DungeonRoomType roomType,
            string templateId = null)
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
}

        public bool TryMarkVisited()
        {
            if (IsVisited) return false;

            IsVisited = true;
            return true;
        }
    }
}