using System.Collections.Generic;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Items;
using UnityEngine;

namespace Godmist.Runtime.Dungeons
{
    public class ChestDungeonObjectInstance : DungeonObjectInstance
    {
        private readonly List<ItemInstance> items = new();

        public IReadOnlyList<ItemInstance> Items => items;

        public ChestDungeonObjectInstance(
            string instanceId,
            ChestDungeonObjectDefinition definition,
            Vector2Int position,
            IEnumerable<ItemInstance> initialItems)
            : base(instanceId, definition, position)
        {
            if (initialItems == null) return;

            foreach (var item in initialItems)
            {
                if (item != null)
                {
                    items.Add(item);
                }
            }
        }

        public bool TryClaimItems(out List<ItemInstance> claimedItems)
        {
            claimedItems = new List<ItemInstance>();

            if (!IsActive) return false;

            foreach (var item in items)
            {
                if (item != null)
                {
                    claimedItems.Add(item);
                }
            }

            items.Clear();
            TryDeactivate();

            return true;
        }
    }
}