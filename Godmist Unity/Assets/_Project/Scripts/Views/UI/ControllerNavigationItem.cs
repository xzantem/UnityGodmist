using Godmist.Views.UI.Workspace;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    /// <summary>Keyboard/gamepad focus exposes the same item interactions and hover information.</summary>
    public sealed class ControllerNavigationItem : MonoBehaviour, ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        [SerializeField] private GameObject focusFrame;
        [SerializeField] private MonoBehaviour pointerHandler;
        public bool UsesPointerSubmit => pointerHandler != null;
        private bool entered;

        public void OnSelect(BaseEventData eventData)
        {
            if (!ControllerInput.UiActive) return;
            if (focusFrame != null) focusFrame.SetActive(true);
            if (entered) return;
            var pointer = Pointer();
            entered = true;
            ExecuteEvents.Execute(gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        }

        public void OnDeselect(BaseEventData eventData) => Clear();
        public void ReleaseControllerFocus() => Clear();
        private void OnDisable() => Clear();
        private void Clear()
        {
            if (focusFrame != null) focusFrame.SetActive(false);
            if (!entered) return;
            entered = false;
            ExecuteEvents.Execute(gameObject, Pointer(), ExecuteEvents.pointerExitHandler);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (ControllerInput.UiActive && UsesPointerSubmit) SubmitPointer();
        }

        public void SubmitPointer()
        {
            if (pointerHandler is IPointerClickHandler handler)
            {
                var data = Pointer(); data.button = PointerEventData.InputButton.Left; data.clickCount = 1;
                handler.OnPointerClick(data);
            }
        }

        private PointerEventData Pointer() => new(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(null,
                transform is RectTransform rect ? rect.TransformPoint(rect.rect.center) : transform.position),
            pointerId = -1
        };
    }
}
