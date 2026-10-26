using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;

namespace Godmist.Systems.Dungeons
{
    public static class DungeonExitRules
    {
        public static bool IsExitRoomCleared(DungeonInstance dungeon, DungeonFloorInstance floor)
        {
            if (dungeon == null || floor == null) return false;
            if (dungeon.IsAssetAuditRoom) return true;
            var room = floor.Rooms.FirstOrDefault(r => r.RoomType == DungeonRoomType.Final);
            if (room == null) return false;
            // Legacy snapshots without a final encounter remain finishable.
            return floor.Encounters.Where(e => e != null && e.RoomId == room.RoomId)
                .All(e => dungeon.IsEncounterCleared(e.InstanceId));
        }
    }
}
