using System.Collections.Generic;
using UnityEngine;

namespace Godmist.Data.Items
{
    [CreateAssetMenu(
        fileName = "LootTable",
        menuName = "Godmist/Items/Loot Table")]
    public class LootTableData : ScriptableObject
    {
        [field: SerializeField]
        public List<LootEntryData> Entries { get; private set; } = new();
    }
}