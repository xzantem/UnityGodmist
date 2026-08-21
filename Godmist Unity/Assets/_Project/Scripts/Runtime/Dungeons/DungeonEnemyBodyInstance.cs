using System;
using System.Collections.Generic;
using Godmist.Runtime.Items;
using UnityEngine;

namespace Godmist.Runtime.Dungeons
{
    /// <summary>
    /// Persistent loot left at the position of a defeated dungeon enemy.
    /// </summary>
    public class DungeonEnemyBodyInstance
    {
        private readonly List<ItemInstance> items = new();

        public string InstanceId { get; }
        public string EnemyInstanceId { get; }
        public Vector2Int Position { get; }
        public int Gold { get; private set; }
        public IReadOnlyList<ItemInstance> Items => items;
        public bool IsEmpty => Gold <= 0 && items.Count == 0;

        public DungeonEnemyBodyInstance(
            string instanceId,
            string enemyInstanceId,
            Vector2Int position,
            int gold,
            IEnumerable<ItemInstance> initialItems)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                throw new ArgumentException(
                    "Enemy body InstanceId cannot be empty.",
                    nameof(instanceId));
            }

            if (string.IsNullOrWhiteSpace(enemyInstanceId))
            {
                throw new ArgumentException(
                    "Enemy body EnemyInstanceId cannot be empty.",
                    nameof(enemyInstanceId));
            }

            if (gold < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(gold),
                    "Enemy body gold cannot be negative.");
            }

            InstanceId = instanceId;
            EnemyInstanceId = enemyInstanceId;
            Position = position;
            Gold = gold;

            if (initialItems == null) return;

            foreach (var item in initialItems)
            {
                if (item != null) items.Add(item);
            }
        }

        public bool TryGetItem(
            string itemInstanceId,
            out ItemInstance item)
        {
            item = null;

            if (string.IsNullOrWhiteSpace(itemInstanceId)) return false;

            foreach (var candidate in items)
            {
                if (candidate?.InstanceId != itemInstanceId) continue;

                item = candidate;
                return true;
            }

            return false;
        }

        public bool TryClaimItem(
            string itemInstanceId,
            out ItemInstance claimedItem)
        {
            claimedItem = null;

            if (string.IsNullOrWhiteSpace(itemInstanceId)) return false;

            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];

                if (item?.InstanceId != itemInstanceId) continue;

                claimedItem = item;
                items.RemoveAt(index);
                return true;
            }

            return false;
        }

        public bool TryClaimGold(out int claimedGold)
        {
            claimedGold = 0;

            if (Gold <= 0) return false;

            claimedGold = Gold;
            Gold = 0;
            return true;
        }
    }
}
