using System;
using System.Linq;
using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public class FullSpikeDiagnosticsTests
    {
        [Test] public void ParentsFollowSubtreeEndsAndKeepOriginalIndicesAcrossOmittedAllocationLeaves()
        {
            var nodes = new[] { Node(0, 12), Node(1, 6), Node(2, 5), Node(4, 5), Node(5, 6), Node(6, 11), Node(9, 11), Node(11, 12), Node(12, 13) };
            var stack = new System.Collections.Generic.List<DungeonLiveFrameCapture.FullSpikeNode>();
            foreach (var node in nodes)
            {
                DungeonLiveFrameCapture.CloseFullSpikeParents(stack, node.index);
                node.parent = stack.Count > 0 ? stack[stack.Count - 1].index : -1;
                if (node.end > node.index + 1) stack.Add(node);
            }
            Assert.That(nodes.Select(n => n.parent), Is.EqualTo(new[] { -1, 0, 1, 2, 1, 0, 6, 0, -1 }));
        }
        [Test] public void SelectionKeepsEditorOnlyGapGameplayPeaksAndGcNeighbours()
        {
            var rows = Enumerable.Range(1, 12).Select(i => new DungeonLiveFrameCapture.FullSpikeRow {
                frame = i, frameMs = 10 + i, playerMs = 5, editorMs = 1 }).ToArray();
            rows[1].playerMs = 25; rows[2].playerMs = 26;
            rows[5].frameMs = 100; rows[5].playerMs = 0; rows[5].editorMs = 100;
            rows[7].gcMs = 9;
            var selected = DungeonLiveFrameCapture.SelectFullSpikeFrames(rows);
            Assert.That(selected, Does.Contain(6)); Assert.That(selected, Does.Contain(2)); Assert.That(selected, Does.Contain(3));
            Assert.That(selected, Does.Contain(7)); Assert.That(selected, Does.Contain(8)); Assert.That(selected, Does.Contain(9));
            Assert.That(selected, Is.Ordered.And.Unique); Assert.That(selected.Length, Is.LessThanOrEqualTo(9));
        }
        [Test] public void MissingGcDoesNotManufactureNeighbourFrames()
        {
            Assert.That(DungeonLiveFrameCapture.SelectFullSpikeFrames(Array.Empty<DungeonLiveFrameCapture.FullSpikeRow>()), Is.Empty);
            var one = new[] { new DungeonLiveFrameCapture.FullSpikeRow { frame = 10, frameMs = 12, playerMs = 10 } };
            Assert.That(DungeonLiveFrameCapture.SelectFullSpikeFrames(one), Is.EqualTo(new[] { 10 }));
        }
        [Test] public void SelectionKeepsCameraAndShadowPreparationSpikesEvenBelowOtherFramePeaks()
        {
            var rows = Enumerable.Range(1, 20).Select(i => new DungeonLiveFrameCapture.FullSpikeRow {
                frame = i, frameMs = 10 + i, playerMs = 5, editorMs = 1 }).ToArray();
            rows[1].cameraMs = 10; rows[2].cameraMs = 8;
            rows[4].shadowPrepareMs = 9; rows[5].shadowPrepareMs = 7;
            var selected = DungeonLiveFrameCapture.SelectFullSpikeFrames(rows);
            foreach (var frame in new[] { 2, 3, 5, 6 }) Assert.That(selected, Does.Contain(frame));
            Assert.That(selected, Is.Ordered.And.Unique);
            Assert.That(selected.Length, Is.LessThanOrEqualTo(13));
        }
        private static DungeonLiveFrameCapture.FullSpikeNode Node(int index, int end) => new() { index = index, end = end };
    }
}
