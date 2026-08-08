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

        // Pusta lista oznacza uniwersalnego handlarza.
        [field: SerializeField] public List<ItemType> RelevantItemTypes { get; private set; } = new();
    }
}