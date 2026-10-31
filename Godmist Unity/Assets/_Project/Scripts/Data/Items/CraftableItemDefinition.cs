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
        [field: SerializeField, Min(1)] public int ResultLevel { get; private set; } = 1;
        [field: SerializeField] public string PrimaryVariantLabel { get; private set; } = string.Empty;
        // Most material recipes are their own result. Fixed utility recipes use
        // the same NPC crafting flow while producing a ConsumableItemInstance.
        [field: SerializeField] public ItemDefinition CraftedResult { get; private set; }
        public ItemDefinition ResultDefinition => CraftedResult != null ? CraftedResult : this;
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
            => variantIndex == 0 ? !string.IsNullOrWhiteSpace(PrimaryVariantLabel)
                    ? PrimaryVariantLabel
                    : string.Join(", ", Ingredients.Where(i => i?.Material != null).Select(i => i.Material.DisplayName))
                : variantIndex > 0 && variantIndex < RecipeVariantCount ? AlternativeRecipes[variantIndex - 1]?.Label ?? string.Empty : string.Empty;

        /// <summary>
        /// Unit value inherited from the full cost of recipe ingredients. A CID
        /// ingredient resolves its own recipe recursively, which lets multi-step
        /// production chains retain their input value.
        /// </summary>
        public int CalculatedBaseValue => TryCalculateBaseValue(out var value, out var failure)
            ? value : throw new System.InvalidOperationException(failure.Message);

        public bool TryCalculateBaseValue(out int value, out CraftingValueFailure failure)
            => CraftingValueCalculator.TryGetValue(this, out value, out failure);
    }
}
