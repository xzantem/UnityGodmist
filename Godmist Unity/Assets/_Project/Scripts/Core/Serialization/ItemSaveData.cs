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
        public bool IsConsumable;
        public int CurrentCharges;
        public int BlastRange = 5;
        public int BlastAreaSize = 3;
        public int CatalystDurationModifier;
        public float CatalystEffectMultiplier = 1f;
        public int CatalystChargeBonus;
        public string CatalystDisplayName;
        public int ConsumableCraftedBaseValue;
        public List<ConsumableEffectSaveData> ConsumableEffects = new();
        public List<string> LootBagContentItemInstanceIds = new();
        public string RequiredClassId = string.Empty;
        public int RequiredLevel;
        public EquipmentQuality Quality = EquipmentQuality.Normal;
        public List<string> PartIds = new();
        public string PrimaryPartDisplayName = string.Empty;
        public List<StatModifierSaveData> BaseStatModifiers = new();
        public List<StatModifierSaveData> StatModifiers = new();
        public float UpgradeProgress;
        public string JewelryMetalId = string.Empty;
        public string JewelryMetalDisplayName = string.Empty;
        public string JewelryGemId = string.Empty;
        public string JewelryGemDisplayName = string.Empty;
        public int JewelryCraftedBaseValue;
    }

    [Serializable]
    public class ConsumableEffectSaveData
    {
        public ConsumableRuntimeEffectType EffectType;
        public float Value;
        public int DurationTurns;
        public float Chance = 1f;
        public int TargetModeIndex;
        public string StatusId = string.Empty;
        public ResistanceType ResistanceType = ResistanceType.Weakening;
        public TargetType TargetType = TargetType.Caster;
        public TargetingType TargetingType = TargetingType.Allies;
    }
}
