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

        public bool IsEquipment;
        public string RequiredClassId = string.Empty;
        public int RequiredLevel;
        public List<StatModifierSaveData> StatModifiers = new();
    }
}