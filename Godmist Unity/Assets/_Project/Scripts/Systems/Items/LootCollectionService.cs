using Godmist.Core.Enums;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;

namespace Godmist.Systems.Items
{
    public class LootCollectionService
    {
        private readonly InventoryService inventoryService = new();

        public bool TryClaimPendingLoot(
            GameSession gameSession,
            string characterId,
            string itemInstanceId)
        {
            if (gameSession == null
                || string.IsNullOrWhiteSpace(characterId)
                || string.IsNullOrWhiteSpace(itemInstanceId))
            {
                return false;
            }

            if (!gameSession.PlayerParty.IsActiveMember(characterId))
            {
                return false;
            }

            if (!gameSession.CharactersById.TryGetValue(
                    characterId,
                    out var character))
            {
                return false;
            }

            if (character.LifeState == CharacterLifeState.Dead
                || character.Inventory == null)
            {
                return false;
            }

            var pendingItem = FindPendingItem(gameSession, itemInstanceId);

            if (pendingItem == null)
            {
                return false;
            }

            if (!inventoryService.TryAddItem(character.Inventory, pendingItem))
            {
                return false;
            }

            if (gameSession.TryRemovePendingLoot(
                    itemInstanceId,
                    out _))
            {
                return true;
            }

            inventoryService.TryRemoveItem(
                character.Inventory,
                itemInstanceId,
                out _);

            return false;
        }

        private static ItemInstance FindPendingItem(
            GameSession gameSession,
            string itemInstanceId)
        {
            foreach (var item in gameSession.PendingLoot)
            {
                if (item?.InstanceId == itemInstanceId)
                {
                    return item;
                }
            }

            return null;
        }
    }
}