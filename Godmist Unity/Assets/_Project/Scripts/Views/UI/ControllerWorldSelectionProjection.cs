using UnityEngine;
namespace Godmist.Views.UI
{
    // Input is read early; the visual pointer must be projected after both camera framers finish.
    [DefaultExecutionOrder(1000)]
    public sealed class ControllerWorldSelectionProjection : MonoBehaviour
    {
        private ControllerInputView owner;
        public void Bind(ControllerInputView view) => owner = view;
        private void LateUpdate() { if (owner != null && owner.isActiveAndEnabled) owner.ProjectWorldSelection(); }
    }
}
