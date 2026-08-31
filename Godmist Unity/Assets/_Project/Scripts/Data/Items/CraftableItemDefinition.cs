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

            var total = Ingredients
                .Where(requirement => requirement?.Material != null && requirement.Amount > 0)
                .Sum(requirement => (long)requirement.Amount
                    * GetIngredientBaseValue(requirement.Material, visiting));
            visiting.Remove(this);

            return total <= 0 ? 0 : (int)(total / OutputAmount);
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
