using System;
using Godmist.Data.Items;
using Godmist.Runtime.Items;

namespace Godmist.Systems.Items
{
    public class ItemInstanceFactory
    {
        public ItemInstance Create(ItemDefinition definition)
        {
            return Create(definition, null);
        }

        public ItemInstance Create(
            ItemDefinition definition,
            string instanceId)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (definition is JewelryItemDefinition jewelryDefinition)
            {
                return new JewelryItemInstance(jewelryDefinition, instanceId);
            }

            if (definition is EquipmentItemDefinition equipmentDefinition)
            {
                return new EquipmentItemInstance(
                    equipmentDefinition,
                    instanceId);
            }

            if (definition is LootBagItemDefinition lootBagDefinition)
            {
                return new LootBagItemInstance(
                    lootBagDefinition,
                    instanceId);
            }

            if (definition is ConsumableItemDefinition consumableDefinition)
            {
                return new ConsumableItemInstance(consumableDefinition, instanceId);
            }

            return new ItemInstance(definition, instanceId);
        }
    }
}
