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