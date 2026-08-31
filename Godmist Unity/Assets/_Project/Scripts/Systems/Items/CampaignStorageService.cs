using System.Collections.Generic;
using System.Linq;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;

namespace Godmist.Systems.Items
{
    /// <summary>Transfers item instances between a character inventory and campaign storage.</summary>
    public sealed class CampaignStorageService
    {
        private readonly InventoryService inventories = new();
        private readonly EquipmentService equipment = new();

        public bool TryDeposit(GameSession session, Inventory source, string itemInstanceId, bool singleItem) =>
            TryTransfer(session?.Stash, source, itemInstanceId, singleItem, deposit: true);

        public bool TryWithdraw(GameSession session, Inventory destination, string itemInstanceId, bool singleItem) =>
            TryTransfer(session?.Stash, destination, itemInstanceId, singleItem, deposit: false);

        public bool TryEquipFromStash(GameSession session, CharacterInstance character, string itemInstanceId)
        {
            if (session?.Stash == null || character?.Inventory == null || string.IsNullOrWhiteSpace(itemInstanceId)) return false;
            if (!inventories.TryTransferItem(session.Stash, character.Inventory, itemInstanceId)) return false;
            if (equipment.TryEquip(character, itemInstanceId)) return true;

            inventories.TryTransferItem(character.Inventory, session.Stash, itemInstanceId);
            return false;
        }

        private bool TryTransfer(Inventory stash, Inventory characterInventory, string itemInstanceId, bool singleItem, bool deposit)
        {
            var source = deposit ? characterInventory : stash;
            var destination = deposit ? stash : characterInventory;
            if (source?.Items == null || destination?.Items == null || string.IsNullOrWhiteSpace(itemInstanceId)) return false;

            var representative = source.Items.FirstOrDefault(item => item?.InstanceId == itemInstanceId);
            if (representative == null) return false;

            var itemIds = singleItem || !representative.Definition.IsStackable
                ? new List<string> { representative.InstanceId }
                : source.Items.Where(item => item != null && representative.CanStackWith(item)).Select(item => item.InstanceId).ToList();
            if (itemIds.Count == 0) return false;

            var transferred = new List<string>();
            foreach (var id in itemIds)
            {
                if (inventories.TryTransferItem(source, destination, id))
                {
                    transferred.Add(id);
                    continue;
                }

                foreach (var movedId in transferred)
                    inventories.TryTransferItem(destination, source, movedId);
                return false;
            }

            return true;
        }
    }
}
