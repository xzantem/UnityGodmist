using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
namespace Godmist.Runtime.Dungeons
{
    public sealed class DungeonExpeditionOffer
    {
        public string DefinitionId { get; }
        public int Seed { get; }
        public int LevelOffset { get; }
        public int FixedLevel { get; }
        public DungeonExpeditionOffer(string definitionId, int seed, int levelOffset, int fixedLevel = 0)
        { DefinitionId = definitionId; Seed = seed; LevelOffset = levelOffset; FixedLevel = fixedLevel; }
    }
    public sealed class DungeonBossOpportunity
    {
        public DungeonTheme Theme { get; }
        public string DefinitionId { get; }
        public int Level { get; }
        public DungeonBossOpportunity(DungeonTheme theme, string definitionId, int level)
        { Theme = theme; DefinitionId = definitionId; Level = level; }
    }
    public sealed class DungeonOfferBoard
    {
        private readonly List<DungeonExpeditionOffer> offers = new();
        private readonly Dictionary<DungeonTheme, int> dangerByTheme = new();
        private readonly List<DungeonBossOpportunity> bossOpportunities = new();
        public IReadOnlyList<DungeonExpeditionOffer> Offers => offers;
        public IReadOnlyDictionary<DungeonTheme, int> DangerByTheme => dangerByTheme;
        public IReadOnlyList<DungeonBossOpportunity> BossOpportunities => bossOpportunities;
        public int Generation { get; private set; }
        public int SelectedIndex { get; private set; }
        public int HighestMilestoneProcessed { get; private set; }
        public int GetDanger(DungeonTheme theme) => dangerByTheme.TryGetValue(theme, out var value) ? value : 0;
        public void SetDanger(DungeonTheme theme, int value)
        {
            if (theme == DungeonTheme.Story || !Enum.IsDefined(typeof(DungeonTheme), theme)) return;
            dangerByTheme[theme] = Math.Clamp(value, 0, 100);
        }
        public bool QueueBoss(DungeonTheme theme, string definitionId, int level)
        {
            if (theme == DungeonTheme.Story || !Enum.IsDefined(typeof(DungeonTheme), theme)
                || string.IsNullOrWhiteSpace(definitionId) || level < 1 || level > 50
                || bossOpportunities.Exists(boss => boss.DefinitionId == definitionId && boss.Level == level))
                return false;
            bossOpportunities.Add(new DungeonBossOpportunity(theme, definitionId, level));
            return true;
        }
        public bool CompleteBoss(string definitionId, int level)
        {
            var index = bossOpportunities.FindIndex(boss => boss.DefinitionId == definitionId && boss.Level == level);
            if (index < 0) return false;
            var theme = bossOpportunities[index].Theme;
            bossOpportunities.RemoveAt(index);
            SetDanger(theme, 0);
            return true;
        }
        public bool MarkMilestoneProcessed(int level)
        {
            if (level < 10 || level > 50 || level % 10 != 0 || level <= HighestMilestoneProcessed) return false;
            HighestMilestoneProcessed = level;
            return true;
        }
        public bool TrySelect(int index)
        { if (index < 0 || index >= offers.Count) return false; SelectedIndex = index; return true; }
        public void CompleteExpedition() { offers.Clear(); Generation = unchecked(Generation + 1); SelectedIndex = 0; }
        public void InvalidateOffers() => CompleteExpedition();
        public bool TryRestore(int generation, int selectedIndex, IReadOnlyList<DungeonExpeditionOffer> next)
        {
            if (next == null || (next.Count != 0 && next.Count != 6) || selectedIndex < 0 || selectedIndex >= Math.Max(1,next.Count)) return false;
            var seeds = new HashSet<int>();
            for (var i=0;i<next.Count;i++)
            {
                var offer=next[i];
                if (offer == null || string.IsNullOrWhiteSpace(offer.DefinitionId) || !seeds.Add(offer.Seed)
                    || offer.FixedLevel < 0 || offer.FixedLevel > 50
                    || (offer.FixedLevel > 0 && offer.LevelOffset != 0)
                    || (offer.FixedLevel == 0 && (i == 0 || i == 1 && next[0].FixedLevel > 0
                        ? offer.LevelOffset != 0
                        : Math.Abs((long)offer.LevelOffset) < 1 || Math.Abs((long)offer.LevelOffset) > 3))) return false;
            }
            offers.Clear(); offers.AddRange(next); Generation=generation; SelectedIndex=selectedIndex; return true;
        }
        public bool TryRestoreProgression(int highestMilestoneProcessed,
            IReadOnlyDictionary<DungeonTheme, int> nextDanger,
            IReadOnlyList<DungeonBossOpportunity> nextBosses)
        {
            if (highestMilestoneProcessed < 0 || highestMilestoneProcessed > 50
                || highestMilestoneProcessed % 10 != 0 || nextDanger == null || nextBosses == null)
                return false;
            foreach (var pair in nextDanger)
                if (pair.Key == DungeonTheme.Story || !Enum.IsDefined(typeof(DungeonTheme), pair.Key)
                    || pair.Value < 0 || pair.Value > 100) return false;
            var keys = new HashSet<(string, int)>();
            foreach (var boss in nextBosses)
                if (boss == null || boss.Theme == DungeonTheme.Story
                    || !Enum.IsDefined(typeof(DungeonTheme), boss.Theme)
                    || string.IsNullOrWhiteSpace(boss.DefinitionId) || boss.Level < 1 || boss.Level > 50
                    || !keys.Add((boss.DefinitionId, boss.Level))) return false;
            dangerByTheme.Clear();
            foreach (var pair in nextDanger) dangerByTheme.Add(pair.Key, pair.Value);
            bossOpportunities.Clear(); bossOpportunities.AddRange(nextBosses);
            HighestMilestoneProcessed = highestMilestoneProcessed;
            return true;
        }
    }
}
