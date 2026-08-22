using UnityEngine;
using UnityEngine.EventSystems;

namespace Godmist.Views.UI.Workspace
{
    public sealed class UIWorkspaceWindowResizeHandle : MonoBehaviour,
        IBeginDragHandler,
        IDragHandler
    {
        [SerializeField] private UIWorkspaceWindow window;

        private void Awake()
        {
            if (window == null)
            {
                window = GetComponentInParent<UIWorkspaceWindow>();
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            window?.Focus();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (window == null)
            {
                return;
            }

            var canvas = window.GetComponentInParent<Canvas>();
            var scaleFactor = canvas != null ? canvas.scaleFactor : 1f;
            window.ResizeFromBottomRight(eventData.delta / scaleFactor);
        }
    }
}
