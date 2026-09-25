using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Serialization;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using Godmist.Data.NPC;
using Godmist.Data.World;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;
using Godmist.Systems.Items;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class CraftingUpgradingConsumablesAcceptanceTests
    {
        [Test]
        public void BlacksmithPreviewBeforeEquipmentSelectionReturnsFailureWithoutMutation()
        {
            var npc=CreateBlacksmith();var character=new CharacterInstance{CharacterId="preview.owner"};
            var session=new GameSession(GameModeState.Exploration,"preview.empty");
            session.TryRegisterCharacter(character);session.TryAddCharacterToActiveParty(character.CharacterId);
            var service=new BlacksmithCraftingService();
            var request=new EquipmentCraftRequest{RecipientCharacterId=character.CharacterId,Quality=EquipmentQuality.Normal};
            var preview=service.CreatePreview(session,npc,request);
            Assert.That(preview.CanCraft,Is.False);Assert.That(preview.Result,Is.Null);
            Assert.That(preview.FailureReason,Is.Not.Empty);Assert.That(preview.Materials,Is.Empty);
            Assert.That(service.TryCraft(session,npc,request,out var result,out var reason),Is.False);
            Assert.That(result,Is.Null);Assert.That(character.Inventory.Items,Is.Empty);
            Assert.That(service.CreatePreview(session,npc,null).CanCraft,Is.False);
        }

        [Test]
        public void QualityInitializationDoesNotInvokePreviewCallback()
        {
            var go=new GameObject("quality.init",typeof(RectTransform));
            try
            {
                var dropdown=go.AddComponent(System.Type.GetType("TMPro.TMP_Dropdown, Unity.TextMeshPro",true));
                var value=dropdown.GetType().GetProperty("value");
                var changed=(UnityEngine.Events.UnityEvent<int>)dropdown.GetType().GetProperty("onValueChanged").GetValue(dropdown);
                var view=go.AddComponent<Godmist.Views.UI.Workspace.BlacksmithCraftingWindowView>();
                typeof(Godmist.Views.UI.Workspace.BlacksmithCraftingWindowView).GetField("qualityDropdown",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(view,dropdown);
                var callbacks=0;changed.AddListener(_=>callbacks++);
                var populate=view.GetType().GetMethod("PopulateQualityOptions",BindingFlags.Instance|BindingFlags.NonPublic);
                populate.Invoke(view,null);populate.Invoke(view,null);
                Assert.That(value.GetValue(dropdown),Is.EqualTo(1));Assert.That(callbacks,Is.Zero);
                value.SetValue(dropdown,2);Assert.That(callbacks,Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void MaterialVariantsConsumeChosenMetalAndShareCheapestOutputValue()
        {
            var cheap=CreateItem("cheap",ItemType.Material,14);var costly=CreateItem("costly",ItemType.Material,20);
            var recipe=CreateRecipe("needle",3,(cheap,1));
            recipe.AlternativeRecipes.Add(new CraftableItemDefinition.RecipeVariant{Label="costly",Ingredients=new List<CraftingMaterialRequirement>{new(costly,1)}});
            Assert.That(recipe.CalculatedBaseValue,Is.EqualTo(4));
            var npc=CreateBlacksmith(recipe);var character=new CharacterInstance{CharacterId="variant.crafter"};
            var session=new GameSession(GameModeState.Exploration,"variants");session.TryRegisterCharacter(character);session.TryAddCharacterToActiveParty(character.CharacterId);
            character.Inventory.Items.Add(new ItemInstance(cheap,"cheap.one"));character.Inventory.Items.Add(new ItemInstance(costly,"costly.one"));
            var service=new MaterialCraftingService();
            var request=new MaterialCraftRequest{Recipe=recipe,RecipeVariantIndex=1,RecipientCharacterId=character.CharacterId};
            Assert.That(service.TryCraft(session,npc,request,out var items,out var failure),Is.True,failure);
            Assert.That(items.Count,Is.EqualTo(3));Assert.That(items.All(item=>item.Definition==recipe),Is.True);
            Assert.That(character.Inventory.Items.Any(item=>item.Definition==cheap),Is.True);
            Assert.That(character.Inventory.Items.Any(item=>item.Definition==costly),Is.False);
            request.RecipeVariantIndex=99;Assert.That(service.CreatePreview(session,npc,request).CanCraft,Is.False);
            request.RecipeVariantIndex=-1;Assert.That(service.CreatePreview(session,npc,request).CanCraft,Is.False);
            request.RecipeVariantIndex=0;Assert.That(service.TryCraft(session,npc,request,out items,out failure),Is.True,failure);
            Assert.That(character.Inventory.Items.Count(item=>item.Definition==recipe),Is.EqualTo(6));
        }

        [Test]
        public void BlacksmithRejectsMixedTiersWithoutConsumingMaterials()
        {
            var ore = CreateItem("material.ore", ItemType.Material, 8);
            var scout = CreateClass("class.scout");
            var definition = CreateEquipmentDefinition("equipment.scout.weapon", scout);
            var head = CreatePart("head", scout, ore, EquipmentPartKind.WeaponHead);
            var binder = CreatePart("binder", scout, ore, EquipmentPartKind.WeaponBinder);
            var handle = CreatePart("handle", scout, ore, EquipmentPartKind.WeaponHandle);
            SetAutoProperty(head, "LevelBracket", EquipmentPartLevelBracket.Level15);
            var character = new CharacterInstance { CharacterId = "crafter" };
            var session = new GameSession(GameModeState.Exploration, "mixed.tiers");
            session.TryRegisterCharacter(character);
            session.TryAddCharacterToActiveParty(character.CharacterId);
            var material = new ItemInstance(ore, "ore.01");
            character.Inventory.Items.Add(material);
            var request = new EquipmentCraftRequest { Definition = definition,
                Parts = new[] { head, binder, handle }, Quality = EquipmentQuality.Normal,
                RecipientCharacterId = character.CharacterId };
            var service = new BlacksmithCraftingService();
            var blacksmith = CreateBlacksmith();
            var preview = service.CreatePreview(session, blacksmith, request);
            Assert.That(preview.CanCraft, Is.False);
            Assert.That(preview.FailureReason, Does.Contain("tego samego tieru"));
            Assert.That(service.TryCraft(session, blacksmith, request, out var item, out var reason), Is.False);
            Assert.That(item, Is.Null);
            Assert.That(reason, Is.EqualTo(preview.FailureReason));
            Assert.That(character.Inventory.Items, Is.EqualTo(new[] { material }));
        }

        [Test]
        public void CraftableItemDefinition_CalculatesRecursiveUnitValue()
        {
            var ore = CreateItem("material.ore", ItemType.Material, 8);
            var ingot = CreateRecipe(
                "material.ingot",
                outputAmount: 1,
                (ore, 2));
            var plate = CreateRecipe(
                "material.plate",
                outputAmount: 2,
                (ingot, 3));

            Assert.That(ingot.CalculatedBaseValue, Is.EqualTo(16));
            Assert.That(plate.CalculatedBaseValue, Is.EqualTo(24));
        }

        [Test]
        public void MaterialCraftingService_CraftsOutputAndConsumesActivePartyMaterials()
        {
            var material = CreateItem("material.ore", ItemType.Material, 8);
            var recipe = CreateRecipe("material.ingot", 3, (material, 2));
            var blacksmith = CreateBlacksmith(recipe);
            var character = new CharacterInstance { CharacterId = "character.crafter" };
            var session = new GameSession(GameModeState.Exploration, "crafting.acceptance");

            Assert.That(session.TryRegisterCharacter(character), Is.True);
            Assert.That(session.TryAddCharacterToActiveParty(character.CharacterId), Is.True);
            character.Inventory.Items.Add(new ItemInstance(material, "ore.01"));
            character.Inventory.Items.Add(new ItemInstance(material, "ore.02"));

            var crafted = new MaterialCraftingService().TryCraft(
                session,
                blacksmith,
                new MaterialCraftRequest
                {
                    Recipe = recipe,
                    RecipientCharacterId = character.CharacterId
                },
                out var craftedItems,
                out var failureReason);

            Assert.That(crafted, Is.True, failureReason);
            Assert.That(craftedItems, Has.Count.EqualTo(3));
            Assert.That(character.Inventory.Items.Count(item => item.Definition == material), Is.EqualTo(0));
            Assert.That(craftedItems.Select(item => item.InstanceId).Distinct().Count(), Is.EqualTo(3));
        }

        [Test]
        public void EquipmentUpgradePreview_UsesConfiguredValueFormulaAndUnlocksFiveSocketsAtFullProgress()
        {
            var scout = CreateClass("class.scout");
            var material = CreateItem("material.iron", ItemType.Material, 10);
            var part = CreatePart("part.weapon", scout, material);
            var binder = CreatePart("part.binder", scout, material, EquipmentPartKind.WeaponBinder);
            var handle = CreatePart("part.handle", scout, material, EquipmentPartKind.WeaponHandle);
            var equipmentDefinition = CreateEquipmentDefinition("equipment.weapon", scout);
            var item = new EquipmentGenerationService().GenerateFromSelectedParts(
                equipmentDefinition,
                new[] { part, binder, handle },
                EquipmentQuality.Normal,
                RarityTier.Common);
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            catalog.EquipmentPartDefinitions.AddRange(new[] { part, binder, handle });
            var blacksmith = CreateBlacksmith();
            var session = new GameSession(GameModeState.Exploration, "upgrade.acceptance");
            session.TryAddGold(100000);

            var service = new EquipmentUpgradeService();
            Assert.That(service.CreateUpgradePreview(session, blacksmith, item, 1f, .7f, catalog).CanExecute, Is.False);
            Assert.That(service.CreateUpgradePreview(session, blacksmith, item, float.NaN, .7f, catalog).CanExecute, Is.False);
            var preview = service.CreateUpgradePreview(session, blacksmith, item, .2f, .7f, catalog);
            Assert.That(preview.CanExecute, Is.True);
            Assert.That(preview.GoldCost, Is.EqualTo(7));
            item.CurrentRarity = RarityTier.Legendary;
            item.UpgradeProgress = .4f;
            var enhanced = service.CreateUpgradePreview(session, blacksmith, item, .6f, .7f, catalog);
            Assert.That(enhanced.CurrentValue, Is.EqualTo(136.5f).Within(.001f));
            var before = session.Gold;
            Assert.That(new EquipmentUpgradeService(new AlwaysFailRandom()).TryUpgrade(
                session, blacksmith, null, item, .6f, .7f, catalog, out var success, out var paid, out _), Is.True);
            Assert.That(success, Is.False);
            Assert.That(paid, Is.EqualTo(enhanced.GoldCost));
            Assert.That(session.Gold, Is.EqualTo(before - paid));
            Assert.That(item.UpgradeProgress, Is.InRange(.2f, .3f));
            item.CurrentRarity = RarityTier.Common;
            item.UpgradeProgress = 0f;
            var total = 0;
            for (var step = 1; step <= 5; step++)
            {
                var target = step / 5f;
                var next = service.CreateUpgradePreview(session, blacksmith, item, target, .7f, catalog);
                Assert.That(next.CanExecute, Is.True);
                total += next.GoldCost;
                item.UpgradeProgress = target;
                Assert.That(item.SocketCapacity, Is.EqualTo(step));
            }
            Assert.That(total, Is.EqualTo(System.Math.Ceiling(EquipmentUpgradeService.UpgradeCost(30, 1, .7f))));
            item.UpgradeProgress = 0;
            var commonCost = service.CreateReforgePreview(session, blacksmith, item, catalog).GoldCost;
            Assert.That(commonCost, Is.EqualTo(8));
            var rarities = new[] { RarityTier.Common, RarityTier.Uncommon, RarityTier.Rare, RarityTier.Ancient, RarityTier.Legendary, RarityTier.Mythic };
            var costs = new[] { 8, 14, 21, 31, 45, 65 };
            for (var index = 0; index < rarities.Length; index++)
            {
                item.CurrentRarity = rarities[index];
                Assert.That(service.CreateReforgePreview(session, blacksmith, item, catalog).GoldCost, Is.EqualTo(costs[index]), rarities[index].ToString());
            }
            item.CurrentRarity = RarityTier.Mythic;
            Assert.That(service.CreateReforgePreview(session, blacksmith, item, catalog).GoldCost, Is.EqualTo(65));
            item.Quality = EquipmentQuality.Excellent;
            Assert.That(service.CreateReforgePreview(session, blacksmith, item, catalog).CurrentValue, Is.EqualTo(item.BaseValue).Within(1));
        }

        [Test]
        public void EquipmentRarity_RebuildsFromBaselineWithoutCompounding()
        {
            var definition = CreateEquipmentDefinition("equipment.rarity", CreateClass("class.scout"));
            var item = new EquipmentItemInstance(definition)
            {
                CurrentRarity = RarityTier.Godly, UpgradeProgress = .4f,
                BaseStatModifiers = new List<StatModifier>
                {
                    new() { StatType = CharacterStatType.MinAttack, Value = 100 },
                    new() { StatType = CharacterStatType.CritChance, Value = .1f },
                    new() { StatType = CharacterStatType.CritDamage, Value = .5f },
                    new() { StatType = CharacterStatType.WeaponAccuracy, Value = 100 },
                    new() { StatType = CharacterStatType.MaxResource, Value = 100 }
                }
            };
            item.RecalculateStatModifiers();
            item.RecalculateStatModifiers();
            Assert.That(item.StatModifiers.Select(x => x.Value), Is.EqualTo(new[] { 140f, .27f, 1.35f, 151f, 202f }).Within(.001f));
            item.CurrentRarity = RarityTier.Common;
            item.RecalculateStatModifiers();
            Assert.That(item.StatModifiers[1].Value, Is.EqualTo(.1f).Within(.0001f));
            Assert.That(item.BaseStatModifiers[0].Value, Is.EqualTo(100));
        }

        private sealed class AlwaysFailRandom : System.Random
        {
            public override double NextDouble() => .999;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MaterialCrafting_BuysMissingMaterialFromBuyback(bool rejectTransfer)
        {
            var ore = CreateItem("material.buyback", ItemType.Material, 10);
            var recipe = CreateRecipe("recipe.buyback", 1, (ore, 1));
            var npc = CreateBlacksmith(recipe);
            npc.Definition.Roles.Add(ScriptableObject.CreateInstance<TraderRoleData>());
            npc.ShopInventory ??= new ShopInventory();
            var item = new ItemInstance(ore);
            npc.ShopInventory.BuybackStock.Add(new BuybackEntry(item, 5));
            var owner = new CharacterInstance { CharacterId = "buyer" };
            var session = new GameSession();
            session.TryRegisterCharacter(owner);
            session.TryAddCharacterToActiveParty(owner.CharacterId);
            session.TryAddGold(1000);
            if (rejectTransfer)
                owner.Inventory.Items.Add(new ItemInstance(CreateItem("other", ItemType.Material, 1), item.InstanceId));
            var bought = new MaterialCraftingService().TryBuyMissingMaterial(session, npc,
                new MaterialCraftRequest { Recipe = recipe, RecipientCharacterId = owner.CharacterId },
                ore.Id, out var paid, out var reason);
            if (rejectTransfer)
            {
                Assert.That(bought, Is.False);
                Assert.That(npc.ShopInventory.BuybackStock.Single().Item, Is.SameAs(item));
                Assert.That(session.Gold, Is.EqualTo(1000));
                Assert.That(npc.GoldBalance, Is.Zero);
                return;
            }
            Assert.That(bought, Is.True, reason);
            Assert.That(owner.Inventory.Items.Single(), Is.SameAs(item));
            Assert.That(npc.ShopInventory.BuybackStock, Is.Empty);
            Assert.That(npc.GoldBalance, Is.EqualTo(paid));
        }

        [Test]
        public void ConsumableRuntimeBuilder_CollapsesDuplicateEffectsAndReturnScrollConsumesOnlyAfterTransition()
        {
            var potionDefinition = CreateConsumable("item.consumable.potion", ConsumableUseType.Potion, 5);
            var potion = new ConsumableItemInstance(potionDefinition, "potion.01");
            potion.RuntimeEffects.Add(new ConsumableEffectRuntime
            {
                EffectType = ConsumableRuntimeEffectType.ModifyDamage,
                Value = .1f
            });
            potion.RuntimeEffects.Add(new ConsumableEffectRuntime
            {
                EffectType = ConsumableRuntimeEffectType.ModifyDamage,
                Value = .1f
            });

            var skill = ConsumableRuntimeActionBuilder.Build(potion);
            Assert.That(skill.Effects, Has.Count.EqualTo(1));
            Assert.That(skill.Effects[0].ModifierValue, Is.EqualTo(.15f).Within(.0001f));
            Assert.That(skill.Effects[0].AlwaysHits, Is.True);

            var returnDefinition = CreateConsumable("item.consumable.return_scroll", ConsumableUseType.Utility, 1);
            var returnScroll = new ConsumableItemInstance(returnDefinition, "scroll.01");
            var character = new CharacterInstance { CharacterId = "character.return" };
            character.Inventory.Items.Add(returnScroll);

            var service = new ConsumableService();
            Assert.That(service.TryUseInExploration(
                character,
                returnScroll,
                out var effect,
                returnTransition: () => false), Is.False);
            Assert.That(effect, Is.EqualTo(ExplorationConsumableEffect.None));
            Assert.That(character.Inventory.Items, Has.Count.EqualTo(1));
            Assert.That(returnScroll.CurrentCharges, Is.EqualTo(1));

            Assert.That(service.TryUseInExploration(
                character,
                returnScroll,
                out effect,
                returnTransition: () => true), Is.True);
            Assert.That(effect, Is.EqualTo(ExplorationConsumableEffect.ReturnToArungard));
            Assert.That(character.Inventory.Items, Is.Empty);
            Assert.That(returnScroll.CurrentCharges, Is.EqualTo(0));
        }

        [Test]
        public void Vs12UtilityItems_RemoveStatusAndGrantThreeTurnTypedResistance()
        {
            var cases = new[]
            {
                ("item.consumable.bandage", Godmist.Core.Constants.StatusEffectIds.Bleed, ResistanceType.Bleed),
                ("item.consumable.antidote", Godmist.Core.Constants.StatusEffectIds.Poison, ResistanceType.Poison),
                ("item.consumable.damp_towel", Godmist.Core.Constants.StatusEffectIds.Burn, ResistanceType.Fire)
            };
            foreach (var (id, statusId, resistanceType) in cases)
            {
                var definition = CreateConsumable(id, ConsumableUseType.Utility, 1);
                var effects = ConsumableRuntimeRules.GetImpliedUtilityEffects(definition);
                Assert.That(effects.Count, Is.EqualTo(2), id);
                Assert.That(effects[0].EffectType, Is.EqualTo(ConsumableRuntimeEffectType.RemoveStatus), id);
                Assert.That(effects[0].StatusId, Is.EqualTo(statusId), id);
                Assert.That(effects[1].EffectType, Is.EqualTo(ConsumableRuntimeEffectType.ModifyResistance), id);
                Assert.That(effects[1].ResistanceType, Is.EqualTo(resistanceType), id);
                Assert.That(effects[1].Value, Is.EqualTo(.30f).Within(.0001f), id);
                var skill = ConsumableRuntimeActionBuilder.Build(new ConsumableItemInstance(definition));
                Assert.That(skill.Effects, Has.Count.EqualTo(2), id);
                Assert.That(skill.Effects[1].DurationTurns, Is.EqualTo(3), id);
                Assert.That(skill.Effects[1].ResistanceType, Is.EqualTo(resistanceType), id);
            }
        }

        [Test]
        public void Vs12ExplorationRegeneration_KeepsTotalRecoveryAcrossCondensedDuration()
        {
            var definition = CreateConsumable("item.consumable.potion", ConsumableUseType.Potion, 1);
            var potion = new ConsumableItemInstance(definition);
            potion.RuntimeEffects.Add(new ConsumableEffectRuntime
            {
                EffectType = ConsumableRuntimeEffectType.RegenHealth,
                Value = .25f,
                DurationTurns = 10
            });
            potion.CatalystDurationModifier = -2;
            var character = new CharacterInstance { CharacterId = "vs12.regen" };
            character.Inventory.Items.Add(potion);

            Assert.That(new ConsumableService().TryUseInExploration(
                character, potion, out var result), Is.True);
            Assert.That(result, Is.EqualTo(ExplorationConsumableEffect.ApplyModifier));
            Assert.That(character.PassiveEffects, Has.Count.EqualTo(1));
            Assert.That(character.PassiveEffects[0].RemainingDuration, Is.EqualTo(8));
            Assert.That(character.PassiveEffects[0].GetPayload<PeriodicRecoveryPayload>()
                .HealthPercent * 8f, Is.EqualTo(.25f).Within(.0001f));
            Assert.That(character.Inventory.Items, Is.Empty);

            var cityPotion = new ConsumableItemInstance(definition);
            cityPotion.RuntimeEffects.Add(new ConsumableEffectRuntime
            {
                EffectType = ConsumableRuntimeEffectType.RegenHealth,
                Value = .25f,
                DurationTurns = 10
            });
            var cityCharacter = new CharacterInstance
            {
                CharacterId = "vs12.city.regen",
                Stats = new CharacterStats { MaxHealth = new Stat(100) },
                CurrentHealth = 50
            };
            cityCharacter.Inventory.Items.Add(cityPotion);
            Assert.That(new ConsumableService().TryUseInExploration(
                cityCharacter, cityPotion, out var cityResult,
                returnTransition: null, passiveTicksActive: false), Is.True);
            Assert.That(cityResult, Is.EqualTo(ExplorationConsumableEffect.RestoreHealthPercent));
            Assert.That(cityCharacter.CurrentHealth, Is.EqualTo(75));
            Assert.That(cityCharacter.PassiveEffects, Is.Empty);
        }

        [Test]
        public void Vs12MaterialRecipe_ProducesUsableConsumableFromAlchemyAndFabric()
        {
            var alcohol = CreateItem("alcohol", ItemType.Material, 10);
            SetAutoProperty(alcohol, "MaterialDiscipline", MaterialDiscipline.Alchemy);
            var fabric = CreateItem("fabric", ItemType.Material, 5);
            var product = CreateConsumable("item.consumable.bandage", ConsumableUseType.Utility, 1);
            SetAutoProperty(product, "ItemType", ItemType.Tool);
            var recipe = CreateRecipe("recipe.vs12.bandage", 1, (alcohol, 1), (fabric, 2));
            SetAutoProperty(recipe, "CraftingDiscipline", MaterialDiscipline.Alchemy);
            SetAutoProperty(recipe, "CraftedResult", product);
            SetAutoProperty(recipe, "ResultLevel", 11);
            SetAutoProperty(recipe, "PrimaryVariantLabel", "Jutowa tkanina");
            Assert.That(recipe.GetVariantLabel(), Is.EqualTo("Jutowa tkanina"));
            var role = ScriptableObject.CreateInstance<AlchemistRoleData>();
            SetAutoProperty(role, "MaterialRecipes", new List<CraftableItemDefinition> { recipe });
            var definition = ScriptableObject.CreateInstance<NpcDefinition>();
            SetAutoProperty(definition, "Id", "npc.vs12.alchemist");
            SetAutoProperty(definition, "Roles", new List<NpcRoleData> { role });
            var npc = new NpcInstance { NpcId = definition.Id, Definition = definition, ShopInventory = new ShopInventory() };
            var character = new CharacterInstance { CharacterId = "vs12.crafter" };
            var session = new GameSession(GameModeState.Exploration, "vs12.crafting");
            session.TryRegisterCharacter(character);
            session.TryAddCharacterToActiveParty(character.CharacterId);
            character.Inventory.Items.Add(new ItemInstance(alcohol));
            character.Inventory.Items.Add(new ItemInstance(fabric));
            character.Inventory.Items.Add(new ItemInstance(fabric));
            var request = new MaterialCraftRequest { Recipe = recipe, RecipientCharacterId = character.CharacterId };
            var service = new MaterialCraftingService();
            Assert.That(service.CreatePreview(session, npc, request).Result, Is.SameAs(product));
            Assert.That(service.TryCraft(session, npc, request, out var items, out var failure), Is.True, failure);
            Assert.That(items, Has.Count.EqualTo(1));
            Assert.That(items[0], Is.TypeOf<ConsumableItemInstance>());
            Assert.That(items[0].Definition, Is.SameAs(product));
            Assert.That(items[0].ItemLevel, Is.EqualTo(11));
            Assert.That(items[0].BaseValue, Is.EqualTo(20));
            Assert.That(character.Inventory.Items, Has.Count.EqualTo(1));
        }

        [Test]
        public void Vs12SharedIngredientsExpandDefenseAndAllResistances()
        {
            var potion = new ConsumableItemInstance(
                CreateConsumable("item.consumable.potion", ConsumableUseType.Potion, 5));
            potion.RuntimeEffects.Add(new ConsumableEffectRuntime
            {
                EffectType = ConsumableRuntimeEffectType.ModifyDefense,
                Value = .1f,
                DurationTurns = 8
            });
            potion.RuntimeEffects.Add(new ConsumableEffectRuntime
            {
                EffectType = ConsumableRuntimeEffectType.ModifyAllResistances,
                Value = .1f,
                DurationTurns = 8,
                ResistanceType = ResistanceType.Bleed
            });
            var skill = ConsumableRuntimeActionBuilder.Build(potion);
            Assert.That(skill.Effects.Count(effect => effect.EffectType == EffectType.ModifyStat), Is.EqualTo(2));
            Assert.That(skill.Effects.Where(effect => effect.EffectType == EffectType.ModifyStat)
                .Select(effect => effect.ModifierStatType),
                Is.EquivalentTo(new[] { CharacterStatType.PhysicalDefense, CharacterStatType.MagicalDefense }));
            Assert.That(skill.Effects.Where(effect => effect.EffectType == EffectType.ModifyResistance)
                .Select(effect => effect.ResistanceType),
                Is.EquivalentTo(System.Enum.GetValues(typeof(ResistanceType)).Cast<ResistanceType>()));

            var bomb = new ConsumableItemInstance(
                CreateConsumable("item.consumable.bomb", ConsumableUseType.Bomb, 5));
            bomb.RuntimeEffects.Add(new ConsumableEffectRuntime
            {
                EffectType = ConsumableRuntimeEffectType.ModifyDodge,
                Value = .1f,
                DurationTurns = 3
            });
            var weakening = ConsumableRuntimeActionBuilder.Build(bomb).Effects.Single();
            Assert.That(weakening.ModifierStatType, Is.EqualTo(CharacterStatType.Dodge));
            Assert.That(weakening.ModifierOperation, Is.EqualTo(StatModifierOperation.Percent));
            Assert.That(weakening.ModifierValue, Is.EqualTo(-.1f).Within(.0001f));
        }

        [Test]
        public void Vs12Catalyst_PreservesLegacyBombTypeUntilConfigured()
        {
            var catalyst = new AlchemyCatalystData();
            SetAutoProperty(catalyst, "CatalystType", ConsumableCatalystType.Strength);
            Assert.That(catalyst.GetTypeFor(ConsumableUseType.Bomb),
                Is.EqualTo(ConsumableCatalystType.Strength));
            SetAutoProperty(catalyst, "BombCatalystType", ConsumableCatalystType.Range);
            SetAutoProperty(catalyst, "HasBombCatalystOverride", true);
            Assert.That(catalyst.GetTypeFor(ConsumableUseType.Bomb),
                Is.EqualTo(ConsumableCatalystType.Range));
            Assert.That(catalyst.GetTypeFor(ConsumableUseType.Potion),
                Is.EqualTo(ConsumableCatalystType.Strength));
        }

        [Test]
        public void CampaignStorageService_TransfersWholeStackAndSingleItemWithoutChangingInstanceIds()
        {
            var material = CreateItem("material.stack", ItemType.Material, 1, stackable: true);
            var first = new ItemInstance(material, "stack.01");
            var second = new ItemInstance(material, "stack.02");
            var character = new CharacterInstance { CharacterId = "character.storage" };
            character.Inventory.Items.Add(first);
            character.Inventory.Items.Add(second);
            var session = new GameSession(GameModeState.Exploration, "storage.acceptance");
            var service = new CampaignStorageService();

            Assert.That(service.TryDeposit(session, character.Inventory, first.InstanceId, singleItem: false), Is.True);
            Assert.That(session.Stash.Items.Select(item => item.InstanceId), Is.EquivalentTo(new[] { "stack.01", "stack.02" }));
            Assert.That(service.TryWithdraw(session, character.Inventory, first.InstanceId, singleItem: true), Is.True);
            Assert.That(character.Inventory.Items.Single().InstanceId, Is.EqualTo("stack.01"));
            Assert.That(session.Stash.Items.Single().InstanceId, Is.EqualTo("stack.02"));
        }

        [Test]
        public void SaveRestore_PreservesStashConsumablePayloadShopBuybackGoldAndInstanceIds()
        {
            var consumableDefinition = CreateConsumable("item.consumable.save", ConsumableUseType.Potion, 5);
            var shopDefinition = CreateItem("item.shop", ItemType.Material, 10);
            var equipmentDefinition = CreateEquipmentDefinition("item.equipment.save", CreateClass("class.save"));
            var npcDefinition = CreateTraderDefinition("npc.save.trader");
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            catalog.ItemDefinitions.AddRange(new ItemDefinition[] { consumableDefinition, shopDefinition, equipmentDefinition });
            catalog.NpcDefinitions.Add(npcDefinition);

            var session = new GameSession(GameModeState.Exploration, "save.acceptance");
            session.TryAddGold(1234);
            var consumable = new ConsumableItemInstance(consumableDefinition, "save.potion");
            consumable.CurrentCharges = 2;
            consumable.ConfigureCatalyst(2, 1.2f, 3);
            consumable.RuntimeEffects.Add(new ConsumableEffectRuntime
            {
                EffectType = ConsumableRuntimeEffectType.RegenHealth,
                Value = .25f,
                DurationTurns = 6
            });
            session.Stash.Items.Add(consumable);
            var equipment = new EquipmentItemInstance(equipmentDefinition, "save.equipment")
            {
                CurrentRarity = RarityTier.Rare,
                ItemLevel = 18,
                RequiredLevel = 15,
                RequiredClassId = "class.save",
                Quality = EquipmentQuality.Excellent,
                PartIds = new List<string> { "part.save" },
                UpgradeProgress = .75f,
                BaseStatModifiers = new List<StatModifier>
                {
                    new() { StatType = CharacterStatType.MinAttack, Value = 10f }
                },
                StatModifiers = new List<StatModifier>
                {
                    new() { StatType = CharacterStatType.MinAttack, Value = 17.5f }
                }
            };
            session.Stash.Items.Add(equipment);

            var npc = new NpcInstance
            {
                NpcId = npcDefinition.Id,
                Definition = npcDefinition,
                GoldBalance = 340,
                TotalGoldReceived = 900,
                TotalGoldSpent = 560,
                GuildPayoutProgress = .6f,
                ShopInventory = new ShopInventory()
            };
            npc.ShopInventory.Items.Add(new ItemInstance(shopDefinition, "save.shop"));
            npc.ShopInventory.BuybackStock.Add(new BuybackEntry(
                new ItemInstance(shopDefinition, "save.buyback"),
                7));
            Assert.That(session.TryRegisterNpc(npc), Is.True);

            var snapshot = new GameSessionSaveSnapshotFactory().CreateSnapshot(session, "Acceptance");
            Assert.That(new GameSessionRestoreFactory().TryCreate(snapshot, catalog, out var restored), Is.True);

            var restoredConsumable = restored.Stash.Items.Single(item => item.InstanceId == "save.potion") as ConsumableItemInstance;
            Assert.That(restored.Gold, Is.EqualTo(1234));
            Assert.That(restoredConsumable.InstanceId, Is.EqualTo("save.potion"));
            Assert.That(restoredConsumable.CurrentCharges, Is.EqualTo(2));
            Assert.That(restoredConsumable.CatalystDurationModifier, Is.EqualTo(2));
            Assert.That(restoredConsumable.CatalystChargeBonus, Is.EqualTo(3));
            Assert.That(restoredConsumable.RuntimeEffects.Single().DurationTurns, Is.EqualTo(6));
            var restoredEquipment = restored.Stash.Items.Single(item => item.InstanceId == "save.equipment") as EquipmentItemInstance;
            Assert.That(restoredEquipment.CurrentRarity, Is.EqualTo(RarityTier.Rare));
            Assert.That(restoredEquipment.Quality, Is.EqualTo(EquipmentQuality.Excellent));
            Assert.That(restoredEquipment.UpgradeProgress, Is.EqualTo(.75f));
            Assert.That(restoredEquipment.StatModifiers.Single().Value, Is.EqualTo(17.5f));
            Assert.That(restored.NpcsById[npcDefinition.Id].GoldBalance, Is.EqualTo(340));
            Assert.That(restored.NpcsById[npcDefinition.Id].TotalGoldReceived, Is.EqualTo(900));
            Assert.That(restored.NpcsById[npcDefinition.Id].TotalGoldSpent, Is.EqualTo(560));
            Assert.That(restored.NpcsById[npcDefinition.Id].GuildPayoutProgress, Is.EqualTo(.6f));
            Assert.That(restored.NpcsById[npcDefinition.Id].ShopInventory.Items.Single().InstanceId, Is.EqualTo("save.shop"));
            Assert.That(restored.NpcsById[npcDefinition.Id].ShopInventory.BuybackStock.Single().Item.InstanceId, Is.EqualTo("save.buyback"));
        }

        private static CraftableItemDefinition CreateRecipe(
            string id,
            int outputAmount,
            params (ItemDefinition Material, int Amount)[] ingredients)
        {
            var recipe = ScriptableObject.CreateInstance<CraftableItemDefinition>();
            SetAutoProperty(recipe, "Id", id);
            SetAutoProperty(recipe, "DisplayName", id);
            SetAutoProperty(recipe, "ItemType", ItemType.Material);
            SetAutoProperty(recipe, "OutputAmount", outputAmount);
            SetAutoProperty(recipe, "Ingredients", ingredients.Select(entry =>
            {
                var requirement = new CraftingMaterialRequirement();
                SetAutoProperty(requirement, "Material", entry.Material);
                SetAutoProperty(requirement, "Amount", entry.Amount);
                return requirement;
            }).ToList());
            SetAutoProperty(recipe, "CraftingDiscipline", MaterialDiscipline.Smithing);
            return recipe;
        }

        private static BlacksmithRoleData CreateBlacksmithRole(CraftableItemDefinition recipe = null)
        {
            var role = ScriptableObject.CreateInstance<BlacksmithRoleData>();
            SetAutoProperty(role, "MaterialRecipes", recipe == null
                ? new List<CraftableItemDefinition>()
                : new List<CraftableItemDefinition> { recipe });
            return role;
        }

        private static NpcInstance CreateBlacksmith(CraftableItemDefinition recipe = null)
        {
            var role = CreateBlacksmithRole(recipe);
            var definition = ScriptableObject.CreateInstance<NpcDefinition>();
            SetAutoProperty(definition, "Id", "npc.blacksmith.acceptance");
            SetAutoProperty(definition, "Roles", new List<NpcRoleData> { role });
            return new NpcInstance
            {
                NpcId = definition.Id,
                Definition = definition,
                ShopInventory = new ShopInventory()
            };
        }

        private static NpcDefinition CreateTraderDefinition(string id)
        {
            var role = ScriptableObject.CreateInstance<TraderRoleData>();
            SetAutoProperty(role, "InitialOffer", new List<ItemDefinition>());
            var definition = ScriptableObject.CreateInstance<NpcDefinition>();
            SetAutoProperty(definition, "Id", id);
            SetAutoProperty(definition, "Roles", new List<NpcRoleData> { role });
            return definition;
        }

        private static ConsumableItemDefinition CreateConsumable(string id, ConsumableUseType useType, int maxCharges)
        {
            var definition = ScriptableObject.CreateInstance<ConsumableItemDefinition>();
            SetAutoProperty(definition, "Id", id);
            SetAutoProperty(definition, "DisplayName", id);
            SetAutoProperty(definition, "ItemType", useType == ConsumableUseType.Bomb ? ItemType.Bomb : ItemType.Potion);
            SetAutoProperty(definition, "BaseRarity", RarityTier.Common);
            SetAutoProperty(definition, "UseType", useType);
            SetAutoProperty(definition, "MaxCharges", maxCharges);
            return definition;
        }

        private static ItemDefinition CreateItem(string id, ItemType itemType, int baseValue, bool stackable = false)
        {
            var definition = ScriptableObject.CreateInstance<ItemDefinition>();
            SetAutoProperty(definition, "Id", id);
            SetAutoProperty(definition, "DisplayName", id);
            SetAutoProperty(definition, "ItemType", itemType);
            SetAutoProperty(definition, "BaseValue", baseValue);
            SetAutoProperty(definition, "BaseRarity", RarityTier.Common);
            SetAutoProperty(definition, "IsStackable", stackable);
            if (itemType == ItemType.Material)
            {
                SetAutoProperty(definition, "MaterialDiscipline", MaterialDiscipline.Smithing);
            }
            return definition;
        }

        private static CharacterClassData CreateClass(string id)
        {
            var value = ScriptableObject.CreateInstance<CharacterClassData>();
            SetAutoProperty(value, "Id", id);
            return value;
        }

        private static EquipmentItemDefinition CreateEquipmentDefinition(string id, CharacterClassData requiredClass)
        {
            var definition = ScriptableObject.CreateInstance<EquipmentItemDefinition>();
            SetAutoProperty(definition, "Id", id);
            SetAutoProperty(definition, "ItemType", ItemType.Weapon);
            SetAutoProperty(definition, "RequiredClass", requiredClass);
            return definition;
        }

        private static EquipmentPartDefinition CreatePart(
            string id,
            CharacterClassData requiredClass,
            ItemDefinition material,
            EquipmentPartKind kind = EquipmentPartKind.WeaponHead)
        {
            var part = ScriptableObject.CreateInstance<EquipmentPartDefinition>();
            SetAutoProperty(part, "Id", id);
            SetAutoProperty(part, "RequiredClass", requiredClass);
            SetAutoProperty(part, "Kind", kind);
            SetAutoProperty(part, "LevelBracket", EquipmentPartLevelBracket.Level5);
            SetAutoProperty(part, "Stats", new EquipmentPartStats { MinAttackBase = 10, MaxAttackBase = 10 });
            var requirement = new CraftingMaterialRequirement();
            SetAutoProperty(requirement, "Material", material);
            SetAutoProperty(requirement, "Amount", 1);
            SetAutoProperty(part, "CraftingMaterials", new List<CraftingMaterialRequirement> { requirement });
            return part;
        }

        private static void SetAutoProperty(object target, string propertyName, object value)
        {
            var type = target.GetType();
            FieldInfo field = null;
            while (type != null && field == null)
            {
                field = type.GetField(
                    $"<{propertyName}>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                type = type.BaseType;
            }

            Assert.That(field, Is.Not.Null, $"Nie znaleziono backing field dla '{propertyName}'.");
            field.SetValue(target, value);
        }
    }
}
