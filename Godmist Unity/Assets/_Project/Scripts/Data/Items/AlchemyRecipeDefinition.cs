using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Interfaces;
using UnityEngine;

namespace Godmist.Data.Items
{
    [CreateAssetMenu(
        fileName = "AlchemyRecipe",
        menuName = "Godmist/Items/Alchemy Recipe")]
    public sealed class AlchemyRecipeDefinition : ScriptableObject, IIdentifiable
    {
        [field: SerializeField] public string Id { get; private set; } = string.Empty;
        [field: SerializeField] public string DisplayName { get; private set; } = string.Empty;
        [field: SerializeField] public string Description { get; private set; } = string.Empty;
        [field: SerializeField] public ConsumableItemDefinition Result { get; private set; }
        [field: SerializeField] public ItemDefinition Alcohol { get; private set; }
        [field: SerializeField, Min(1)] public int AlcoholAmount { get; private set; } = 1;
        [field: SerializeField] public List<CraftingMaterialRequirement> Ingredients { get; private set; } = new();
        [field: SerializeField] public ItemDefinition Catalyst { get; private set; }
        [field: SerializeField] public ConsumableCatalystType CatalystType { get; private set; }
        [field: SerializeField, Range(0, 5)] public int CatalystTier { get; private set; }
        [field: SerializeField] public List<ConsumableEffectDefinition> Effects { get; private set; } = new();
        [field: SerializeField, Min(1)] public int OutputAmount { get; private set; } = 1;

        public IReadOnlyList<CraftingMaterialRequirement> AllRequirements
        {
            get
            {
                var requirements = new List<CraftingMaterialRequirement>();
                if (Alcohol != null)
                {
                    requirements.Add(new CraftingMaterialRequirement(Alcohol, AlcoholAmount));
                }

                requirements.AddRange(Ingredients ?? Enumerable.Empty<CraftingMaterialRequirement>());
                if (Catalyst != null)
                {
                    requirements.Add(new CraftingMaterialRequirement(Catalyst, 1));
                }

                return requirements;
            }
        }

        public bool UsesCatalyst => Catalyst != null;
    }
}
