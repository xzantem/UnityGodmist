using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Items;
using Godmist.Systems.Characters;

namespace Godmist.Systems.Items
{
    public class EquipmentService
    {
        private readonly InventoryService inventoryService = new();
        private readonly CharacterStatPipelineSystem statPipeline = new();

        public bool TryEquip(CharacterInstance character, string itemInstanceId)
        {
            if (character?.Inventory?.Items == null || character.Equipment == null
                || string.IsNullOrWhiteSpace(itemInstanceId)) return false;

            var item = FindEquipmentItem(character.Inventory, itemInstanceId);

            if (item == null) return false;
            if (!MeetsRequirements(character, item)) return false;
            if (!IsCompatibleWithSlot(item)) return false;

            var previouslyEquipped = character.Equipment.GetItem(item.EquipmentSlot);

            if (!CanStoreReplacedItem(character.Inventory, item, previouslyEquipped)) return false;
            if (!inventoryService.TryRemoveItem(character.Inventory, item.InstanceId, out _)) return false;

            character.Equipment.SetItem(item.EquipmentSlot, item);

            if (previouslyEquipped != null
                && !inventoryService.TryAddItem(character.Inventory, previouslyEquipped))
            {
                character.Equipment.SetItem(item.EquipmentSlot, previouslyEquipped);
                inventoryService.TryAddItem(character.Inventory, item);
                return false;
            }

            statPipeline.Refresh(character);
            return true;
        }

        public bool TryUnequip(CharacterInstance character, EquipmentSlot slot)
        {
            if (character?.Inventory == null || character.Equipment == null) return false;

            var equippedItem = character.Equipment.GetItem(slot);

            if (equippedItem == null) return false;

            if (!inventoryService.TryAddItem(character.Inventory, equippedItem)) return false;

            character.Equipment.SetItem(slot, null);

            statPipeline.Refresh(character);

            return true;
        }

        private static EquipmentItemInstance FindEquipmentItem(Inventory inventory, 
            string itemInstanceId)
        {
            foreach (var item in inventory.Items)
            {
                if (item?.InstanceId != itemInstanceId) continue;

                return item as EquipmentItemInstance;
            }

            return null;
        }

        private static bool MeetsRequirements(CharacterInstance character, 
            EquipmentItemInstance item)
        {
            if (item.RequiredLevel > character.Level) return false;

            if (string.IsNullOrWhiteSpace(item.RequiredClassId)) return true;

            return character.ClassData != null
                && character.ClassData.Id == item.RequiredClassId;
        }

        private static bool IsCompatibleWithSlot(EquipmentItemInstance item)
        {
            if (item?.EquipmentDefinition == null) return false;

            return item.EquipmentSlot switch
            {
                EquipmentSlot.Weapon => item.Definition.ItemType == ItemType.Weapon,

                EquipmentSlot.Armor => item.Definition.ItemType == ItemType.Armor,

                EquipmentSlot.Necklace or EquipmentSlot.Ring or EquipmentSlot.Bracelet
                    or EquipmentSlot.Earrings => item.Definition.ItemType == ItemType.Jewelry,

                _ => false
            };
        }

        private bool CanStoreReplacedItem(Inventory inventory, EquipmentItemInstance itemToEquip,
            EquipmentItemInstance replacedItem)
        {
            if (replacedItem == null) return true;
            if (!inventory.MaxWeight.HasValue) return true;

            var weightAfterRemovingNewItem = inventoryService.GetCurrentWeight(inventory)
                - itemToEquip.Definition.Weight;

            var weightAfterSwap = weightAfterRemovingNewItem + replacedItem.Definition.Weight;

            return weightAfterSwap <= inventory.MaxWeight.Value;
        }
    }
}