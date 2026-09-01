using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Godmist.Views.UI
{
    /// <summary>
    /// Prevents TMP from applying its own caret navigation to console arrows.
    /// DevConsoleView owns Up/Down while the console is open.
    /// </summary>
    public class DevConsoleInputField : TMP_InputField
    {
        public override void OnUpdateSelected(BaseEventData eventData)
        {
            if (DevConsoleView.IsOpen
                && (Input.GetKey(KeyCode.UpArrow)
                    || Input.GetKey(KeyCode.DownArrow)))
            {
                eventData.Use();
                return;
            }

            base.OnUpdateSelected(eventData);
        }
    }
}
