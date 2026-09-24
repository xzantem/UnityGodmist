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

        [SerializeField] private GameObject questMarker;
        private float nextQuestMarkerUpdate;
        private MeshRenderer questMarkerRenderer;
        private Color originalColor;
        public float InteractionRadius => interactionRadius;
        public string DisplayName => npc?.Definition?.DisplayName ?? NpcId;
        public bool IsPointerHit(Vector2 point) => isActiveAndEnabled && npc != null && bodyRenderer != null
            && bodyRenderer.enabled && !bodyRenderer.forceRenderingOff
            && bodyRenderer.bounds.Contains(new Vector3(point.x, point.y, bodyRenderer.bounds.center.z));
        public void SetInspectionHighlight(bool value)
        {
            if (bodyRenderer != null) bodyRenderer.color = value ? new Color(.5f, .86f, 1f, 1f) : originalColor;
        }
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
            if (questMarker != null && Time.unscaledTime >= nextQuestMarkerUpdate)
            {
                nextQuestMarkerUpdate = Time.unscaledTime + .25f;
                if (questMarkerRenderer != null && bodyRenderer != null)
                {
                    questMarkerRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
                    questMarkerRenderer.sortingOrder = bodyRenderer.sortingOrder + 2;
                }
                var currentSession = GameBootstrap.Instance?.Session;
                var show = Godmist.Systems.NPC.QuestOfferAvailability.HasOffer(currentSession, npc?.Definition?.BaseDialogue)
                    || npc?.NpcId == "npc.arungard.questgiver_01"
                        && Godmist.Systems.Quests.CatacombsMissionBoard.HasAction(currentSession);
                if (questMarker.activeSelf != show) questMarker.SetActive(show);
            }
            if (DevConsoleView.IsOpen || Godmist.Views.UI.Workspace.UIWorkspaceInputRouter.HasFocusedTextInput()
                || Godmist.Views.UI.Workspace.UIWorkspaceInputRouter.IsPointerOverUi()) return;
            
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
            if (questMarker != null) questMarkerRenderer = questMarker.GetComponent<MeshRenderer>();
            if (bodyRenderer != null) originalColor = bodyRenderer.color;
            if (nameLabel != null) nameLabel.gameObject.SetActive(false);
            if (bodyRenderer == null || nameLabel == null) return;

            var labelRenderer = nameLabel.GetComponent<MeshRenderer>();

            if (labelRenderer == null) return;

            labelRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
            labelRenderer.sortingOrder = bodyRenderer.sortingOrder + 1;
        }
    }
}
