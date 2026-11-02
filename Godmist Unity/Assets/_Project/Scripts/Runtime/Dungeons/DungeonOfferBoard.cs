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
        public DungeonLength Length { get; }
        public DungeonExpeditionOffer(string definitionId, int seed, int levelOffset, int fixedLevel = 0,
            DungeonLength length = DungeonLength.Unspecified)
        { DefinitionId = definitionId; Seed = seed; LevelOffset = levelOffset; FixedLevel = fixedLevel; Length = length; }
    }
    public sealed class DungeonBossOpportunity
    {
        public DungeonTheme Theme { get; }
        public string DefinitionId { get; }
        public int Level { get; }
        public DungeonLength Length { get; }
        public DungeonBossOpportunity(DungeonTheme theme, string definitionId, int level,
            DungeonLength length = DungeonLength.Unspecified)
        { Theme = theme; DefinitionId = definitionId; Level = level; Length = length; }
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
        // Contract cycles advance on successful exits, not abandonment or offer repair.
        public int CompletedExpeditions { get; private set; }
        public void RecordCompletedExpedition() => CompletedExpeditions = (int)Math.Min(int.MaxValue, (long)CompletedExpeditions+1);
        public int SelectedIndex { get; private set; }
        public int HighestMilestoneProcessed { get; private set; }
        public int GetDanger(DungeonTheme theme) => dangerByTheme.TryGetValue(theme, out var value) ? value : 0;
        public void SetDanger(DungeonTheme theme, int value)
        {
            if (theme == DungeonTheme.Story || !Enum.IsDefined(typeof(DungeonTheme), theme)) return;
            dangerByTheme[theme] = Math.Clamp(value, 0, 100);
        }
        public bool QueueBoss(DungeonTheme theme, string definitionId, int level,
            DungeonLength length = DungeonLength.Unspecified)
        {
            if (theme == DungeonTheme.Story || !Enum.IsDefined(typeof(DungeonTheme), theme)
                || string.IsNullOrWhiteSpace(definitionId) || level < 1 || level > 50
                || !Enum.IsDefined(typeof(DungeonLength), length)
                || bossOpportunities.Exists(boss => boss.DefinitionId == definitionId && boss.Level == level))
                return false;
            bossOpportunities.Add(new DungeonBossOpportunity(theme, definitionId, level, length));
            return true;
        }
        public bool TryAssignLegacyBossLength(string definitionId, int level, DungeonLength length)
        {
            if (length < DungeonLength.Short || length > DungeonLength.Long) return false;
            var index = bossOpportunities.FindIndex(boss => boss.DefinitionId == definitionId
                && boss.Level == level && boss.Length == DungeonLength.Unspecified);
            if (index < 0) return false;
            var boss = bossOpportunities[index];
            bossOpportunities[index] = new DungeonBossOpportunity(boss.Theme, definitionId, level, length);
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
            => TryRestore(generation, selectedIndex, next, out _, out _);

        public bool TryRestore(int generation, int selectedIndex, IReadOnlyList<DungeonExpeditionOffer> next,
            out string field, out string reason)
        {
            field = "Offers"; reason = "Lista ofert musi zawierać 0 lub 6 pozycji.";
            if (next == null || next.Count != 0 && next.Count != 6) return false;
            field = "SelectedIndex"; reason = "Wybrany indeks znajduje się poza listą ofert.";
            if (selectedIndex < 0 || selectedIndex >= Math.Max(1, next.Count)) return false;
            var seeds = new HashSet<int>();
            for (var i = 0; i < next.Count; i++)
            {
                var offer = next[i]; field = $"Offers[{i}]"; reason = "Pusta oferta.";
                if (offer == null) return false;
                field = $"Offers[{i}].DefinitionId"; reason = "Brak ID definicji lochu.";
                if (string.IsNullOrWhiteSpace(offer.DefinitionId)) return false;
                field = $"Offers[{i}].Seed"; reason = "Powtórzony seed oferty.";
                if (!seeds.Add(offer.Seed)) return false;
                field = $"Offers[{i}].Length"; reason = "Nieobsługiwana długość lochu.";
                if (!Enum.IsDefined(typeof(DungeonLength), offer.Length)) return false;
                field = $"Offers[{i}].FixedLevel"; reason = "Stały poziom musi mieścić się w zakresie 0–50.";
                if (offer.FixedLevel < 0 || offer.FixedLevel > 50) return false;
                field = $"Offers[{i}].LevelOffset"; reason = "Przesunięcie poziomu nie odpowiada rodzajowi i pozycji oferty.";
                if (offer.FixedLevel > 0 && offer.LevelOffset != 0
                    || offer.FixedLevel == 0 && (i == 0 || i == 1 && next[0].FixedLevel > 0
                        ? offer.LevelOffset != 0 : Math.Abs((long)offer.LevelOffset) < 1 || Math.Abs((long)offer.LevelOffset) > 3)) return false;
            }
            offers.Clear(); offers.AddRange(next); Generation = generation; SelectedIndex = selectedIndex;
            field = reason = string.Empty; return true;
        }

        public bool TryRestoreProgression(int highestMilestoneProcessed,
            IReadOnlyDictionary<DungeonTheme, int> nextDanger,
            IReadOnlyList<DungeonBossOpportunity> nextBosses, int completedExpeditions = 0)
            => TryRestoreProgression(highestMilestoneProcessed, nextDanger, nextBosses, completedExpeditions, out _, out _);

        public bool TryRestoreProgression(int highestMilestoneProcessed,
            IReadOnlyDictionary<DungeonTheme, int> nextDanger,
            IReadOnlyList<DungeonBossOpportunity> nextBosses, int completedExpeditions,
            out string field, out string reason)
        {
            field = "HighestMilestoneProcessed"; reason = "Próg musi być wielokrotnością 10 w zakresie 0–50.";
            if (highestMilestoneProcessed < 0 || highestMilestoneProcessed > 50 || highestMilestoneProcessed % 10 != 0) return false;
            field = "CompletedExpeditions"; reason = "Liczba wypraw nie może być ujemna.";
            if (completedExpeditions < 0) return false;
            field = "Danger"; reason = "Brak tabeli zagrożenia.";
            if (nextDanger == null) return false;
            field = "BossOpportunities"; reason = "Brak listy okazji bossów.";
            if (nextBosses == null) return false;
            foreach (var pair in nextDanger)
            {
                field = $"Danger[{pair.Key}].Theme"; reason = "Nieobsługiwany biom zagrożenia.";
                if (pair.Key == DungeonTheme.Story || !Enum.IsDefined(typeof(DungeonTheme), pair.Key)) return false;
                field = $"Danger[{pair.Key}].Value"; reason = "Zagrożenie musi mieścić się w zakresie 0–100.";
                if (pair.Value < 0 || pair.Value > 100) return false;
            }
            var keys = new HashSet<(string, int)>();
            for (var i = 0; i < nextBosses.Count; i++)
            {
                var boss = nextBosses[i]; field = $"BossOpportunities[{i}]"; reason = "Pusta okazja bossa.";
                if (boss == null) return false;
                field = $"BossOpportunities[{i}].Theme"; reason = "Nieobsługiwany biom bossa.";
                if (boss.Theme == DungeonTheme.Story || !Enum.IsDefined(typeof(DungeonTheme), boss.Theme)) return false;
                field = $"BossOpportunities[{i}].Length"; reason = "Nieobsługiwana długość lochu bossa.";
                if (!Enum.IsDefined(typeof(DungeonLength), boss.Length)) return false;
                field = $"BossOpportunities[{i}].DefinitionId"; reason = "Brak ID definicji bossa.";
                if (string.IsNullOrWhiteSpace(boss.DefinitionId)) return false;
                field = $"BossOpportunities[{i}].Level"; reason = "Poziom bossa musi mieścić się w zakresie 1–50.";
                if (boss.Level < 1 || boss.Level > 50) return false;
                field = $"BossOpportunities[{i}].DefinitionId"; reason = "Powtórzona para ID bossa i poziomu.";
                if (!keys.Add((boss.DefinitionId, boss.Level))) return false;
            }
            dangerByTheme.Clear();
            foreach (var pair in nextDanger) dangerByTheme.Add(pair.Key, pair.Value);
            bossOpportunities.Clear(); bossOpportunities.AddRange(nextBosses);
            HighestMilestoneProcessed = highestMilestoneProcessed; CompletedExpeditions = completedExpeditions;
            field = reason = string.Empty; return true;
        }
    }
}
