using Godmist.Core.Enums;
using Godmist.Core.Interfaces;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Game;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Godmist.Views.Dungeons
{
    public class DungeonChestView : DungeonObjectView, IInteractable
    {
        [SerializeField] private float interactionRadius = 1.25f;
        [SerializeField] private KeyCode interactionKey = KeyCode.E;

        private readonly DungeonChestInteractionService chestInteractionService = new();
        private ExplorationPartyController partyController;
        private DungeonFloorInstance floor;
        private Tilemap floorTilemap;
        private ChestDungeonObjectInstance chest;
        private DungeonLootWindowView lootWindowPrefab;

        public void Initialize(
            ExplorationPartyController nextPartyController,
            DungeonFloorInstance nextFloor,
            ChestDungeonObjectInstance nextChest,
            Vector3 worldPosition,
            Tilemap nextFloorTilemap,
            DungeonLootWindowView nextLootWindowPrefab = null)
        {
            if (!TryBind(nextChest, worldPosition)) return;

            partyController = nextPartyController;
            floor = nextFloor;
            floorTilemap = nextFloorTilemap;
            chest = nextChest;
            lootWindowPrefab = nextLootWindowPrefab;
        }

        private void Update()
        {
            if (DevConsoleView.IsOpen) return;
            
            if (!Input.GetKeyDown(interactionKey)) return;
            if (!CanInteract()) return;

            Interact();
        }

        public bool CanInteract()
        {
            if (!IsElevationVisible || partyController?.CanInteractWithGroundObjects != true) return false;
            var gameBootstrap = GameBootstrap.Instance;

            if (chest == null
                || !chest.IsActive
                || partyController == null
                || partyController.ActiveLeaderTransform == null
                || floor == null
                || floorTilemap == null
                || gameBootstrap == null
                || gameBootstrap.Session == null
                || gameBootstrap.Session.CurrentMode != GameModeState.Exploration)
            {
                return false;
            }

            var offset = partyController.ActiveLeaderTransform.position
                         - transform.position;

            return offset.sqrMagnitude
                   <= interactionRadius * interactionRadius
                   && DungeonExplorationInteractionUtility.HasDirectAccess(
                       floor,
                       ToDungeonCell(
                           floorTilemap.WorldToCell(
                               partyController.ActiveLeaderTransform.position)),
                       chest.Position);
        }

        private static Vector2Int ToDungeonCell(Vector3Int cell)
        {
            return new Vector2Int(cell.x, cell.y);
        }

        public void Interact()
        {
            if (!CanInteract()) return;

            var session = GameBootstrap.Instance?.Session;
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
