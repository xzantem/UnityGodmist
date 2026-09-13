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
        // The material of the first structural part determines the crafted name.
        // Keep the id on the instance so names remain stable after save/load.
        public string PrimaryMaterialId = string.Empty;
        public int CraftedBaseValue;
        // Payload before the mutable upgrade modifier is applied.
        public List<StatModifier> BaseStatModifiers = new();
        public List<StatModifier> StatModifiers = new();
        // Socketed items are owned by this equipment instance until the later
        // extraction service releases them back into an inventory.
        public List<GalduryteItemInstance> SocketedGaldurytes = new();
        public float UpgradeProgress;

        public int SocketCapacity => GetSocketCapacity(UpgradeProgress);

        public static int GetSocketCapacity(float progress) => Math.Clamp(
            (int)Math.Floor(progress * GameDefinesRuntime.GetInt(
                GameDefineIds.SocketCapacityAtFullUpgrade, 5) + .0001f),
            0,
            GameDefinesRuntime.GetInt(GameDefineIds.SocketCapacityAtFullUpgrade, 5));

        public EquipmentItemDefinition EquipmentDefinition =>
            Definition is UniqueEquipmentDefinition unique && unique.BaseDefinition != null
                ? unique.BaseDefinition
                : Definition as EquipmentItemDefinition;

        public override int BaseValue => CraftedBaseValue > 0
            ? CraftedBaseValue
            : base.BaseValue;

        public override string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(CustomName)) return CustomName;
                if (Definition is UniqueEquipmentDefinition || this is JewelryItemInstance)
                    return base.DisplayName;

                return CraftedEquipmentNameFormatter.Format(
                    base.DisplayName,
                    Quality,
                    PrimaryMaterialId,
                    PrimaryPartDisplayName);
            }
        }

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
