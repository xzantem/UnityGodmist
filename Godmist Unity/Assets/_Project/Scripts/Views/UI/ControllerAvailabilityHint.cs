using UnityEngine;
namespace Godmist.Views.UI
{
    /// <summary>Explicit opt-in: a disabled action can be focused to read its reason, but never submitted.</summary>
    public sealed class ControllerAvailabilityHint : MonoBehaviour
    {
        public string Reason { get; private set; }
        public bool CanInspect => isActiveAndEnabled && !string.IsNullOrWhiteSpace(Reason);
        public void Bind(string reason) => Reason = reason;
    }
}
