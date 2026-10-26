using Godmist.Core.Enums;
using Godmist.Core.Interfaces;
using Godmist.Runtime.NPC;
using Godmist.Runtime.Game;
using Godmist.Systems.NPC;
using Godmist.Views.Exploration;
using Godmist.Views.Game;
using Godmist.Views.UI;
using Godmist.Views.Characters;
using Godmist.Views.Combat;
using TMPro;
using UnityEngine;

namespace Godmist.Views.NPC
{
    public class NpcView : MonoBehaviour, IInteractable
    {
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private SpriteRenderer workstationRenderer;
        [SerializeField] private TextMeshPro nameLabel;
        [SerializeField] private float interactionRadius = 1.25f;
        [SerializeField] private KeyCode interactionKey = KeyCode.E;

        private ExplorationPartyController partyController;
        private ExplorationPartyController registeredPartyController;
        private NpcInstance npc;

        [SerializeField] private GameObject questMarker;
        private float nextQuestMarkerUpdate;
        private MeshRenderer questMarkerRenderer;
        private TextMeshPro questMarkerText;
        private CombatUnitVisualStateView inspectionVisualState;
        private bool inspectionHighlighted;
        private CharacterAnimationClip idleAnimation;
        private CharacterAnimationClip workAnimation;
        private CharacterAnimationClip conversationAnimation;
        private CharacterAnimationClip workForeground;
        private CharacterAnimationClip conversationForeground;
        private SpriteRenderer stationRenderer;
        private SpriteRenderer foregroundRenderer;
        private MaterialPropertyBlock stationProperties;
        private DialogueService animationDialogueService;
        private int idleAnimationFrameIndex;
        private float nextIdleAnimationFrameTime;
        public SpriteRenderer BodyRenderer => bodyRenderer;
        public float InteractionRadius => interactionRadius;
        public string DisplayName => npc?.Definition?.DisplayName ?? NpcId;
        public bool IsPointerHit(Vector2 point) => isActiveAndEnabled && npc != null && bodyRenderer != null
            && bodyRenderer.enabled && !bodyRenderer.forceRenderingOff
            && bodyRenderer.bounds.Contains(new Vector3(point.x, point.y, bodyRenderer.bounds.center.z));
        public void SetInspectionHighlight(bool value)
        {
            inspectionHighlighted = value;
            if (!value) inspectionVisualState?.ClearHighlight();
        }

        private void LateUpdate()
        {
            RefreshStationLayers();
            PositionQuestMarker();
            if (bodyRenderer == null || !inspectionHighlighted) return;
            // The same procedural outline as party members, owned by the NPC body.
            if (inspectionVisualState == null)
                inspectionVisualState = bodyRenderer.GetComponent<CombatUnitVisualStateView>()
                    ?? bodyRenderer.gameObject.AddComponent<CombatUnitVisualStateView>();
            inspectionVisualState.Refresh(null, false, true, false, true);
        }

        private void OnDisable()
        {
            DisconnectAnimationDialogue();
            SwitchAnimation(workAnimation);
            inspectionHighlighted = false;
            inspectionVisualState?.ClearHighlight();
        }

        private void OnDestroy()
        {
            registeredPartyController?.UnregisterInteraction(this);
        }
        public string NpcId => npc?.NpcId ?? string.Empty;

        public void Initialize(
            ExplorationPartyController nextPartyController,
            NpcInstance nextNpc)
        {
            registeredPartyController?.UnregisterInteraction(this);
            partyController = nextPartyController;
            registeredPartyController = nextPartyController;
            registeredPartyController?.RegisterInteraction(this);
            npc = nextNpc;

            Bind(npc);
            ConnectAnimationDialogue();
        }

        private void Start() => ConnectAnimationDialogue();
        private void OnEnable() => ConnectAnimationDialogue();

        private void ConnectAnimationDialogue()
        {
            var service = GameBootstrap.Instance?.DialogueService;
            if (service != animationDialogueService)
            {
                DisconnectAnimationDialogue();
                animationDialogueService = service;
                if (service != null)
                {
                    service.DialogueStarted += HandleAnimationDialogueStarted;
                    service.DialogueEnded += HandleAnimationDialogueEnded;
                }
            }
            SwitchAnimation(service?.ActiveDialogue?.Npc?.NpcId == NpcId ? conversationAnimation : workAnimation);
        }

        private void DisconnectAnimationDialogue()
        {
            if (animationDialogueService == null) return;
            animationDialogueService.DialogueStarted -= HandleAnimationDialogueStarted;
            animationDialogueService.DialogueEnded -= HandleAnimationDialogueEnded;
            animationDialogueService = null;
        }

        private void HandleAnimationDialogueStarted(DialogueSession dialogue)
        {
            if (dialogue?.Npc?.NpcId == NpcId) SwitchAnimation(conversationAnimation);
        }

        private void HandleAnimationDialogueEnded(NpcInstance endedNpc)
        {
            if (endedNpc?.NpcId == NpcId) SwitchAnimation(workAnimation);
        }

        private void SwitchAnimation(CharacterAnimationClip clip)
        {
            if (clip == null || clip == idleAnimation || bodyRenderer == null) return;
            idleAnimation = clip;
            idleAnimationFrameIndex = 0;
            bodyRenderer.sprite = clip.Frames[0];
            RefreshStationLayers();
            nextIdleAnimationFrameTime = Time.time + clip.GetFrameDuration(0);
        }

        public bool CanInteract()
        {
            var session = GameBootstrap.Instance?.Session;

            if (!isActiveAndEnabled
                || npc == null
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
            UpdateIdleAnimation();

            if (questMarker != null && Time.unscaledTime >= nextQuestMarkerUpdate)
            {
                nextQuestMarkerUpdate = Time.unscaledTime + .25f;
                RefreshQuestMarker(GameBootstrap.Instance?.Session);
            }
            if (DevConsoleView.IsOpen || Godmist.Views.UI.Workspace.UIWorkspaceInputRouter.HasFocusedTextInput()
                || Godmist.Views.UI.Workspace.UIWorkspaceInputRouter.IsPointerOverUi()) return;
            
            if (!Input.GetKeyDown(interactionKey)) return;
            if (!CanInteract()) return;

            Interact();
        }

        private void RefreshQuestMarker(GameSession session)
        {
            if (questMarker == null) return;
            var state = QuestOfferAvailability.GetMarkerState(session, npc);
            var visible = state != NpcQuestMarkerState.None;
            if (questMarker.activeSelf != visible) questMarker.SetActive(visible);
            if (!visible || questMarkerText == null) return;
            var text = state == NpcQuestMarkerState.Reward ? "?" : "!";
            if (questMarkerText.text != text) questMarkerText.text = text;
            questMarkerText.color = state == NpcQuestMarkerState.Reward
                ? new Color(.35f, 1f, .45f) : new Color(1f, .8f, .15f);
        }

        private void PositionQuestMarker()
        {
            if (questMarkerText == null || bodyRenderer == null || !questMarker.activeSelf) return;
            if (questMarkerRenderer != null)
            {
                questMarkerRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
                questMarkerRenderer.sortingOrder = bodyRenderer.sortingOrder + 2;
            }
            questMarkerText.ForceMeshUpdate();
            var bounds = questMarkerText.textBounds;
            if (bounds.size.x < 0f || bounds.size.y < 0f) return;
            var offset = questMarkerText.transform.TransformVector(new Vector3(bounds.center.x, bounds.min.y, 0f));
            // Assign an absolute anchor: TMP may rebuild the inherited fitted rect when
            // the hidden marker first appears; accumulating world deltas can drift.
            questMarkerText.transform.position = new Vector3(
                bodyRenderer.bounds.center.x - offset.x,
                bodyRenderer.bounds.max.y + .2f - offset.y,
                questMarkerText.transform.position.z);
        }

        private void Bind(NpcInstance nextNpc)
        {
            if (nextNpc == null) return;

            idleAnimation = null;
            idleAnimationFrameIndex = 0;
            if (bodyRenderer != null && nextNpc.Definition?.Portrait != null)
            {
                bodyRenderer.sprite = nextNpc.Definition.Portrait;
            }

            CharacterAnimationResolver.TryResolveNpcIdle(nextNpc.NpcId, out conversationAnimation);
            CharacterAnimationResolver.TryResolveNpcWork(nextNpc.NpcId, out workAnimation);
            CharacterAnimationResolver.TryResolveNpcForeground(nextNpc.NpcId, true, out workForeground);
            CharacterAnimationResolver.TryResolveNpcForeground(nextNpc.NpcId, false, out conversationForeground);
            BindStation(nextNpc.NpcId);
            SwitchAnimation(workAnimation ?? conversationAnimation);

            if (nameLabel != null)
            {
                nameLabel.text = nextNpc.Definition != null
                    ? nextNpc.Definition.DisplayName
                    : nextNpc.NpcId;
            }
        }

        private void UpdateIdleAnimation()
        {
            if (bodyRenderer == null || idleAnimation == null || idleAnimation.FrameCount < 2)
                return;
            if (Time.time < nextIdleAnimationFrameTime)
                return;

            idleAnimationFrameIndex = (idleAnimationFrameIndex + 1) % idleAnimation.FrameCount;
            bodyRenderer.sprite = idleAnimation.Frames[idleAnimationFrameIndex];
            nextIdleAnimationFrameTime = Time.time + idleAnimation.GetFrameDuration(idleAnimationFrameIndex);
        }

        private void BindStation(string id)
        {
            if (bodyRenderer == null) return;
            if (!CharacterAnimationResolver.TryResolveNpcStation(id, out var sprite, out var offset))
            {
                if (stationRenderer != null) stationRenderer.gameObject.SetActive(false);
                if (foregroundRenderer != null) foregroundRenderer.gameObject.SetActive(false);
                return;
            }
            if (stationRenderer == null)
            {
                stationRenderer = workstationRenderer != null ? workstationRenderer : CreateStationLayer("NPC Workstation");
                foregroundRenderer = CreateStationLayer("NPC Above Workstation");
                stationProperties = new MaterialPropertyBlock();
            }
            stationRenderer.gameObject.SetActive(true);
            foregroundRenderer.gameObject.SetActive(true);
            if (workstationRenderer != null)
            {
                // Keep the authored city prop and collider. Align the NPC's feet
                // to the approved hand/prop registration, without moving the anvil.
                var anchor = new Vector3(workstationRenderer.bounds.min.x, workstationRenderer.bounds.max.y,
                    bodyRenderer.transform.position.z);
                transform.position += anchor - bodyRenderer.transform.TransformPoint(offset);
                // Share the NPC SortingGroup so the upper-body layer can cover
                // the tabletop even when the city uses Y-sorted actor groups.
                workstationRenderer.transform.SetParent(bodyRenderer.transform, true);
            }
            else
            {
                stationRenderer.sprite = sprite;
                stationRenderer.transform.localPosition = offset;
            }
        }

        private SpriteRenderer CreateStationLayer(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(bodyRenderer.transform, false);
            return child.AddComponent<SpriteRenderer>();
        }

        private void RefreshStationLayers()
        {
            if (stationRenderer == null || !stationRenderer.gameObject.activeSelf || bodyRenderer == null) return;
            var clip = idleAnimation == workAnimation ? workForeground : conversationForeground;
            foregroundRenderer.sprite = clip != null ? clip.Frames[idleAnimationFrameIndex % clip.FrameCount] : null;
            bodyRenderer.GetPropertyBlock(stationProperties);
            CopyStationPresentation(stationRenderer, 1);
            CopyStationPresentation(foregroundRenderer, 2);
        }

        private void CopyStationPresentation(SpriteRenderer layer, int order)
        {
            layer.sharedMaterial = bodyRenderer.sharedMaterial;
            layer.SetPropertyBlock(stationProperties);
            layer.color = bodyRenderer.color;
            layer.sortingLayerID = bodyRenderer.sortingLayerID;
            layer.sortingOrder = bodyRenderer.sortingOrder + order;
            layer.enabled = bodyRenderer.enabled;
            layer.forceRenderingOff = bodyRenderer.forceRenderingOff;
        }

        private void Awake()
        {
            if (questMarker != null)
            {
                questMarkerRenderer = questMarker.GetComponent<MeshRenderer>();
                questMarkerText = questMarker.GetComponent<TextMeshPro>();
                if (questMarkerText != null)
                {
                    // This is a single world-space symbol. The copied name label's
                    // ellipsis mode can hide it while its fitted rect still has height 0.
                    questMarkerText.overflowMode = TextOverflowModes.Overflow;
                    questMarkerText.textWrappingMode = TextWrappingModes.NoWrap;
                }
            }
            if (nameLabel != null) nameLabel.gameObject.SetActive(false);
            if (bodyRenderer == null || nameLabel == null) return;

            var labelRenderer = nameLabel.GetComponent<MeshRenderer>();

            if (labelRenderer == null) return;

            labelRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
            labelRenderer.sortingOrder = bodyRenderer.sortingOrder + 1;
        }
    }
}
