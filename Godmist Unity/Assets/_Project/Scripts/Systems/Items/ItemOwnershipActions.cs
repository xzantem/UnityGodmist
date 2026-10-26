using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;

namespace Godmist.Systems.Items
{
    /// <summary>Shared validation for inventory and trade context-menu ownership actions.</summary>
    public sealed class ItemOwnershipActions
    {
        private readonly InventoryService inventory = new();
        private readonly EquipmentService equipment = new();

        public static bool Owns(CharacterInstance owner, ItemInstance item) => item != null
            && (owner?.Inventory?.Items.Contains(item) == true
                || owner?.Equipment?.GetEquippedItems().Contains(item) == true);

        public static List<string> CaptureStack(CharacterInstance owner, ItemInstance item) =>
            owner?.Inventory?.Items.Contains(item) == true
                ? owner.Inventory.Items.Where(candidate => ReferenceEquals(candidate, item) || item.CanStackWith(candidate))
                    .Select(candidate => candidate.InstanceId).ToList()
                : Owns(owner, item) ? new List<string> { item.InstanceId } : new List<string>();

        public static bool CanReceive(GameSession session, CharacterInstance recipient, bool inCity) =>
            session != null && recipient != null && session.CharactersById.TryGetValue(recipient.CharacterId, out var registered)
            && ReferenceEquals(registered, recipient)
            && recipient.AvailabilityState == CharacterAvailabilityState.Available
            && recipient.LifeState != CharacterLifeState.Dead
            && (session.PlayerParty.IsActiveMember(recipient.CharacterId)
                || inCity && session.PlayerParty.BenchMemberIds.Contains(recipient.CharacterId))
            && !session.IsCharacterInCombat(recipient.CharacterId);

        public bool TryTransfer(GameSession session, CharacterInstance owner, ItemInstance expectedItem,
            IReadOnlyList<string> capturedIds, int count, CharacterInstance recipient, bool toStash,
            bool inCity, out string reason)
        {
            reason = "Przedmiot nie jest już dostępny.";
            if (session == null || !session.CharactersById.TryGetValue(owner?.CharacterId ?? "", out var registered)
                || !ReferenceEquals(registered, owner) || !Owns(owner, expectedItem)
                || capturedIds == null || count < 1 || count > capturedIds.Count) return false;
            if (session.IsCharacterInCombat(owner.CharacterId))
            { reason = "Przedmiotów nie można przekazywać podczas walki."; return false; }
            if (toStash && !inCity)
            { reason = "Schowek jest dostępny tylko w mieście."; return false; }
            if (!toStash && (recipient == owner || !CanReceive(session, recipient, inCity)))
            { reason = "Ten towarzysz nie może teraz przyjąć przedmiotu."; return false; }
            var destination = toStash ? session.Stash : recipient.Inventory;
            var ids = capturedIds.Take(count).ToArray();
            if (owner.Inventory.Items.Contains(expectedItem))
            {
                if (ids.Any(id => !owner.Inventory.Items.Any(item => item?.InstanceId == id
                    && (ReferenceEquals(item, expectedItem) || expectedItem.CanStackWith(item))))) return false;
                if (!inventory.TryTransferItems(owner.Inventory, destination, ids))
                { reason = "Brak miejsca w plecaku odbiorcy. Sprawdź limit wagi."; return false; }
            }
            else if (count != 1 || expectedItem is not EquipmentItemInstance equipped
                || ids[0] != equipped.InstanceId
                || !equipment.TryTransferEquippedItem(owner, equipped.EquipmentSlot, equipped.InstanceId, destination))
            { reason = "Brak miejsca w plecaku odbiorcy."; return false; }
            reason = string.Empty;
            return true;
        }

        public bool TrySetSaleProtection(CharacterInstance owner, ItemInstance expectedItem,
            IReadOnlyList<string> ids, bool protect)
        {
            if (!Owns(owner, expectedItem) || ids == null || ids.Count == 0) return false;
            var owned = owner.Inventory.Items.Concat(owner.Equipment.GetEquippedItems());
            var items = ids.Select(id => owned.FirstOrDefault(item => item?.InstanceId == id)).ToList();
            if (items.Any(item => item == null || !ReferenceEquals(item, expectedItem) && !expectedItem.CanStackWith(item))) return false;
            foreach (var item in items) item.IsSaleProtected = protect;
            return true;
        }
    }
}
