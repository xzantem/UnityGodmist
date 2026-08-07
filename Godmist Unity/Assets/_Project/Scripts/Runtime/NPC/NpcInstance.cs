using System;
using Godmist.Data.NPC;

namespace Godmist.Runtime.NPC
{
    [Serializable]
    public class NpcInstance
    {
        public string NpcId = string.Empty;
        public NpcDefinition Definition;

        public int LoyaltyLevel = 1;
        public int SpentGoldTotal;
    }
}