using System;
using System.Collections.Generic;
namespace Godmist.Runtime.Dungeons
{
    public sealed class DungeonExpeditionOffer
    {
        public string DefinitionId { get; }
        public int Seed { get; }
        public int LevelOffset { get; }
        public DungeonExpeditionOffer(string definitionId, int seed, int levelOffset)
        { DefinitionId = definitionId; Seed = seed; LevelOffset = levelOffset; }
    }
    public sealed class DungeonOfferBoard
    {
        private readonly List<DungeonExpeditionOffer> offers = new();
        public IReadOnlyList<DungeonExpeditionOffer> Offers => offers;
        public int Generation { get; private set; }
        public int SelectedIndex { get; private set; }
        public bool TrySelect(int index)
        { if (index < 0 || index >= offers.Count) return false; SelectedIndex = index; return true; }
        public void CompleteExpedition() { offers.Clear(); Generation = unchecked(Generation + 1); SelectedIndex = 0; }
        public bool TryRestore(int generation, int selectedIndex, IReadOnlyList<DungeonExpeditionOffer> next)
        {
            if (next == null || (next.Count != 0 && next.Count != 6) || selectedIndex < 0 || selectedIndex >= Math.Max(1,next.Count)) return false;
            var seeds = new HashSet<int>();
            for (var i=0;i<next.Count;i++)
            {
                var offer=next[i];
                if (offer == null || string.IsNullOrWhiteSpace(offer.DefinitionId) || !seeds.Add(offer.Seed)
                    || (i==0 ? offer.LevelOffset != 0 : Math.Abs((long)offer.LevelOffset) < 1 || Math.Abs((long)offer.LevelOffset) > 3)) return false;
            }
            offers.Clear(); offers.AddRange(next); Generation=generation; SelectedIndex=selectedIndex; return true;
        }
    }
}
