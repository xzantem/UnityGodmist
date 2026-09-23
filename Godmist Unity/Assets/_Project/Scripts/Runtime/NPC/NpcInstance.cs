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
        public long GoldBalance;
        public long TotalGoldReceived;
        public long TotalGoldSpent;
        public float GuildPayoutProgress;
        public ShopInventory ShopInventory;
        public System.Collections.Generic.List<string> RotatingStockItemIds = new();
        // Tracks exact generated instances, not their shared ItemDefinition Id.
        public System.Collections.Generic.List<string> CraftedStockInstanceIds = new();
    }
}
