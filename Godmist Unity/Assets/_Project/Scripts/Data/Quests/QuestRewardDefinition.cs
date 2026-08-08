using System;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using UnityEngine;

namespace Godmist.Data.Quests
{
    [Serializable]
    public class QuestRewardDefinition
    {
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

        public QuestRewardType RewardType => rewardType;
        public int Amount => amount;
        public ItemDefinition ItemDefinition => itemDefinition;
        public string WorldFlagId => worldFlagId;
        public string WorldFlagValue => worldFlagValue;
    }
}