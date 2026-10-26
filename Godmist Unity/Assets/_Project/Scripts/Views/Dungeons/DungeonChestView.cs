using Godmist.Core.Enums;
using Godmist.Core.Interfaces;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Godmist.Views.Dungeons
{
    [DefaultExecutionOrder(-90)]
    public class DungeonChestView : DungeonObjectView, IInteractable
    {
        [SerializeField] private float interactionRadius = 1.25f;
        [SerializeField] private KeyCode interactionKey = KeyCode.E;

        private readonly DungeonChestInteractionService chestInteractionService = new();
        private ExplorationPartyController partyController;
        private ExplorationPartyController registeredPartyController;
        private DungeonFloorInstance floor;
        private Tilemap floorTilemap;
        private ChestDungeonObjectInstance chest;
        private DungeonLootWindowView lootWindowPrefab;
        private SpriteRenderer bodyRenderer;
        private Godmist.Views.Combat.CombatUnitVisualStateView hoverOutline;
        private bool inspectionHighlighted;
        public float InteractionRadius => interactionRadius;
        public DungeonFloorInstance Floor => floor;
        public bool CanRequestInteraction => isActiveAndEnabled && IsElevationVisible && chest?.IsActive == true
            && floor != null && floorTilemap != null && partyController?.CanAcceptExplorationInput == true
            && partyController.ActiveLeaderTransform != null
            && partyController.Session.ActiveCombat == null
            && partyController.Session.CharactersById.TryGetValue(partyController.Session.PlayerParty.ActiveLeaderId, out var leader)
            && leader.LifeState == CharacterLifeState.Alive
            && !partyController.Session.IsCharacterInCombat(leader.CharacterId);
        public bool IsOwnedBy(ExplorationPartyController controller) => partyController == controller;

        public void Initialize(
            ExplorationPartyController nextPartyController,
            DungeonFloorInstance nextFloor,
            ChestDungeonObjectInstance nextChest,
            Vector3 worldPosition,
            Tilemap nextFloorTilemap,
            DungeonLootWindowView nextLootWindowPrefab = null)
        {
            if (!TryBind(nextChest, worldPosition)) return;

            registeredPartyController?.UnregisterInteraction(this);
            partyController = nextPartyController;
            registeredPartyController = nextPartyController;
            registeredPartyController?.RegisterInteraction(this);
            floor = nextFloor;
            floorTilemap = nextFloorTilemap;
            chest = nextChest;
            lootWindowPrefab = nextLootWindowPrefab;
            bodyRenderer = GetComponentInChildren<SpriteRenderer>();
            SetInspectionHighlight(false);
        }

        private void OnDestroy()
        {
            registeredPartyController?.UnregisterInteraction(this);
        }

        private void OnDisable() => SetInspectionHighlight(false);

        public bool IsPointerHit(Vector2 point) => CanRequestInteraction && bodyRenderer != null
            && bodyRenderer.enabled && !bodyRenderer.forceRenderingOff
            && bodyRenderer.bounds.Contains(new Vector3(point.x, point.y, bodyRenderer.bounds.center.z));

        public void SetInspectionHighlight(bool value)
        {
            inspectionHighlighted = value;
            if (!value) hoverOutline?.ClearHighlight();
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (!inspectionHighlighted) return;
            if (!CanRequestInteraction || bodyRenderer == null || !bodyRenderer.enabled || bodyRenderer.forceRenderingOff)
            { SetInspectionHighlight(false); return; }
            if (hoverOutline == null)
            {
                hoverOutline = bodyRenderer.GetComponent<Godmist.Views.Combat.CombatUnitVisualStateView>()
                    ?? bodyRenderer.gameObject.AddComponent<Godmist.Views.Combat.CombatUnitVisualStateView>();
                hoverOutline.SetPulseSpeed(0f);
                hoverOutline.SetOutlineWidth(1f / Mathf.Max(1f, bodyRenderer.sprite?.pixelsPerUnit ?? 32f));
            }
            hoverOutline.Refresh(null, false, true, false, true);
        }

        private void Update()
        {
            if (!CanRequestInteraction || UIWorkspaceInputRouter.IsPointerOverUi())
            { SetInspectionHighlight(false); return; }
            if (Input.GetKeyDown(interactionKey) && CanInteract())
            { partyController.CancelMovement(); SetInspectionHighlight(false); Interact(); return; }
            var point = Vector2.zero;
            var hovered = !partyController.IsWorldPrimaryClickClaimed
                && partyController.TryGetPointerWorldPosition(out point) && IsPointerHit(point);
            SetInspectionHighlight(hovered);
            if (!hovered || !Input.GetMouseButtonDown(0)) return;
            TryPrimaryInteraction(point);
        }

        public bool TryPrimaryInteraction(Vector2 point)
        {
            if (!IsPointerHit(point) || partyController.IsWorldPrimaryClickClaimed
                || UIWorkspaceInputRouter.IsPointerOverUi()) return false;
            partyController.ClaimWorldPrimaryClick();
            if (!partyController.RequestChestInteraction(this)) ShowFeedback("Nie można podejść do skrzyni.");
            return true;
        }

        public bool CanInteract() => CanRequestInteraction && partyController.CanInteractWithGroundObjects
            && CanInteractFrom(partyController.ActiveLeaderTransform.position);

        public bool CanInteractFrom(Vector2 position) => chest != null && floorTilemap != null
            && (position - (Vector2)transform.position).sqrMagnitude <= interactionRadius * interactionRadius
            && DungeonExplorationInteractionUtility.HasDirectAccess(floor,
                ToDungeonCell(floorTilemap.WorldToCell(position)), chest.Position);

        private static Vector2Int ToDungeonCell(Vector3Int cell)
        {
            return new Vector2Int(cell.x, cell.y);
        }

        public void Interact()
        {
            if (!CanInteract()) return;

            var session = partyController.Session;
            if (!chest.IsUnlocked
                && !chestInteractionService.TryUnlock(
                    session,
                    chest,
                    out var failureReason))
            {
                ShowFeedback(failureReason);
                return;
            }

            DungeonLootWindowView.OpenChestOrReuse(
                session,
                chest.InstanceId,
                lootWindowPrefab);
        }

        private void ShowFeedback(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            var feedbackObject = new GameObject(
                "Dungeon Chest Feedback",
                typeof(TextMeshPro),
                typeof(DungeonLootFloatingText));
            feedbackObject.transform.position = transform.position + Vector3.up * 0.6f;

            feedbackObject.GetComponent<DungeonLootFloatingText>().Initialize(message);
        }
    }
}
