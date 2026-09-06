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

            var preview = new EquipmentUpgradeService().CreateUpgradePreview(
                session,
                blacksmith,
                item,
                1f,
                .5f,
                catalog);

            Assert.That(preview.CanExecute, Is.True);
            Assert.That(preview.SocketCapacity, Is.EqualTo(5));
            var expectedGoldCost = Mathf.CeilToInt((float)(
                EquipmentUpgradeService.UpgradeCost(30f, 1f, .5f)
                - EquipmentUpgradeService.UpgradeCost(30f, 0f, .5f)));
            Assert.That(preview.GoldCost, Is.EqualTo(expectedGoldCost));
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
            SetAutoProperty(definition, "ItemType", ItemType.Tool);
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
