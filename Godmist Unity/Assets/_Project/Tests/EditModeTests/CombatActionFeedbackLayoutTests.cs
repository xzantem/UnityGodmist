using System.Reflection;
using Godmist.Runtime.Combat;
using Godmist.Views.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class CombatActionFeedbackLayoutTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void ModifierBundleHasOneNameButDifferentTargetsCastsAndDamageStaySeparate()
        {
            var root = new GameObject("feedback batch", typeof(RectTransform));
            try
            {
                var view = root.AddComponent<CombatFloatingFeedbackController>();
                var accept = typeof(CombatFloatingFeedbackController).GetMethod("TryAcceptNamedNotification", Flags);
                bool Accept(CombatFeedbackEvent e, int frame = 1) => (bool)accept.Invoke(view, new object[] { e, frame });
                var target = new BattleUnit(); var other = new BattleUnit();
                Assert.That(Accept(new CombatFeedbackEvent(target, CombatFeedbackType.BuffApplied, effectName: "Zapach Krwi")), Is.True);
                Assert.That(Accept(new CombatFeedbackEvent(target, CombatFeedbackType.BuffApplied, effectName: "Zapach Krwi")), Is.False);
                Assert.That(Accept(new CombatFeedbackEvent(target, CombatFeedbackType.DebuffApplied, effectName: "Zapach Krwi")), Is.False);
                Assert.That(Accept(new CombatFeedbackEvent(target, CombatFeedbackType.StatusApplied, effectName: "Zapach Krwi")), Is.False);
                Assert.That(Accept(new CombatFeedbackEvent(other, CombatFeedbackType.BuffApplied, effectName: "Zapach Krwi")), Is.True);
                Assert.That(Accept(new CombatFeedbackEvent(target, CombatFeedbackType.BuffApplied, effectName: "Tarcza")), Is.True);
                for (var i = 0; i < 3; i++)
                    Assert.That(Accept(new CombatFeedbackEvent(target, CombatFeedbackType.Damage, 12)), Is.True);
                Accept(new CombatFeedbackEvent(target, CombatFeedbackType.SkillUsed));
                Assert.That(Accept(new CombatFeedbackEvent(target, CombatFeedbackType.BuffApplied, effectName: "Zapach Krwi")), Is.True);
                Assert.That(Accept(new CombatFeedbackEvent(target, CombatFeedbackType.BuffApplied, effectName: "Zapach Krwi"), 2), Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void AuthoredBannerMovesBelowTimelineAndReservesViewportWithoutEditingPrefab()
        {
            var root = new GameObject("hud test", typeof(RectTransform), typeof(Canvas));
            var cameraRoot = new GameObject("camera", typeof(Camera));
            try
            {
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var prefab = AssetDatabase.LoadAssetAtPath<CombatHudView>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatHud.prefab");
                var prefabState = new SerializedObject(prefab);
                var authoredBanner = ((GameObject)prefabState.FindProperty("skillCastBanner").objectReferenceValue).GetComponent<RectTransform>();
                var authoredPosition = authoredBanner.anchoredPosition;
                var hud = Object.Instantiate(prefab, root.transform);
                Canvas.ForceUpdateCanvases();
                var state = new SerializedObject(hud);
                var banner = ((GameObject)state.FindProperty("skillCastBanner").objectReferenceValue).GetComponent<RectTransform>();
                var timeline = (RectTransform)state.FindProperty("turnOrderContainer").objectReferenceValue;
                var area = (Rect)typeof(CombatHudView).GetMethod("ActionSafeViewport", Flags).Invoke(hud, new object[] { cameraRoot.GetComponent<Camera>() });
                var corners = new Vector3[4]; banner.GetWorldCorners(corners); var bannerTop = corners[1].y;
                timeline.GetWorldCorners(corners);
                Assert.That(bannerTop, Is.LessThan(corners[0].y));
                Assert.That(area.yMax, Is.LessThan(1));
                Assert.That(area.height, Is.GreaterThan(0));
                Assert.That(authoredBanner.anchoredPosition, Is.EqualTo(authoredPosition));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(cameraRoot); }
        }
    }
}
