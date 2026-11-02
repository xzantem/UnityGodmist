using Godmist.Runtime.Items;
using System.Collections.Generic;
using System.Linq;
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

        public bool CanAddItem(Inventory inventory, ItemInstance item)
        {
            if (inventory?.Items == null || item?.Definition == null
                || string.IsNullOrWhiteSpace(item.InstanceId)
                || ContainsInstanceId(inventory, item.InstanceId)) return false;
            return !inventory.MaxWeight.HasValue
                || GetCurrentWeight(inventory) + Mathf.Max(0f, item.Definition.Weight) <= inventory.MaxWeight.Value;
        }

        public bool TryAddItem(Inventory inventory, ItemInstance item)
        {
            if (!CanAddItem(inventory, item)) return false;
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

        // Preview and execution resolve the same complete selection without partial mutations.
        public bool TryPreviewTransferItems(Inventory source, Inventory destination,
            IReadOnlyList<string> ids, out float? resultingWeight)
        {
            resultingWeight = null;
            if (!TryResolveTransferItems(source, destination, ids, out var items)) return false;
            resultingWeight = TransferResultWeight(destination, items);
            return HasWeightCapacity(destination, resultingWeight.Value);
        }

        public bool TryTransferItems(Inventory source, Inventory destination, IReadOnlyList<string> ids)
        {
            if (!TryResolveTransferItems(source, destination, ids, out var items)) return false;
            if (!HasWeightCapacity(destination, TransferResultWeight(destination, items))) return false;
            foreach (var item in items) source.Items.Remove(item);
            destination.Items.AddRange(items);
            return true;
        }

        private float TransferResultWeight(Inventory destination, List<ItemInstance> items) =>
            GetCurrentWeight(destination) + items.Sum(item => Mathf.Max(0, item.Definition.Weight));
        private static bool HasWeightCapacity(Inventory destination, float weight) =>
            !destination.MaxWeight.HasValue || !(weight > destination.MaxWeight.Value);

        private static bool TryResolveTransferItems(Inventory source, Inventory destination,
            IReadOnlyList<string> ids, out List<ItemInstance> items)
        {
            items = null;
            if (source?.Items == null || destination?.Items == null || source == destination
                || ids == null || ids.Count == 0 || ids.Distinct().Count() != ids.Count) return false;
            items = ids.Select(id => source.Items.FirstOrDefault(item => item?.InstanceId == id)).ToList();
            return !items.Any(item => item?.Definition == null || string.IsNullOrWhiteSpace(item.InstanceId)
                || ContainsInstanceId(destination, item.InstanceId));
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
