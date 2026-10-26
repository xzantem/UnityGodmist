using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Data.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class CatacombsEntranceTests
    {
        [Test]
        public void AuthoredEntrancesAreOffsetAndApproachesRemainClearInEveryRotation()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(
                "Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            var definition = catalog.DungeonDefinitions.Single(d => d != null && d.Id == "dungeon.catacombs.expedition");
            var offsetRooms = 0;
            var staggeredRooms = 0;
            foreach (var room in definition.RoomTemplateCatalog.ProductionTemplates.Where(t => t.RoomType != DungeonRoomType.Final))
            {
                var bands = room.AllowedBoundaryEdges.GroupBy(e => e.Direction)
                    .ToDictionary(g => g.Key, g => g.OrderBy(e => e.LocalCell.y).ThenBy(e => e.LocalCell.x).First().LocalCell);
                if (bands.Any(p => p.Key is DungeonEdgeDirection.Up or DungeonEdgeDirection.Down
                        ? p.Value.x != (room.SizeInCells.x - 2) / 2 : p.Value.y != (room.SizeInCells.y - 2) / 2)) offsetRooms++;
                if ((bands.TryGetValue(DungeonEdgeDirection.Up, out var up) && bands.TryGetValue(DungeonEdgeDirection.Down, out var down) && up.x != down.x)
                    || (bands.TryGetValue(DungeonEdgeDirection.Left, out var left) && bands.TryGetValue(DungeonEdgeDirection.Right, out var right) && left.y != right.y)) staggeredRooms++;
                foreach (var rotation in room.AllowedRotationDegrees)
                {
                    Vector2Int Transform(Vector2Int cell) => DungeonRoomTransformUtility.TransformCell(cell, room.SizeInCells, rotation);
                    var floor = room.EnumerateSurfaces().Where(s => s.Level == 0).Select(s => Transform(s.LocalCell)).ToHashSet();
                    var occupied = room.SemanticAnchors.Where(a => a.ObjectDefinition is ObstacleDungeonObjectDefinition)
                        .SelectMany(a => ((ObstacleDungeonObjectDefinition)a.ObjectDefinition).EnumerateOccupiedCells(Transform(a.LocalCell))).ToHashSet();
                    foreach (var edge in room.AllowedBoundaryEdges)
                    {
                        var normal = new DungeonRoomBoundaryEdgeData(Vector2Int.zero,
                            DungeonRoomTransformUtility.TransformDirection(edge.Direction, rotation)).GetDirectionOffset();
                        for (var depth = 0; depth < 3; depth++)
                        {
                            var cell = Transform(edge.LocalCell) - normal * depth;
                            Assert.That(floor.Contains(cell) && !occupied.Contains(cell), Is.True,
                                room.Id + " rotation=" + rotation + " entrance=" + edge.LocalCell + " depth=" + depth);
                        }
                    }
                }
            }
            Assert.That(offsetRooms, Is.EqualTo(16), "Catacombs must not fall back to centred ports on every template.");
            Assert.That(staggeredRooms, Is.GreaterThanOrEqualTo(8));
        }
    }
}
