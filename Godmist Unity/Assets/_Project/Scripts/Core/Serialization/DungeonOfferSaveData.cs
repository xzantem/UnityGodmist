using System;
using System.Collections.Generic;
namespace Godmist.Core.Serialization
{
    [Serializable] public sealed class DungeonOfferBoardSaveData
    {
        public int Generation;
        public int SelectedIndex;
        public List<DungeonOfferSaveData> Offers = new();
    }
    [Serializable] public sealed class DungeonOfferSaveData
    {
        public string DefinitionId;
        public int Seed;
        public int LevelOffset;
    }
}
