using System;
using System.Linq;
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

        public bool UsesEquipmentProgression => this is not JewelryItemInstance
            && Definition?.ItemType is ItemType.Weapon or ItemType.Armor;

        public static float RarityMultiplier(RarityTier rarity) => rarity switch
        {
            RarityTier.Trash => .7f, RarityTier.Broken => .8f, RarityTier.Damaged => .9f,
            RarityTier.Common => 1f, RarityTier.Uncommon => 1.1f, RarityTier.Rare => 1.25f,
            RarityTier.Ancient => 1.45f, RarityTier.Legendary => 1.75f,
            RarityTier.Mythic => 2.15f, RarityTier.Godly => 2.7f, _ => 1f
        };

        public static float QualityValueMultiplier(EquipmentQuality quality) => quality switch
        {
            EquipmentQuality.Poor => .5f,
            EquipmentQuality.Excellent => 2f,
            EquipmentQuality.Masterpiece => 4f,
            _ => 1f
        };

        public float CurrentEquipmentValue(float materialValue) => Math.Max(0, materialValue)
            * QualityValueMultiplier(Quality) * RarityMultiplier(CurrentRarity)
            * (1f + 4f * Math.Clamp(UpgradeProgress, 0f, 1f));

        public override int BaseValue
        {
            get
            {
                var value = CraftedBaseValue > 0 ? CraftedBaseValue : base.BaseValue;
                return UsesEquipmentProgression
                    ? (int)Math.Min(int.MaxValue, Math.Ceiling((decimal)CurrentEquipmentValue(value)))
                    : value;
            }
        }

        // List order is the persistent socket order; overflow remains owned but inactive.
        public bool IsSocketActive(int index) => index >= 0 && index < SocketCapacity;

        public void RecalculateStatModifiers()
        {
            if (!UsesEquipmentProgression) return;
            BaseStatModifiers ??= new();
            if (BaseStatModifiers.Count == 0 && StatModifiers?.Count > 0)
                throw new InvalidOperationException("Equipment stat baseline is missing. Legacy saves must be migrated before recalculation.");
            StatModifiers = BaseStatModifiers.Where(x => x != null)
                .Select(x => CopyStat(x, GetStatProgressionMultiplier(x.StatType))).ToList();
        }

        // Shared pure query: presentation must use the same progression as the item payload.
        public float GetStatProgressionMultiplier(CharacterStatType stat)
        {
            if (!UsesEquipmentProgression) return 1f;
            var rarity = RarityMultiplier(CurrentRarity);
            return IsPrimaryStat(stat)
                ? 1f + (Definition.ItemType == ItemType.Weapon ? .5f : 1.5f) * Math.Clamp(UpgradeProgress, 0f, 1f)
                : stat is CharacterStatType.WeaponAccuracy or CharacterStatType.Dodge
                    ? 1f + .3f * (rarity - 1f)
                : stat is CharacterStatType.MaxResource or CharacterStatType.ResourceRegeneration
                    ? 1f + .6f * (rarity - 1f)
                : stat == CharacterStatType.CritDamage
                    ? 1f + .5f * (rarity - 1f)
                : stat is CharacterStatType.MaxHealth or CharacterStatType.CritChance
                    ? rarity : 1f;
        }

        public bool IsPrimaryStat(CharacterStatType stat) => Definition.ItemType == ItemType.Weapon
            ? stat is CharacterStatType.MinAttack or CharacterStatType.MaxAttack
            : stat is CharacterStatType.PhysicalDefense or CharacterStatType.MagicalDefense;

        private static StatModifier CopyStat(StatModifier modifier, float multiplier) => new()
        {
            StatType = modifier.StatType, Operation = modifier.Operation,
            Stacking = modifier.Stacking, Value = modifier.Value * multiplier
        };

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
