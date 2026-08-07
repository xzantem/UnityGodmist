using System.Collections.Generic;
using Godmist.Data.Items;
using UnityEngine;

namespace Godmist.Data.NPC
{
    [CreateAssetMenu(
        fileName = "TraderRole",
        menuName = "Godmist/NPC/Roles/Trader Role")]
    public class TraderRoleData : NpcRoleData
    {
        [field: SerializeField] public List<ItemDefinition> InitialOffer
        {
            get;
            private set;
        } = new();
    }
}