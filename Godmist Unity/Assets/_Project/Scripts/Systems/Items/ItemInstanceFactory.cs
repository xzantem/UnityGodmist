using System;
using Godmist.Data.Items;
using Godmist.Runtime.Items;

namespace Godmist.Systems.Items
{
    public class ItemInstanceFactory
    {
        public ItemInstance Create(ItemDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (definition is EquipmentItemDefinition equipmentDefinition)
            {
                return new EquipmentItemInstance(equipmentDefinition);
            }

            return new ItemInstance(definition);
        }
    }
}