using System;
using System.Linq;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Tests.EditMode
{
    public sealed class ControllerRevisionTests
    {
        [TestCase(30)] [TestCase(60)] [TestCase(144)]
        public void ScrollRectTravelsSameDistancePerSecondAtEveryFrameRate(int fps)
        {
            var root = new GameObject("Scroll test", typeof(RectTransform), typeof(Canvas), typeof(ScrollRect));
            try
            {
                var view = new GameObject("Viewport",typeof(RectTransform)).GetComponent<RectTransform>(); view.SetParent(root.transform,false);
                view.sizeDelta = new Vector2(300,300);
                var content = new GameObject("Content",typeof(RectTransform)).GetComponent<RectTransform>(); content.SetParent(view,false);
                content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f,1); content.sizeDelta = new Vector2(300,2000);
                var scroll = root.GetComponent<ScrollRect>(); scroll.viewport = view; scroll.content = content;
                scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = 42; Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 1;
                var start = content.anchoredPosition;
                for (var i = 0; i < fps; i++) ControllerInputView.ScrollContent(scroll, -1, 1f/fps, 700);
                Assert.That(content.anchoredPosition.y-start.y, Is.EqualTo(700).Within(.2f));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [TestCase(1080)] [TestCase(1440)] [TestCase(2160)]
        public void CursorCrossesSameScreenFractionAtEveryResolution(int height)
        {
            var delta = ControllerInputView.CursorDelta(Vector2.right, 1, height);
            Assert.That(delta.x / height, Is.EqualTo(.65f).Within(.0001f));
            Assert.That(ControllerInputView.CursorDelta(Vector2.right*.3f,1,height).x, Is.LessThan(delta.x*.3f), "Fine stick movement must give precision, not full speed.");
        }
        [TestCase(0,1,0)] [TestCase(1,1,1)] [TestCase(1,0,2)] [TestCase(1,-1,3)]
        [TestCase(0,-1,4)] [TestCase(-1,-1,5)] [TestCase(-1,0,6)] [TestCase(-1,1,7)]
        public void RadialDirectionsMatchVisiblePositions(float x,float y,int index)
            => Assert.That(ControllerWheelView.DirectionIndex(new Vector2(x,y)),Is.EqualTo(index));
        [TestCase(4,1)] [TestCase(7,2)] [TestCase(2,1)]
        public void ShortRadialPagesDistributeTheirEntriesAroundTheWholeCircle(int count,int rightIndex)
            => Assert.That(ControllerWheelView.DirectionIndex(Vector2.right,count),Is.EqualTo(rightIndex));
        [Test]
        public void ReauthoringKeepsExplicitModalNavigation()
        {
            var root = new GameObject("Navigation test",typeof(RectTransform));
            try
            {
                var first = new GameObject("Keep",typeof(RectTransform),typeof(Button)).GetComponent<Button>(); first.transform.SetParent(root.transform,false);
                var second = new GameObject("Revert",typeof(RectTransform),typeof(Button)).GetComponent<Button>(); second.transform.SetParent(root.transform,false);
                first.navigation = new Navigation { mode=Navigation.Mode.Explicit,selectOnRight=second,selectOnLeft=second };
                Godmist.Editor.ControllerSupportSetup.ConfigureNavigation(root.transform);
                Assert.That(first.navigation.mode,Is.EqualTo(Navigation.Mode.Explicit));
                Assert.That(first.navigation.selectOnRight,Is.SameAs(second));
                Assert.That(first.navigation.selectOnLeft,Is.SameAs(second));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test]
        public void HoldProgressResetsWhenLeavingItsContext()
        {
            var hold = new ControllerHoldGesture();
            Assert.That(hold.Tick(true,true,true,.2f),Is.False);
            Assert.That(hold.Progress,Is.InRange(.3f,.32f));
            hold.Tick(false,false,true,.3f);
            Assert.That(hold.Progress,Is.Zero);
            Assert.That(hold.Tick(true,false,true,2),Is.False,"A held button cannot rearm after a modal transition.");
        }
        [Test]
        public void WheelKeepsDisabledActionsInspectableWithoutExecutingThem()
        {
            var root = new GameObject("Wheel test",typeof(Canvas),typeof(EventSystem));
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<PartyRailMenuView>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_PartyContextMenu.prefab");
                var menu = UnityEngine.Object.Instantiate(prefab,root.transform); menu.gameObject.SetActive(false);
                var wheel = menu.CreateControllerWheel(); var count = 0;
                wheel.Show("Akcje",new [] {
                    new ControllerWheelView.Entry("blocked","Zablokowana",()=>count++,()=>false,"Brak PA"),
                    new ControllerWheelView.Entry("ready","Gotowa",()=>count++) },"blocked",wheel.Hide);
                Assert.That(wheel.SelectedId,Is.EqualTo("blocked"));
                foreach(var graphic in wheel.GetComponentsInChildren<Graphic>())
                    Assert.That(graphic.GetComponent<CanvasRenderer>(),Is.Not.Null,"Every generated graphic must actually render: "+graphic.name);
                foreach (var border in wheel.GetComponentsInChildren<Image>().Where(image=>image.name=="Radial Entry Frame"))
                    Assert.That(border.GetComponentsInChildren<TMP_Text>().Length,Is.Zero,"A frame must not clone the menu hierarchy.");
                var disabled = wheel.GetComponentsInChildren<Button>().Single(b=>b.GetComponentInChildren<TMP_Text>().text=="Zablokowana");
                disabled.onClick.Invoke(); Assert.That(count,Is.Zero); Assert.That(wheel.IsOpen,Is.True);
                var ready = wheel.GetComponentsInChildren<Button>().Single(b=>b.GetComponentInChildren<TMP_Text>().text=="Gotowa");
                ready.onClick.Invoke(); Assert.That(count,Is.EqualTo(1)); Assert.That(wheel.IsOpen,Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
