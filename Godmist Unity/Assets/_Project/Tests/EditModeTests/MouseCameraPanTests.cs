using System.Reflection;
using Godmist.Views.Exploration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Godmist.Tests.EditMode
{
    public sealed class MouseCameraPanTests : InputTestFixture
    {
        private GameObject root;
        private Camera camera;
        private ExplorationCameraFollow follow;
        private Mouse mouse;
        private static void Call(object owner, string method, params object[] args) => owner.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            root = new GameObject("mouse camera pan");
            camera = root.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 5;
            camera.pixelRect = new Rect(0, 0, 1000, 600);
            root.transform.position = new Vector3(0, 0, -10);
            follow = root.AddComponent<ExplorationCameraFollow>();
            Call(follow, "Awake");
            follow.CenterOn(Vector3.zero);
            mouse = InputSystem.AddDevice<Mouse>("Camera pan test mouse");
        }

        [TearDown]
        public override void TearDown()
        {
            try { Object.DestroyImmediate(root); }
            finally { base.TearDown(); }
        }

        private void Send(float x, float y = 300, bool held = true, MouseButton button = MouseButton.Middle)
        {
            var state = new MouseState { position = new Vector2(x,y) };
            if (held) state = state.WithButton(button);
            InputSystem.QueueStateEvent(mouse, state);
            InputSystem.Update();
            Call(follow, "HandleMousePanning");
        }

        [TestCase(2.5f)] [TestCase(5f)] [TestCase(10f)]
        public void DragTracksScreenDistanceWithoutOscillationOrStationaryDrift(float zoom)
        {
            camera.orthographicSize = zoom;
            Send(500); Send(600);
            var first = root.transform.position;
            Assert.That(first.x, Is.EqualTo(-100 * 2f * zoom / 600).Within(.001));
            for (var frame=0;frame<8;frame++) Send(600);
            Assert.That(Vector3.Distance(root.transform.position, first), Is.LessThan(.0001));
            Send(700);
            Assert.That(root.transform.position.x, Is.EqualTo(first.x * 2).Within(.001));
            Assert.That(root.transform.position.z, Is.EqualTo(-10));
        }

        [Test]
        public void FollowModeAndLeftClicksDoNotPanAndHeldPressCannotCarryAcrossUnlock()
        {
            follow.CenterOn(Vector3.zero, true);
            Send(500); Send(650);
            Assert.That(root.transform.position.x, Is.Zero);
            Assert.That(follow.IsFollowingLeader, Is.True);
            follow.CenterOn(Vector3.zero);
            Send(700);
            Assert.That(root.transform.position.x, Is.Zero);
            Send(700, held:false);
            Send(500, button:MouseButton.Left); Send(650, button:MouseButton.Left);
            Assert.That(root.transform.position.x, Is.Zero);
        }

        [Test]
        public void ReleaseFocusLossAndDisableRequireANewPress()
        {
            Send(500); Send(600);
            var before = root.transform.position;
            Call(follow, "OnApplicationFocus", false);
            Send(700);
            Assert.That(root.transform.position, Is.EqualTo(before));
            Send(700, held:false); Send(700); Send(750);
            Assert.That(root.transform.position.x, Is.LessThan(before.x));
            before = root.transform.position;
            follow.enabled = false; follow.enabled = true;
            // Plain MonoBehaviour lifecycle callbacks are not dispatched in EditMode.
            Call(follow, "OnDisable");
            Send(800);
            Assert.That(root.transform.position, Is.EqualTo(before));
            Send(800, held:false); Send(900, held:false);
            Assert.That(root.transform.position, Is.EqualTo(before));
        }

        [Test]
        public void BoundsClampWithoutStoredOvershootAndReverseImmediately()
        {
            // Bounds belong to a stationary owner, as in authored scenes.
            var boundsRoot = new GameObject("bounds");
            try
            {
                var bounds = boundsRoot.AddComponent<BoxCollider2D>(); bounds.size = new Vector2(40, 32);
                follow.SetCameraBounds(bounds);
                Send(500); Send(5000);
                var edge = root.transform.position;
                Assert.That(edge.x, Is.EqualTo(-20 + camera.orthographicSize * camera.aspect).Within(.001));
                Send(5000);
                Assert.That(root.transform.position, Is.EqualTo(edge));
                Send(4990);
                Assert.That(root.transform.position.x, Is.GreaterThan(edge.x));
            }
            finally { Object.DestroyImmediate(boundsRoot); }
        }

        [Test]
        public void RightDragUnlocksFollowOnlyAfterThresholdAndAClickDoesNotUnlock()
        {
            follow.CenterOn(Vector3.zero,true);
            Send(500,button:MouseButton.Right); Send(503,button:MouseButton.Right);
            Assert.That(follow.IsFollowingLeader,Is.True);
            Assert.That(root.transform.position.x,Is.Zero);
            Send(503,held:false);
            Assert.That(follow.IsFollowingLeader,Is.True);
            Send(500,button:MouseButton.Right); Send(600,button:MouseButton.Right);
            Assert.That(follow.IsFollowingLeader,Is.False);
            Assert.That(root.transform.position.x,Is.LessThan(-1));
            var before=root.transform.position;
            Send(600,button:MouseButton.Right);
            Assert.That(root.transform.position,Is.EqualTo(before));
        }

        [Test]
        public void ExistingRightButtonDragUsesTheSameStableDelta()
        {
            Send(500, button:MouseButton.Right); Send(650, button:MouseButton.Right);
            var before = root.transform.position;
            Assert.That(before.x, Is.LessThan(0));
            Send(650, button:MouseButton.Right);
            Assert.That(root.transform.position, Is.EqualTo(before));
        }
    }
}
