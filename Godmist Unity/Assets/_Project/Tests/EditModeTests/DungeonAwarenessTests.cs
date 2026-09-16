using System;
using Godmist.Runtime.Dungeons;
using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public class DungeonAwarenessTests
    {
        [Test]
        public void ProductionPrefabHasWiredNonInteractiveWorldSpaceAwarenessBar()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(
                "Assets/_Project/Prefabs/Characters/PF_ExplorationCharacter.prefab");
            Assert.That(prefab, Is.Not.Null);
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var view = instance.GetComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                var serialized = new UnityEditor.SerializedObject(view);
                var root = serialized.FindProperty("awarenessRoot").objectReferenceValue as UnityEngine.GameObject;
                var fill = serialized.FindProperty("awarenessFill").objectReferenceValue as UnityEngine.UI.Image;
                Assert.That(root, Is.Not.Null);
                Assert.That(fill, Is.Not.Null);
                Assert.That(root.activeSelf, Is.False);
                Assert.That(root.transform.parent, Is.EqualTo(instance.transform));
                Assert.That(root.GetComponent<UnityEngine.Canvas>().renderMode, Is.EqualTo(UnityEngine.RenderMode.WorldSpace));
                Assert.That(root.GetComponentsInChildren<UnityEngine.UI.GraphicRaycaster>(true), Is.Empty);
                foreach (var graphic in root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                    Assert.That(graphic.raycastTarget, Is.False);
                Assert.That(fill.sprite, Is.Not.Null, "A Filled Image without a sprite cannot show progress.");
                Assert.That(fill.type, Is.EqualTo(UnityEngine.UI.Image.Type.Filled));
                Assert.That(fill.fillMethod, Is.EqualTo(UnityEngine.UI.Image.FillMethod.Horizontal));
                view.Bind(new Godmist.Runtime.Characters.CharacterInstance { LifeState = Godmist.Core.Enums.CharacterLifeState.Alive });
                view.ShowAwareness(.5f, true);
                Assert.That(root.activeSelf, Is.True);
                Assert.That(fill.fillAmount, Is.EqualTo(.5f));
                view.ShowAwareness(.5f, false);
                Assert.That(root.activeSelf, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        [Test]
        public void DetectsAfterTwoSecondsAndDecaysOverThree()
        {
            var state = new DungeonAwarenessState();
            Assert.That(state.Advance(1.9f, true, false), Is.False);
            Assert.That(state.Advance(.11f, true, false), Is.True);
            Assert.That(state.Advance(1.5f, false, false), Is.False);
            Assert.That(state.Progress, Is.EqualTo(.5f).Within(.00001f));
            state.Advance(1.5f, false, false);
            Assert.That(state.Progress, Is.Zero);
        }

        [Test]
        public void ReacquisitionContinuesRemainingProgress()
        {
            var state = new DungeonAwarenessState();
            state.Advance(1f, true, false);
            state.Advance(.6f, false, false);
            Assert.That(state.Progress, Is.EqualTo(.3f).Within(.00001f));
            Assert.That(state.Advance(1f, true, false), Is.False);
            Assert.That(state.Advance(.41f, true, false), Is.True);
        }

        [Test]
        public void FightingObserverBypassesDelayButStillNeedsSight()
        {
            var state = new DungeonAwarenessState();
            Assert.That(state.Advance(0f, true, true), Is.True);
            Assert.That(state.Advance(0f, false, true), Is.False);
            Assert.That(state.Progress, Is.Zero);
        }

        [Test]
        public void FailedStartCanRetryWhileVisibleButFullProgressDoesNotSeeThroughWalls()
        {
            var state = new DungeonAwarenessState();
            state.Advance(2f, true, false);
            Assert.That(state.Advance(0f, false, false), Is.False);
            Assert.That(state.Advance(0f, true, false), Is.True);
        }

        [Test]
        public void EnemyHistoryIsIndependentAndFloorInstanceRetainsIt()
        {
            var floor = new DungeonFloorInstance(0);
            floor.GetEnemyAwareness("a").Advance(1f, true, false);
            Assert.That(floor.GetEnemyAwareness("a").Progress, Is.EqualTo(.5f));
            Assert.That(floor.GetEnemyAwareness("b").Progress, Is.Zero);
            Assert.That(new DungeonFloorInstance(1).GetEnemyAwareness("a").Progress, Is.Zero);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidElapsedTimeCannotCorruptState(float elapsed)
        {
            var state = new DungeonAwarenessState();
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Advance(elapsed, true, false));
            Assert.That(state.Progress, Is.Zero);
        }

        [TestCase(-.1f)]
        [TestCase(1.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidSavedProgressIsRejected(float progress)
        {
            Assert.That(new DungeonAwarenessState().TryRestore(progress), Is.False);
        }

        [Test]
        public void BarHidesWithoutPartyVisibilityAtZeroAndAfterDeath()
        {
            var actor = new UnityEngine.GameObject("observer");
            var bar = new UnityEngine.GameObject("awareness", typeof(UnityEngine.RectTransform));
            try
            {
                var view = actor.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                var fill = bar.AddComponent<UnityEngine.UI.Image>();
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(Godmist.Views.Exploration.ExplorationCharacterView).GetField("awarenessRoot", flags).SetValue(view, bar);
                typeof(Godmist.Views.Exploration.ExplorationCharacterView).GetField("awarenessFill", flags).SetValue(view, fill);
                var character = new Godmist.Runtime.Characters.CharacterInstance { LifeState = Godmist.Core.Enums.CharacterLifeState.Alive };
                view.Bind(character);
                view.ShowAwareness(.5f, true);
                Assert.That(bar.activeSelf, Is.True);
                Assert.That(fill.fillAmount, Is.EqualTo(.5f));
                view.ShowAwareness(.5f, false);
                Assert.That(bar.activeSelf, Is.False);
                view.ShowAwareness(0f, true);
                Assert.That(bar.activeSelf, Is.False);
                character.LifeState = Godmist.Core.Enums.CharacterLifeState.Dead;
                view.ShowAwareness(1f, true);
                Assert.That(bar.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
                UnityEngine.Object.DestroyImmediate(bar);
            }
        }
    }
}
