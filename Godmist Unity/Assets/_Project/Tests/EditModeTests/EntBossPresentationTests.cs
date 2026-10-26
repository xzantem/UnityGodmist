using NUnit.Framework;
using UnityEngine;
using Godmist.Views.Dungeons;
using Godmist.Views.UI;
namespace Godmist.Tests
{
    public class EntBossPresentationTests
    {
        [Test]
        public void BossHudHasNoSeparateHealthPanel()
        {
            Assert.That(typeof(CombatHudView).GetField("bossStatusRoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), Is.Null);
        }
        [TestCase(0, -1)] [TestCase(0, 1)] [TestCase(-1, 0)] [TestCase(1, 0)]
        public void ArenaBoundaryTreesLeaveRoomForTallBoss(int x, int y)
        {
            var tree = new Bounds(new Vector3(x * 2, y * 2, 0), new Vector3(3, 4, 1));
            var cells = new[]{Vector2.zero};
            var offset = DungeonFloorMapView.ResolveEntArenaTreeClearance(tree, cells, Vector2.zero, 1);
            tree.center += (Vector3)offset;
            Assert.That(tree.Intersects(new Bounds(Vector3.up * 1.5f, new Vector3(4,4,2))), Is.False);
        }
        [Test]
        public void ConnectedPathHasNoGapBetweenNeighbouringCellCentres()
        {
            var a = new Vector2(.45f,.55f); var b = new Vector2(1.55f,.45f);
            for(var i=0;i<=20;i++)
                Assert.That(DungeonFloorMapView.ForestPathSegmentDistance(Vector2.Lerp(a,b,i/20f),a,b), Is.LessThan(.0001f));
            Assert.That(DungeonFloorMapView.ForestPathSegmentDistance(new Vector2(.5f,2),a,b), Is.GreaterThan(1));
        }
        [Test]
        public void DistantTreesStayInPlace()
        {
            Assert.That(DungeonFloorMapView.ResolveEntArenaTreeClearance(new Bounds(new Vector3(20,20,0), Vector3.one), new[]{Vector2.zero}, Vector2.zero,1), Is.EqualTo(Vector2.zero));
        }
    }
}
