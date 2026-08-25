using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    public sealed class UIWorkspaceWindowDragHandle : MonoBehaviour,
        IBeginDragHandler,
        IDragHandler
    {
        [SerializeField] private UIWorkspaceWindow window;

        private bool isDragging;

        private void Awake()
        {
            if (window == null)
            {
                window = GetComponentInParent<UIWorkspaceWindow>();
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            isDragging = eventData.pointerPressRaycast.gameObject == null
                || eventData.pointerPressRaycast.gameObject.GetComponentInParent<Button>() == null;
            if (isDragging)
            {
                window?.Focus();
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging || window == null)
            {
                return;
            }

            var canvas = window.GetComponentInParent<Canvas>();
            var scaleFactor = canvas != null ? canvas.scaleFactor : 1f;
            window.Move(eventData.delta / scaleFactor);
        }
    }
}
