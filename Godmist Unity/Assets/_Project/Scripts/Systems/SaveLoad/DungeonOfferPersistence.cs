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
            Offers=board.Offers.Select(o=>new DungeonOfferSaveData{DefinitionId=o.DefinitionId,Seed=o.Seed,LevelOffset=o.LevelOffset,FixedLevel=o.FixedLevel}).ToList(),
            HighestMilestoneProcessed=board.HighestMilestoneProcessed,
            Danger=board.DangerByTheme.OrderBy(pair=>pair.Key)
                .Select(pair=>new DungeonDangerSaveData{Theme=pair.Key,Value=pair.Value}).ToList(),
            BossOpportunities=board.BossOpportunities.Select(boss=>new DungeonBossOpportunitySaveData
                {Theme=boss.Theme,DefinitionId=boss.DefinitionId,Level=boss.Level}).ToList()
        };
        public static bool Restore(DungeonOfferBoardSaveData data, DungeonOfferBoard board)
        {
            if(data==null) return true; // Saves predating the selector generate their first board when opened.
            if(data.Offers==null || data.Offers.Any(o=>o==null)
                || data.Danger?.Any(entry=>entry==null) == true
                || data.BossOpportunities?.Any(entry=>entry==null) == true) return false;
            var danger=new Dictionary<DungeonTheme,int>();
            foreach(var entry in data.Danger ?? new List<DungeonDangerSaveData>())
                if(!danger.TryAdd(entry.Theme,entry.Value)) return false;
            var bosses=(data.BossOpportunities ?? new List<DungeonBossOpportunitySaveData>())
                .Select(entry=>new DungeonBossOpportunity(entry.Theme,entry.DefinitionId,entry.Level)).ToArray();
            var offers=data.Offers.Select(o=>new DungeonExpeditionOffer(
                o.DefinitionId,o.Seed,o.LevelOffset,o.FixedLevel)).ToArray();
            var validated=new DungeonOfferBoard();
            if(!validated.TryRestore(data.Generation,data.SelectedIndex,offers)
                || !validated.TryRestoreProgression(data.HighestMilestoneProcessed,danger,bosses)) return false;
            board.TryRestore(data.Generation,data.SelectedIndex,offers);
            return board.TryRestoreProgression(data.HighestMilestoneProcessed,danger,bosses);
        }
    }
}
