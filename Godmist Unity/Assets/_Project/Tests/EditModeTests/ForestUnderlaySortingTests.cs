using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Views.Dungeons;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class ForestUnderlaySortingTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [TestCase(DungeonLength.Short)]
        [TestCase(DungeonLength.Normal)]
        [TestCase(DungeonLength.Long)]
        public void ReproducedForestKeepsOverlappingSoilInDistinctStableLanesBelowTheFloor(DungeonLength length)
        {
            var output = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../LocalHandoffs/fog-seed-959042448-20261024"));
            var fixture = new DungeonEntryPerformanceTests { OutputOverride = output,
                StageOverride = "underlay-sorting", SeedOverride = 959042448 };
            fixture.FrameProbe = (bootstrap, session) =>
            {
                var map = (DungeonFloorMapView)bootstrap.GetType().GetField("floorMapView", Private).GetValue(bootstrap);
                T Read<T>(string name) => (T)typeof(DungeonFloorMapView).GetField(name, Private).GetValue(map);
                var backdrop = Read<SpriteRenderer>("forestBackdrop");
                var patches = Read<HashSet<SpriteRenderer>>("vegetationGroundRenderers").Where(r => r != backdrop).ToArray();
                Assert.That(patches.Length, Is.GreaterThan(50), "Use the production overlapping woodland soil, not an empty fixture.");
                var before = patches.Select(r => r.sortingOrder).ToArray();
                Assert.That(before.Distinct().Count(), Is.EqualTo(patches.Length),
                    "Different translucent soil tints cannot share a sorting lane: their overlap is order dependent.");
                Assert.That(backdrop.sortingOrder, Is.LessThan(before.Min()));
                Assert.That(before.Max(), Is.LessThan(map.GetFloorFeedbackDepthSortingOrder() - 1));
                // Reserving a new actor/object baseline moves the floor band.
                // Every patch must follow without losing its relative order.
                map.GetStandingObjectDepthSortingOrder(12.34567f);
                var after = patches.Select(r => r.sortingOrder).ToArray();
                Assert.That(after.Distinct().Count(), Is.EqualTo(patches.Length));
                int shift = after[0] - before[0];
                for (int i = 0; i < before.Length; i++) Assert.That(after[i] - before[i], Is.EqualTo(shift));
                Assert.That(backdrop.sortingOrder, Is.LessThan(after.Min()));
                File.WriteAllText(Path.Combine(output, "underlay-sort-" + length + ".txt"),
                    $"{patches.Length} distinct soil lanes; backdrop={backdrop.sortingOrder}; patches={after.Min()}..{after.Max()}; floor shift={shift}");
            };
            fixture.MeasureProductionFloorEntry("dungeon.forest.expedition", length);
        }
    }
}
