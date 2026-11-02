using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Serialization;
using Godmist.Runtime.Dungeons;
namespace Godmist.Systems.SaveLoad
{
    public static class DungeonOfferPersistence
    {
        public static DungeonOfferBoardSaveData Capture(DungeonOfferBoard board) => new()
        {
            Generation=board.Generation, SelectedIndex=board.SelectedIndex,
            CompletedExpeditions=board.CompletedExpeditions,
            Offers=board.Offers.Select(o=>new DungeonOfferSaveData{DefinitionId=o.DefinitionId,Seed=o.Seed,LevelOffset=o.LevelOffset,FixedLevel=o.FixedLevel,Length=o.Length}).ToList(),
            HighestMilestoneProcessed=board.HighestMilestoneProcessed,
            Danger=board.DangerByTheme.OrderBy(pair=>pair.Key)
                .Select(pair=>new DungeonDangerSaveData{Theme=pair.Key,Value=pair.Value}).ToList(),
            BossOpportunities=board.BossOpportunities.Select(boss=>new DungeonBossOpportunitySaveData
                {Theme=boss.Theme,DefinitionId=boss.DefinitionId,Level=boss.Level,Length=boss.Length}).ToList()
        };
        public static bool Restore(DungeonOfferBoardSaveData data, DungeonOfferBoard board)
            => Restore(data, board, new RestoreContext());

        internal static bool Restore(DungeonOfferBoardSaveData data, DungeonOfferBoard board, RestoreContext context)
        {
            if (data == null) return true; // Saves predating the selector.
            if (data.Offers == null) return context.Reject(SaveRestoreErrorCode.MissingValue, "Offers", "Brak listy ofert.");
            for (var i = 0; i < data.Offers.Count; i++)
                if (data.Offers[i] == null) return context.Field("Offers").Entry(i).Reject(SaveRestoreErrorCode.MissingValue, "", "Pusta oferta.");
            var danger = new Dictionary<DungeonTheme, int>();
            for (var i = 0; i < (data.Danger?.Count ?? 0); i++)
            {
                var entry = data.Danger[i]; var row = context.Field("Danger").Entry(i);
                if (entry == null) return row.Reject(SaveRestoreErrorCode.MissingValue, "", "Puste zagrożenie biomu.");
                if (!danger.TryAdd(entry.Theme, entry.Value)) return row.Reject(SaveRestoreErrorCode.DuplicateId, "Theme", "Powtórzony biom zagrożenia.");
            }
            for (var i = 0; i < (data.BossOpportunities?.Count ?? 0); i++)
                if (data.BossOpportunities[i] == null) return context.Field("BossOpportunities").Entry(i).Reject(SaveRestoreErrorCode.MissingValue, "", "Pusta okazja bossa.");
            var bosses = (data.BossOpportunities ?? new List<DungeonBossOpportunitySaveData>())
                .Select(entry => new DungeonBossOpportunity(entry.Theme, entry.DefinitionId, entry.Level, entry.Length)).ToArray();
            var offers = data.Offers.Select(o => new DungeonExpeditionOffer(o.DefinitionId, o.Seed, o.LevelOffset, o.FixedLevel, o.Length)).ToArray();
            var validated = new DungeonOfferBoard();
            if (!validated.TryRestore(data.Generation, data.SelectedIndex, offers, out var field, out var reason))
                return context.Reject(SaveRestoreErrorCode.InvalidValue, field, reason);
            if (!validated.TryRestoreProgression(data.HighestMilestoneProcessed, danger, bosses, data.CompletedExpeditions, out field, out reason))
                return context.Reject(SaveRestoreErrorCode.InvalidValue, field, reason);
            board.TryRestore(data.Generation, data.SelectedIndex, offers);
            return board.TryRestoreProgression(data.HighestMilestoneProcessed, danger, bosses, data.CompletedExpeditions);
        }
    }
}
