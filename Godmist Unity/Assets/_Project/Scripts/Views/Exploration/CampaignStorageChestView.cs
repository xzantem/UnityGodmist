using Godmist.Core.Enums;
using Godmist.Core.Interfaces;
using Godmist.Views.Game;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using UnityEngine;

namespace Godmist.Views.Exploration
{
    public sealed class CampaignStorageChestView : MonoBehaviour, IInteractable
    {
        [SerializeField] private ExplorationPartyController partyController;
        [SerializeField] private CampaignStorageWindowView storageWindowPrefab;
        [SerializeField] private float interactionRadius = 1.25f;
        [SerializeField] private KeyCode interactionKey = KeyCode.E;
        private CampaignStorageWindowView window;

        public bool CanInteract()
        {
            var session = GameBootstrap.Instance?.Session;
            if (partyController?.ActiveLeaderTransform == null || session?.CurrentMode != GameModeState.Exploration) return false;
            return (partyController.ActiveLeaderTransform.position - transform.position).sqrMagnitude <= interactionRadius * interactionRadius;
        }
        public void Interact()
        {
            if (!CanInteract() || storageWindowPrefab == null) return;
            var manager = UIWorkspaceWindowManager.Instance;
            var previousWindow = manager?.FocusedWindow;
            if (window == null && manager?.WorkspaceRoot != null)
            {
                window = Instantiate(storageWindowPrefab, manager.WorkspaceRoot);
                manager.CascadeAndFocus(window.GetComponent<UIWorkspaceWindow>(), previousWindow);
            }
            window?.Open(GameBootstrap.Instance.Session);
        }
        private void Update() { if (!DevConsoleView.IsOpen && Input.GetKeyDown(interactionKey) && CanInteract()) Interact(); }
    }
}
