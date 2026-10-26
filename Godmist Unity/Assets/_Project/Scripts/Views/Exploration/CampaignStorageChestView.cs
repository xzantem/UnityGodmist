using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;
using Godmist.Core.Interfaces;
using Godmist.Views.Game;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using UnityEngine;

namespace Godmist.Views.Exploration
{
    public sealed class CampaignStorageChestView : MonoBehaviour, IExplorationWorldInteraction
    {
        [SerializeField] private ExplorationPartyController partyController;
        [SerializeField] private CampaignStorageWindowView storageWindowPrefab;
        [SerializeField] private float interactionRadius = 1.25f;
        [SerializeField] private KeyCode interactionKey = KeyCode.E;
        private CampaignStorageWindowView window;

        public ExplorationPartyController InteractionParty => partyController;
        public DungeonFloorInstance InteractionFloor => null;
        public float InteractionRadius => interactionRadius;
        public KeyCode InteractionKey => interactionKey;
        public bool CanRequestInteraction => isActiveAndEnabled && storageWindowPrefab != null
            && partyController?.CanRequestWorldInteraction == true && partyController.Session.ActiveDungeon == null;
        private void OnEnable()
        {
            partyController?.RegisterInteraction(this);
            ExplorationWorldObjectInteraction.Attach(this, this);
        }

        private void OnDestroy() => partyController?.UnregisterInteraction(this);

        public bool CanInteract() => CanRequestInteraction && partyController.CanInteractWithGroundObjects
            && partyController.ActiveLeaderTransform != null && CanInteractFrom(partyController.ActiveLeaderTransform.position);
        public bool CanInteractFrom(Vector2 position) => CanRequestInteraction
            && (position-(Vector2)transform.position).sqrMagnitude <= interactionRadius*interactionRadius;
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
            window?.Open(partyController.Session);
        }
    }
}
