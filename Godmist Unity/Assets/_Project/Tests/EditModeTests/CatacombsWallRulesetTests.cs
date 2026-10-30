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
    }
}
