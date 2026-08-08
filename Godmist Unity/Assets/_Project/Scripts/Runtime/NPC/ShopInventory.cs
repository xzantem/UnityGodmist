using System.Collections.Generic;
using Godmist.Runtime.Items;

namespace Godmist.Runtime.NPC
{
    public class ShopInventory : Inventory
    {
        // Przedmioty sprzedane przez gracza. Nie są częścią rotującej oferty.
        public List<BuybackEntry> BuybackStock = new();
    }
}