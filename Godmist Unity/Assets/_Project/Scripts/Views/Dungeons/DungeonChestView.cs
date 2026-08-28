using Godmist.Core.Enums;
using Godmist.Core.Interfaces;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Game;
using Godmist.Views.UI;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Godmist.Views.Dungeons
{
    public class DungeonChestView : DungeonObjectView, IInteractable
    {
        [SerializeField] private float interactionRadius = 1.25f;
        [SerializeField] private KeyCode interactionKey = KeyCode.E;

        private ExplorationPartyController partyController;
        private DungeonFloorInstance floor;
        private Tilemap floorTilemap;
        private ChestDungeonObjectInstance chest;

        public void Initialize(
            ExplorationPartyController nextPartyController,
            DungeonFloorInstance nextFloor,
            ChestDungeonObjectInstance nextChest,
            Vector3 worldPosition,
            Tilemap nextFloorTilemap)
        {
            if (!TryBind(nextChest, worldPosition)) return;

            partyController = nextPartyController;
            floor = nextFloor;
            floorTilemap = nextFloorTilemap;
            chest = nextChest;
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
            if (!chest.TryClaimItems(out var items)) return;

            GameBootstrap.Instance.Session.AddPendingLoot(items);

            Destroy(gameObject);
        }
    }
}
