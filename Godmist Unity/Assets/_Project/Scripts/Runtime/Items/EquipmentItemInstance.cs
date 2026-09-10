using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Configuration;
using Godmist.Data.Items;

namespace Godmist.Runtime.Items
{
    [Serializable]
    public class EquipmentItemInstance : ItemInstance
    {
        public string RequiredClassId = string.Empty;
        public int RequiredLevel = 1;
        public EquipmentQuality Quality = EquipmentQuality.Normal;
        public List<string> PartIds = new();
        public string PrimaryPartDisplayName = string.Empty;
        // Payload before the mutable upgrade modifier is applied.
        public List<StatModifier> BaseStatModifiers = new();
        public List<StatModifier> StatModifiers = new();
        public float UpgradeProgress;

        public int SocketCapacity => Math.Clamp(
            (int)Math.Floor(UpgradeProgress * GameDefinesRuntime.GetInt(
                GameDefineIds.SocketCapacityAtFullUpgrade, 5) + .0001f),
            0,
            GameDefinesRuntime.GetInt(GameDefineIds.SocketCapacityAtFullUpgrade, 5));

        public EquipmentItemDefinition EquipmentDefinition =>
            Definition is UniqueEquipmentDefinition unique && unique.BaseDefinition != null
                ? unique.BaseDefinition
                : Definition as EquipmentItemDefinition;

        public override string DisplayName => !string.IsNullOrWhiteSpace(CustomName)
            ? CustomName
            : Definition is UniqueEquipmentDefinition
                ? base.DisplayName
                : !string.IsNullOrWhiteSpace(PrimaryPartDisplayName)
                    ? PrimaryPartDisplayName
                    : base.DisplayName;

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
            RequiredClassId = definition.RequiredClass?.Id ?? string.Empty;
            RequiredLevel = 1;
        }
    }
}
