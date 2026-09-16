using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
namespace Godmist.Systems.Dungeons
{
    public static class DungeonOfferSystem
    {
        public static int PartyLevel(GameSession session)
        {
            var levels=session.PlayerParty.ActiveMemberIds.Where(session.CharactersById.ContainsKey)
                .Select(id=>Math.Clamp(session.CharactersById[id].Level,1,50)).ToArray();
            return levels.Length==0 ? 1 : (int)Math.Round(levels.Average(),MidpointRounding.AwayFromZero);
        }
        public static int OfferLevel(int average, int offset)
        {
            average=Math.Clamp(average,1,50);
            if(offset==0) return average;
            var direction=offset<0 ? -1 : 1;
            if(average==1 && direction<0 || average==50 && direction>0) direction=-direction;
            var distance=Math.Min(Math.Abs(offset),direction<0 ? average-1 : 50-average);
            return average+direction*distance;
        }
        public static bool EnsureOffers(GameSession session, IEnumerable<DungeonDefinition> candidates)
        {
            if(session==null || session.ActiveDungeon!=null) return false;
            if(session.DungeonOffers.Offers.Count!=0) return true;
            var pool=candidates?.Where(d=>d!=null && d.IsProcedural && !d.IsStoryDungeon && !string.IsNullOrWhiteSpace(d.Id))
                .GroupBy(d=>d.Id,StringComparer.Ordinal).Select(g=>g.First()).OrderBy(d=>d.Id,StringComparer.Ordinal).ToArray();
            if(pool==null || pool.Length==0) return false;
            // Stable campaign/generation seed: reopening, loading and party changes do not reroll offers.
            uint hash=2166136261;
            foreach(var c in session.CampaignId) hash=unchecked((hash^c)*16777619);
            var random=new Random(unchecked((int)hash+session.DungeonOffers.Generation*16777619));
            var offers=new List<DungeonExpeditionOffer>();var seeds=new HashSet<int>();
            for(var i=0;i<6;i++)
            {
                int seed; do { seed=random.Next(); } while(!seeds.Add(seed));
                int offset=i==0 ? 0 : (i==1 ? -1 : i==2 ? 1 : random.Next(2)==0 ? -1 : 1)*random.Next(1,4);
                offers.Add(new DungeonExpeditionOffer(pool[random.Next(pool.Length)].Id,seed,offset));
            }
            return session.DungeonOffers.TryRestore(session.DungeonOffers.Generation,0,offers);
        }
    }
}
