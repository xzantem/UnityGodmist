using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Items;

namespace Godmist.Runtime.Items
{
    [Serializable]
    public class EquipmentItemInstance : ItemInstance
    {
        public string RequiredClassId = string.Empty;
        public int RequiredLevel = 1;
        public List<StatModifier> StatModifiers = new();

        public EquipmentItemDefinition EquipmentDefinition =>
            Definition as EquipmentItemDefinition;

        public EquipmentSlot EquipmentSlot =>
            EquipmentDefinition != null
                ? EquipmentDefinition.EquipmentSlot
                : throw new InvalidOperationException(
                    "EquipmentItemInstance wymaga EquipmentItemDefinition.");

        public EquipmentItemInstance() {}

        public EquipmentItemInstance(
            EquipmentItemDefinition definition,
            string instanceId = null)
            : base(definition, instanceId)
        {
            RequiredLevel = 1;
        }
    }
}