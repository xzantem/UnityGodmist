using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using UnityEngine;

namespace Godmist.Data.NPC
{
    [CreateAssetMenu(
        fileName = "EnchanterRole",
        menuName = "Godmist/NPC/Roles/Enchanter Role")]
    public sealed class EnchanterRoleData : NpcRoleData, IMaterialCraftingSpecialist
    {
        public MaterialDiscipline CraftingDiscipline => MaterialDiscipline.Runic;
        IReadOnlyList<CraftableItemDefinition> IMaterialCraftingSpecialist.MaterialRecipes => MaterialRecipes;

        [field: SerializeField] public List<JewelryItemDefinition> JewelryOffers { get; private set; } = new();
        [field: SerializeField] public List<CraftableItemDefinition> MaterialRecipes { get; private set; } = new();
        [field: SerializeField] public List<GalduryteStoneData> GalduryteStones { get; private set; } = new();
        [field: SerializeField] public List<GaldurytePowderData> GaldurytePowders { get; private set; } = new();
        [field: SerializeField] public GalduryteItemDefinition GalduryteDefinition { get; private set; }
    }
}
