using System.Collections.Generic;
using Godmist.Data.Items;
using UnityEngine;

namespace Godmist.Data.NPC
{
    [CreateAssetMenu(
        fileName = "EnchanterRole",
        menuName = "Godmist/NPC/Roles/Enchanter Role")]
    public sealed class EnchanterRoleData : NpcRoleData
    {
        [field: SerializeField] public List<JewelryItemDefinition> JewelryOffers { get; private set; } = new();
    }
}
