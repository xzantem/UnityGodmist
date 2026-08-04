using System;
using Godmist.Core.Enums;
using Godmist.Data.Items;

namespace Godmist.Runtime.Items
{
    [Serializable]
    public class ItemInstance
    {
        public string InstanceId = string.Empty;
        public ItemDefinition Definition;
        public RarityTier CurrentRarity;
        public int ItemLevel;

        public ItemInstance() {}

        public ItemInstance(ItemDefinition definition, string instanceId = null)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (string.IsNullOrWhiteSpace(definition.Id))
            {
                throw new ArgumentException(
                    "ItemDefinition musi miec niepuste Id.", nameof(definition));
            }

            InstanceId = string.IsNullOrWhiteSpace(instanceId)
                ? Guid.NewGuid().ToString("N")
                : instanceId;

            Definition = definition;
            CurrentRarity = definition.BaseRarity;
            ItemLevel = 0;
        }
    }
}