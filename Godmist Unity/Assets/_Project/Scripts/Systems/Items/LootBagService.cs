using Godmist.Core.Enums;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;

namespace Godmist.Systems.Items
{
    public class LootBagService
    {
        private readonly InventoryService inventoryService = new();

        public bool TryOpenLootBag(
            GameSession gameSession,
            string characterId,
            string bagInstanceId,
            out int openedItemCount)
        {
            openedItemCount = 0;

            if (gameSession == null
                || gameSession.ActiveCombat != null
                || string.IsNullOrWhiteSpace(characterId)
                || string.IsNullOrWhiteSpace(bagInstanceId)
                || !gameSession.PlayerParty.IsActiveMember(characterId)
                || !gameSession.CharactersById.TryGetValue(
                    characterId,
                    out var character)
                || character == null
                || character.LifeState == CharacterLifeState.Dead
                || character.Inventory == null)
            {
                return false;
            }

            LootBagItemInstance bag = null;

            foreach (var item in character.Inventory.Items)
            {
                if (item?.InstanceId == bagInstanceId)
                {
                    bag = item as LootBagItemInstance;
                    break;
                }
            }

            if (bag == null || bag.Contents == null || bag.IsEmpty)
            {
                return false;
            }

            for (var index = bag.Contents.Count - 1; index >= 0; index--)
            {
                var item = bag.Contents[index];

                if (item == null
                    || !inventoryService.TryAddItem(
                        character.Inventory,
                        item))
                {
                    continue;
                }

                bag.Contents.RemoveAt(index);
                openedItemCount++;
            }

            if (openedItemCount > 0
                && bag.IsEmpty)
            {
                inventoryService.TryRemoveItem(
                    character.Inventory,
                    bag.InstanceId,
                    out _);
            }

            return openedItemCount > 0;
        }
    }
}
