using Godmist.Runtime.Items;
using UnityEngine;

namespace Godmist.Systems.Items
{
    public class InventoryService
    {
        public float GetCurrentWeight(Inventory inventory)
        {
            if (inventory?.Items == null) return 0f;

            var currentWeight = 0f;

            foreach (var item in inventory.Items)
            {
                if (item?.Definition == null) continue;

                currentWeight += Mathf.Max(0f, item.Definition.Weight);
            }

            return currentWeight;
        }

        public bool TryAddItem(Inventory inventory, ItemInstance item)
        {
            if (inventory?.Items == null || item?.Definition == null
                || string.IsNullOrWhiteSpace(item.InstanceId))
            {
                return false;
            }

            if (ContainsInstanceId(inventory, item.InstanceId)) return false;

            var itemWeight = Mathf.Max(0f, item.Definition.Weight);
            var currentWeight = GetCurrentWeight(inventory);

            if (inventory.MaxWeight.HasValue
                && currentWeight + itemWeight > inventory.MaxWeight.Value)
            {
                return false;
            }

            inventory.Items.Add(item);
            return true;
        }

        public bool TryRemoveItem(
            Inventory inventory,
            string instanceId,
            out ItemInstance removedItem)
        {
            removedItem = null;

            if (inventory?.Items == null || string.IsNullOrWhiteSpace(instanceId))
            {
                return false;
            }

            for (var index = 0; index < inventory.Items.Count; index++)
            {
                var item = inventory.Items[index];

                if (item?.InstanceId != instanceId) continue;

                removedItem = item;
                inventory.Items.RemoveAt(index);
                return true;
            }

            return false;
        }

        public bool TryTransferItem(
            Inventory sourceInventory,
            Inventory destinationInventory,
            string instanceId)
        {
            if (sourceInventory?.Items == null
                || destinationInventory?.Items == null
                || sourceInventory == destinationInventory
                || string.IsNullOrWhiteSpace(instanceId))
            {
                return false;
            }

            ItemInstance item = null;
            foreach (var candidate in sourceInventory.Items)
            {
                if (candidate?.InstanceId == instanceId)
                {
                    item = candidate;
                    break;
                }
            }

            if (item?.Definition == null || ContainsInstanceId(destinationInventory, instanceId))
            {
                return false;
            }

            var itemWeight = Mathf.Max(0f, item.Definition.Weight);
            if (destinationInventory.MaxWeight.HasValue
                && GetCurrentWeight(destinationInventory) + itemWeight > destinationInventory.MaxWeight.Value)
            {
                return false;
            }

            if (!TryRemoveItem(sourceInventory, instanceId, out var removedItem))
            {
                return false;
            }

            if (TryAddItem(destinationInventory, removedItem))
            {
                return true;
            }

            sourceInventory.Items.Add(removedItem);
            return false;
        }

        private static bool ContainsInstanceId(Inventory inventory, string instanceId)
        {
            foreach (var item in inventory.Items)
            {
                if (item?.InstanceId == instanceId) return true;
            }

            return false;
        }
    }
}
