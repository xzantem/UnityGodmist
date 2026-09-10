using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using UnityEngine;

namespace Godmist.Data.Items
{
    [Serializable]
    public enum JewelryModifierTarget
    {
        Stat = 0,
        Resistance = 1,
        GameplayVariable = 2
    }

    /// <summary>
    /// A persistent modifier granted by one jewelry crafting ingredient.
    /// Jewelry cannot author generic passives, triggers, flags, or durations.
    /// </summary>
    [Serializable]
    public sealed class JewelryModifierData
    {
        [field: SerializeField] public JewelryModifierTarget Target { get; private set; }
        [field: SerializeField] public CharacterStatType StatType { get; private set; }
        [field: SerializeField] public ResistanceType ResistanceType { get; private set; }
        [field: SerializeField] public GameplayVariableType GameplayVariable { get; private set; }
        [field: SerializeField] public string ReferenceId { get; private set; } = string.Empty;
        [field: SerializeField] public StatModifierOperation Operation { get; private set; } =
            StatModifierOperation.FlatBase;
        [field: SerializeField] public float Value { get; private set; }
    }

    [Serializable]
    public class JewelryMetalData
    {
        [field: SerializeField] public ItemDefinition Material { get; private set; }
        [field: SerializeField, Min(1)] public int Amount { get; private set; } = 1;
        [field: SerializeField] public List<JewelryModifierData> Modifiers { get; private set; } = new();
    }

    [CreateAssetMenu(fileName = "JewelryItemDefinition", menuName = "Godmist/Items/Jewelry Item Definition")]
    public class JewelryItemDefinition : EquipmentItemDefinition
    {
        [field: SerializeField] public List<JewelryMetalData> Metals { get; private set; } = new();
        [field: SerializeField] public List<JewelryGemDefinition> Gems { get; private set; } = new();
        [field: SerializeField] public EquipmentSlot JewelrySlot { get; private set; }

        public override EquipmentSlot EquipmentSlot => JewelrySlot;

        public string JewelryTypeDisplayName => EquipmentSlot switch
        {
            EquipmentSlot.Ring => "Pierścień",
            EquipmentSlot.Necklace => "Naszyjnik",
            EquipmentSlot.Bracelet => "Bransoleta",
            EquipmentSlot.Earrings => "Kolczyki",
            _ => "Biżuteria"
        };

        private void OnValidate() => ItemType = Godmist.Core.Enums.ItemType.Jewelry;
    }
}
