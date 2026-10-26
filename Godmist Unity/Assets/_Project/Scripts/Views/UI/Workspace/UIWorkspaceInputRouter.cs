using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Godmist.Views.UI.Workspace
{
    /// <summary>
    /// Floating windows leave world movement independent. UGUI pointer clicks,
    /// focused text inputs and context-menu navigation claim their own input.
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
            var events = EventSystem.current;
            var selected = events != null ? events.currentSelectedGameObject : null;
            return selected != null && selected.GetComponentInParent<TMP_InputField>() != null;
        }

        public static bool HasFocusedContextMenu()
        {
            var events = EventSystem.current;
            var selected = events != null ? events.currentSelectedGameObject : null;
            // Unity's destroyed objects compare equal to null, but ?. only checks
            // the managed wrapper, which the EventSystem can still retain.
            var menu = selected != null ? selected.GetComponentInParent<PartyRailMenuView>() : null;
            // A menu action can dismiss its popup before world Update runs.
            // Keep that same Enter/Escape/click from also confirming a world action.
            return (menu != null && menu.isActiveAndEnabled)
                || (selected != null && selected.GetComponentInParent<ItemActionPopupView>() is { IsModalOpen: true })
                || (selected != null && selected.GetComponentInParent<Godmist.Views.UI.CombatConsumableSelectionView>(true) is { OwnsMenuInput: true })
                || UIWorkspaceWindowManager.Instance?.ConsumedContextMenuInputThisFrame == true;
        }
    }
}
