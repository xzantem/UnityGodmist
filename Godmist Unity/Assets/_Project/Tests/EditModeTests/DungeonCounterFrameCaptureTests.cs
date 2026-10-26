using System;
using NUnit.Framework;
using UnityEngine.LowLevel;

namespace Godmist.Tests.EditMode
{
    public class DungeonCounterFrameCaptureTests
    {
        private struct ExistingSystem { }
        private struct AddedByAnotherTool { }

        [Test]
        public void RemovingCapturePreservesExistingAndLaterAddedPlayerLoopNodes()
        {
            var loop = new PlayerLoopSystem { type = typeof(ExistingSystem), subSystemList = new[] {
                new PlayerLoopSystem { type = typeof(UnityEngine.PlayerLoop.Update), subSystemList = new[] {
                    new PlayerLoopSystem { type = typeof(ExistingSystem) } } },
                new PlayerLoopSystem { type = typeof(UnityEngine.PlayerLoop.PreLateUpdate), subSystemList = new[] {
                    new PlayerLoopSystem { type = typeof(ExistingSystem) } } } } };
            var original = loop;
            Assert.That(DungeonCounterFrameCapture.InsertCallbacks(ref loop), Is.True);
            Assert.That(original.subSystemList[0].subSystemList.Length, Is.EqualTo(1), "Insertion must not mutate borrowed arrays.");
            var update = loop.subSystemList[0];
            var children = new PlayerLoopSystem[update.subSystemList.Length + 1];
            Array.Copy(update.subSystemList, children, update.subSystemList.Length);
            children[children.Length - 1] = new PlayerLoopSystem { type = typeof(AddedByAnotherTool) };
            update.subSystemList = children; loop.subSystemList[0] = update;
            DungeonCounterFrameCapture.RemoveCallbacks(ref loop);
            Assert.That(loop.subSystemList[0].subSystemList.Length, Is.EqualTo(2));
            Assert.That(loop.subSystemList[0].subSystemList[0].type, Is.EqualTo(typeof(ExistingSystem)));
            Assert.That(loop.subSystemList[0].subSystemList[1].type, Is.EqualTo(typeof(AddedByAnotherTool)));
            Assert.That(loop.subSystemList[1].subSystemList.Length, Is.EqualTo(1));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void AlignmentFindsKnownLagIncludingZeroSampleFrames(int lag)
        {
            var rows = new DungeonCounterFrameCapture.FrameRow[70];
            var counts = new int[rows.Length * 3];
            for (var i = 0; i < rows.Length; i++)
            {
                rows[i].observedFrame = 100 + i;
                counts[i * 3] = (100 + i - lag) % 7;
            }
            Assert.That(DungeonCounterFrameCapture.InferLag(rows, counts, 3, rows.Length, out var matches), Is.EqualTo(lag));
            Assert.That(matches, Is.EqualTo(rows.Length));
        }

        [Test]
        public void MissingOrStaleCounterCannotPassAlignment()
        {
            var rows = new DungeonCounterFrameCapture.FrameRow[70];
            var counts = new int[70];
            for (var i = 0; i < rows.Length; i++) rows[i].observedFrame = 100 + i;
            DungeonCounterFrameCapture.InferLag(rows, counts, 1, rows.Length, out var matches);
            Assert.That(matches, Is.LessThan(rows.Length * .99d));
        }
    }
}
