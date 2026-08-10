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

            if (definition is EquipmentItemDefinition equipmentDefinition)
            {
                return new EquipmentItemInstance(
                    equipmentDefinition,
                    instanceId);
            }

            return new ItemInstance(definition, instanceId);
        }
    }
}