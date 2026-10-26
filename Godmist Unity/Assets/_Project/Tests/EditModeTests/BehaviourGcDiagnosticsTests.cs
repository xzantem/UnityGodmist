using System.Collections.Generic;
using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public class BehaviourGcDiagnosticsTests
    {
        [Test]
        public void AllocationOwnersFollowNestedCallbacksAndPopBeforeSiblingSamples()
        {
            var scopes = new List<DungeonLiveFrameCapture.AllocationScope> {
                new("PlayerLoop", 20), new("BehaviourUpdate", 15), new("A.Update() [Invoke]", 10), new("Godmist.A.Query", 8) };
            var context = DungeonLiveFrameCapture.AllocationContext(scopes);
            Assert.That(context.Gameplay, Is.True); Assert.That(context.Callback, Is.EqualTo("A.Update() [Invoke]"));
            Assert.That(context.Operation, Is.EqualTo("Godmist.A.Query"));
            DungeonLiveFrameCapture.CloseAllocationScopes(scopes, 8);
            Assert.That(DungeonLiveFrameCapture.AllocationContext(scopes).Operation, Is.EqualTo("A.Update() [Invoke]"));
            DungeonLiveFrameCapture.CloseAllocationScopes(scopes, 10);
            scopes.Add(new("B.Update() [Invoke]", 14));
            Assert.That(DungeonLiveFrameCapture.AllocationContext(scopes).Callback, Is.EqualTo("B.Update() [Invoke]"));
            scopes.Add(new("Inner.Update() [Invoke]", 13));
            Assert.That(DungeonLiveFrameCapture.AllocationContext(scopes).Callback, Is.EqualTo("Inner.Update() [Invoke]"));
            DungeonLiveFrameCapture.CloseAllocationScopes(scopes, 14);
            Assert.That(DungeonLiveFrameCapture.AllocationContext(scopes).Callback, Is.EqualTo("(no callback marker)"));
            DungeonLiveFrameCapture.CloseAllocationScopes(scopes, 20);
            Assert.That(DungeonLiveFrameCapture.AllocationContext(scopes).Gameplay, Is.False);
            Assert.That(scopes.Count, Is.Zero);
        }

        [Test]
        public void EditorAllocationsAndBareAllocationsAreNotClassifiedAsGameplay()
        {
            var scopes = new List<DungeonLiveFrameCapture.AllocationScope> { new("EditorLoop", 9), new("Editor.Update() [Invoke]", 6) };
            Assert.That(DungeonLiveFrameCapture.AllocationContext(scopes).Gameplay, Is.False);
            DungeonLiveFrameCapture.CloseAllocationScopes(scopes, 9);
            var bare = DungeonLiveFrameCapture.AllocationContext(scopes);
            Assert.That(bare.Gameplay, Is.False); Assert.That(bare.Operation, Is.EqualTo("(no parent marker)"));
        }
    }
}
