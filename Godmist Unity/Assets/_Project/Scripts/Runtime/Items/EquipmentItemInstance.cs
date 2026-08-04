using System;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using System.Collections.Generic;
using Godmist.Core.Types;

namespace Godmist.Runtime.Items
{
    [Serializable]
    public class EquipmentItemInstance : ItemInstance
    {
        public EquipmentSlot EquipmentSlot;
        public string RequiredClassId = string.Empty;
        public int RequiredLevel = 1;

        public List<StatModifier> StatModifiers = new();

        public EquipmentItemInstance() {}

        public EquipmentItemInstance(
            ItemDefinition definition,
            EquipmentSlot equipmentSlot,
            string instanceId = null)
            : base(definition, instanceId)
        {
            EquipmentSlot = equipmentSlot;
            RequiredLevel = 1;
        }
    }
}