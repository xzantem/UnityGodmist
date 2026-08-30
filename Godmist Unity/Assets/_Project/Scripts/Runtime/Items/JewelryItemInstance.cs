using System;
using Godmist.Core.Enums;
using Godmist.Data.Items;

namespace Godmist.Runtime.Items
{
    [Serializable]
    public class JewelryItemInstance : EquipmentItemInstance
    {
        public string MetalId = string.Empty;
        public string MetalDisplayName = string.Empty;
        public string GemId = string.Empty;
        public string GemDisplayName = string.Empty;
        public string GemDescription = string.Empty;
        public JewelryPassiveType PassiveType;
        public float PassiveValue;
        public DungeonTheme PassiveDungeonTheme;
        public ResistanceType ResistanceType;
        public float ResistanceValue;

        public JewelryItemInstance() {}

        public JewelryItemInstance(JewelryItemDefinition definition, string instanceId = null)
            : base(definition, instanceId) {}

        public string GeneratedDisplayName => string.IsNullOrWhiteSpace(MetalDisplayName)
            ? Definition?.DisplayName ?? "Biżuteria"
            : $"{MetalDisplayName} {Definition?.DisplayName ?? "biżuteria"}";

        public override string DisplayName => GeneratedDisplayName;
    }
}
