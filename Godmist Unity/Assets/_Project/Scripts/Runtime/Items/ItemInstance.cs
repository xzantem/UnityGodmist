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
        public string CustomName = string.Empty;

        public virtual string DisplayName => !string.IsNullOrWhiteSpace(CustomName)
            ? CustomName
            : Definition?.DisplayName ?? "Przedmiot";

        public virtual int BaseValue => Math.Max(0, Definition?.BaseValue ?? 0);

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

        public virtual bool CanStackWith(ItemInstance other)
        {
            if (Definition == null || other?.Definition == null) return false;

            if (!Definition.IsStackable || !other.Definition.IsStackable) return false;

            return Definition.Id == other.Definition.Id
                && CurrentRarity == other.CurrentRarity
                && ItemLevel == other.ItemLevel;
        }
    }
}
