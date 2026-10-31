using UnityEngine;
using UnityEngine.Events;

namespace Godmist.Views.UI
{
    /// <summary>A panel owns focus and cancel while visible; runtime clones inherit the authored scope.</summary>
    public sealed class ControllerNavigationScope : MonoBehaviour
    {
        [SerializeField] private bool modal = true;
        [SerializeField] private UnityEvent cancel = new();
        private ControllerInputView owner;
        public bool IsModal => modal;
        public UnityEvent CancelAction => cancel;
        public void Cancel() => cancel.Invoke();
        public void Focus() { if (owner != null) owner.Focus(this); }
        private void OnEnable()
        {
            owner = GetComponentInParent<ControllerInputView>();
            if (owner != null) owner.Register(this);
        }
        private void OnDisable() { if (owner != null) owner.Unregister(this); }
    }
}
