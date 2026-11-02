using System;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using UnityEngine;

namespace Godmist.Data.Quests
{
    [Serializable]
    public class QuestRewardDefinition
    {
        [SerializeField] private string rewardId = Guid.NewGuid().ToString("N");
        public string RewardId => rewardId;
        [SerializeField] private EquipmentQuality equipmentQuality = EquipmentQuality.Normal;
        [SerializeField] private RarityTier equipmentRarity = RarityTier.Common;
        public EquipmentQuality EquipmentQuality => equipmentQuality;
        public RarityTier EquipmentRarity => equipmentRarity;
        [SerializeField]
        private QuestRewardType rewardType;

        [SerializeField, Min(1)]
        private int amount = 1;

        [SerializeField]
        private ItemDefinition itemDefinition;

        [SerializeField]
        private string worldFlagId = string.Empty;

        [SerializeField]
        private string worldFlagValue = "true";

        [SerializeField, Range(0, 50), Tooltip("0: fixed Amount. 1-50: standard mission XP worth 20 enemies of this level.")]
        private int standardExperienceLevel;

        public int StandardExperienceLevel => standardExperienceLevel;
        public QuestRewardType RewardType => rewardType;
        public int Amount => amount;
        public ItemDefinition ItemDefinition => itemDefinition;
        public string WorldFlagId => worldFlagId;
        public string WorldFlagValue => worldFlagValue;
    }
}