using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Data.Characters;
using Godmist.Data.NPC;
using Godmist.Data.World;
using Godmist.Editor;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;
using Godmist.Systems.Items;
using Godmist.Systems.NPC;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class RecipeValuationCycleTests
    {
        private readonly List<ScriptableObject> objects = new();
        [TearDown] public void Cleanup()
        {
            foreach (var item in objects) UnityEngine.Object.DestroyImmediate(item);
            objects.Clear();
        }

        [Test] public void SelfCycleCannotTurnIntoAFreeIngredient()
        {
            var a = Recipe("A", Leaf(10));
            a.Ingredients.Add(new CraftingMaterialRequirement(a, 1));
            AssertCycle(a, "A", "A");
            Assert.Throws<InvalidOperationException>(() => { var ignored = a.CalculatedBaseValue; });
        }

        [Test] public void NestedCycleReportsTheFullPathAndClosingEdge()
        {
            var a = Recipe("A", Leaf(10)); var b = Recipe("B", a); var root = Recipe("root", b);
            a.Ingredients.Add(new CraftingMaterialRequirement(b, 1));
            AssertCycle(root, "root", "B", "A", "B");
            root.TryCalculateBaseValue(out _, out var failure);
            Assert.That(failure.RecipeId, Is.EqualTo("A"));
            Assert.That(failure.VariantIndex, Is.Zero);
            Assert.That(failure.IngredientIndex, Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void AlternativeCycleInvalidatesTheWholeRecipeEvenWithMalformedAmount(bool malformed)
        {
            var a = Recipe("A", Leaf(100));
            a.AlternativeRecipes.Add(new CraftableItemDefinition.RecipeVariant {
                Ingredients = new() { new CraftingMaterialRequirement(a, malformed ? 0 : 1) } });
            AssertCycle(a, "A", "A");
            a.TryCalculateBaseValue(out _, out var failure);
            Assert.That(failure.VariantIndex, Is.EqualTo(1));
        }

        [Test] public void SharedDependencyIsNotACycleAndKeepsFloorAndCheapestVariantRules()
        {
            var shared = Recipe("shared", Leaf(11));
            var a = Recipe("A", shared); var b = Recipe("B", shared);
            var root = Recipe("root", a, b);
            root.AlternativeRecipes.Add(new CraftableItemDefinition.RecipeVariant {
                Ingredients = new() { new CraftingMaterialRequirement(Leaf(17), 1) } });
            Set(root, "OutputAmount", 2);
            Assert.That(root.TryCalculateBaseValue(out var value, out var failure), Is.True);
            Assert.That(failure, Is.Null); Assert.That(value, Is.EqualTo(8));
        }

        [Test] public void LegitimateZeroIsNotAnError()
        {
            var a = Recipe("A", Leaf(0));
            Assert.That(a.TryCalculateBaseValue(out var value, out var failure), Is.True);
            Assert.That(value, Is.Zero); Assert.That(failure, Is.Null);
        }

        [Test] public void FailedOrSuccessfulEvaluationsDoNotLeakIntoTheNextCall()
        {
            var a = Recipe("A", Leaf(10)); Assert.That(a.CalculatedBaseValue, Is.EqualTo(10));
            a.Ingredients.Add(new CraftingMaterialRequirement(a, 1)); AssertCycle(a, "A", "A");
            a.Ingredients.RemoveAt(1); Set(a.Ingredients[0].Material, "BaseValue", 20);
            Assert.That(a.CalculatedBaseValue, Is.EqualTo(20));
        }

        [TestCase(0)] [TestCase(-1)] public void InvalidOutputIsAnExplicitFailure(int output)
        {
            var a = Recipe("A", Leaf(10)); Set(a, "OutputAmount", output);
            Assert.That(a.TryCalculateBaseValue(out _, out var failure), Is.False);
            Assert.That(failure.Kind, Is.EqualTo(CraftingValueFailureKind.InvalidRecipe));
        }

        [Test] public void ArithmeticOverflowFailsRatherThanWrappingThePrice()
        {
            var a = Recipe("A", Leaf(int.MaxValue));
            a.Ingredients.Clear();
            for (var i = 0; i < 3; i++) a.Ingredients.Add(new CraftingMaterialRequirement(Leaf(int.MaxValue), int.MaxValue));
            Assert.That(a.TryCalculateBaseValue(out _, out var failure), Is.False);
            Assert.That(failure.Kind, Is.EqualTo(CraftingValueFailureKind.ArithmeticOverflow));
        }

        [Test] public void AuthoringValidationUsesTheSameCycleDiagnostic()
        {
            var a = Recipe("A", Leaf(10)); var b = Recipe("B", a);
            a.Ingredients.Add(new CraftingMaterialRequirement(b, 1));
            var report = ContentAuthoringValidator.Validate(a);
            Assert.That(report.Errors.Any(e => e.Contains("DependencyCycle") && e.Contains("A → B → A")), Is.True);
        }

        [Test] public void CurrentAssetGraphsAreValidAndExportTheirPrices()
        {
            var rows = new List<string>();
            foreach (var path in AssetDatabase.FindAssets("t:CraftableItemDefinition")
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p))
            {
                var recipe = AssetDatabase.LoadAssetAtPath<CraftableItemDefinition>(path);
                Assert.That(recipe.TryCalculateBaseValue(out var value, out var failure), Is.True, failure?.Message ?? path);
                rows.Add(path + "\t" + value);
            }
            Assert.That(rows, Is.Not.Empty);
            var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/recipe-valuation-cycles-20261029"));
            Directory.CreateDirectory(folder);
            File.WriteAllLines(Path.Combine(folder, "prices-after.tsv"), rows);
        }

        [Test] public void QuoteFailureIsDistinctFromAValidZeroPriceAcrossTradeApis()
        {
            var recipe = Cyclic(); var (_, _, npc) = Session(recipe);
            var item = new ItemInstanceFactory().Create(recipe); var trade = new TradeService();
            Assert.That(trade.TryGetBuyPrice(item, out _, out var reason), Is.False);
            Assert.That(reason, Does.Contain("DependencyCycle"));
            Assert.That(trade.TryGetBuyPrice(npc, item, out _, out reason), Is.False);
            Assert.That(trade.TryGetBuybackPrice(npc, new BuybackEntry(item, 1), out _, out reason), Is.False);
            Assert.That(trade.TryGetSellPrice(npc, item, out _, out reason), Is.False);
            Assert.Throws<InvalidOperationException>(() => trade.GetPriceToBuyFromTrader(item));
            Assert.Throws<InvalidOperationException>(() => trade.GetPriceWhenSellingToTrader(npc, item));
            var free = Recipe("free", Leaf(0));
            var freeItem = new ItemInstanceFactory().Create(free);
            Assert.That(trade.TryGetBuyPrice(freeItem, out var price, out reason), Is.True, reason);
            Assert.That(price, Is.Zero); Assert.That(reason, Is.Empty);
        }

        [TestCase(false)] [TestCase(true)]
        public void TradeRejectsCyclicDefinitionsEvenWithPositiveAuthoredValue(bool positiveValue)
        {
            var recipe = Cyclic(); var (session, owner, npc) = Session(recipe);
            var item = new ItemInstanceFactory().Create(recipe);
            // A positive authored price must not conceal an invalid source graph.
            if (positiveValue) Set(recipe, "BaseValue", 999);
            npc.ShopInventory.Items.Add(item);
            var trade = new TradeService(); var gold = session.Gold;
            Assert.That(trade.CanBuyItem(npc, item, out var reason), Is.False);
            Assert.That(reason, Does.Contain("DependencyCycle"));
            Assert.That(trade.TryBuyFromTrader(session, npc, owner, item.InstanceId, out var paid), Is.False);
            Assert.That(paid, Is.Zero); Assert.That(npc.ShopInventory.Items, Does.Contain(item));
            npc.ShopInventory.Items.Remove(item); owner.Inventory.Items.Add(item);
            Assert.That(trade.TryGetSellQuote(npc, item, out _, out _, out reason), Is.False);
            Assert.That(trade.TrySellToTrader(session, npc, owner, item.InstanceId, out _), Is.False);
            Assert.That(owner.Inventory.Items, Does.Contain(item));
            Assert.That(session.Gold, Is.EqualTo(gold)); Assert.That(npc.GoldBalance, Is.EqualTo(500));
            Assert.That(npc.SpentGoldTotal, Is.EqualTo(123));
            Assert.That(Godmist.Views.UI.Workspace.UIWorkspaceTooltipView.BuildItemMeta(item), Does.Contain("Błąd wyceny"));
        }

        [Test] public void MaterialCraftAndMissingInputPurchaseRejectAnInvalidAlternativeBeforeMutation()
        {
            var recipe = Cyclic(); var (session, owner, npc) = Session(recipe);
            var input = new ItemInstanceFactory().Create(recipe.Ingredients[0].Material);
            npc.ShopInventory.Items.Add(input);
            var request = new MaterialCraftRequest { Recipe = recipe, RecipientCharacterId = owner.CharacterId };
            var service = new MaterialCraftingService();
            Assert.That(service.CreatePreview(session, npc, request).FailureReason, Does.Contain("DependencyCycle"));
            Assert.That(service.TryCraft(session, npc, request, out _, out var reason), Is.False);
            Assert.That(reason, Does.Contain("DependencyCycle"));
            Assert.That(service.TryBuyMissingMaterial(session, npc, request, input.Definition.Id, out var paid, out reason), Is.False);
            Assert.That(reason, Does.Contain("DependencyCycle")); Assert.That(paid, Is.Zero);
            Assert.That(session.Gold, Is.EqualTo(10000)); Assert.That(owner.Inventory.Items, Is.Empty);
            Assert.That(npc.ShopInventory.Items, Does.Contain(input));
        }

        [Test] public void UpgradeAndReforgeRejectCyclicPartMaterialsWithoutPaymentOrRng()
        {
            var recipe = Cyclic(); var (session, owner, npc) = Session(recipe);
            var definition = New<EquipmentItemDefinition>(); Set(definition, "Id", "equipment"); Set(definition, "ItemType", ItemType.Weapon);
            var item = (EquipmentItemInstance)new ItemInstanceFactory().Create(definition);
            item.PartIds = new() { "part" }; item.CraftedBaseValue = 500;
            owner.Inventory.Items.Add(item);
            var part = New<EquipmentPartDefinition>(); Set(part, "Id", "part");
            Set(part, "CraftingMaterials", new List<CraftingMaterialRequirement> { new(recipe, 1) });
            var catalog = New<GameContentCatalog>(); catalog.EquipmentPartDefinitions.Add(part);
            var random = new CountingRandom(); var service = new EquipmentUpgradeService(random);
            Assert.That(service.CreateUpgradePreview(session, npc, item, .1f, .9f, catalog).FailureReason, Does.Contain("DependencyCycle"));
            Assert.That(service.TryUpgrade(session, npc, owner, item, .1f, .9f, catalog, out _, out _, out _), Is.False);
            Assert.That(service.TryReforge(session, npc, owner, item, catalog, out _, out _, out _), Is.False);
            Assert.That(random.Calls, Is.Zero); Assert.That(session.Gold, Is.EqualTo(10000));
            Assert.That(item.UpgradeProgress, Is.Zero); Assert.That(item.CraftedBaseValue, Is.EqualTo(500));
        }

        [Test] public void BuybackRejectsCyclicItemBeforeRemovingItFromStock()
        {
            var recipe = Cyclic(); var (session, owner, npc) = Session(recipe);
            var item = new ItemInstanceFactory().Create(recipe);
            var entry = new BuybackEntry(item, 100); npc.ShopInventory.BuybackStock.Add(entry);
            Assert.That(new TradeService().TryBuyFromBuyback(session, npc, owner, item.InstanceId, out var paid), Is.False);
            Assert.That(paid, Is.Zero); Assert.That(session.Gold, Is.EqualTo(10000));
            Assert.That(npc.ShopInventory.BuybackStock, Does.Contain(entry)); Assert.That(owner.Inventory.Items, Is.Empty);
        }

        [Test] public void JewelryGenerationAndCraftingRefuseCyclicMaterials()
        {
            var recipe = Cyclic(); var (session, owner, npc) = Session(recipe);
            var metal = new JewelryMetalData(); Set(metal, "Material", recipe); Set(metal, "Amount", 1);
            var gem = New<JewelryGemDefinition>(); Set(gem, "CraftingMaterial", Leaf(20));
            var definition = New<JewelryItemDefinition>(); Set(definition, "Id", "ring"); Set(definition, "ItemType", ItemType.Jewelry);
            definition.Metals.Add(metal); definition.Gems.Add(gem);
            var role = New<EnchanterRoleData>(); role.JewelryOffers.Add(definition); npc.Definition.Roles.Add(role);
            var request = new JewelryCraftRequest { Definition = definition, Metal = metal, Gem = gem, RecipientCharacterId = owner.CharacterId };
            var generation = new JewelryGenerationService();
            Assert.That(generation.GenerateSelected(definition, metal, gem), Is.Null);
            Assert.That(generation.Generate(definition, 50), Is.Null);
            var service = new JewelryCraftingService();
            Assert.That(service.CreatePreview(session, npc, request).FailureReason, Does.Contain("DependencyCycle"));
            Assert.That(service.TryCraft(session, npc, request, out _, out var reason), Is.False);
            Assert.That(reason, Does.Contain("DependencyCycle")); Assert.That(session.Gold, Is.EqualTo(10000));
            Assert.That(owner.Inventory.Items, Is.Empty);
        }

        [TestCase(false)] [TestCase(true)]
        public void AlchemyRefusesACyclicIngredientOrMediumBeforeCrafting(bool cyclicMedium)
        {
            var recipe = Cyclic(); var (session, owner, npc) = Session(recipe);
            Set(recipe, "MaterialDiscipline", MaterialDiscipline.Alchemy);
            var inputs = new List<ItemDefinition> { cyclicMedium ? Leaf(10) : recipe, Leaf(20), Leaf(30) };
            var medium = cyclicMedium ? recipe : Leaf(40);
            var role = New<AlchemistRoleData>();
            foreach (var input in inputs)
            {
                Set(input, "MaterialDiscipline", MaterialDiscipline.Alchemy);
                var option = new AlchemyIngredientData(); Set(option, "Material", input);
                Set(option, "PotionEffect", new ConsumableEffectDefinition()); role.IngredientOptions.Add(option);
            }
            Set(medium, "MaterialDiscipline", MaterialDiscipline.Alchemy);
            var mediumOption = new AlchemyMediumData(); Set(mediumOption, "Material", medium); Set(mediumOption, "Tier", 5);
            role.PotionAlcohols.Add(mediumOption);
            var potion = New<ConsumableItemDefinition>(); Set(potion, "Id", "potion"); Set(potion, "UseType", ConsumableUseType.Potion);
            Set(role, "PotionBaseDefinition", potion); npc.Definition.Roles.Add(role);
            var request = new AlchemyCraftRequest { Ingredients = inputs, Medium = medium, RecipientCharacterId = owner.CharacterId };
            var service = new AlchemyCraftingService();
            Assert.That(service.CreatePreview(session, npc, request).FailureReason, Does.Contain("DependencyCycle"));
            Assert.That(service.TryCraft(session, npc, request, out _, out var reason), Is.False);
            Assert.That(reason, Does.Contain("DependencyCycle")); Assert.That(session.Gold, Is.EqualTo(10000));
            Assert.That(service.TryBuyMissingMaterial(session, npc, request, medium.Id, out var paid, out reason), Is.False);
            Assert.That(reason, Does.Contain("DependencyCycle")); Assert.That(paid, Is.Zero);
            Assert.That(owner.Inventory.Items, Is.Empty);
        }

        [Test] public void GalduryteMerchantAndDebugGenerationRefuseCyclesBeforeRolling()
        {
            var recipe = Cyclic(); var (_, _, npc) = Session(recipe); var powder = Leaf(20);
            var role = New<EnchanterRoleData>();
            var tier = Enum.GetValues(typeof(GalduryteTier)).Cast<GalduryteTier>().First(t => GalduryteRuntimeRules.GetLevel(t) > 0);
            role.GalduryteStones.Add(new GalduryteStoneData(recipe, tier));
            role.GaldurytePowders.Add(new GaldurytePowderData(powder, GaldurytePowderColor.Red));
            var definition = New<GalduryteItemDefinition>(); Set(definition, "Id", "galduryte");
            Set(role, "GalduryteDefinition", definition); npc.Definition.Roles.Add(role);
            var random = new CountingRandom(); var service = new GalduryteCraftingService(random: random);
            Assert.That(service.CreateForMerchantStock(npc, new GalduryteCraftRequest { Stone = recipe, Powder = powder }), Is.Null);
            Assert.That(service.TryCreateDebugGalduryte(definition, recipe, powder, tier, default, false, out _, out var reason), Is.False);
            Assert.That(reason, Does.Contain("DependencyCycle")); Assert.That(random.Calls, Is.Zero);
        }

        [TestCase(ItemType.Weapon)] [TestCase(ItemType.Armor)]
        public void EquipmentGenerationAndCraftingRejectCyclesInPartRecipes(ItemType itemType)
        {
            var recipe = Cyclic(); var (session, owner, npc) = Session(recipe);
            var characterClass = New<CharacterClassData>(); Set(characterClass, "Id", "class.scout");
            var definition = New<EquipmentItemDefinition>(); Set(definition, "Id", "equipment");
            Set(definition, "ItemType", itemType); Set(definition, "RequiredClass", characterClass);
            var kinds = itemType == ItemType.Weapon
                ? new[] { EquipmentPartKind.WeaponHead, EquipmentPartKind.WeaponBinder, EquipmentPartKind.WeaponHandle }
                : new[] { EquipmentPartKind.ArmorPlate, EquipmentPartKind.ArmorBinder, EquipmentPartKind.ArmorBase };
            var parts = kinds.Select(kind => {
                var part = New<EquipmentPartDefinition>(); Set(part, "Id", kind.ToString());
                Set(part, "Kind", kind); Set(part, "RequiredClass", characterClass);
                Set(part, "CraftingMaterials", new List<CraftingMaterialRequirement> { new(recipe, 1) }); return part;
            }).ToList();
            Assert.That(new EquipmentGenerationService().GenerateFromSelectedParts(definition, parts,
                EquipmentQuality.Normal, RarityTier.Common), Is.Null);
            var request = new EquipmentCraftRequest { Definition = definition, Parts = parts,
                Quality = EquipmentQuality.Normal, RecipientCharacterId = owner.CharacterId };
            var service = new BlacksmithCraftingService();
            Assert.That(service.CreatePreview(session, npc, request).FailureReason, Does.Contain("DependencyCycle"));
            Assert.That(service.TryCraft(session, npc, request, out _, out var reason), Is.False);
            Assert.That(reason, Does.Contain("DependencyCycle")); Assert.That(session.Gold, Is.EqualTo(10000));
            Assert.That(service.TryBuyMissingMaterial(session, npc, request, recipe.Id, out var paid, out reason), Is.False);
            Assert.That(reason, Does.Contain("DependencyCycle")); Assert.That(paid, Is.Zero);
            Assert.That(owner.Inventory.Items, Is.Empty);
        }

        [Test] public void RecipeWithNoValidVariantIsNotMistakenForALegitimateZero()
        {
            var recipe = Recipe("invalid", Leaf(10)); Set(recipe.Ingredients[0], "Amount", 0);
            Assert.That(recipe.TryCalculateBaseValue(out _, out var failure), Is.False);
            Assert.That(failure.Kind, Is.EqualTo(CraftingValueFailureKind.InvalidRecipe));
        }

        [Test] public void BatchPurchaseCannotSmuggleACyclicZeroPriceItemWithAPaidItem()
        {
            var input = Leaf(10); var recipe = Recipe("valid", input); Set(recipe.Ingredients[0], "Amount", 2);
            var (session, owner, npc) = Session(recipe);
            var invalid = Cyclic(); Set(invalid, "Id", input.Id);
            var good = new ItemInstanceFactory().Create(input); var bad = new ItemInstanceFactory().Create(invalid);
            npc.ShopInventory.Items.Add(good); npc.ShopInventory.Items.Add(bad);
            var request = new MaterialCraftRequest { Recipe = recipe, RecipientCharacterId = owner.CharacterId };
            Assert.That(new MaterialCraftingService().TryBuyMissingMaterial(session, npc, request, input.Id,
                out var paid, out var reason), Is.False);
            Assert.That(reason, Does.Contain("DependencyCycle")); Assert.That(paid, Is.Zero);
            Assert.That(npc.ShopInventory.Items, Is.EqualTo(new[] { good, bad }));
            Assert.That(owner.Inventory.Items, Is.Empty); Assert.That(session.Gold, Is.EqualTo(10000));
        }

        private CraftableItemDefinition Cyclic()
        {
            var a = Recipe("A", Leaf(10));
            a.AlternativeRecipes.Add(new CraftableItemDefinition.RecipeVariant {
                Ingredients = new() { new CraftingMaterialRequirement(a, 1) } }); return a;
        }
        private static void AssertCycle(CraftableItemDefinition root, params string[] chain)
        {
            Assert.That(root.TryCalculateBaseValue(out var value, out var failure), Is.False);
            Assert.That(value, Is.Zero); Assert.That(failure.Kind, Is.EqualTo(CraftingValueFailureKind.DependencyCycle));
            Assert.That(failure.DependencyChain, Is.EqualTo(chain));
        }
        private CraftableItemDefinition Recipe(string id, params ItemDefinition[] inputs)
        {
            var a = New<CraftableItemDefinition>(); Set(a, "Id", id); Set(a, "DisplayName", id);
            Set(a, "ItemType", ItemType.Material); Set(a, "CraftingDiscipline", MaterialDiscipline.Smithing);
            Set(a, "Ingredients", inputs.Select(i => new CraftingMaterialRequirement(i, 1)).ToList()); return a;
        }
        private ItemDefinition Leaf(int value)
        {
            var a = New<ItemDefinition>(); Set(a, "Id", "leaf." + objects.Count); Set(a, "BaseValue", value);
            Set(a, "ItemType", ItemType.Material); Set(a, "MaterialDiscipline", MaterialDiscipline.Smithing); return a;
        }
        private (GameSession, CharacterInstance, NpcInstance) Session(CraftableItemDefinition recipe)
        {
            var session = new GameSession(GameModeState.Exploration, "recipe.cycles"); session.TryAddGold(10000);
            var owner = new CharacterInstance { CharacterId = "owner" };
            session.TryRegisterCharacter(owner); session.TryAddCharacterToActiveParty(owner.CharacterId);
            var role = New<BlacksmithRoleData>(); Set(role, "MaterialRecipes", new List<CraftableItemDefinition> { recipe });
            var definition = New<NpcDefinition>(); Set(definition, "Id", "smith");
            Set(definition, "Roles", new List<NpcRoleData> { role, New<TraderRoleData>() });
            return (session, owner, new NpcInstance { Definition = definition, NpcId = "smith",
                GoldBalance = 500, SpentGoldTotal = 123, ShopInventory = new ShopInventory() });
        }
        private T New<T>() where T : ScriptableObject
        { var item = ScriptableObject.CreateInstance<T>(); objects.Add(item); return item; }
        private static void Set(object target, string property, object value)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField("<" + property + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null) continue; field.SetValue(target, value); return;
            }
            throw new InvalidOperationException(property);
        }
        private sealed class CountingRandom : System.Random
        { public int Calls; public override double NextDouble() { Calls++; return .5; } }
    }
}
