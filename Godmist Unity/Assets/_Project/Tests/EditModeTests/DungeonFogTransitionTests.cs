using Godmist.Views.Dungeons;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace Godmist.Tests.EditModeTests
{
    public class DungeonFogTransitionTests
    {
        [Test]
        public void FogPresentationWaitsForTwoHundredMillisecondsAndHidingRemainsImmediate()
        {
            var root = new GameObject("Fog cadence test");
            var spriteRoot = new GameObject("Fog sprite");
            try
            {
                var map = root.AddComponent<DungeonFloorMapView>();
                var sprite = spriteRoot.AddComponent<SpriteRenderer>();
                typeof(DungeonFloorMapView).GetField("snapFogAnimation", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(map, false);
                var tick = typeof(DungeonFloorMapView).GetMethod("AdvanceFogAnimation", BindingFlags.NonPublic | BindingFlags.Instance);
                map.PresentFogSprite(sprite, Color.white, true);
                tick.Invoke(map, new object[] { .05f });
                tick.Invoke(map, new object[] { .05f });
                Assert.That(sprite.color.a, Is.Zero, "Render frames between fog ticks must not upload fog colors.");
                tick.Invoke(map, new object[] { .1f });
                Assert.That(sprite.color.a, Is.EqualTo(Mathf.SmoothStep(0, 1, .2f / DungeonFogTransition.Duration)).Within(.00001f));
                tick.Invoke(map, new object[] { .2f });
                Assert.That(sprite.color.a, Is.EqualTo(1), "Use elapsed time, not a single render-frame delta.");
                map.PresentFogSprite(sprite, Color.white, false);
                Assert.That(sprite.forceRenderingOff, Is.True, "Hiding must bypass presentation cadence.");
            }
            finally { Object.DestroyImmediate(spriteRoot); Object.DestroyImmediate(root); }
        }

        [Test]
        public void ResetFogDiscardsOldPresentationTimeAndPausedFramesDoNotReveal()
        {
            var root = new GameObject("Fog cadence reset test");
            var spriteRoot = new GameObject("Fog sprite");
            try
            {
                var map = root.AddComponent<DungeonFloorMapView>();
                var sprite = spriteRoot.AddComponent<SpriteRenderer>();
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                var tick = typeof(DungeonFloorMapView).GetMethod("AdvanceFogAnimation", flags);
                tick.Invoke(map, new object[] { .3f });
                typeof(DungeonFloorMapView).GetMethod("ResetFogAnimations", flags).Invoke(map, null);
                typeof(DungeonFloorMapView).GetField("snapFogAnimation", flags).SetValue(map, false);
                map.PresentFogSprite(sprite, Color.white, true);
                tick.Invoke(map, new object[] { .1f });
                tick.Invoke(map, new object[] { 0f });
                tick.Invoke(map, new object[] { -.1f });
                Assert.That(sprite.color.a, Is.Zero);
                tick.Invoke(map, new object[] { .3f });
                Assert.That(sprite.color.a, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(spriteRoot); Object.DestroyImmediate(root); }
        }

        [Test]
        public void RevealStartsOpaqueAndFinishesAtTarget()
        {
            var transition = new DungeonFogTransition(1, new[] { 0f, 0f });
            transition.Retarget(1, new[] { 1f, .55f });
            Assert.That(transition[0], Is.Zero);
            transition.Advance(.15f);
            Assert.That(transition[0], Is.EqualTo(.5f).Within(.00001f));
            Assert.That(transition[1], Is.EqualTo(.275f).Within(.00001f));
            transition.Advance(.15f);
            Assert.That(transition[0], Is.EqualTo(1));
            Assert.That(transition.Active, Is.False);
        }

        [Test]
        public void RepeatedTargetDoesNotRestartReveal()
        {
            var transition = new DungeonFogTransition(1, new[] { 0f });
            transition.Retarget(1, new[] { 1f });
            transition.Advance(.2f);
            transition.Retarget(1, new[] { 1f });
            transition.Advance(.1f);
            Assert.That(transition.Active, Is.False);
            Assert.That(transition[0], Is.EqualTo(1));
        }

        [Test]
        public void ReversalPreservesCurrentBrightnessAndBaseline()
        {
            var transition = new DungeonFogTransition(1, new[] { .8f });
            transition.Retarget(.55f, new[] { .4f });
            transition.Advance(.1f);
            var brightness = transition[0]; var baseline = transition.Baseline;
            transition.Retarget(1, new[] { .8f });
            Assert.That(transition[0], Is.EqualTo(brightness));
            Assert.That(transition.Baseline, Is.EqualTo(baseline));
            transition.Advance(.3f);
            Assert.That(transition[0], Is.EqualTo(.8f));
            Assert.That(transition.Baseline, Is.EqualTo(1));
        }

        [Test]
        public void PauseDoesNotAdvanceAndSnapCancelsTransition()
        {
            var transition = new DungeonFogTransition(1, new[] { 0f });
            transition.Retarget(1, new[] { 1f });
            transition.Advance(0); transition.Advance(-1);
            Assert.That(transition[0], Is.Zero);
            transition.Retarget(.55f, new[] { .4f }, true);
            Assert.That(transition[0], Is.EqualTo(.4f));
            Assert.That(transition.Baseline, Is.EqualTo(.55f));
            Assert.That(transition.Active, Is.False);
        }

        [Test]
        public void AnimatedWashAndTileTintPreserveSharedEdgeAcrossKnowledgeStates()
        {
            var visible = new DungeonFogTransition(1, new[] { .55f });
            var remembered = new DungeonFogTransition(.55f, new[] { .55f });
            visible.Retarget(.55f, new[] { .4f });
            remembered.Retarget(1, new[] { .4f });
            for (var i = 0; i < 30; i++)
            {
                visible.Advance(.01f); remembered.Advance(.01f);
                Assert.That(visible[0], Is.EqualTo(remembered[0]).Within(.00001f));
                Assert.That(visible[0] / visible.Baseline, Is.InRange(0f, 1f));
                Assert.That(remembered[0] / remembered.Baseline, Is.InRange(0f, 1f));
            }
        }
    }
}
