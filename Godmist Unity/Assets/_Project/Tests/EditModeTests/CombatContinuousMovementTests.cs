using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Types;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Characters;
using Godmist.Views.Characters;
using Godmist.Views.Combat;
using Godmist.Views.Exploration;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class CombatContinuousMovementTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject root;
        private CombatUnitViewSpawner spawner;
        private BattleUnit unit;
        private Transform actor;
        private CharacterView standalone;
        private void Create(bool borrowed)
        {
            root = new GameObject("continuous combat movement");
            root.AddComponent<CombatSandboxBootstrap>();
            spawner = root.AddComponent<CombatUnitViewSpawner>();
            actor = new GameObject("actor").transform;
            actor.SetParent(root.transform);
            unit = new BattleUnit { Position = new GridPosition(3, 0), Character = new CharacterInstance() };
            if (borrowed)
            {
                var view = actor.gameObject.AddComponent<ExplorationCharacterView>();
                view.BindCombat(unit);
                Field<Dictionary<BattleUnit, ExplorationCharacterView>>("borrowed").Add(unit, view);
            }
            else
            {
                standalone = actor.gameObject.AddComponent<CharacterView>();
                Field<Dictionary<BattleUnit, CharacterView>>("views").Add(unit, standalone);
            }
        }
        private T Field<T>(string name) => (T)typeof(CombatUnitViewSpawner).GetField(name, Private).GetValue(spawner);
        private void Tick(float seconds) => typeof(CombatUnitViewSpawner).GetMethod("AdvancePresentationPath", Private)
            .Invoke(spawner, new object[] { unit, seconds });
        private static GridPosition[] Straight() => new[] { new GridPosition(1, 0), new GridPosition(2, 0), new GridPosition(3, 0) };
        [TearDown] public void Cleanup() { if (root != null) Object.DestroyImmediate(root); }

        [TestCase(false)] [TestCase(true)]
        public void OneFrameCarriesRemainingDistanceAcrossWaypoints(bool borrowed)
        {
            Create(borrowed);
            Assert.That(spawner.TryBeginPresentationPath(unit, Straight()), Is.True);
            Tick(.25f); // 1.5 cells: halfway through the second segment.
            Assert.That(actor.position.x, Is.EqualTo(1.5f).Within(.00001));
            Assert.That(spawner.IsPresentationMovementInProgress(unit), Is.True);
            Tick(.25f);
            Assert.That(actor.position.x, Is.EqualTo(3).Within(.00001));
            Assert.That(spawner.IsPresentationMovementInProgress(unit), Is.False);
            Assert.That(unit.Position, Is.EqualTo(new GridPosition(3, 0)), "Presentation never mutates logical movement.");
        }

        [TestCase(false, 30)] [TestCase(false, 60)] [TestCase(false, 144)]
        [TestCase(true, 30)] [TestCase(true, 60)] [TestCase(true, 144)]
        public void DistanceMatchesSpeedOnEveryFrameIncludingCorners(bool borrowed, int fps)
        {
            Create(borrowed);
            var path = new[] { new GridPosition(0, 0), new GridPosition(1, 0), new GridPosition(1, 1), new GridPosition(1, 2) };
            unit.Position = path[3];
            spawner.TryBeginPresentationPath(unit, path);
            var total = 0f;
            for (var frame = 0; frame < fps / 2; frame++)
            {
                total = Mathf.Min(3f, total + 6f / fps);
                Tick(1f / fps);
                var expected = total <= 1 ? new Vector3(total, 0) : new Vector3(1, total - 1);
                Assert.That(Vector3.Distance(actor.position, expected), Is.LessThan(.00002), $"Frame {frame} at {fps} fps");
            }
            Assert.That(spawner.IsPresentationMovementInProgress(unit), Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void TinyRemainderDoesNotDelayOrStallFinalArrival(bool borrowed)
        {
            Create(borrowed);
            actor.position = new Vector3(.995f, 0);
            unit.Position = new GridPosition(1, 0);
            spawner.TryBeginPresentationPath(unit, new[] { unit.Position });
            Tick(.001f);
            Assert.That(actor.position.x, Is.EqualTo(1).Within(.000001));
            Assert.That(spawner.IsPresentationMovementInProgress(unit), Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void ZeroDeltaTimeDoesNotMoveAndLargeDeltaFinishesWithoutOvershoot(bool borrowed)
        {
            Create(borrowed);
            spawner.TryBeginPresentationPath(unit, Straight());
            Tick(0);
            Assert.That(actor.position, Is.EqualTo(Vector3.zero));
            Tick(10);
            Assert.That(actor.position, Is.EqualTo(new Vector3(3, 0)));
            Assert.That(spawner.IsPresentationMovementInProgress(unit), Is.False);
        }

        [Test]
        public void StandaloneUsesAuthoredSpeedAndDoesNotAlsoAdvanceInUpdate()
        {
            Create(false);
            typeof(CharacterView).GetField("movementSpeed", Private).SetValue(standalone, 3f);
            spawner.TryBeginPresentationPath(unit, Straight());
            Tick(.5f);
            Assert.That(actor.position.x, Is.EqualTo(1.5f).Within(.00001));
            typeof(CharacterView).GetMethod("UpdateMovement", Private).Invoke(standalone, null);
            Assert.That(actor.position.x, Is.EqualTo(1.5f).Within(.00001));
            spawner.EndPresentationMovement(unit);
            Assert.That(Field<System.Collections.IDictionary>("movementPaths").Count, Is.Zero);
            Assert.That((bool)typeof(CharacterView).GetField("pathPresentationControlled", Private).GetValue(standalone), Is.False);
        }

        [Test]
        public void IntermediateWaypointKeepsRunAnimationUntilFinalArrival()
        {
            Create(false);
            var body = actor.gameObject.AddComponent<SpriteRenderer>();
            var idle = new List<Sprite> { null };
            var run = new List<Sprite> { null };
            typeof(CharacterView).GetField("bodyRenderer", Private).SetValue(standalone, body);
            typeof(CharacterView).GetField("idleAnimationFrames", Private).SetValue(standalone, idle);
            typeof(CharacterView).GetField("runAnimationFrames", Private).SetValue(standalone, run);
            spawner.TryBeginPresentationPath(unit, Straight());
            Tick(1f / 6);
            Assert.That(typeof(CharacterView).GetField("activeAnimationFrames", Private).GetValue(standalone), Is.SameAs(run));
            Tick(1f / 3);
            Assert.That(typeof(CharacterView).GetField("activeAnimationFrames", Private).GetValue(standalone), Is.SameAs(idle));
        }

        [TestCase(false)] [TestCase(true)]
        public void EndingOrReleasingAPathClearsItsCursor(bool borrowed)
        {
            Create(borrowed);
            spawner.TryBeginPresentationPath(unit, Straight());
            Tick(.1f);
            if (borrowed) spawner.Release(unit);
            else spawner.EndPresentationMovement(unit);
            Assert.That(Field<System.Collections.IDictionary>("movementPaths").Count, Is.Zero);
            Assert.That(Field<System.Collections.IDictionary>("movementTargets").Count, Is.Zero);
            Assert.That(spawner.IsPresentationMovementInProgress(unit), Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void HiddenPathsAdvanceWithoutRevealingOrMovingTheirView(bool borrowed)
        {
            Create(borrowed);
            actor.gameObject.SetActive(false);
            spawner.TryBeginPresentationPath(unit, Straight());
            Tick(0);
            Assert.That(spawner.GetPresentationMovementPosition(unit).x, Is.EqualTo(1));
            for (var i = 0; i < 3; i++) Tick(1f / 60);
            Assert.That(spawner.IsPresentationMovementInProgress(unit), Is.False);
            Assert.That(actor.position, Is.EqualTo(Vector3.zero));
            Assert.That(actor.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void RemovingAUnitDuringPresentationClearsItsPathAndTarget()
        {
            Create(false);
            spawner.TryBeginPresentationPath(unit, Straight());
            Tick(.1f);
            typeof(CombatUnitViewSpawner).GetMethod("RemoveMissingViews", Private)
                .Invoke(spawner, new object[] { new CombatSession() });
            Assert.That(Field<System.Collections.IDictionary>("movementPaths").Count, Is.Zero);
            Assert.That(Field<System.Collections.IDictionary>("movementTargets").Count, Is.Zero);
            Assert.That(spawner.IsPresentationMovementInProgress(unit), Is.False);
        }
    }
}
