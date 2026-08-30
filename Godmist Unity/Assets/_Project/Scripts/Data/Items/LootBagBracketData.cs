using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using UnityEngine;

namespace Godmist.Data.Items
{
    [Serializable]
    public class LootBagBracketData
    {
        [field: SerializeField]
        public LootLevelBracket Bracket { get; private set; }

        [field: SerializeField]
        public List<LootBagPoolData> Pools { get; private set; } = new();
    }
}
