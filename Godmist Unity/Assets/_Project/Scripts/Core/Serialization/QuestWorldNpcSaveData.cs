using System;
using System.Collections.Generic;
using Godmist.Core.Enums;

namespace Godmist.Core.Serialization
{
    [Serializable]
    public class KeyValueSaveData
    {
        public string Key = string.Empty;
        public string Value = string.Empty;
    }

    [Serializable]
    public class QuestSaveData
    {
        public string QuestId = string.Empty;
        public QuestStatus Status;
        public string CurrentStageId = string.Empty;
        public bool IsHidden;

        public List<QuestObjectiveProgressSaveData> ObjectiveProgress = new();
        public List<KeyValueSaveData> QuestFlags = new();
        public List<string> ChosenBranchIds = new();
        public List<string> ClaimedRewardKeys = new();
        public int RadiantRewardLevel;
        public int RadiantRewardExperience;
        public int RadiantRewardGold;
        public int RadiantRewardHonor;
        public string RadiantBonusItemInstanceId = string.Empty;
    }

    [Serializable]
    public class QuestObjectiveProgressSaveData
    {
        public string ObjectiveId = string.Empty;
        public int CurrentAmount;
        public bool IsCompleted;
    }

    [Serializable]
    public class NpcSaveData
    {
        public string NpcId = string.Empty;
        public int LoyaltyLevel;
        public int SpentGoldTotal;
        public long GoldBalance;
        public long TotalGoldReceived;
        public long TotalGoldSpent;
        public float GuildPayoutProgress;

        public List<string> RotatingStockItemIds = new();
        public List<string> CraftedStockInstanceIds = new();
        public List<string> ShopItemInstanceIds = new();
        public List<BuybackEntrySaveData> BuybackEntries = new();
    }

    [Serializable]
    public class BuybackEntrySaveData
    {
        public string ItemInstanceId = string.Empty;
        public int SoldPrice;
    }
}
