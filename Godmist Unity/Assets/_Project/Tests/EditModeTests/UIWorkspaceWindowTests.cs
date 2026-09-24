using System.Reflection;
using Godmist.Core.Types;
using Godmist.Runtime.Characters;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class UIWorkspaceWindowTests
    {
        private GameObject workspaceObject;
        private GameObject firstWindowObject;
        private GameObject secondWindowObject;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(secondWindowObject);
            Object.DestroyImmediate(firstWindowObject);
            Object.DestroyImmediate(workspaceObject);
        }

        [TestCase(0f, 0f)]
        [TestCase(0f, .5f)]
        [TestCase(1f, 1f)]
        public void Move_NonCenteredAnchorsReachBothWorkspaceEdges(float anchor, float pivot)
        {
            var manager = CreateWorkspace();
            var window = CreateWindow("Inspect", manager, out var rect);
            rect.anchorMin = rect.anchorMax = new Vector2(anchor, .5f);
            rect.pivot = new Vector2(pivot, .5f);
            var corners = new Vector3[4];
            window.Move(new Vector2(5000f, 0));
            rect.GetWorldCorners(corners);
            Assert.That(manager.transform.InverseTransformPoint(corners[2]).x, Is.EqualTo(500f).Within(.01f));
            window.Move(new Vector2(-5000f, 0));
            rect.GetWorldCorners(corners);
            Assert.That(manager.transform.InverseTransformPoint(corners[0]).x, Is.EqualTo(-500f).Within(.01f));
        }

        [Test]
        public void Move_ClampsWindowInsideWorkspace()
        {
            var manager = CreateWorkspace();
            var window = CreateWindow("FirstWindow", manager, out var rectTransform);

            window.Move(new Vector2(1000f, 1000f));

            Assert.That(rectTransform.anchoredPosition.x, Is.EqualTo(300f).Within(0.01f));
            Assert.That(rectTransform.anchoredPosition.y, Is.EqualTo(200f).Within(0.01f));
        }

        [Test]
        public void ResizeFromBottomRight_RespectsWorkspaceBounds()
        {
            var manager = CreateWorkspace();
            var window = CreateWindow("FirstWindow", manager, out var rectTransform);

            window.ResizeFromBottomRight(new Vector2(1000f, -1000f));

            Assert.That(rectTransform.sizeDelta.x, Is.EqualTo(1000f).Within(0.01f));
            Assert.That(rectTransform.sizeDelta.y, Is.EqualTo(700f).Within(0.01f));
            Assert.That(rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ResizeFromBottomRight_MovingHandleUpReducesHeightAndKeepsTopEdge()
        {
            var manager = CreateWorkspace();
            var window = CreateWindow("FirstWindow", manager, out var rectTransform);
            var topBeforeResize = rectTransform.anchoredPosition.y + rectTransform.rect.height * 0.5f;

            window.ResizeFromBottomRight(new Vector2(0f, 100f));

            Assert.That(rectTransform.sizeDelta.y, Is.EqualTo(240f).Within(0.01f));
            Assert.That(
                rectTransform.anchoredPosition.y + rectTransform.rect.height * 0.5f,
                Is.EqualTo(topBeforeResize).Within(0.01f));
        }

        [Test]
        public void Focus_MovesWindowToFrontAndTracksFocusedWindow()
        {
            var manager = CreateWorkspace();
            var first = CreateWindow("FirstWindow", manager, out _);
            var second = CreateWindow("SecondWindow", manager, out _);

            first.Focus();

            Assert.That(manager.FocusedWindow, Is.SameAs(first));
            Assert.That(first.transform.GetSiblingIndex(), Is.EqualTo(first.transform.parent.childCount - 1));
            Assert.That(second.transform.GetSiblingIndex(), Is.LessThan(first.transform.GetSiblingIndex()));
        }

        [Test]
        public void CombatUnitInspect_FormatsCombatStatsInDisplayUnits()
        {
            var character = new CharacterInstance
            {
                Stats = new CharacterStats
                {
                    MinAttack = new Stat(46.2f),
                    MaxAttack = new Stat(114.9f),
                    PhysicalDefense = new Stat(12.6f),
                    MagicalDefense = new Stat(9.4f),
                    PhysicalDefensePenetration = new Stat(0.125f),
                    MagicalDefensePenetration = new Stat(0.256f),
                    Dodge = new Stat(17.8f),
                    CritChance = new Stat(0.078f),
                    CritDamage = new Stat(2.596f),
                    Speed = new Stat(55f),
                    ResourceRegeneration = new Stat(0),
                    WeaponAccuracy = new Stat(100),
                    MaxResource = new Stat(0),
                    MaxHealth = new Stat(100),
                    HealingPower = new Stat(0)
                }
            };
            var method = typeof(CombatUnitInspectWindowView).GetMethod(
                "BuildStatsColumns",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
            var arguments = new object[] { character, null, null };
            method.Invoke(null, arguments);

            var left = (string)arguments[1];
            Assert.That(left, Does.Contain("Atak: 46–115"));
            Assert.That(left, Does.Contain("Obrona fizyczna: 13"));
            Assert.That(left, Does.Contain("Obrona magiczna: 9"));
            Assert.That(left, Does.Contain("Unik: 18"));
            Assert.That(left, Does.Contain("Obrażenia krytyczne: 2,60x").Or.Contain("Obrażenia krytyczne: 2.60x"));
            Assert.That(left, Does.Not.Contain("259.60%"));
            Assert.That(left, Does.Not.Contain("259,60%"));
        }

        [Test]
        public void Tooltip_DisablingCombatHudRestoresExplorationTooltip()
        {
            var explorationObject = new GameObject("Exploration Tooltip");
            var combatHud = new GameObject("Combat HUD");
            combatHud.SetActive(false);
            var combatObject = new GameObject("Combat Tooltip");
            combatObject.transform.SetParent(combatHud.transform, false);
            try
            {
                var exploration = explorationObject.AddComponent<UIWorkspaceTooltipView>();
                var combat = combatObject.AddComponent<UIWorkspaceTooltipView>();
                var onEnable = typeof(UIWorkspaceTooltipView).GetMethod(
                    "OnEnable", BindingFlags.Instance | BindingFlags.NonPublic);
                var onDisable = typeof(UIWorkspaceTooltipView).GetMethod(
                    "OnDisable", BindingFlags.Instance | BindingFlags.NonPublic);
                onEnable.Invoke(exploration, null);
                Assert.That(UIWorkspaceTooltipView.ActiveInstance, Is.SameAs(exploration));

                combatHud.SetActive(true);
                onEnable.Invoke(combat, null);
                Assert.That(UIWorkspaceTooltipView.ActiveInstance, Is.SameAs(combat));

                combatHud.SetActive(false);
                onDisable.Invoke(combat, null);
                Assert.That(UIWorkspaceTooltipView.ActiveInstance, Is.SameAs(exploration));

                explorationObject.SetActive(false);
                onDisable.Invoke(exploration, null);
                Assert.That(UIWorkspaceTooltipView.ActiveInstance, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(combatHud);
                Object.DestroyImmediate(explorationObject);
            }
        }

        [Test]
        public void Tooltip_InvalidateSource_HidesTooltipImmediately()
        {
            var tooltipObject = new GameObject("Tooltip");
            var sourceObject = new GameObject("Tooltip Source");
            try
            {
                var tooltip = tooltipObject.AddComponent<UIWorkspaceTooltipView>();
                tooltip.ShowText("Title", "Body", sourceObject);

                var setContent = typeof(UIWorkspaceTooltipView).GetMethod(
                    "SetContent",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                setContent.Invoke(
                    tooltip,
                    new object[] { null, "Title", "Body", string.Empty, false });

                var visible = typeof(UIWorkspaceTooltipView).GetField(
                    "visible",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(visible, Is.Not.Null);
                Assert.That((bool)visible.GetValue(tooltip), Is.True);

                tooltip.InvalidateSource(sourceObject);

                Assert.That((bool)visible.GetValue(tooltip), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(sourceObject);
                Object.DestroyImmediate(tooltipObject);
            }
        }

        private UIWorkspaceWindowManager CreateWorkspace()
        {
            workspaceObject = new GameObject("Workspace", typeof(RectTransform));
            var rectTransform = workspaceObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(1000f, 700f);
            return workspaceObject.AddComponent<UIWorkspaceWindowManager>();
        }

        private UIWorkspaceWindow CreateWindow(
            string objectName,
            UIWorkspaceWindowManager manager,
            out RectTransform rectTransform)
        {
            var windowObject = new GameObject(objectName, typeof(RectTransform));
            windowObject.transform.SetParent(manager.transform, false);
            rectTransform = windowObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(400f, 300f);

            var window = windowObject.AddComponent<UIWorkspaceWindow>();
            if (firstWindowObject == null)
            {
                firstWindowObject = windowObject;
            }
            else
            {
                secondWindowObject = windowObject;
            }

            return window;
        }
    }
}
