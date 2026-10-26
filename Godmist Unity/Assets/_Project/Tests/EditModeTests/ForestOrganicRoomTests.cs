using System;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.World;
using NUnit.Framework;
using UnityEditor;

namespace Godmist.Tests.EditMode
{
    public class ForestOrganicRoomTests
    {
        [Test]
        public void ForestHasElongatedBodiesAndStaggeredAuthoredEntrances()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(
                "Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            var definition = catalog.DungeonDefinitions.Single(d => d != null && d.Id == "dungeon.forest.expedition");
            var rooms = definition.RoomTemplateCatalog.ProductionTemplates.Where(t => t.RoomType != DungeonRoomType.Final).ToArray();
            Assert.That(rooms.Count(t => (float)Math.Max(t.SizeInCells.x, t.SizeInCells.y) / Math.Min(t.SizeInCells.x, t.SizeInCells.y) >= 1.6f),
                Is.GreaterThanOrEqualTo(10), "Most forest bodies should be elongated, with wider glades retained as a minority.");
            var offCentre = 0;
            var staggered = 0;
            foreach (var room in rooms)
            {
                var ports = room.AllowedBoundaryEdges.GroupBy(e => e.Direction)
                    .ToDictionary(g => g.Key, g => g.OrderBy(e => e.LocalCell.y).ThenBy(e => e.LocalCell.x).First().LocalCell);
                if (ports.Any(p => p.Key is DungeonEdgeDirection.Up or DungeonEdgeDirection.Down
                        ? p.Value.x != (room.SizeInCells.x - 3) / 2 : p.Value.y != (room.SizeInCells.y - 3) / 2)) offCentre++;
                if ((ports.TryGetValue(DungeonEdgeDirection.Up, out var up) && ports.TryGetValue(DungeonEdgeDirection.Down, out var down) && up.x != down.x)
                    || (ports.TryGetValue(DungeonEdgeDirection.Left, out var left) && ports.TryGetValue(DungeonEdgeDirection.Right, out var right) && left.y != right.y)) staggered++;
            }
            Assert.That(offCentre, Is.EqualTo(rooms.Length), "Every non-arena forest template must have a deliberately positioned entrance.");
            Assert.That(staggered, Is.GreaterThanOrEqualTo(8), "Opposing exits must not enforce a straight central path through every room.");
        }
    }
}
