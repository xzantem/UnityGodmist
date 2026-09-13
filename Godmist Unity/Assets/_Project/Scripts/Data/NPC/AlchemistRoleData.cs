using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using UnityEngine;

namespace Godmist.Data.NPC
{
    [CreateAssetMenu(
        fileName = "AlchemistRole",
        menuName = "Godmist/NPC/Roles/Alchemist Role")]
    public sealed class AlchemistRoleData : NpcRoleData, IMaterialCraftingSpecialist
    {
        public MaterialDiscipline CraftingDiscipline => MaterialDiscipline.Alchemy;
        IReadOnlyList<CraftableItemDefinition> IMaterialCraftingSpecialist.MaterialRecipes => MaterialRecipes;

        [field: SerializeField] public List<CraftableItemDefinition> MaterialRecipes { get; private set; } = new();
        [field: SerializeField] public List<AlchemyIngredientData> IngredientOptions { get; private set; } = new();
        [field: SerializeField] public List<AlchemyMediumData> PotionAlcohols { get; private set; } = new();
        [field: SerializeField] public List<AlchemyMediumData> BombGunpowders { get; private set; } = new();
        [field: SerializeField] public List<AlchemyCatalystData> Catalysts { get; private set; } = new();
        [field: SerializeField] public ConsumableItemDefinition PotionBaseDefinition { get; private set; }
        [field: SerializeField] public ConsumableItemDefinition BombBaseDefinition { get; private set; }
        [field: SerializeField] public List<AlchemyTierNameData> TierNameAdjectives { get; private set; } = new();

        // Kept for serialized compatibility with the first Alchemist prototype.
        [field: SerializeField] public List<AlchemyRecipeDefinition> PotionRecipes { get; private set; } = new();
        [field: SerializeField] public ItemDefinition RefillAlcohol { get; private set; }
    }
}
