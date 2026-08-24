using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;
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

        public bool TryClaimDungeonBodyItem(
            GameSession gameSession,
            string bodyInstanceId,
            string itemInstanceId)
        {
            if (!TryGetActiveLeaderCharacterId(gameSession, out var characterId))
            {
                return false;
            }

            return TryClaimDungeonBodyItem(
                gameSession,
                bodyInstanceId,
                characterId,
                itemInstanceId);
        }

        public bool TryClaimDungeonBodyItem(
            GameSession gameSession,
            string bodyInstanceId,
            string characterId,
            string itemInstanceId)
        {
            if (!TryGetCharacterInventory(
                    gameSession,
                    characterId,
                    out var characterInventory))
            {
                return false;
            }

            if (!TryGetActiveFloorBody(
                    gameSession,
                    bodyInstanceId,
                    out var floor,
                    out var body)
                || !body.TryGetItem(itemInstanceId, out var item))
            {
                return false;
            }

            if (!inventoryService.TryAddItem(characterInventory, item))
            {
                return false;
            }

            if (!body.TryClaimItem(itemInstanceId, out _))
            {
                inventoryService.TryRemoveItem(
                    characterInventory,
                    itemInstanceId,
                    out _);

                return false;
            }

            RemoveBodyIfEmpty(floor, body);
            return true;
        }

        public bool TryClaimBodyItem(
            GameSession gameSession,
            string bodyInstanceId,
            string itemInstanceId)
        {
            return TryClaimDungeonBodyItem(
                gameSession,
                bodyInstanceId,
                itemInstanceId);
        }

        public bool TryClaimBodyItem(
            GameSession gameSession,
            string bodyInstanceId,
            string characterId,
            string itemInstanceId)
        {
            return TryClaimDungeonBodyItem(
                gameSession,
                bodyInstanceId,
                characterId,
                itemInstanceId);
        }

        public bool TryClaimNextDungeonBodyItem(
            GameSession gameSession,
            string bodyInstanceId,
            out string claimedItemInstanceId)
        {
            claimedItemInstanceId = string.Empty;

            if (!TryGetActiveFloorBody(
                    gameSession,
                    bodyInstanceId,
                    out var floor,
                    out var body)
                || body.Items.Count == 0)
            {
                return false;
            }

            var item = body.Items[0];

            if (item == null
                || !TryClaimDungeonBodyItem(
                    gameSession,
                    bodyInstanceId,
                    item.InstanceId))
            {
                return false;
            }

            claimedItemInstanceId = item.InstanceId;
            return true;
        }

        public bool TryClaimNextBodyItem(
            GameSession gameSession,
            string bodyInstanceId,
            out string claimedItemInstanceId)
        {
            return TryClaimNextDungeonBodyItem(
                gameSession,
                bodyInstanceId,
                out claimedItemInstanceId);
        }

        public bool TryClaimDungeonBodyGold(
            GameSession gameSession,
            string bodyInstanceId,
            out int claimedGold)
        {
            claimedGold = 0;

            if (!TryGetActiveFloorBody(
                    gameSession,
                    bodyInstanceId,
                    out var floor,
                    out var body)
                || body.Gold <= 0)
            {
                return false;
            }

            if (!gameSession.TryAddGold(body.Gold))
            {
                return false;
            }

            if (!body.TryClaimGold(out claimedGold))
            {
                gameSession.TrySpendGold(body.Gold);
                claimedGold = 0;
                return false;
            }

            RemoveBodyIfEmpty(floor, body);
            return true;
        }

        public bool TryClaimBodyGold(
            GameSession gameSession,
            string bodyInstanceId,
            out int claimedGold)
        {
            return TryClaimDungeonBodyGold(
                gameSession,
                bodyInstanceId,
                out claimedGold);
        }

        private static bool TryGetActiveLeaderInventory(
            GameSession gameSession,
            out Inventory inventory)
        {
            inventory = null;

            if (!TryGetActiveLeaderCharacterId(gameSession, out var characterId))
            {
                return false;
            }

            return TryGetCharacterInventory(
                gameSession,
                characterId,
                out inventory);
        }

        private static bool TryGetActiveLeaderCharacterId(
            GameSession gameSession,
            out string characterId)
        {
            characterId = string.Empty;

            if (gameSession == null
                || gameSession.CurrentMode != GameModeState.Exploration
                || gameSession.ActiveCombat != null
                || string.IsNullOrWhiteSpace(
                    gameSession.PlayerParty.ActiveLeaderId))
            {
                return false;
            }

            characterId = gameSession.PlayerParty.ActiveLeaderId;
            return true;
        }

        private static bool TryGetCharacterInventory(
            GameSession gameSession,
            string characterId,
            out Inventory inventory)
        {
            inventory = null;

            if (gameSession == null
                || gameSession.CurrentMode != GameModeState.Exploration
                || gameSession.ActiveCombat != null
                || string.IsNullOrWhiteSpace(characterId)
                || !gameSession.PlayerParty.IsActiveMember(characterId)
                || !gameSession.CharactersById.TryGetValue(
                    characterId,
                    out var character)
                || character == null
                || character.LifeState == CharacterLifeState.Dead)
            {
                return false;
            }

            inventory = character.Inventory;
            return inventory != null;
        }

        private static bool TryGetActiveFloorBody(
            GameSession gameSession,
            string bodyInstanceId,
            out DungeonFloorInstance floor,
            out DungeonEnemyBodyInstance body)
        {
            floor = null;
            body = null;

            if (gameSession?.ActiveDungeon == null
                || gameSession.CurrentMode != GameModeState.Exploration
                || gameSession.ActiveCombat != null
                || !gameSession.ActiveDungeon.TryGetCurrentFloor(out floor)
                || !floor.TryGetEnemyBody(bodyInstanceId, out body))
            {
                floor = null;
                body = null;
                return false;
            }

            return true;
        }

        private static void RemoveBodyIfEmpty(
            DungeonFloorInstance floor,
            DungeonEnemyBodyInstance body)
        {
            if (floor == null || body == null || !body.IsEmpty) return;

            floor.TryRemoveEnemyBody(body.InstanceId);
        }
    }
}
