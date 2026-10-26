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
        public void ForestSeamlessOverviewFramesVisibleBodiesAndRestoresManualView()
        {
            var bodies = new Bounds(new Vector3(20, -15), new Vector3(6, 5));
            var safe = new Rect(.12f, .22f, .88f, .56f);
            controller.ActionSafeViewport = _ => safe;
            var battle = new CombatSession { IsSeamless = true, ActiveUnit = new BattleUnit() };
            controller.Focus(battle, new Bounds(Vector3.zero, new Vector3(45, 40)), floor,
                _ => bodies.center, () => bodies);
            controller.TickTransition(10);
            Assert.That(camera.orthographicSize, Is.LessThanOrEqualTo(6));
            Assert.That(camera.WorldToViewportPoint(bodies.center).x, Is.EqualTo(safe.center.x).Within(.001));
            Assert.That(camera.WorldToViewportPoint(bodies.center).y, Is.EqualTo(safe.center.y).Within(.001));
            controller.Zoom(-1); controller.Pan(Vector2.right); controller.TickTransition(10);
            var manual = camera.transform.position; var zoom = camera.orthographicSize;
            controller.Focus(new CombatSession(), arena, floor, _ => unitPosition);
            controller.Focus(battle, arena, floor, _ => bodies.center, () => bodies); controller.TickTransition(10);
            Assert.That(camera.orthographicSize, Is.EqualTo(zoom).Within(.001));
            Assert.That(Vector3.Distance(camera.transform.position, manual), Is.LessThan(.001));
        }

        [Test]
        public void BossOverviewCentersParticipantsNearFloorEdgeAndHomeRefreshesTheirPositions()
        {
            var bodies = new Bounds(new Vector3(-42, 0), new Vector3(3, 8));
            var narrowFloor = new Bounds(Vector3.zero, new Vector3(100, 12));
            var safe = new Rect(.12f, .22f, .88f, .53f);
            controller.ActionSafeViewport = _ => safe;
            session = new CombatSession { ActiveUnit = session.ActiveUnit };
            controller.Focus(session, arena, narrowFloor, _ => unitPosition, () => bodies);
            controller.TickTransition(10);
            var center = camera.WorldToViewportPoint(bodies.center);
            Assert.That(center.x, Is.EqualTo(safe.center.x).Within(.001));
            Assert.That(center.y, Is.EqualTo(safe.center.y).Within(.001));
            Assert.That(camera.WorldToViewportPoint(bodies.min).y, Is.GreaterThan(safe.yMin));
            Assert.That(camera.WorldToViewportPoint(bodies.max).y, Is.LessThan(safe.yMax));
            var before = camera.transform.position;
            controller.BeginActionFocus(session, session.ActiveUnit, () => new Bounds(Vector3.zero, Vector3.one));
            controller.EndActionFocus(session); controller.TickTransition(10);
            Assert.That(camera.transform.position, Is.EqualTo(before));
            bodies.center += Vector3.right * 10;
            controller.ShowArena(); controller.TickTransition(10);
            Assert.That(camera.WorldToViewportPoint(bodies.center).x, Is.EqualTo(safe.center.x).Within(.001));
        }

        [TestCase(.65f)]
        [TestCase(1.6f)]
        [TestCase(2.5f)]
        public void ActionFrameFitsCasterTargetAndFeedbackBelowBannerAndRestoresUserView(float aspect)
        {
            camera.aspect = aspect;
            var beforePosition = camera.transform.position;
            var beforeZoom = camera.orthographicSize;
            var safe = new Rect(0, .22f, 1, .43f);
            controller.ActionSafeViewport = _ => safe;
            // Includes both silhouettes and the floating-number space, close to
            // the floor boundary where manual pan constraints would clip them.
            var bounds = new Bounds(new Vector3(94, 45), new Vector3(48, 9, 0));
            controller.BeginActionFocus(session, session.ActiveUnit, () => bounds);
            controller.TickTransition(10);
            var min = camera.WorldToViewportPoint(bounds.min);
            var max = camera.WorldToViewportPoint(bounds.max);
            Assert.That(min.x, Is.GreaterThanOrEqualTo(safe.xMin - .001f));
            Assert.That(min.y, Is.GreaterThanOrEqualTo(safe.yMin - .001f));
            Assert.That(max.x, Is.LessThanOrEqualTo(safe.xMax + .001f));
            Assert.That(max.y, Is.LessThanOrEqualTo(safe.yMax + .001f));
            Assert.That(camera.orthographicSize, Is.GreaterThan(beforeZoom));
            bounds.center += new Vector3(-12, -8);
            controller.TickTransition(10);
            Assert.That(camera.WorldToViewportPoint(bounds.max).y, Is.LessThanOrEqualTo(safe.yMax + .001f));
            controller.EndActionFocus(session); controller.TickTransition(10);
            Assert.That(camera.orthographicSize, Is.EqualTo(beforeZoom).Within(.001f));
            Assert.That(Vector3.Distance(camera.transform.position, beforePosition), Is.LessThan(.001f));
        }

        [TestCase(.65f)][TestCase(1.6f)][TestCase(2.5f)]
        public void CompactSelfCastUsesCloserFrameWithSmallBottomMarginAndRestoresUserZoom(float aspect)
        {
            camera.aspect = aspect;
            controller.Zoom(-4); controller.TickTransition(10);
            var beforeZoom = camera.orthographicSize;
            var beforePosition = camera.transform.position;
            var safe = new Rect(0, .22f, 1, .43f);
            controller.ActionSafeViewport = _ => safe;
            var body = new Bounds(new Vector3(0, 1), new Vector3(1.5f, 2.25f));
            controller.BeginActionFocus(session, session.ActiveUnit, () => body); controller.TickTransition(10);
            Assert.That(camera.orthographicSize, Is.LessThan(beforeZoom));
            Assert.That(camera.orthographicSize, Is.LessThanOrEqualTo(3.2f));
            Assert.That(camera.WorldToViewportPoint(body.min).y, Is.GreaterThan(safe.yMin));
            Assert.That(camera.WorldToViewportPoint(body.max).y, Is.LessThan(safe.yMax));
            // At least half the safe vertical area contains the silhouette.
            var height = camera.WorldToViewportPoint(body.max).y - camera.WorldToViewportPoint(body.min).y;
            Assert.That(height, Is.GreaterThan(safe.height * .5f));
            controller.EndActionFocus(session); controller.TickTransition(10);
            Assert.That(camera.orthographicSize, Is.EqualTo(beforeZoom).Within(.001));
            Assert.That(Vector3.Distance(camera.transform.position, beforePosition), Is.LessThan(.001));
        }

        [Test]
        public void InvisibleActionCannotReplaceCurrentBoundsProvider()
        {
            var bounds = new Bounds(Vector3.zero, new Vector3(2, 3));
            controller.BeginActionFocus(session, session.ActiveUnit, () => bounds); controller.TickTransition(10);
            var before = camera.transform.position;
            controller.CanPresentUnit = _ => false;
            controller.BeginActionFocus(session, session.ActiveUnit, () => new Bounds(new Vector3(80, 40), Vector3.one));
            controller.TickTransition(10);
            Assert.That(camera.transform.position, Is.EqualTo(before));
        }

        [Test]
        public void InvisibleEnemyMovementNeverReadsItsHiddenPosition()
        {
            var enemy=new BattleUnit {IsPlayerSide=false,Character=new Godmist.Runtime.Characters.CharacterInstance()};
            var effect=new Godmist.Runtime.Effects.PassiveEffectRuntime {
                EffectId=Godmist.Core.Constants.StatusEffectIds.Invisibility,
                Lifetime=Godmist.Core.Enums.PassiveEffectLifetime.Timed,RemainingDuration=2};
            enemy.Character.PassiveEffects.Add(effect);
            var reads=0;var position=new Vector3(40,0);
            Vector3 Resolve() {reads++;return position;}
            controller.TickTransition(10);var before=camera.transform.position;
            unitPosition=position;
            controller.FocusUnit(enemy);controller.BeginActionFocus(session,enemy);controller.TickTransition(10);
            Assert.That(camera.transform.position,Is.EqualTo(before),"Inspection/action framing must also reject invisibility without a dungeon callback.");
            controller.BeginMovementFocus(session,Resolve,enemy);controller.TickTransition(10);
            Assert.That(reads,Is.Zero);Assert.That(camera.transform.position,Is.EqualTo(before));
            enemy.Character.PassiveEffects.Clear();
            controller.BeginMovementFocus(session,Resolve,enemy);controller.TickTransition(10);
            Assert.That(reads,Is.GreaterThan(0));var visible=camera.transform.position;var previousReads=reads;
            enemy.Character.PassiveEffects.Add(effect);position=new Vector3(-40,0);
            controller.TickTransition(10);controller.EndMovementFocus(session);controller.TickTransition(10);
            Assert.That(reads,Is.EqualTo(previousReads));Assert.That(camera.transform.position,Is.EqualTo(visible));
        }

        [Test]
        public void ActionFocusRestoresPreviousCameraAndPreservesZoom()
        {
            controller.Pan(new Vector2(20, 0)); controller.Zoom(1); controller.TickTransition(10);
            var before = camera.transform.position; var zoom = camera.orthographicSize;
            unitPosition = new Vector3(-40, 0);
            controller.BeginActionFocus(session, session.ActiveUnit); controller.TickTransition(10);
            Assert.That(camera.transform.position.x, Is.EqualTo(-40).Within(.01f));
            Assert.That(camera.orthographicSize, Is.EqualTo(zoom));
            unitPosition = new Vector3(-35, 0); controller.TickTransition(10);
            Assert.That(camera.transform.position.x, Is.EqualTo(-35).Within(.01f), "charge/teleport follows the presented caster");
            controller.EndActionFocus(new CombatSession()); controller.TickTransition(10);
            Assert.That(camera.transform.position.x, Is.EqualTo(-35).Within(.01f));
            controller.EndActionFocus(session); controller.TickTransition(10);
            Assert.That(Vector3.Distance(camera.transform.position, before), Is.LessThan(.01f));
        }

        [Test]
        public void SwitchingDuringActionDoesNotRememberTemporaryActionCamera()
        {
            controller.Pan(new Vector2(25,0)); var before = camera.transform.position;
            unitPosition = new Vector3(-40,0); controller.BeginActionFocus(session, session.ActiveUnit); controller.TickTransition(10);
            controller.Focus(new CombatSession(), arena, floor, _ => Vector3.zero); controller.TickTransition(10);
            controller.Focus(session, arena, floor, _ => unitPosition); controller.TickTransition(10);
            Assert.That(Vector3.Distance(camera.transform.position, before), Is.LessThan(.01f));
        }

        [Test]
        public void HiddenEnemyMovementTracksPositionAndKeepsZoomEvenWhenActionCleanupRuns()
        {
            var zoom = camera.orthographicSize;
            controller.CanPresentUnit = _ => false;
            var position = new Vector3(60, 20);
            controller.BeginMovementFocus(session, () => position);
            controller.EndActionFocus(session); controller.TickTransition(10);
            Assert.That(camera.transform.position.x, Is.EqualTo(60).Within(.01));
            position = new Vector3(-20, -10);
            controller.TickTransition(10);
            Assert.That(camera.transform.position.x, Is.EqualTo(-20).Within(.01));
            Assert.That(camera.orthographicSize, Is.EqualTo(zoom).Within(.01));
            controller.EndMovementFocus(new CombatSession());
            position.x = 35; controller.TickTransition(10);
            Assert.That(camera.transform.position.x, Is.EqualTo(35).Within(.01));
            controller.EndMovementFocus(session);
            position.x = 80; controller.TickTransition(10);
            Assert.That(camera.transform.position.x, Is.EqualTo(35).Within(.01));
        }

        [Test]
        public void OtherSessionMovementCannotStealFocusAndSessionSwitchClearsFollow()
        {
            var before = camera.transform.position;
            controller.BeginMovementFocus(new CombatSession(), () => new Vector3(80, 20));
            controller.TickTransition(10);
            Assert.That(camera.transform.position, Is.EqualTo(before));
            var position = new Vector3(60, 20);
            controller.BeginMovementFocus(session, () => position); controller.TickTransition(10);
            controller.Focus(new CombatSession(), arena, floor, _ => Vector3.zero); controller.TickTransition(10);
            before = camera.transform.position;
            position.x = -60; controller.TickTransition(10);
            Assert.That(camera.transform.position, Is.EqualTo(before));
        }

        [Test]
        public void InvisibleOrOtherSessionActionsCannotRevealTheirPosition()
        {
            var before = camera.transform.position; unitPosition = new Vector3(60,0);
            controller.BeginActionFocus(new CombatSession(), session.ActiveUnit); controller.TickTransition(10);
            Assert.That(camera.transform.position, Is.EqualTo(before));
            controller.CanPresentUnit = _ => false;
            controller.BeginActionFocus(session, session.ActiveUnit); controller.TickTransition(10);
            Assert.That(camera.transform.position, Is.EqualTo(before));
        }

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
            controller.TickTransition(10);
            Assert.That(camera.orthographicSize, Is.EqualTo(2.5f));
            controller.Zoom(-100);
            controller.TickTransition(10);
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
