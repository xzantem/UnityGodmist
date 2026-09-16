using System.Linq;
using Godmist.Core.Serialization;
using Godmist.Runtime.Dungeons;
namespace Godmist.Systems.SaveLoad
{
    public static class DungeonOfferPersistence
    {
        public static DungeonOfferBoardSaveData Capture(DungeonOfferBoard board) => new()
        {
            Generation=board.Generation, SelectedIndex=board.SelectedIndex,
            Offers=board.Offers.Select(o=>new DungeonOfferSaveData{DefinitionId=o.DefinitionId,Seed=o.Seed,LevelOffset=o.LevelOffset}).ToList()
        };
        public static bool Restore(DungeonOfferBoardSaveData data, DungeonOfferBoard board)
        {
            if(data==null) return true; // Saves predating the selector generate their first board when opened.
            if(data.Offers==null || data.Offers.Any(o=>o==null)) return false;
            return board.TryRestore(data.Generation,data.SelectedIndex,data.Offers.Select(o=>new DungeonExpeditionOffer(o.DefinitionId,o.Seed,o.LevelOffset)).ToArray());
        }
    }
}
