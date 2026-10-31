using System.Linq;
using Godmist.Editor;
using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public class CatacombsWallRulesetTests
    {
        [Test]
        public void CatalogCoversAllLocalCasesAndAllEightyOneInteriorCombinations()
        {
            var cases=CatacombsWallRulesetReview.Cases();
            Assert.That(cases.Length,Is.EqualTo(711));
            Assert.That(cases.Select(c=>c.id).Distinct().Count(),Is.EqualTo(711));
            Assert.That(cases.Where(c=>c.floorMask==15).Select(c=>c.wallCode),Is.EquivalentTo(Enumerable.Range(0,81)));
            Assert.That(cases.Count(c=>c.boundaryLow),Is.EqualTo(390));
            foreach(var sample in cases)
            {
                var map=CatacombsWallRulesetReview.BuildMap(sample);
                Assert.That(map.SurfaceWallEdges.Count(),Is.EqualTo(sample.low+sample.full),sample.id);
                foreach(var edge in map.SurfaceWallEdges)
                    if(map.GetWallType(edge)==Godmist.Core.Enums.DungeonWallType.Low)
                        Assert.That(map.IsWalkable(edge.First)||map.IsWalkable(edge.Second),sample.id);
            }
        }

        [Test,Explicit("Exports production GPU renders in an isolated preview scene; requires Edit Mode.")]
        public void ExportProductionRuleset() => CatacombsWallRulesetReview.Export();

        [Test,Explicit("Exports selected mixed-junction production GPU renders into a separate repair folder.")]
        public void ExportMixedJunctionRepairPreview() => CatacombsWallRulesetReview.Export(
            System.IO.Path.GetFullPath("../LocalHandoffs/catacombs-wall-joins-repair-20261030/preview"),
            new[] { "F15-A00", "F15-A04", "F15-A19", "F15-A21", "F15-A22", "F15-A29", "F15-A33", "F15-A36", "F15-A45", "F13-A45", "F14-A45", "F15-A49", "F15-A63", "F15-A80" });

        [Test,Explicit("Exports the complete repaired matrix; preserves the original gallery as before-fix evidence.")]
        public void ExportRepairedProductionRuleset() => CatacombsWallRulesetReview.Export(
            System.IO.Path.GetFullPath("../LocalHandoffs/catacombs-wall-joins-repair-20261030/gallery"));

        [Test,Explicit("Shows the mixed vertical junction away from artificial free ends.")]
        public void ExportVerticalTransitionContext() => CatacombsWallRulesetReview.Export(
            System.IO.Path.GetFullPath("../LocalHandoffs/catacombs-wall-joins-repair-20261030/vertical-v2/context"),
            new[] { "F15-A45", "F15-A63" }, 3);

        [Test,Explicit("Renders all full-profile mixtures next to low walls, with long arms away from artificial terminals.")]
        public void ExportMixedProfileContext()
        {
            var output = System.IO.Path.GetFullPath("../LocalHandoffs/catacombs-mixed-profiles-repair-20261031/current");
            CatacombsWallRulesetReview.Export(output,
            new[] { "F14-A17", "F13-A17", "F06-A17", "F09-A17", "F14-A65", "F13-A69", "F06-A65", "F09-A69",
                "F14-A47", "F13-A51", "F06-A47", "F09-A51", "F14-A35", "F13-A35", "F06-A44", "F09-A44",
                "F14-A14", "F13-A16", "F15-A45", "F15-A63", "F15-A22", "F15-A80", "F06-A71", "F09-A71" }, 4, true);
            foreach (var mode in new[] { "native", "lit" })
                foreach (var id in new[] { "F14-A17", "F13-A17", "F06-A17", "F09-A17", "F14-A65", "F13-A69", "F06-A65", "F09-A69" })
                {
                    var texture = new UnityEngine.Texture2D(2, 2);
                    try
                    {
                        UnityEngine.ImageConversion.LoadImage(texture, System.IO.File.ReadAllBytes(System.IO.Path.Combine(output, mode, id + ".png")));
                        for (var x = 188; x < 196; x++)
                        {
                            var pixel = texture.GetPixel(x, 183);
                            Assert.That(pixel.r + pixel.g + pixel.b, Is.GreaterThan(.001f), id + "/" + mode + ": black slit at column " + x);
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(texture); }
                }
        }

        [Test,Explicit("Compares low-wall turn faces and descending roofs with their unobstructed straight references.")]
        public void ExportLowCornerFaceContext()
        {
            var output = System.IO.Path.GetFullPath("../LocalHandoffs/catacombs-low-corner-face-20261031/current");
            CatacombsWallRulesetReview.Export(output,
                new[] { "F15-A01", "F15-A03", "F15-A04", "F15-A09", "F15-A10", "F15-A12", "F15-A13", "F15-A28", "F15-A30", "F15-A31", "F15-A37", "F15-A39", "F15-A40",
                    "F14-A30", "F13-A28", "F15-A57", "F15-A11", "F14-A17", "F14-A65", "F14-A47" }, 4, true);
            foreach (var mode in new[] { "native", "lit" })
            {
                foreach (var id in new[] { "F15-A10", "F15-A12", "F15-A13", "F15-A37", "F15-A39", "F15-A40" })
                    AssertPatch(id, "F15-A09", mode, "The descending roof must not be covered by the horizontal wall face.");
                AssertPatch("F15-A30", "F15-A03", mode, "The eastward face must continue under the whole L corner.");
                AssertPatch("F15-A28", "F15-A01", mode, "The westward face must continue under the whole L corner.");
                AssertPatch("F15-A12", "F15-A03", mode, "Clipping the east face must preserve its stone UV phase.", 200, 216);
                AssertPatch("F15-A10", "F15-A01", mode, "Clipping the west face must preserve its stone UV phase.", 168, 184);
                AssertPatch("F15-A13", "F15-A04", mode, "The other east face at a T must retain its UV phase.", 200, 216);
            }

            void AssertPatch(string actualId, string expectedId, string mode, string reason, int minimumX = 188, int maximumX = 196)
            {
                var actual = new UnityEngine.Texture2D(2, 2);
                var expected = new UnityEngine.Texture2D(2, 2);
                try
                {
                    UnityEngine.ImageConversion.LoadImage(actual, System.IO.File.ReadAllBytes(System.IO.Path.Combine(output, mode, actualId + ".png")));
                    UnityEngine.ImageConversion.LoadImage(expected, System.IO.File.ReadAllBytes(System.IO.Path.Combine(output, mode, expectedId + ".png")));
                    for (var x = minimumX; x < maximumX; x++)
                        for (var y = 176; y < 192; y++)
                            Assert.That(actual.GetPixel(x, y), Is.EqualTo(expected.GetPixel(x, y)), actualId + "/" + mode + ": " + reason + " Pixel " + x + "," + y);
                }
                finally { UnityEngine.Object.DestroyImmediate(actual); UnityEngine.Object.DestroyImmediate(expected); }
            }
        }

        [Test,Explicit("Compares production black masks against unobstructed floors in larger room fragments.")]
        public void ExportOcclusionRoomContext() => CatacombsWallRulesetReview.Export(
            System.IO.Path.GetFullPath("../LocalHandoffs/catacombs-mask-context-20261030"),
            new[] { "F04-A02", "F06-A56", "F07-A06", "F14-A14", "F15-A80" }, 4, true);

        [Test,Explicit("Shows mirrored South-profile/low-wall roof corners with production lighting.")]
        public void ExportSouthLowCornerContext()
        {
            var output = System.IO.Path.GetFullPath("../LocalHandoffs/catacombs-south-low-corner-20261030/current");
            CatacombsWallRulesetReview.Export(output,
                new[] { "F13-A16", "F14-A14", "F06-A56", "F07-A06", "F15-A22", "F15-A63", "F15-A80" }, 4, true);
            foreach (var id in new[] { "F13-A16", "F14-A14" })
                foreach (var mode in new[] { "native", "lit" })
                {
                    var texture = new UnityEngine.Texture2D(2, 2);
                    try
                    {
                        UnityEngine.ImageConversion.LoadImage(texture, System.IO.File.ReadAllBytes(System.IO.Path.Combine(output, mode, id + ".png")));
                        // Both halves of the roof used to disappear behind
                        // the South mask / neighbouring low horizontal face.
                        foreach (var x in new[] { 190, 193 })
                        {
                            var roof = texture.GetPixel(x, 183);
                            Assert.That(roof.r + roof.g + roof.b, Is.GreaterThan(.001f), id + "/" + mode + ": roof corner is swallowed by a mask or face.");
                        }
                        var maskedFloorX = id == "F14-A14" ? 100 : 280;
                        Assert.That(texture.GetPixel(maskedFloorX, 183), Is.EqualTo(texture.GetPixel(0, 0)),
                            id + "/" + mode + ": intentional floor mask must match the dungeon background.");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(texture); }
                }
        }
    }
}
