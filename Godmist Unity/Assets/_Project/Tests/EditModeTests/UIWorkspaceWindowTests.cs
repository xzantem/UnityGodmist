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
