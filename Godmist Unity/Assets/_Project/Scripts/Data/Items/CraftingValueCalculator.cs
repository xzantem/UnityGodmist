using System;
using System.Collections.Generic;
using System.Linq;

namespace Godmist.Data.Items
{
    public enum CraftingValueFailureKind { DependencyCycle, InvalidRecipe, ArithmeticOverflow }

    public sealed class CraftingValueFailure
    {
        public CraftingValueFailureKind Kind { get; }
        public string RecipeId { get; }
        public int VariantIndex { get; }
        public int IngredientIndex { get; }
        public IReadOnlyList<string> DependencyChain { get; }
        public string Message { get; }

        internal CraftingValueFailure(CraftingValueFailureKind kind, CraftableItemDefinition recipe,
            int variant, int ingredient, IEnumerable<CraftableItemDefinition> path)
        {
            Kind = kind;
            RecipeId = Identify(recipe);
            VariantIndex = variant;
            IngredientIndex = ingredient;
            DependencyChain = Array.AsReadOnly(path.Select(Identify).ToArray());
            Message = $"Błąd wyceny receptury ({kind}): {string.Join(" → ", DependencyChain)}"
                + $"; receptura '{RecipeId}', wariant {variant}, składnik {ingredient}.";
        }

        private static string Identify(CraftableItemDefinition recipe) => recipe == null ? "<null>"
            : !string.IsNullOrEmpty(recipe.Id) ? recipe.Id : recipe.name;
    }

    /// <summary>Per-call graph traversal. No cached state survives changes to content.</summary>
    public static class CraftingValueCalculator
    {
        public static bool TryGetValue(ItemDefinition definition, out int value, out CraftingValueFailure failure)
        {
            if (definition is CraftableItemDefinition)
                return new Evaluation().TryEvaluate(definition, out value, out failure);
            value = Math.Max(0, definition?.BaseValue ?? 0);
            failure = null;
            return true;
        }

        public static bool TryValidate(IEnumerable<ItemDefinition> definitions, out string failureReason)
        {
            var evaluation = new Evaluation();
            foreach (var definition in definitions)
                if (!evaluation.TryEvaluate(definition, out _, out var failure))
                {
                    failureReason = failure.Message;
                    return false;
                }
            failureReason = string.Empty;
            return true;
        }

        private sealed class Evaluation
        {
            private readonly Dictionary<CraftableItemDefinition, int> completed = new();
            private readonly List<CraftableItemDefinition> path = new();

            public bool TryEvaluate(ItemDefinition definition, out int value, out CraftingValueFailure failure,
                CraftableItemDefinition parent = null, int variantIndex = -1, int ingredientIndex = -1)
            {
                value = 0;
                failure = null;
                if (definition is not CraftableItemDefinition recipe)
                {
                    value = Math.Max(0, definition?.BaseValue ?? 0);
                    return true;
                }
                if (path.Contains(recipe))
                {
                    failure = new CraftingValueFailure(CraftingValueFailureKind.DependencyCycle,
                        parent ?? recipe, variantIndex, ingredientIndex, path.Concat(new[] { recipe }));
                    return false;
                }
                if (completed.TryGetValue(recipe, out value)) return true;
                path.Add(recipe);
                try
                {
                    if (recipe.OutputAmount < 1 || recipe.Ingredients == null || recipe.Ingredients.Count == 0)
                    {
                        failure = new CraftingValueFailure(CraftingValueFailureKind.InvalidRecipe, recipe, -1, -1, path);
                        return false;
                    }
                    long minimum = long.MaxValue;
                    for (var variant = 0; variant < recipe.RecipeVariantCount; variant++)
                    {
                        var ingredients = recipe.GetIngredients(variant);
                        if (ingredients == null || ingredients.Count == 0) continue;
                        long total = 0;
                        var valid = true;
                        for (var index = 0; index < ingredients.Count; index++)
                        {
                            var input = ingredients[index];
                            if (input?.Material == null) { valid = false; continue; }
                            // Visit every dependency, including malformed/expensive alternatives:
                            // neither a cheaper variant nor an invalid amount may hide a cycle.
                            if (!TryEvaluate(input.Material, out var inputValue, out failure, recipe, variant, index))
                                return false;
                            if (input.Amount <= 0) { valid = false; continue; }
                            total = checked(total + (long)input.Amount * inputValue);
                        }
                        if (valid) minimum = Math.Min(minimum, total);
                    }
                    if (minimum == long.MaxValue)
                    {
                        failure = new CraftingValueFailure(CraftingValueFailureKind.InvalidRecipe, recipe, -1, -1, path);
                        return false;
                    }
                    value = (int)Math.Min(int.MaxValue, minimum / recipe.OutputAmount);
                    completed.Add(recipe, value);
                    return true;
                }
                catch (OverflowException)
                {
                    failure = new CraftingValueFailure(CraftingValueFailureKind.ArithmeticOverflow, recipe, -1, -1, path);
                    return false;
                }
                finally { path.RemoveAt(path.Count - 1); }
            }
        }
    }
}
