using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Godmist.Views.UI.Workspace
{
    /// <summary>
    /// Keeps world input independent from floating windows. Only a pointer click
    /// that lands on UGUI is consumed; keyboard movement remains a world concern.
    /// </summary>
    public static class UIWorkspaceInputRouter
    {
        public static bool IsPointerOverUi()
        {
            return EventSystem.current != null
                && EventSystem.current.IsPointerOverGameObject();
        }

        public static bool HasFocusedTextInput()
        {
            var selected = EventSystem.current?.currentSelectedGameObject;
            return selected != null && selected.GetComponentInParent<TMP_InputField>() != null;
        }
    }
}
