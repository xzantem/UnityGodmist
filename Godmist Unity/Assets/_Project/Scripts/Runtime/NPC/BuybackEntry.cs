using System;
using Godmist.Runtime.Items;

namespace Godmist.Runtime.NPC
{
    [Serializable]
    public class BuybackEntry
    {
        public ItemInstance Item;
        public int SoldPrice;

        public BuybackEntry(ItemInstance item, int soldPrice)
        {
            Item = item;
            SoldPrice = soldPrice;
        }
    }
}