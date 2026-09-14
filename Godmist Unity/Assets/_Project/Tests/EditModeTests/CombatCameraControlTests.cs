using Godmist.Runtime.Combat;
using Godmist.Views.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class CombatCameraControlTests
    {
        private GameObject root;
        private Camera camera;
        private CombatCameraFramer controller;
        private readonly Bounds floor = new(Vector3.zero, new Vector3(200, 100, 0));
        private readonly Bounds arena = new(Vector3.zero, new Vector3(20, 10, 0));
        private CombatSession session;
        private Vector3 unitPosition;

        [SetUp]
        public void Setup()
        {
            root = new GameObject("camera");
            camera = root.AddComponent<Camera>();
            camera.orthographic = true;
            camera.aspect = 1.6f;
            root.transform.position = new Vector3(0, 0, -10);
            controller = root.AddComponent<CombatCameraFramer>();
            // EditMode does not invoke Awake reliably for a plain MonoBehaviour.
            typeof(CombatCameraFramer).GetField("worldCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(controller, camera);
            session = new CombatSession { ActiveUnit = new BattleUnit() };
            controller.Focus(session, arena, floor, _ => unitPosition);
            controller.TickTransition(10f);
        }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(root);

        [Test]
        public void PanCanLeaveArenaButNotFloor()
        {
            controller.Pan(new Vector2(45, 0));
            Assert.That(camera.transform.position.x, Is.GreaterThan(arena.max.x));
            controller.Pan(new Vector2(1000, 1000));
            Assert.That(camera.transform.position.x, Is.LessThan(floor.max.x));
            Assert.That(camera.transform.position.y, Is.LessThan(floor.max.y));
        }
        [Test]
        public void SwitchingSessionsRestoresEachViewAndForgettingRemovesIt()
        {
            controller.Pan(new Vector2(30, 0));
            controller.Zoom(2);
            var position = camera.transform.position;
            var zoom = camera.orthographicSize;
            var other = new CombatSession();
            controller.Focus(other, new Bounds(new Vector3(-40, 0), arena.size), floor, _ => Vector3.zero);
            controller.TickTransition(10);
            controller.Focus(session, arena, floor, _ => unitPosition);
            controller.TickTransition(10);
            Assert.That(Vector3.Distance(position, camera.transform.position), Is.LessThan(0.01f));
            Assert.That(camera.orthographicSize, Is.EqualTo(zoom).Within(0.01f));
            controller.Forget(other);
            controller.Forget(session);
            Assert.That(controller.RememberedSessionCount, Is.Zero);
            Assert.That(controller.FocusedSession, Is.Null);
        }
        [Test]
        public void OtherCombatTurnsCannotMoveCamera()
        {
            var before = camera.transform.position;
            unitPosition = new Vector3(60, 0);
            controller.NotifyTurnBegan(new CombatSession(), session.ActiveUnit);
            controller.TickTransition(10);
            Assert.That(camera.transform.position, Is.EqualTo(before));
        }
        [Test]
        public void OffscreenTurnFocusPreservesZoomButMovementDoesNotFollow()
        {
            var zoom = camera.orthographicSize;
            unitPosition = new Vector3(60, 0);
            controller.NotifyTurnBegan(session, session.ActiveUnit);
            controller.TickTransition(10);
            Assert.That(camera.transform.position.x, Is.EqualTo(60).Within(0.01f));
            Assert.That(camera.orthographicSize, Is.EqualTo(zoom));
            unitPosition = Vector3.zero;
            controller.TickTransition(10);
            Assert.That(camera.transform.position.x, Is.EqualTo(60).Within(0.01f));
        }
        [Test]
        public void VisibleTurnDoesNotRecenter()
        {
            var before = camera.transform.position;
            unitPosition = new Vector3(1, 1);
            controller.NotifyTurnBegan(session, session.ActiveUnit);
            controller.TickTransition(10);
            Assert.That(camera.transform.position, Is.EqualTo(before));
        }
        [Test]
        public void ManualPanInterruptsFocusTransition()
        {
            unitPosition = new Vector3(60, 0);
            controller.FocusActiveUnit();
            controller.Pan(Vector2.right);
            var before = camera.transform.position;
            controller.TickTransition(10);
            Assert.That(camera.transform.position, Is.EqualTo(before));
        }
        [Test]
        public void SuspendedCameraDoesNotWriteExplorationView()
        {
            controller.Focus(null, default, default, null);
            camera.orthographicSize = 7;
            camera.transform.position = new Vector3(3, 4, -10);
            controller.Pan(Vector2.one);
            controller.Zoom(3);
            controller.TickTransition(10);
            Assert.That(camera.orthographicSize, Is.EqualTo(7));
            Assert.That(camera.transform.position, Is.EqualTo(new Vector3(3, 4, -10)));
        }
        [TestCase(0.6f)]
        [TestCase(2.4f)]
        public void ZoomLimitsAreAbsoluteRegardlessOfFloorSize(float floorScale)
        {
            controller.Focus(session, arena, new Bounds(Vector3.zero, floor.size * floorScale), _ => unitPosition);
            controller.Zoom(100);
            Assert.That(camera.orthographicSize, Is.EqualTo(2.5f));
            controller.Zoom(-100);
            Assert.That(camera.orthographicSize, Is.EqualTo(10f));
        }
        [Test]
        public void HomeAndFrameCannotZoomOutBeyondFiftyPercent()
        {
            var huge = new Bounds(Vector3.zero, new Vector3(1000, 1000, 0));
            controller.Focus(session, huge, huge, _ => unitPosition);
            controller.ShowArena();
            controller.TickTransition(10);
            Assert.That(camera.orthographicSize, Is.EqualTo(10f));
            controller.Frame(huge);
            Assert.That(camera.orthographicSize, Is.EqualTo(10f));
        }
        [TestCase(0.6f)]
        [TestCase(2.4f)]
        public void FitKeepsSmallArenaInsideHudSafeViewport(float aspect)
        {
            camera.aspect = aspect;
            var smallArena = new Bounds(Vector3.zero, new Vector3(6, 4, 0));
            controller.Focus(session, smallArena, floor, _ => unitPosition);
            controller.ShowArena();
            controller.TickTransition(10);
            var min = camera.WorldToViewportPoint(smallArena.min);
            var max = camera.WorldToViewportPoint(smallArena.max);
            Assert.That(min.x, Is.GreaterThanOrEqualTo(0));
            Assert.That(max.x, Is.LessThanOrEqualTo(1));
            Assert.That(min.y, Is.GreaterThanOrEqualTo(0.22f));
            Assert.That(max.y, Is.LessThanOrEqualTo(0.88f));
        }
    }
}
