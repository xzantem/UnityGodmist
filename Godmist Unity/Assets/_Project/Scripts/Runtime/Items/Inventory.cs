using System;
using System.Collections.Generic;

namespace Godmist.Runtime.Items
{
    [Serializable]
    public class Inventory
    {
        public List<ItemInstance> Items = new();
        public float? MaxWeight;
    }
}