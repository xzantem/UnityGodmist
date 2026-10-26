using System.Collections.Generic;
using System.Linq;
using Godmist.Data.Items;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class MaterialRecipePickerTests
    {
        private const string Content = "Assets/_Project/ScriptableObjects/ContentPass1/";

        [TestCase("Assets/_Project/ScriptableObjects/Items/Materials/Smithing/CID_IronIngot.asset", "Metale")]
        [TestCase(Content + "item.vs10.sztabka_brazu.asset", "Metale")]
        [TestCase(Content + "item.vs10.sproszkowana_miedz.asset", "Metale")]
        [TestCase(Content + "item.vs10.paski_ze_skory_warana.asset", "Skóry")]
        [TestCase(Content + "item.vs10.brzozowa_deska.asset", "Drewno")]
        [TestCase(Content + "item.vs10.nitka_z_owczej_welny.asset", "Tkaniny")]
        [TestCase(Content + "item.vs10.igla_do_nici.asset", "Narzędzia")]
        public void AuthoredRecipesHaveFamiliesIndependentOfDisplayName(string path, string group)
        {
            var recipe = AssetDatabase.LoadAssetAtPath<CraftableItemDefinition>(path);
            Assert.That(recipe, Is.Not.Null, path);
            Assert.That(MaterialRecipePresentation.GroupLabel(MaterialRecipePresentation.GroupIndex(recipe)), Is.EqualTo(group));
            // Do not persist asset changes: an instantiated copy represents another locale.
            var copy = Object.Instantiate(recipe);
            try
            {
                var translated = new SerializedObject(copy);
                translated.FindProperty("<DisplayName>k__BackingField").stringValue = "Unrelated translation";
                translated.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(MaterialRecipePresentation.GroupLabel(MaterialRecipePresentation.GroupIndex(copy)), Is.EqualTo(group));
            }
            finally { Object.DestroyImmediate(copy); }
        }

        [Test]
        public void UnknownRecipeRemainsVisibleInOtherFamily()
        {
            var recipe = ScriptableObject.CreateInstance<CraftableItemDefinition>();
            try { Assert.That(MaterialRecipePresentation.GroupLabel(MaterialRecipePresentation.GroupIndex(recipe)), Is.EqualTo("Pozostałe")); }
            finally { Object.DestroyImmediate(recipe); }
        }

        [Test]
        public void AntidoteUsesTwoHerbSelectorsForEveryAuthoredVariant()
        {
            var recipe = AssetDatabase.LoadAssetAtPath<CraftableItemDefinition>(
                Content + "recipe.vs12.antidote.asset");
            Assert.That(recipe, Is.Not.Null);
            var picker = MaterialCraftingWindowView.VariantSelection.ForRecipe(recipe);
            Assert.That(picker, Is.Not.Null);
            Assert.That(picker.HasSecondChoice, Is.True);
            Assert.That(picker.OptionLabels.Count, Is.EqualTo(12));

            var seen = new HashSet<int>();
            for (var first = 0; first < picker.OptionLabels.Count; first++)
            for (var second = first; second < picker.OptionLabels.Count; second++)
            {
                var variant = picker.Resolve(first, second);
                Assert.That(seen.Add(variant), Is.True, $"Powtórzony wariant {first}/{second}");
                var herbs = recipe.GetIngredients(variant)
                    .Where(ingredient => ingredient.Material.DisplayName != "Alkohol")
                    .ToDictionary(ingredient => ingredient.Material.DisplayName,
                        ingredient => ingredient.Amount);
                Assert.That(herbs.Values.Sum(), Is.EqualTo(8));
                Assert.That(herbs[picker.OptionLabels[first]],
                    Is.EqualTo(first == second ? 8 : 4));
                Assert.That(herbs[picker.OptionLabels[second]],
                    Is.EqualTo(first == second ? 8 : 4));
            }
            Assert.That(seen.Count, Is.EqualTo(recipe.RecipeVariantCount));
        }

        [TestCase("Assets/_Project/ScriptableObjects/Items/Materials/Smithing/CID_WeakThreadNeedle.asset")]
        [TestCase(Content + "item.vs10.igla_do_nici.asset")]
        public void NeedleVariantsUseOneMetalSelector(string path)
        {
            var recipe = AssetDatabase.LoadAssetAtPath<CraftableItemDefinition>(path);
            Assert.That(recipe, Is.Not.Null, path);
            var picker = MaterialCraftingWindowView.VariantSelection.ForRecipe(recipe);
            Assert.That(picker, Is.Not.Null);
            Assert.That(picker.HasSecondChoice, Is.False);
            Assert.That(picker.OptionLabels.Count, Is.EqualTo(4));

            for (var variant = 0; variant < recipe.RecipeVariantCount; variant++)
            {
                var choice = picker.FirstChoiceFor(variant);
                Assert.That(picker.Resolve(choice, 0), Is.EqualTo(variant));
                Assert.That(recipe.GetIngredients(variant).Single().Material.DisplayName,
                    Is.EqualTo(picker.OptionLabels[choice]));
            }
        }

        [Test]
        public void NpcServicePrefabWiresBothVariantDropdowns()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_NpcServicesWindow.prefab");
            Assert.That(prefab, Is.Not.Null);
            var view = prefab.GetComponentInChildren<MaterialCraftingWindowView>(true);
            Assert.That(view, Is.Not.Null);
            var serialized = new SerializedObject(view);
            var section = serialized.FindProperty("variantSection").objectReferenceValue as GameObject;
            var first = serialized.FindProperty("firstVariantDropdown").objectReferenceValue as Component;
            var second = serialized.FindProperty("secondVariantDropdown").objectReferenceValue as Component;
            Assert.That(section, Is.Not.Null);
            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Not.Null);
            Assert.That(first.transform.parent, Is.EqualTo(section.transform));
            Assert.That(second.transform.parent, Is.EqualTo(section.transform));
        }
    }
}
