using System;
using System.Collections.Generic;

namespace Godmist.Core.Serialization
{
    [Serializable]
    public class GameSaveData
    {
        public const int CurrentSaveVersion = 1;

        public int SaveVersion = CurrentSaveVersion;
        public string SaveId = string.Empty;
        public string CampaignId = string.Empty;
        public string DisplayName = string.Empty;
        public string CreatedAtUtc = string.Empty;
        public string UpdatedAtUtc = string.Empty;

        public int Gold;
        public string TrackedQuestId = string.Empty;
        public PartySaveData Party = new();
        public List<CharacterSaveData> Characters = new();
        public List<ItemSaveData> ItemInstances = new();
        public List<string> StashItemInstanceIds = new();
        public List<string> PendingLootItemInstanceIds = new();
        public List<KeyValueSaveData> WorldFlags = new();
        public List<QuestSaveData> Quests = new();
        public List<NpcSaveData> Npcs = new();
        public DungeonSaveData ActiveDungeon;
        public bool HasExplorationPosition;
        public float ExplorationPositionX;
        public float ExplorationPositionY;

        public static GameSaveData CreateNew(
            string displayName,
            string campaignId = null)
        {
            var now = DateTime.UtcNow.ToString("O");

            return new GameSaveData
            {
                SaveVersion = CurrentSaveVersion,
                SaveId = Guid.NewGuid().ToString("N"),
                CampaignId = campaignId?.Trim() ?? string.Empty,
                DisplayName = string.IsNullOrWhiteSpace(displayName)
                    ? "Nowa kampania"
                    : displayName.Trim(),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
        }
    }

    [Serializable]
    public class PartySaveData
    {
        public int MaxActiveMembers;
        public string ActiveLeaderId = string.Empty;
        public List<string> ActiveMemberIds = new();
        public List<string> BenchMemberIds = new();
    }
}
