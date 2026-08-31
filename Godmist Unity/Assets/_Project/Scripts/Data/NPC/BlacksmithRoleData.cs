using System;
using System.Collections.Generic;
using Godmist.Data.Items;
using UnityEngine;

namespace Godmist.Data.NPC
{
    [Serializable]
    public sealed class BlacksmithClassCraftingOffer
    {
        [field: SerializeField] public EquipmentItemDefinition WeaponBase { get; private set; }
        [field: SerializeField] public EquipmentItemDefinition ArmorBase { get; private set; }
    }

    [CreateAssetMenu(
        fileName = "BlacksmithRole",
        menuName = "Godmist/NPC/Roles/Blacksmith Role")]
    public sealed class BlacksmithRoleData : NpcRoleData
    {
        [field: SerializeField] public List<BlacksmithClassCraftingOffer> ClassOffers { get; private set; } = new();
        [field: SerializeField] public List<CraftableItemDefinition> MaterialRecipes
        {
            get;
            private set;
        } = new();
    }
}
