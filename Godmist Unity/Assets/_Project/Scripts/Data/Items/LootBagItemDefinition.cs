using System.Collections.Generic;
using Godmist.Core.Enums;
using UnityEngine;

namespace Godmist.Data.Items
{
    [CreateAssetMenu(
        fileName = "LootBagItemDefinition",
        menuName = "Godmist/Items/Lootbag Item Definition")]
    public class LootBagItemDefinition : ItemDefinition
    {
        [field: SerializeField]
        public List<LootBagBracketData> Brackets { get; private set; } = new();

        [field: SerializeField]
        public BossUniqueDropData BossUniqueDrop { get; private set; }

        public bool TryGetBracket(
            LootLevelBracket bracket,
            out LootBagBracketData data)
        {
            data = null;

            if (Brackets == null) return false;

            foreach (var candidate in Brackets)
            {
                if (candidate != null && candidate.Bracket == bracket)
                {
                    data = candidate;
                    return true;
                }
            }

            return false;
        }

        private void OnValidate()
        {
            if (ItemType != ItemType.Lootbag)
            {
                Debug.LogWarning(
                    $"Lootbag '{name}' powinien miec ItemType Lootbag.",
                    this);
            }
        }
    }
}
