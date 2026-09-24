using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
namespace Godmist.Core.Serialization
{
    [Serializable] public sealed class DungeonOfferBoardSaveData
    {
        public int Generation;
        public int SelectedIndex;
        public List<DungeonOfferSaveData> Offers = new();
        public int HighestMilestoneProcessed;
        public List<DungeonDangerSaveData> Danger = new();
        public List<DungeonBossOpportunitySaveData> BossOpportunities = new();
    }
    [Serializable] public sealed class DungeonOfferSaveData
    {
        public string DefinitionId;
        public int Seed;
        public int LevelOffset;
        public int FixedLevel;
    }
    [Serializable] public sealed class DungeonDangerSaveData
    {
        public DungeonTheme Theme;
        public int Value;
    }
    [Serializable] public sealed class DungeonBossOpportunitySaveData
    {
        public DungeonTheme Theme;
        public string DefinitionId;
        public int Level;
    }
}
