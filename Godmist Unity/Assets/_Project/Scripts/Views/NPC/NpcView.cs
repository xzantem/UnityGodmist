using Godmist.Core.Enums;
using Godmist.Core.Interfaces;
using Godmist.Runtime.NPC;
using Godmist.Views.Exploration;
using Godmist.Views.Game;
using Godmist.Views.UI;
using TMPro;
using UnityEngine;

namespace Godmist.Views.NPC
{
    public class NpcView : MonoBehaviour, IInteractable
    {
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private TextMeshPro nameLabel;
        [SerializeField] private float interactionRadius = 1.25f;
        [SerializeField] private KeyCode interactionKey = KeyCode.E;

        private ExplorationPartyController partyController;
        private NpcInstance npc;

        public string NpcId => npc?.NpcId ?? string.Empty;

        public void Initialize(
            ExplorationPartyController nextPartyController,
            NpcInstance nextNpc)
        {
            partyController = nextPartyController;
            npc = nextNpc;

            Bind(npc);
        }

        public bool CanInteract()
        {
            var session = GameBootstrap.Instance?.Session;

            if (npc == null
                || partyController == null
                || partyController.ActiveLeaderTransform == null
                || session == null
                || session.CurrentMode != GameModeState.Exploration)
            {
                return false;
            }

            var offset = partyController.ActiveLeaderTransform.position
                         - transform.position;

            return offset.sqrMagnitude
                   <= interactionRadius * interactionRadius;
        }

        public void Interact()
        {
            if (!CanInteract()) return;

            var session = GameBootstrap.Instance.Session;

            if (!session.TryRequestNpcInteraction(npc.NpcId)) return;
        }

        private void Update()
        {
            if (DevConsoleView.IsOpen) return;
            
            if (!Input.GetKeyDown(interactionKey)) return;
            if (!CanInteract()) return;

            Interact();
        }

        private void Bind(NpcInstance nextNpc)
        {
            if (nextNpc == null) return;

            if (bodyRenderer != null && nextNpc.Definition?.Portrait != null)
            {
                bodyRenderer.sprite = nextNpc.Definition.Portrait;
            }

            if (nameLabel != null)
            {
                nameLabel.text = nextNpc.Definition != null
                    ? nextNpc.Definition.DisplayName
                    : nextNpc.NpcId;
            }
        }

        private void Awake()
        {
            if (bodyRenderer == null || nameLabel == null) return;

            var labelRenderer = nameLabel.GetComponent<MeshRenderer>();

            if (labelRenderer == null) return;

            labelRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
            labelRenderer.sortingOrder = bodyRenderer.sortingOrder + 1;
        }
    }
}