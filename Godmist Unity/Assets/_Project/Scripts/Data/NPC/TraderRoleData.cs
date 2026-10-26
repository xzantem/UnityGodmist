using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using UnityEngine;

namespace Godmist.Data.NPC
{
    [CreateAssetMenu(
        fileName = "TraderRole",
        menuName = "Godmist/NPC/Roles/Trader Role")]
    public class TraderRoleData : NpcRoleData
    {
        [field: SerializeField] public List<ItemDefinition> InitialOffer { get; private set; } = new();

        [field: SerializeField] public List<TraderStockEntry> LocalStock { get; private set; } = new();
        [field: SerializeField] public List<TraderStockGroup> RotatingStock { get; private set; } = new();

        // Pusta lista oznacza uniwersalnego handlarza.
        [field: SerializeField] public List<ItemType> RelevantItemTypes { get; private set; } = new();
    }

    [System.Serializable]
    public class TraderStockEntry
    {
        public ItemDefinition Item;
        [Min(1)] public int Limit = 1;
        [Min(1)] public int MinimumLevel = 1;
        [Min(1)] public int MaximumLevel = 50;
        public bool IsAvailable(int level) => Item != null && !string.IsNullOrWhiteSpace(Item.Id)
            && Limit > 0 && level >= MinimumLevel && level <= MaximumLevel;
    }

    [System.Serializable]
    public class TraderStockGroup
    {
        public string Id = string.Empty;
        [Min(1)] public int Slots = 1;
        public List<TraderStockEntry> Candidates = new();
    }
}
