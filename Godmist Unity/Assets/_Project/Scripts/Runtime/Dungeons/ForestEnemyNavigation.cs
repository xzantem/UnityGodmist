using Godmist.Runtime.Dungeons;
using UnityEngine;
namespace Godmist.Runtime.Dungeons
{
    public static class ForestEnemyNavigation
    {
        public static bool IsEnt(string id) => id == "enemy.forest.infected_ent" || id == "enemy.forest.infected_ent.burning";
        public static bool CanOccupy(DungeonFloorMap map, Vector2Int cell, string homeRoom, bool ent)
        {
            if (map == null || !map.IsWalkable(cell)) return false;
            if (ent && (!map.TryGetRoomId(cell, out var room) || room != homeRoom)) return false;
            var radius = ent ? 2 : 1;
            for (var y=-radius;y<=radius;y++) for(var x=-radius;x<=radius;x++)
            {
                var neighbour = cell+new Vector2Int(x,y);
                if (!map.IsWalkable(neighbour)) return false;
                if (ent && (!map.TryGetRoomId(neighbour,out var id) || id != homeRoom)) return false;
            }
            return true;
        }
    }
}
