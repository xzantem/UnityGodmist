using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Serialization;
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
        public GameSavePreview Preview { get; }

        public GameSaveSummary(
            string filePath,
            string saveId,
            string campaignId,
            string displayName,
            DateTime createdAtUtc,
            DateTime updatedAtUtc,
            SaveSlotType slotType,
            GameSavePreview preview = null)
        {
            FilePath = filePath;
            SaveId = saveId;
            CampaignId = campaignId;
            DisplayName = displayName;
            CreatedAtUtc = createdAtUtc;
            UpdatedAtUtc = updatedAtUtc;
            SlotType = slotType;
            Preview = preview;
        }
    }

    /// <summary>Read-only display data extracted from an existing save, never restored state.</summary>
    public sealed class GameSavePreview
    {
        public string DungeonId { get; }
        public int DungeonLevel { get; }
        public int Floor { get; }
        public int Gold { get; }
        public string TrackedQuestId { get; }
        public string TrackedQuestDisplayName { get; }
        public IReadOnlyList<GameSavePartyMember> Party { get; }

        public GameSavePreview(GameSaveData data)
        {
            DungeonId = data.ActiveDungeon?.DungeonId ?? string.Empty;
            DungeonLevel = data.ActiveDungeon?.DungeonLevel ?? 0;
            Floor = data.ActiveDungeon != null ? data.ActiveDungeon.CurrentFloorIndex + 1 : 0;
            Gold = data.Gold;
            TrackedQuestId = data.TrackedQuestId ?? string.Empty;
            var tracked=data.Quests?.FirstOrDefault(q=>q!=null&&q.QuestId==TrackedQuestId);
            if(tracked?.HasBoardContract==true)
            {
                TrackedQuestDisplayName="Zlecenie";
                if(tracked.BoardContract is {} contract)
                    try {
                        TrackedQuestDisplayName=new Godmist.Runtime.Quests.RadiantBoardContract("preview",contract.ObjectiveType,
                            contract.Theme,contract.Length,contract.Level,contract.RequiredAmount,contract.RewardMultiplier,contract.Cycle).DisplayName;
                    } catch(ArgumentException) { /* Invalid preview data must not impersonate the slot's authored quest. */ }
            }
            var ids = data.Party?.ActiveMemberIds ?? new List<string>();
            Party = Array.AsReadOnly(ids.Distinct().OrderByDescending(id => id == data.Party?.ActiveLeaderId).Select(id => data.Characters?.FirstOrDefault(character => character != null && character.CharacterId == id))
                .Where(character => character != null)
                .Select(character => new GameSavePartyMember(character.CharacterId, character.ClassId, character.Level)).ToArray());
        }
    }

    public sealed class GameSavePartyMember
    {
        public string CharacterId { get; }
        public string ClassId { get; }
        public int Level { get; }
        public GameSavePartyMember(string characterId, string classId, int level)
        { CharacterId = characterId; ClassId = classId; Level = level; }
    }
}
