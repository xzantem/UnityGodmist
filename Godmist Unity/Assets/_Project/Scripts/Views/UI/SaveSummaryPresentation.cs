using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Godmist.Core.Enums;
using Godmist.Data.World;
using Godmist.Systems.SaveLoad;

namespace Godmist.Views.UI
{
    public static class SaveSummaryPresentation
    {
        public static string Safe(string text) => (text ?? string.Empty).Replace('<', '‹').Replace('>', '›');
        public static string Slot(GameSaveSummary summary) => summary.SlotType switch
        { SaveSlotType.Quick => "Szybki zapis", SaveSlotType.Autosave => "Autozapis", _ => "Zapis ręczny" };
        public static string Date(GameSaveSummary summary) => summary.UpdatedAtUtc == DateTime.MinValue
            ? "Nieznana data" : summary.UpdatedAtUtc.ToLocalTime().ToString("dd.MM.yyyy • HH:mm");
        public static string Campaign(GameSaveSummary summary) => Safe(string.IsNullOrEmpty(summary.CampaignId)
            ? "—" : summary.CampaignId.Substring(0, Math.Min(6, summary.CampaignId.Length)).ToUpperInvariant());
        public static string Location(GameSaveSummary summary, GameContentCatalog catalog)
        {
            var preview = summary.Preview;
            if (preview == null) return "Zapis kampanii";
            if (string.IsNullOrEmpty(preview.DungeonId)) return "Arungard";
            var name = catalog != null && catalog.TryGetDungeon(preview.DungeonId, out var dungeon)
                ? dungeon.DisplayName : "Wyprawa";
            return $"{Safe(name)} • piętro {preview.Floor}";
        }
        public static string Title(GameSaveSummary summary, GameContentCatalog catalog) => summary.SlotType == SaveSlotType.Manual
            ? Safe(summary.DisplayName) : Location(summary, catalog);
        public static string RowDetails(GameSaveSummary summary, GameContentCatalog catalog)
        {
            var party = summary.Preview?.Party;
            var leader = party != null && party.Count > 0 ? Name(party[0].CharacterId, catalog) : "Brak danych drużyny";
            return $"{Slot(summary)} • {Date(summary)}\n{leader} • kampania {Campaign(summary)}";
        }
        public static string Name(string id, GameContentCatalog catalog) => Safe(catalog != null && catalog.TryGetCharacter(id, out var character) ? character.DisplayName : id);
        public static IReadOnlyList<SaveBrowserCampaign> Campaigns(IEnumerable<GameSaveSummary> summaries, GameContentCatalog catalog)
        {
            var groups = summaries.GroupBy(save => string.IsNullOrEmpty(save.CampaignId) ? "legacy:" + save.SaveId : save.CampaignId)
                .OrderBy(group => group.Min(save => save.CreatedAtUtc == DateTime.MinValue ? save.UpdatedAtUtc : save.CreatedAtUtc))
                .ThenBy(group => group.Key, StringComparer.Ordinal).ToArray();
            var campaigns = groups.Select(group =>
            {
                var saves = group.OrderByDescending(save => save.UpdatedAtUtc).ToArray();
                var party = saves[0].Preview?.Party;
                var leader = party != null && party.Count > 0 ? Name(party[0].CharacterId, catalog) : "Drużyna";
                return new SaveBrowserCampaign(group.Key, $"{leader} • {Date(saves[0])}", saves);
            }).ToArray();
            // A suffix disambiguates sessions with the same leader and minute without
            // exposing a hexadecimal ID or inventing a persisted campaign name.
            return campaigns.GroupBy(campaign => campaign.Label).SelectMany(group => group.Select((campaign, index) =>
                new SaveBrowserCampaign(campaign.Key, campaign.Label + (group.Count() > 1 ? $" #{index + 1}" : string.Empty), campaign.Saves.ToArray())))
                .OrderByDescending(campaign => campaign.Saves[0].UpdatedAtUtc).ThenBy(campaign => campaign.Key, StringComparer.Ordinal).ToArray();
        }

        private static string TrackedQuestName(GameSavePreview preview,GameContentCatalog catalog) =>
            preview?.TrackedQuestDisplayName ?? (!string.IsNullOrEmpty(preview?.TrackedQuestId)
                && catalog!=null && catalog.TryGetQuest(preview.TrackedQuestId,out var quest)?quest.DisplayName:null);
        public static string Facts(GameSaveSummary summary, GameContentCatalog catalog)
        {
            if (summary.Preview == null) return "Ten zapis nie zawiera danych podglądu.";
            var result = new StringBuilder();
            result.AppendLine($"Złoto: {summary.Preview.Gold}");
            if (summary.Preview.DungeonLevel > 0) result.AppendLine($"Poziom lochu: {summary.Preview.DungeonLevel}");
            if (TrackedQuestName(summary.Preview,catalog) is {} questName)
                result.AppendLine($"\nCel wyprawy\n{Safe(questName)}");
            return result.ToString();
        }
        public static string Details(GameSaveSummary summary, GameContentCatalog catalog)
        {
            var result = new StringBuilder();
            result.AppendLine($"<size=24>{Location(summary, catalog)}</size>");
            result.AppendLine($"{Slot(summary)} • {Date(summary)}");
            result.AppendLine($"Kampania {Campaign(summary)}");
            if (summary.SlotType == SaveSlotType.Manual) result.AppendLine($"Nazwa: {Safe(summary.DisplayName)}");
            if (summary.Preview == null) return result.Append("\nTen zapis nie zawiera danych podglądu.").ToString();
            var preview = summary.Preview;
            result.AppendLine($"\nZłoto: {preview.Gold}");
            if (preview.DungeonLevel > 0) result.AppendLine($"Poziom wyprawy: {preview.DungeonLevel}");
            if (TrackedQuestName(preview,catalog) is {} questName)
                result.AppendLine($"Cel: {Safe(questName)}");
            result.AppendLine("\nDrużyna w chwili zapisu");
            if (preview.Party.Count == 0) result.AppendLine("Brak aktywnych postaci");
            foreach (var member in preview.Party)
                result.AppendLine($"{Name(member.CharacterId, catalog)} • poziom {member.Level}");
            return result.ToString();
        }
    }

    public sealed class SaveBrowserCampaign
    {
        public string Key { get; }
        public string Label { get; }
        public IReadOnlyList<GameSaveSummary> Saves { get; }
        public SaveBrowserCampaign(string key, string label, GameSaveSummary[] saves)
        { Key = key; Label = label; Saves = Array.AsReadOnly(saves); }
        public IReadOnlyList<GameSaveSummary> VisibleSaves(bool includeHistory)
        {
            var latestAuto = Saves.FirstOrDefault(save => save.SlotType == SaveSlotType.Autosave);
            return Saves.Where(save => includeHistory || save.SlotType != SaveSlotType.Autosave || save == latestAuto).ToArray();
        }
        public int OlderAutosaveCount => Math.Max(0, Saves.Count(save => save.SlotType == SaveSlotType.Autosave) - 1);
    }
}
