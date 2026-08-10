using System;
using Godmist.Core.Enums;

namespace Godmist.Systems.SaveLoad
{
    public sealed class GameSaveSummary
    {
        public string FilePath { get; }
        public string SaveId { get; }
        public string CampaignId { get; }
        public string DisplayName { get; }
        public DateTime CreatedAtUtc { get; }
        public DateTime UpdatedAtUtc { get; }
        public SaveSlotType SlotType { get; }

        public GameSaveSummary(
            string filePath,
            string saveId,
            string campaignId,
            string displayName,
            DateTime createdAtUtc,
            DateTime updatedAtUtc,
            SaveSlotType slotType)
        {
            FilePath = filePath;
            SaveId = saveId;
            CampaignId = campaignId;
            DisplayName = displayName;
            CreatedAtUtc = createdAtUtc;
            UpdatedAtUtc = updatedAtUtc;
            SlotType = slotType;
        }
    }
}