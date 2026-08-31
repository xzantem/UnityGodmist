using System;
using System.Collections.Generic;
using Godmist.Core.Enums;

namespace Godmist.Core.Serialization
{
    [Serializable]
    public class ItemSaveData
    {
        public string InstanceId = string.Empty;
        public string ItemDefinitionId = string.Empty;
        public RarityTier CurrentRarity;
        public int ItemLevel;
        public string CustomName = string.Empty;

        public bool IsEquipment;
        public bool IsLootBag;
        public List<string> LootBagContentItemInstanceIds = new();
        public string RequiredClassId = string.Empty;
        public int RequiredLevel;
        public EquipmentQuality Quality = EquipmentQuality.Normal;
        public List<string> PartIds = new();
        public List<StatModifierSaveData> StatModifiers = new();
        public string JewelryMetalId = string.Empty;
        public string JewelryMetalDisplayName = string.Empty;
        public string JewelryGemId = string.Empty;
        public string JewelryGemDisplayName = string.Empty;
        public string JewelryGemDescription = string.Empty;
        public JewelryPassiveType JewelryPassiveType;
        public float JewelryPassiveValue;
        public DungeonTheme JewelryPassiveDungeonTheme;
        public ResistanceType JewelryResistanceType;
        public float JewelryResistanceValue;
    }
}
