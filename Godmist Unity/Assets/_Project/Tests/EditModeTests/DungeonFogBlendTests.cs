using System;
using System.Collections.Generic;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Dungeons;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DungeonFogBlendTests
    {
        [Test]
        public void CachedGridWeightsMatchOriginalContinuousOpacityForMixedKnowledge()
        {
            var neighbours = new DungeonKnowledge[8];
            var samples = new float[81];
            var random = new System.Random(410);
            for (var pattern = 0; pattern < 128; pattern++)
            {
                for (var i = 0; i < 8; i++) neighbours[i] = (DungeonKnowledge)random.Next(3);
                for (var center = 0; center < 3; center++)
                {
                    var knowledge = (DungeonKnowledge)center;
                    DungeonFogBlend.SampleGridBrightness(knowledge, neighbours, samples);
                    for (var y = 0; y <= 8; y++) for (var x = 0; x <= 8; x++)
                        Assert.That(samples[y * 9 + x], Is.EqualTo(DungeonFogBlend.Brightness(knowledge)
                            * (1 - DungeonFogBlend.Opacity(knowledge, neighbours, new Vector2(x / 8f, y / 8f))))
                            .Within(.000001f));
                }
            }
        }
        private static DungeonKnowledge[] Neighbours(DungeonKnowledge state)
        { var values = new DungeonKnowledge[8]; Array.Fill(values, state); return values; }

        [TestCase(DungeonKnowledge.Visible, DungeonKnowledge.Remembered)]
        [TestCase(DungeonKnowledge.Visible, DungeonKnowledge.Unexplored)]
        [TestCase(DungeonKnowledge.Remembered, DungeonKnowledge.Unexplored)]
        public void BrightAndDarkSidesMeetContinuouslyAtSharedEdge(DungeonKnowledge bright, DungeonKnowledge dark)
        {
            var left = Neighbours(bright); left[4] = dark;
            var right = Neighbours(dark); right[3] = bright;
            var a = DungeonFogBlend.Brightness(bright) * (1-DungeonFogBlend.Opacity(bright,left,new Vector2(1,.5f)));
            var b = DungeonFogBlend.Brightness(dark) * (1-DungeonFogBlend.Opacity(dark,right,new Vector2(0,.5f)));
            Assert.That(a, Is.EqualTo(b).Within(.0001f));
            var edge = DungeonFogBlend.Opacity(bright,left,new Vector2(.99f,.5f));
            var middle = DungeonFogBlend.Opacity(bright,left,new Vector2(.6f,.5f));
            var inside = DungeonFogBlend.Opacity(bright,left,new Vector2(0,.5f));
            Assert.That(edge, Is.GreaterThan(middle)); Assert.That(middle, Is.GreaterThan(inside));
        }

        [Test]
        public void DiagonalFrontierRoundsWithoutBrighteningUnexploredTerrain()
        {
            var neighbours = Neighbours(DungeonKnowledge.Visible); neighbours[7] = DungeonKnowledge.Unexplored;
            var a = DungeonFogBlend.Opacity(DungeonKnowledge.Visible,neighbours,new Vector2(.75f,1));
            var b = DungeonFogBlend.Opacity(DungeonKnowledge.Visible,neighbours,new Vector2(1,.75f));
            Assert.That(a, Is.EqualTo(b).Within(.0001f)); Assert.That(a, Is.InRange(.01f,.99f));
            Assert.That(DungeonFogBlend.Opacity(DungeonKnowledge.Visible,neighbours,Vector2.one), Is.EqualTo(1));
            Assert.That(DungeonFogBlend.Opacity(DungeonKnowledge.Unexplored,Neighbours(DungeonKnowledge.Visible),Vector2.one*.5f), Is.EqualTo(1));
        }

        [Test]
        public void RememberedTerrainNeverBrightensAndUniformAreasHaveNoExtraWash()
        {
            Assert.That(DungeonFogBlend.Opacity(DungeonKnowledge.Remembered,Neighbours(DungeonKnowledge.Visible),Vector2.one*.5f), Is.Zero);
            foreach(var state in new[]{DungeonKnowledge.Visible,DungeonKnowledge.Remembered})
                for(var y=0;y<=8;y++) for(var x=0;x<=8;x++)
                    Assert.That(DungeonFogBlend.Opacity(state,Neighbours(state),new Vector2(x/8f,y/8f)), Is.Zero);
        }

        [Test]
        public void AdjacentBoundaryTilesHaveNoInternalSeam()
        {
            var neighbours=Neighbours(DungeonKnowledge.Visible); neighbours[5]=neighbours[6]=neighbours[7]=DungeonKnowledge.Unexplored;
            for(var y=0;y<=16;y++)
                Assert.That(DungeonFogBlend.Opacity(DungeonKnowledge.Visible,neighbours,new Vector2(1,y/16f)),
                    Is.EqualTo(DungeonFogBlend.Opacity(DungeonKnowledge.Visible,neighbours,new Vector2(0,y/16f))).Within(.0001f));
        }

        [Test]
        public void ThreeKnowledgeStatesMeetWithoutTileSeams()
        {
            var cells = new Dictionary<Vector2Int, DungeonKnowledge>();
            foreach (var top in new[] { DungeonKnowledge.Visible, DungeonKnowledge.Remembered, DungeonKnowledge.Unexplored })
            foreach (var bottom in new[] { DungeonKnowledge.Visible, DungeonKnowledge.Remembered, DungeonKnowledge.Unexplored })
            {
                for (var y = -1; y <= 1; y++)
                for (var x = -1; x <= 2; x++) cells[new Vector2Int(x, y)] = DungeonKnowledge.Visible;
                cells[new Vector2Int(1, 0)] = DungeonKnowledge.Remembered;
                cells[new Vector2Int(1, 1)] = top;
                cells[new Vector2Int(0, -1)] = bottom;
                var a = Around(Vector2Int.zero);
                var b = Around(Vector2Int.right);
                for (var sample = 0; sample <= 32; sample++)
                {
                    var y = sample / 32f;
                    var left = 1f - DungeonFogBlend.Opacity(DungeonKnowledge.Visible, a, new Vector2(1, y));
                    var right = .55f * (1f - DungeonFogBlend.Opacity(DungeonKnowledge.Remembered, b, new Vector2(0, y)));
                    Assert.That(left, Is.EqualTo(right).Within(.0001f), $"top={top}, bottom={bottom}, y={y}");
                }
            }

            DungeonKnowledge[] Around(Vector2Int cell)
            {
                var values = new DungeonKnowledge[8]; var index = 0;
                for (var y = -1; y <= 1; y++)
                for (var x = -1; x <= 1; x++)
                    if (x != 0 || y != 0) values[index++] = cells[cell + new Vector2Int(x, y)];
                return values;
            }
        }

        [Test]
        public void UnknownFrontierDoesNotSpreadNearOpaqueFogIntoKnownTerrain()
        {
            var neighbours = Neighbours(DungeonKnowledge.Visible); neighbours[4] = DungeonKnowledge.Unexplored;
            Assert.That(DungeonFogBlend.Opacity(DungeonKnowledge.Visible, neighbours, new Vector2(.8f, .5f)), Is.LessThan(.55f));
            Assert.That(DungeonFogBlend.Opacity(DungeonKnowledge.Visible, neighbours, new Vector2(1, .5f)), Is.EqualTo(1));
        }
    }
}
