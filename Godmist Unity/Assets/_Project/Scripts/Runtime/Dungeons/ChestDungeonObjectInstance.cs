using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using Godmist.Runtime.Items;
using UnityEngine;

namespace Godmist.Runtime.Dungeons
{
    public class ChestDungeonObjectInstance : DungeonObjectInstance
    {
        private readonly List<ItemInstance> items = new();
        private bool isUnlocked;
        private int gold;

        public IReadOnlyList<ItemInstance> Items => items;
        public ChestLockType LockType { get; }
        public ItemDefinition RequiredKey { get; }
        public bool IsUnlocked => isUnlocked;
        public int Gold => gold;

        public ChestDungeonObjectInstance(
            string instanceId,
            ChestDungeonObjectDefinition definition,
            Vector2Int position,
            IEnumerable<ItemInstance> initialItems,
            int initialGold = 0)
            : base(instanceId, definition, position)
        {
            LockType = definition.LockType;
            RequiredKey = definition.RequiredKey;
            isUnlocked = LockType == ChestLockType.Unlocked;
            gold = Mathf.Max(0, initialGold);

            if (initialItems == null) return;

            foreach (var item in initialItems)
            {
                if (item != null)
                {
                    items.Add(item);
                }
            }
        }

        public bool TryUnlockWithKey(ItemDefinition keyDefinition)
        {
            if (isUnlocked
                || LockType == ChestLockType.Unlocked
                || RequiredKey == null
                || keyDefinition == null
                || keyDefinition.Id != RequiredKey.Id)
            {
                return isUnlocked;
            }

            isUnlocked = true;
            return true;
        }

        public bool TryUnlockWithLockpick()
        {
            if (isUnlocked) return true;
            if (LockType != ChestLockType.KeyOrLockpick) return false;

            isUnlocked = true;
            return true;
        }

        public bool TryRestoreUnlocked(bool unlocked)
        {
            if (!unlocked
                && LockType == ChestLockType.Unlocked)
            {
                return false;
            }

            isUnlocked = unlocked;
            return true;
        }

        public bool TryRestoreItems(IEnumerable<ItemInstance> restoredItems)
        {
            if (restoredItems == null) return false;

            items.Clear();

            foreach (var item in restoredItems)
            {
                if (item == null
                    || string.IsNullOrWhiteSpace(item.InstanceId)
                    || items.Exists(existing =>
                        existing.InstanceId == item.InstanceId))
                {
                    return false;
                }

                items.Add(item);
            }

            return true;
        }

        public bool TryRestoreGold(int restoredGold)
        {
            if (restoredGold < 0) return false;
            gold = restoredGold;
            return true;
        }

        public bool TryClaimGold(out int claimedGold)
        {
            claimedGold = 0;
            if (!isUnlocked || gold <= 0) return false;
            claimedGold = gold;
            gold = 0;
            return true;
        }

        public bool TryGetItem(
            string itemInstanceId,
            out ItemInstance item)
        {
            item = null;

            if (!isUnlocked || string.IsNullOrWhiteSpace(itemInstanceId))
            {
                return false;
            }

            foreach (var candidate in items)
            {
                if (candidate?.InstanceId == itemInstanceId)
                {
                    item = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool TryClaimItem(
            string itemInstanceId,
            out ItemInstance claimedItem)
        {
            claimedItem = null;

            if (!TryGetItem(itemInstanceId, out claimedItem))
            {
                return false;
            }

            items.Remove(claimedItem);
            return true;
        }

        public bool TryClaimItems(out List<ItemInstance> claimedItems)
        {
            claimedItems = new List<ItemInstance>();

            if (!isUnlocked) return false;

            foreach (var item in items)
            {
                if (item != null)
                {
                    claimedItems.Add(item);
                }
            }

            items.Clear();

            return true;
        }
    }
}
