using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using UnityEngine;
using UnityEngine.Serialization;

namespace Godmist.Data.Items
{
    /// <summary>
    /// A craftable item is both the output ItemDefinition and its deterministic
    /// specialist recipe. Its Item Type can be Material, Potion, Tool, or another
    /// supported item type.
    /// </summary>
    [CreateAssetMenu(
        fileName = "CraftableItem",
        menuName = "Godmist/Items/Craftable Item Definition")]
    public sealed class CraftableItemDefinition : ItemDefinition
    {
        [field: FormerlySerializedAs("<Discipline>k__BackingField"), SerializeField]
        public MaterialDiscipline CraftingDiscipline { get; private set; }
        [field: SerializeField, Min(1)] public int OutputAmount { get; private set; } = 1;
        [field: SerializeField] public List<CraftingMaterialRequirement> Ingredients
        {
            get;
            private set;
        } = new();

        [System.Serializable]
        public sealed class RecipeVariant
        {
            public string Label = string.Empty;
            public List<CraftingMaterialRequirement> Ingredients = new();
        }
        [field: SerializeField] public List<RecipeVariant> AlternativeRecipes { get; private set; } = new();
        public int RecipeVariantCount => 1 + (AlternativeRecipes?.Count ?? 0);
        public IReadOnlyList<CraftingMaterialRequirement> GetIngredients(int variantIndex = 0)
            => variantIndex == 0 ? Ingredients : variantIndex > 0 && variantIndex < RecipeVariantCount
                ? AlternativeRecipes[variantIndex - 1]?.Ingredients : null;
        public string GetVariantLabel(int variantIndex = 0)
            => variantIndex == 0 ? string.Join(", ", Ingredients.Where(i => i?.Material != null).Select(i => i.Material.DisplayName))
                : variantIndex > 0 && variantIndex < RecipeVariantCount ? AlternativeRecipes[variantIndex - 1]?.Label ?? string.Empty : string.Empty;

        /// <summary>
        /// Unit value inherited from the full cost of recipe ingredients. A CID
        /// ingredient resolves its own recipe recursively, which lets multi-step
        /// production chains retain their input value.
        /// </summary>
        public int CalculatedBaseValue => CalculateBaseValue(new HashSet<CraftableItemDefinition>());

        private int CalculateBaseValue(HashSet<CraftableItemDefinition> visiting)
        {
            if (OutputAmount < 1 || Ingredients == null || Ingredients.Count == 0
                || !visiting.Add(this))
            {
                return 0;
            }

            long minimum = long.MaxValue;
            for (var variant = 0; variant < RecipeVariantCount; variant++)
            {
                var ingredients = GetIngredients(variant);
                if (ingredients == null || ingredients.Count == 0
                    || ingredients.Any(i => i?.Material == null || i.Amount <= 0)) continue;
                var total = ingredients.Sum(i => (long)i.Amount * GetIngredientBaseValue(i.Material, visiting));
                minimum = System.Math.Min(minimum, total);
            }
            visiting.Remove(this);
            return minimum == long.MaxValue || minimum <= 0 ? 0
                : (int)System.Math.Min(int.MaxValue, minimum / OutputAmount);
        }

        private static int GetIngredientBaseValue(
            ItemDefinition ingredient,
            HashSet<CraftableItemDefinition> visiting)
        {
            return ingredient is CraftableItemDefinition craftable
                ? craftable.CalculateBaseValue(visiting)
                : Mathf.Max(0, ingredient.BaseValue);
        }
    }
}
