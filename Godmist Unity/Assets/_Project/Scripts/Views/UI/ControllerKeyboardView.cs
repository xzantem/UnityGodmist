using TMPro;
using UnityEngine;

namespace Godmist.Views.UI
{
    /// <summary>Text entry for desktop pads without requiring an OS touch keyboard.</summary>
    public sealed class ControllerKeyboardView : MonoBehaviour
    {
        [SerializeField] private TMP_Text preview;
        [SerializeField] private UnityEngine.UI.Button firstKey;
        private TMP_InputField target;
        private string original;
        private bool accepted, upper;
        private bool originalFieldEnabled;

        public void Open(TMP_InputField field)
        {
            if (field == null || !field.interactable || field.readOnly) return;
            if (target == field && gameObject.activeInHierarchy) return;
            RestoreUnaccepted();
            target = field; original = field.text; accepted = false; upper = false;
            originalFieldEnabled = field.enabled;
            // TMP can defer activation until LateUpdate and take focus back after our
            // keyboard opens. Suspend native editing while this view owns text input.
            field.DeactivateInputField();
            field.enabled = false;
            gameObject.SetActive(true); transform.SetAsLastSibling(); Refresh();
            if (firstKey != null && UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(firstKey.gameObject);
        }

        public void Enter(string token)
        {
            if (target == null) { Cancel(); return; }
            if (token == "shift") { upper = !upper; Refresh(); return; }
            if (token == "clear") target.text = string.Empty;
            else if (token == "backspace") target.text = target.text.Length > 0 ? target.text[..^1] : string.Empty;
            else
            {
                var value = token == "space" ? " " : upper ? token.ToUpperInvariant() : token;
                if (target.characterLimit > 0 && target.text.Length + value.Length > target.characterLimit) return;
                foreach (var character in value)
                {
                    if (target.characterValidation is TMP_InputField.CharacterValidation.Integer or TMP_InputField.CharacterValidation.Digit
                        && !char.IsDigit(character)) return;
                    if (target.characterValidation == TMP_InputField.CharacterValidation.Decimal
                        && !char.IsDigit(character) && character != '.' && character != ',') return;
                }
                target.text += value;
            }
            Refresh();
        }

        public void Accept()
        {
            accepted = true;
            if (target != null) target.onEndEdit.Invoke(target.text);
            gameObject.SetActive(false);
        }
        public void Cancel()
        {
            RestoreUnaccepted();
            gameObject.SetActive(false);
        }
        private void OnDisable()
        {
            RestoreUnaccepted();
        }
        private void RestoreUnaccepted()
        {
            if (target != null)
            {
                if (!accepted) target.text = original;
                target.enabled = originalFieldEnabled;
            }
            target = null;
        }
        private void Refresh()
        {
            if (preview != null) preview.text = target != null ? target.text + "|" : string.Empty;
            foreach (var key in GetComponentsInChildren<ControllerKeyboardKey>(true)) key.Refresh(upper);
        }
    }
}
