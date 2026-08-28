using System;
using System.Collections.Generic;
using Godmist.Data.Items;

namespace Godmist.Runtime.Items
{
    [Serializable]
    public class LootBagItemInstance : ItemInstance
    {
        public List<ItemInstance> Contents = new();

        public LootBagItemDefinition LootBagDefinition =>
            Definition as LootBagItemDefinition;

        public bool IsEmpty => Contents == null || Contents.Count == 0;

        public LootBagItemInstance() {}

        public LootBagItemInstance(
            LootBagItemDefinition definition,
            string instanceId = null)
            : base(definition, instanceId)
        {
        }

        public bool TryRestoreContents(IEnumerable<ItemInstance> contents)
        {
            if (contents == null) return false;

            Contents ??= new List<ItemInstance>();
            Contents.Clear();

            foreach (var item in contents)
            {
                if (item == null
                    || string.IsNullOrWhiteSpace(item.InstanceId)
                    || Contents.Exists(
                        existing => existing.InstanceId == item.InstanceId))
                {
                    Contents.Clear();
                    return false;
                }

                Contents.Add(item);
            }

            return true;
        }

        public bool TryTakeContent(
            string itemInstanceId,
            out ItemInstance item)
        {
            item = null;

            if (string.IsNullOrWhiteSpace(itemInstanceId)
                || Contents == null)
            {
                return false;
            }

            var index = Contents.FindIndex(
                candidate => candidate?.InstanceId == itemInstanceId);

            if (index < 0) return false;

            item = Contents[index];
            Contents.RemoveAt(index);
            return true;
        }

        public override bool CanStackWith(ItemInstance other)
        {
            return false;
        }
    }
}
