using System;
using System.Collections.Generic;
using Godmist.Runtime.Items;

namespace Godmist.Runtime.NPC
{
    [Serializable]
    public class ShopInventory : Inventory
    {
        // Przedmioty odkupione od gracza nie należą do rotacyjnej oferty.
        public List<ItemInstance> BuybackStock = new();
    }
}