using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Effects;
using UnityEngine;

namespace Godmist.Data.Items
{
    [Serializable]
    public class JewelryMetalData
    {
        [field: SerializeField] public string Id { get; private set; } = string.Empty;
        [field: SerializeField] public string DisplayName { get; private set; } = string.Empty;
        [field: SerializeField, Range(1, 50)] public int MinimumLevel { get; private set; } = 1;
        [field: SerializeField] public List<StatModifier> StatModifiers { get; private set; } = new();
        [field: SerializeField] public JewelryResistanceModifier ResistanceModifier { get; private set; }
    }

    [Serializable]
    public class JewelryResistanceModifier
    {
        [field: SerializeField] public ResistanceType ResistanceType { get; private set; }
        [field: SerializeField, Range(0f, 1f)] public float Value { get; private set; }
    }

    [Serializable]
    public class JewelryGemData
    {
        [field: SerializeField] public string Id { get; private set; } = string.Empty;
        [field: SerializeField] public string DisplayName { get; private set; } = string.Empty;
        [field: SerializeField, TextArea] public string Description { get; private set; } = string.Empty;
        [field: SerializeField] public JewelryPassiveType PassiveType { get; private set; }
        [field: SerializeField] public float Value { get; private set; }
        [field: SerializeField] public DungeonTheme DungeonTheme { get; private set; }
        [field: SerializeField] public bool IsBossUnique { get; private set; }
        [field: SerializeField] public List<PassiveEffectDefinition> PassiveEffects { get; private set; } = new();
    }

    [CreateAssetMenu(fileName = "JewelryItemDefinition", menuName = "Godmist/Items/Jewelry Item Definition")]
    public class JewelryItemDefinition : EquipmentItemDefinition
    {
        [field: SerializeField] public List<JewelryMetalData> Metals { get; private set; } = new();
        [field: SerializeField] public JewelryItemDefinition MetalSource { get; private set; }
        [field: SerializeField] public List<JewelryGemData> Gems { get; private set; } = new();
    }
}
