using TMPro;
using UnityEngine;

namespace Godmist.Views.UI
{
    public sealed class ControllerKeyboardKey : MonoBehaviour
    {
        [SerializeField] private ControllerKeyboardView keyboard;
        [SerializeField] private TMP_Text label;
        [SerializeField] private string token;
        public void Enter() => keyboard.Enter(token);
        public void Refresh(bool upper)
        {
            if (label == null || token.Length != 1) return;
            label.text = upper ? token.ToUpperInvariant() : token;
        }
    }
}
