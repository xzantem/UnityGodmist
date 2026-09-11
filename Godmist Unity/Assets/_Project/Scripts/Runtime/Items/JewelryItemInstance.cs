using System;
using System.Linq;
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
        public JewelryItemInstance() {}

        public JewelryItemInstance(JewelryItemDefinition definition, string instanceId = null)
            : base(definition, instanceId) {}

        public string GeneratedDisplayName
        {
            get
            {
                var baseName = GetBaseName(EquipmentDefinition?.EquipmentSlot);
                var metalDescriptor = GetMetalDescriptor(MetalDisplayName);
                var prefix = string.IsNullOrWhiteSpace(metalDescriptor)
                    ? baseName
                    : $"{metalDescriptor} {baseName}";
                var gemName = GetGemInflectedName(GemDisplayName);
                return string.IsNullOrWhiteSpace(gemName)
                    ? CapitalizeFirst(prefix)
                    : $"{CapitalizeFirst(prefix)} z {gemName}";
            }
        }

        public override string DisplayName => !string.IsNullOrWhiteSpace(CustomName)
            ? CustomName
            : GeneratedDisplayName;

        public override int BaseValue => CraftedBaseValue > 0
            ? CraftedBaseValue
            : base.BaseValue;

        private static string GetBaseName(Godmist.Core.Enums.EquipmentSlot? slot) => slot switch
        {
            Godmist.Core.Enums.EquipmentSlot.Ring => "pierścień",
            Godmist.Core.Enums.EquipmentSlot.Necklace => "naszyjnik",
            Godmist.Core.Enums.EquipmentSlot.Bracelet => "bransoleta",
            Godmist.Core.Enums.EquipmentSlot.Earrings => "kolczyki",
            _ => "biżuteria"
        };

        private static string GetMetalDescriptor(string metalName)
        {
            if (string.IsNullOrWhiteSpace(metalName)) return string.Empty;
            return metalName.Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? string.Empty;
        }

        private static string GetGemInflectedName(string gemName)
        {
            if (string.IsNullOrWhiteSpace(gemName)) return string.Empty;

            var normalized = gemName.Trim().ToLowerInvariant();
            return normalized switch
            {
                "obsydian" => "obsydianem",
                "bursztyn" => "bursztynem",
                "topaz" => "topazem",
                _ => $"{normalized}em"
            };
        }

        private static string CapitalizeFirst(string value) => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : char.ToUpper(value[0]) + value.Substring(1);
    }
}
