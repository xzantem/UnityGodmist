using System;
using System.Collections.Generic;
using Godmist.Runtime.Items;

namespace Godmist.Runtime.Combat
{
    [Serializable]
    public class CombatRewardResult
    {
        public int ExperiencePerActiveCharacter;
        public int Gold;
        public List<ItemInstance> Items = new();
    }
}