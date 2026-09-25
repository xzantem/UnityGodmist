using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Items;
using Godmist.Data.NPC;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;
using Godmist.Systems.Items;
using Godmist.Systems.NPC;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class AlchemyCraftingAndRefillTests
    {
        [Test]
        public void MaterialCrafting_UsesAlchemistAlchemyRecipes()
        {
            var herb = CreateMaterial("alchemy.powder_herb");
            var powder = ScriptableObject.CreateInstance<CraftableItemDefinition>();
            SetAutoProperty(powder, "Id", "alchemy.powder");
            SetAutoProperty(powder, "DisplayName", "Proszek");
            SetAutoProperty(powder, "ItemType", ItemType.Material);
            SetAutoProperty(powder, "CraftingDiscipline", MaterialDiscipline.Alchemy);
            SetAutoProperty(powder, "OutputAmount", 2);
            SetAutoProperty(powder, "Ingredients", new List<CraftingMaterialRequirement>
            {
                new(herb, 2)
            });
            var role = ScriptableObject.CreateInstance<AlchemistRoleData>();
            SetAutoProperty(role, "MaterialRecipes", new List<CraftableItemDefinition> { powder });
            var alchemist = CreateAlchemist(role);
            var character = new CharacterInstance { CharacterId = "alchemy.powder.crafter" };
            var session = CreateSession(character, "alchemy.powder");
            character.Inventory.Items.Add(new ItemInstance(herb, "herb.powder.01"));
            character.Inventory.Items.Add(new ItemInstance(herb, "herb.powder.02"));

            var crafted = new MaterialCraftingService().TryCraft(
                session,
                alchemist,
                new MaterialCraftRequest
                {
                    Recipe = powder,
                    RecipientCharacterId = character.CharacterId
                },
                out var items,
                out var failureReason);

            Assert.That(crafted, Is.True, failureReason);
            Assert.That(items, Has.Count.EqualTo(2));
            Assert.That(character.Inventory.Items.Count(item => item.Definition == herb), Is.EqualTo(0));
            Assert.That(character.Inventory.Items.Count(item => item.Definition == powder), Is.EqualTo(2));
        }

        [Test]
        public void AlchemyCrafting_CreatesNamedPotionFromThreeIngredientsAndOneAlcohol()
        {
            var alcohol = CreateMaterial("alchemy.alcohol.t1", baseValue: 46);
            var leaf = CreateMaterial("alchemy.leaf", baseValue: 21);
            var root = CreateMaterial("alchemy.root", RarityTier.Uncommon, 19);
            var cap = CreateMaterial("alchemy.cap", RarityTier.Rare, 18);
            var potionDefinition = CreateConsumable("alchemy.healing_potion", ConsumableUseType.Potion);
            var role = CreateAlchemyRole(
                potionDefinition,
                null,
                new[]
                {
                    CreateIngredient(leaf, 1, ConsumableRuntimeEffectType.RestoreHealth, 0.35f),
                    CreateIngredient(root, 1, ConsumableRuntimeEffectType.RestoreResource, 0.10f),
                    CreateIngredient(cap, 1, ConsumableRuntimeEffectType.RegenHealth, 0.05f)
                },
                new[] { CreateMedium(alcohol, 3) });
            var alchemist = CreateAlchemist(role);
            var character = new CharacterInstance { CharacterId = "alchemy.crafter" };
            var session = CreateSession(character, "alchemy.crafting");
            character.Inventory.Items.Add(new ItemInstance(alcohol, "alcohol.01"));
            character.Inventory.Items.Add(new ItemInstance(leaf, "leaf.01"));
            character.Inventory.Items.Add(new ItemInstance(root, "root.01"));
            character.Inventory.Items.Add(new ItemInstance(cap, "cap.01"));

            var service = new AlchemyCraftingService();
            var defaultPreview = service.CreatePreview(session, alchemist, new AlchemyCraftRequest
            {
                UseType = ConsumableUseType.Potion,
                Ingredients = new List<ItemDefinition> { leaf, root, cap },
                Medium = alcohol,
                RecipientCharacterId = character.CharacterId
            });
            Assert.That(defaultPreview.Result.DisplayName, Does.Contain("lecznicza").IgnoreCase);
            Assert.That(defaultPreview.Result.DisplayName, Does.Contain("wyrafinowana"));
            Assert.That(defaultPreview.Result.DisplayName, Does.Contain("mikstura"));

            var crafted = service.TryCraft(
                session,
                alchemist,
                new AlchemyCraftRequest
                {
                    UseType = ConsumableUseType.Potion,
                    Ingredients = new List<ItemDefinition> { leaf, root, cap },
                    Medium = alcohol,
                    CustomName = "Eliksir testowy",
                    RecipientCharacterId = character.CharacterId
                },
                out var items,
                out var failureReason);

            Assert.That(crafted, Is.True, failureReason);
            Assert.That(items, Has.Count.EqualTo(1));
            Assert.That(character.Inventory.Items, Has.Count.EqualTo(1));
            var potion = character.Inventory.Items.Single() as ConsumableItemInstance;
            Assert.That(potion, Is.Not.Null);
            Assert.That(potion.ConsumableDefinition, Is.SameAs(potionDefinition));
            Assert.That(potion.ItemLevel, Is.EqualTo(21));
            Assert.That(potion.DisplayName, Is.EqualTo("Eliksir testowy"));
            Assert.That(potion.CurrentRarity, Is.EqualTo(RarityTier.Rare));
            Assert.That(potion.BaseValue, Is.EqualTo(104));
            Assert.That(potion.RuntimeEffects.Select(effect => effect.EffectType), Is.EquivalentTo(new[]
            {
                ConsumableRuntimeEffectType.RestoreHealth,
                ConsumableRuntimeEffectType.RestoreResource,
                ConsumableRuntimeEffectType.RegenHealth
            }));
        }

        [Test]
        public void AlchemyCrafting_UsesHighestEffectTierAndRejectsTooWeakMedium()
        {
            var alcoholT3 = CreateMaterial("alchemy.alcohol.t3");
            var alcoholT5 = CreateMaterial("alchemy.alcohol.t5");
            var ingredientA = CreateMaterial("alchemy.tier1");
            var ingredientB = CreateMaterial("alchemy.tier2");
            var ingredientC = CreateMaterial("alchemy.tier3");
            var potionDefinition = CreateConsumable("alchemy.high_level_potion", ConsumableUseType.Potion);
            var role = CreateAlchemyRole(
                potionDefinition,
                null,
                new[]
                {
                    CreateIngredient(ingredientA, 1, ConsumableRuntimeEffectType.Heal, 1f),
                    CreateIngredient(ingredientB, 2, ConsumableRuntimeEffectType.ModifySpeed, 0.1f),
                    CreateIngredient(ingredientC, 3, ConsumableRuntimeEffectType.ApplyBurn, 0.2f)
                },
                new[] { CreateMedium(alcoholT3, 3), CreateMedium(alcoholT5, 5) });
            var alchemist = CreateAlchemist(role);
            var character = new CharacterInstance { CharacterId = "alchemy.high_level.crafter" };
            var session = CreateSession(character, "alchemy.high_level");
            foreach (var item in new[] { alcoholT3, alcoholT5, ingredientA, ingredientB, ingredientC })
                character.Inventory.Items.Add(new ItemInstance(item, item.Id + ".01"));

            var service = new AlchemyCraftingService();
            var rejected = service.CreatePreview(session, alchemist, new AlchemyCraftRequest
            {
                UseType = ConsumableUseType.Potion,
                Ingredients = new List<ItemDefinition> { ingredientA, ingredientB, ingredientC },
                Medium = alcoholT3,
                RecipientCharacterId = character.CharacterId
            });
            Assert.That(rejected.CanCraft, Is.False);
            Assert.That(rejected.FailureReason, Does.Contain("alkohol"));

            var accepted = service.CreatePreview(session, alchemist, new AlchemyCraftRequest
            {
                UseType = ConsumableUseType.Potion,
                Ingredients = new List<ItemDefinition> { ingredientA, ingredientB, ingredientC },
                Medium = alcoholT5,
                RecipientCharacterId = character.CharacterId
            });
            Assert.That(accepted.CanCraft, Is.True, accepted.FailureReason);
            Assert.That(accepted.ResultLevel, Is.EqualTo(41));
            Assert.That(accepted.ResultEffectTier, Is.EqualTo(3));
        }

        [Test]
        public void AlchemyCrafting_RejectsDuplicateIngredients()
        {
            var alcohol = CreateMaterial("alchemy.duplicate.alcohol");
            var ingredientA = CreateMaterial("alchemy.duplicate.a");
            var ingredientB = CreateMaterial("alchemy.duplicate.b");
            var potionDefinition = CreateConsumable("alchemy.duplicate.potion", ConsumableUseType.Potion);
            var role = CreateAlchemyRole(
                potionDefinition,
                null,
                new[]
                {
                    CreateIngredient(ingredientA, 1, ConsumableRuntimeEffectType.Heal, 1f),
                    CreateIngredient(ingredientB, 1, ConsumableRuntimeEffectType.RestoreResource, 0.1f)
                },
                new[] { CreateMedium(alcohol, 1) });
            var alchemist = CreateAlchemist(role);
            var character = new CharacterInstance { CharacterId = "alchemy.duplicate.crafter" };
            var session = CreateSession(character, "alchemy.duplicate");

            var preview = new AlchemyCraftingService().CreatePreview(session, alchemist, new AlchemyCraftRequest
            {
                Ingredients = new List<ItemDefinition> { ingredientA, ingredientA, ingredientB },
                Medium = alcohol,
                RecipientCharacterId = character.CharacterId
            });

            Assert.That(preview.CanCraft, Is.False);
            Assert.That(preview.FailureReason, Does.Contain("różne"));
        }

        [Test]
        public void BombCraftingAndRefill_UseGunpowderAndRespectBombLevel()
        {
            var gunpowder = CreateMaterial("alchemy.gunpowder.t1");
            var ingredientA = CreateMaterial("alchemy.bomb.a");
            var ingredientB = CreateMaterial("alchemy.bomb.b");
            var ingredientC = CreateMaterial("alchemy.bomb.c");
            var bombDefinition = CreateConsumable("alchemy.bomb", ConsumableUseType.Bomb);
            var role = CreateAlchemyRole(
                null,
                bombDefinition,
                new[]
                {
                    CreateIngredient(ingredientA, 1, ConsumableRuntimeEffectType.DealPhysicalDamage, 12f),
                    CreateIngredient(ingredientB, 1, ConsumableRuntimeEffectType.ApplyBurn, 0.2f),
                    CreateIngredient(ingredientC, 1, ConsumableRuntimeEffectType.ModifyDefense, 0.1f)
                },
                null,
                new[] { CreateMedium(gunpowder, 1) });
            var alchemist = CreateAlchemist(role);
            var character = new CharacterInstance { CharacterId = "alchemy.bomb.crafter" };
            var session = CreateSession(character, "alchemy.bomb");
            character.Inventory.Items.Add(new ItemInstance(gunpowder, "gunpowder.01"));
            character.Inventory.Items.Add(new ItemInstance(ingredientA, "a.01"));
            character.Inventory.Items.Add(new ItemInstance(ingredientB, "b.01"));
            character.Inventory.Items.Add(new ItemInstance(ingredientC, "c.01"));

            var service = new AlchemyCraftingService();
            Assert.That(service.TryCraft(session, alchemist, new AlchemyCraftRequest
            {
                UseType = ConsumableUseType.Bomb,
                Ingredients = new List<ItemDefinition> { ingredientA, ingredientB, ingredientC },
                Medium = gunpowder,
                RecipientCharacterId = character.CharacterId
            }, out var crafted, out var craftFailure), Is.True, craftFailure);
            var bomb = crafted.Single();
            bomb.CurrentCharges = 1;
            character.Inventory.Items.Add(new ItemInstance(gunpowder, "gunpowder.02"));

            var refillService = new PotionRefillService();
            Assert.That(refillService.TryRefill(session, alchemist, new PotionRefillRequest
            {
                ConsumableInstanceId = bomb.InstanceId,
                RecipientCharacterId = character.CharacterId,
                MediumDefinitionId = gunpowder.Id
            }, out var refillFailure), Is.True, refillFailure);
            Assert.That(bomb.CurrentCharges, Is.EqualTo(bomb.MaxCharges));
            Assert.That(character.Inventory.Items.Count(item => item.Definition == gunpowder), Is.EqualTo(0));
        }

        [Test]
        public void PotionRefill_ConsumesExactlyOneAlcoholAndFillsPotionRegardlessOfMissingCharges()
        {
            var alcohol = CreateMaterial("alchemy.refill_alcohol");
            var potionDefinition = CreateConsumable("alchemy.refill_potion", ConsumableUseType.Potion);
            var role = CreateAlchemyRole(
                potionDefinition,
                null,
                new[]
                {
                    CreateIngredient(CreateMaterial("alchemy.refill_leaf"), 1, ConsumableRuntimeEffectType.Heal, 10f),
                    CreateIngredient(CreateMaterial("alchemy.refill_root"), 1, ConsumableRuntimeEffectType.RestoreResource, 5f),
                    CreateIngredient(CreateMaterial("alchemy.refill_cap"), 1, ConsumableRuntimeEffectType.RegenHealth, 2f)
                },
                new[] { CreateMedium(alcohol, 1) });
            var alchemist = CreateAlchemist(role);
            var character = new CharacterInstance { CharacterId = "alchemy.refiller" };
            var session = CreateSession(character, "alchemy.refill");
            var potion = new ConsumableItemInstance(potionDefinition, "potion.refill.01")
            {
                ItemLevel = 11,
                CurrentCharges = 1
            };
            character.Inventory.Items.Add(potion);
            character.Inventory.Items.Add(new ItemInstance(alcohol, "alcohol.refill.01"));

            var service = new PotionRefillService();
            var preview = service.CreatePreview(session, alchemist, new PotionRefillRequest
            {
                ConsumableInstanceId = potion.InstanceId,
                RecipientCharacterId = character.CharacterId,
                MediumDefinitionId = alcohol.Id
            });

            Assert.That(preview.CanRefill, Is.True);
            Assert.That(service.TryRefill(session, alchemist, new PotionRefillRequest
            {
                ConsumableInstanceId = potion.InstanceId,
                RecipientCharacterId = character.CharacterId,
                MediumDefinitionId = alcohol.Id
            }, out var failureReason), Is.True, failureReason);
            Assert.That(potion.CurrentCharges, Is.EqualTo(potion.MaxCharges));
            Assert.That(character.Inventory.Items.Count(item => item.Definition == alcohol), Is.EqualTo(0));
        }

        [TestCase(1)]
        [TestCase(5)]
        [TestCase(15)]
        [TestCase(50)]
        public void BombDamage_IsWeaponIndependentAndPreviewMatches(int level)
        {
            var definition = CreateConsumable("alchemy.level_bomb", ConsumableUseType.Bomb);
            var bomb = new ConsumableItemInstance(definition, "bomb.level");
            bomb.RuntimeEffects.Add(new ConsumableEffectRuntime
            {
                EffectType = ConsumableRuntimeEffectType.DealPhysicalDamage, Value = .15f, Chance = 1f
            });
            var skill = ConsumableRuntimeActionBuilder.Build(bomb);
            try
            {
                var caster = new Godmist.Runtime.Combat.BattleUnit
                {
                    Character = new CharacterInstance { Level = level, Stats = new CharacterStats() }
                };
                caster.Character.Stats.MinAttack = new Stat(1);
                caster.Character.Stats.MaxAttack = new Stat(2);
                var effect = skill.Effects[0];
                Assert.That(effect.DamageValueSource, Is.EqualTo(DamageValueSource.AlchemicalPower));
                var first = Godmist.Systems.Combat.CombatDamageValueCalculator.CalculateDamageValue(caster, effect.DamageValueSource, effect.DamageMultiplier);
                caster.Character.Stats.MinAttack = new Stat(10000);
                caster.Character.Stats.MaxAttack = new Stat(20000);
                var second = Godmist.Systems.Combat.CombatDamageValueCalculator.CalculateDamageValue(caster, effect.DamageValueSource, effect.DamageMultiplier);
                var preview = Godmist.Systems.Combat.CombatSkillPreviewCalculator.CalculateDamageRange(caster, skill);
                Assert.That(second, Is.EqualTo(first));
                Assert.That(preview.Minimum, Is.EqualTo(first));
                Assert.That(preview.Maximum, Is.EqualTo(first));
            }
            finally { Object.DestroyImmediate(skill); Object.DestroyImmediate(definition); }
        }

        [TestCase(.15f, .4f)]
        [TestCase(.225f, .6f)]
        public void BombDamage_MatchesReferenceMitigatedHitAcrossLevels(float strength, float expectedRatio)
        {
            var previous = 0;
            for (var level = 1; level <= 50; level++)
            {
                var reference = Godmist.Systems.Combat.CombatDamageValueCalculator.GetAlchemicalReferenceAttack(level);
                var raw = Godmist.Systems.Combat.CombatDamageValueCalculator.CalculateAlchemicalDamage(level, strength);
                var mitigated = (float)raw * raw / (raw + reference);
                Assert.That(mitigated / (reference / 2), Is.EqualTo(expectedRatio).Within(.05f));
                Assert.That(raw, Is.GreaterThanOrEqualTo(previous));
                previous = raw;
            }
        }

        [Test]
        public void DurationCatalyst_ExtendsRegenerationAndPreservesTotal()
        {
            var definition = CreateConsumable("alchemy.duration_potion", ConsumableUseType.Potion);
            var potion = new ConsumableItemInstance(definition, "alchemy.duration.01");
            potion.RuntimeEffects.Add(new ConsumableEffectRuntime
            {
                EffectType = ConsumableRuntimeEffectType.RegenHealth,
                Value = .25f,
                DurationTurns = 6,
                Chance = 1f
            });
            potion.ApplyCatalyst(ConsumableCatalystType.Duration, 1);

            var skill = ConsumableRuntimeActionBuilder.Build(potion);

            Assert.That(skill.Effects, Has.Count.EqualTo(1));
            Assert.That(skill.Effects[0].DurationTurns, Is.EqualTo(7));
            Assert.That(skill.Effects[0].PercentageValue, Is.EqualTo(.25f / 7f).Within(.0001f));
        }

        [TestCase(ConsumableRuntimeEffectType.RegenHealth, -7)]
        [TestCase(ConsumableRuntimeEffectType.RegenHealth, 0)]
        [TestCase(ConsumableRuntimeEffectType.RegenResource, -7)]
        [TestCase(ConsumableRuntimeEffectType.RegenResource, 0)]
        public void Regeneration_TotalIsPreservedAcrossDurationAndIntegerTicks(ConsumableRuntimeEffectType type, int durationModifier)
        {
            var potion = new ConsumableItemInstance(CreateConsumable("alchemy.total", ConsumableUseType.Potion), "total")
            {
                CatalystDurationModifier = durationModifier
            };
            potion.RuntimeEffects.Add(new ConsumableEffectRuntime { EffectType = type, Value = .25f, DurationTurns = 10 });
            var skill = ConsumableRuntimeActionBuilder.Build(potion);
            try
            {
                var effect = skill.Effects[0];
                Assert.That(effect.DurationTurns, Is.EqualTo(10 + durationModifier));
                Assert.That(effect.PercentageValue * effect.DurationTurns, Is.EqualTo(.25f).Within(.00001f));
                var carry = 0f;
                var total = 0;
                for (var tick = 0; tick < effect.DurationTurns; tick++)
                    total += Godmist.Systems.Combat.PeriodicRecoveryPassiveEffectProcessor.GetRecoveryAmount(100 * effect.PercentageValue, ref carry);
                Assert.That(total, Is.EqualTo(25));
            }
            finally { Object.DestroyImmediate(skill); }
        }

        [Test]
        public void PriestHealing_FullyHealsLivingActivePartyWithoutRevivingDownedMembers()
        {
            var role = ScriptableObject.CreateInstance<PriestRoleData>();
            var definition = ScriptableObject.CreateInstance<NpcDefinition>();
            SetAutoProperty(definition, "Id", "npc.priest.acceptance");
            SetAutoProperty(definition, "Roles", new List<NpcRoleData> { role });
            var priest = new NpcInstance { NpcId = definition.Id, Definition = definition };
            var wounded = CreatePartyMember("priest.wounded", 100, 35, CharacterLifeState.Alive);
            var downed = CreatePartyMember("priest.downed", 80, 0, CharacterLifeState.Downed);
            var session = CreateSession(wounded, "priest.healing");
            Assert.That(session.TryRegisterCharacter(downed), Is.True);
            Assert.That(session.TryAddCharacterToActiveParty(downed.CharacterId), Is.True);

            var service = new PriestHealingService();
            var preview = service.CreatePreview(session, priest);

            Assert.That(preview.CanHeal, Is.True);
            Assert.That(preview.HealableMembers, Is.EqualTo(1));
            Assert.That(preview.MissingHealth, Is.EqualTo(65));
            Assert.That(service.TryHealActiveParty(session, priest, out var restored, out var failure), Is.True, failure);
            Assert.That(restored, Is.EqualTo(65));
            Assert.That(wounded.CurrentHealth, Is.EqualTo(100));
            Assert.That(downed.CurrentHealth, Is.EqualTo(0));
            Assert.That(downed.LifeState, Is.EqualTo(CharacterLifeState.Downed));
        }

        [Test]
        public void DialoguePartyAction_HealsActivePartyWhenHealingChoiceIsSelected()
        {
            var wounded = CreatePartyMember("priest.dialogue", 100, 20, CharacterLifeState.Alive);
            var session = CreateSession(wounded, "priest.dialogue.action");
            var choice = new DialogueChoiceData();
            SetAutoProperty(choice, "ChoiceId", "priest.heal_party");
            SetAutoProperty(choice, "PartyAction", DialoguePartyAction.HealActiveParty);
            var node = new DialogueNodeData();
            SetAutoProperty(node, "NodeId", "greeting");
            SetAutoProperty(node, "Choices", new List<DialogueChoiceData> { choice });
            var dialogueDefinition = ScriptableObject.CreateInstance<DialogueDefinition>();
            SetAutoProperty(dialogueDefinition, "Id", "dialogue.priest.test");
            SetAutoProperty(dialogueDefinition, "StartNodeId", "greeting");
            SetAutoProperty(dialogueDefinition, "Nodes", new List<DialogueNodeData> { node });
            var npcDefinition = ScriptableObject.CreateInstance<NpcDefinition>();
            SetAutoProperty(npcDefinition, "Id", "npc.priest.dialogue");
            SetAutoProperty(npcDefinition, "BaseDialogue", dialogueDefinition);
            var dialogueService = new DialogueService();
            using var partyActions = new DialoguePartyActionSystem(dialogueService, session);

            Assert.That(dialogueService.TryStartDialogue(
                new NpcInstance { NpcId = npcDefinition.Id, Definition = npcDefinition },
                wounded.CharacterId), Is.True);
            Assert.That(dialogueService.TrySelectChoice(0), Is.True);
            Assert.That(wounded.CurrentHealth, Is.EqualTo(100));
        }

        private static AlchemistRoleData CreateAlchemyRole(
            ConsumableItemDefinition potion,
            ConsumableItemDefinition bomb,
            IEnumerable<AlchemyIngredientData> ingredients,
            IEnumerable<AlchemyMediumData> potions,
            IEnumerable<AlchemyMediumData> bombs = null)
        {
            var role = ScriptableObject.CreateInstance<AlchemistRoleData>();
            SetAutoProperty(role, "IngredientOptions", ingredients.ToList());
            SetAutoProperty(role, "PotionAlcohols", potions?.ToList() ?? new List<AlchemyMediumData>());
            SetAutoProperty(role, "BombGunpowders", bombs?.ToList() ?? new List<AlchemyMediumData>());
            SetAutoProperty(role, "PotionBaseDefinition", potion);
            SetAutoProperty(role, "BombBaseDefinition", bomb);
            return role;
        }

        private static AlchemyIngredientData CreateIngredient(
            ItemDefinition material,
            int tier,
            ConsumableRuntimeEffectType effectType,
            float value)
        {
            var ingredient = new AlchemyIngredientData();
            SetAutoProperty(ingredient, "Material", material);
            if (material.BaseRarity == RarityTier.Common)
            {
                SetAutoProperty(material, "BaseRarity", tier switch
                {
                    2 => RarityTier.Uncommon,
                    3 => RarityTier.Ancient,
                    _ => RarityTier.Common
                });
            }
            var effect = new ConsumableEffectDefinition();
            SetAutoProperty(effect, "SourceIngredient", material);
            SetAutoProperty(effect, "EffectType", effectType);
            SetAutoProperty(effect, "Value", value);
            SetAutoProperty(effect, "Chance", 1f);
            SetAutoProperty(effect, "TargetType", TargetType.Caster);
            SetAutoProperty(effect, "TargetingType", TargetingType.Allies);
            SetAutoProperty(ingredient, "PotionEffect", effect);
            SetAutoProperty(ingredient, "BombEffect", effect);
            return ingredient;
        }

        private static AlchemyMediumData CreateMedium(ItemDefinition material, int tier)
        {
            var medium = new AlchemyMediumData();
            SetAutoProperty(medium, "Material", material);
            SetAutoProperty(medium, "Tier", tier);
            return medium;
        }

        private static NpcInstance CreateAlchemist(AlchemistRoleData role)
        {
            var definition = ScriptableObject.CreateInstance<NpcDefinition>();
            SetAutoProperty(definition, "Id", "npc.alchemist.acceptance");
            SetAutoProperty(definition, "Roles", new List<NpcRoleData> { role });
            return new NpcInstance
            {
                NpcId = definition.Id,
                Definition = definition,
                ShopInventory = new ShopInventory()
            };
        }

        private static GameSession CreateSession(CharacterInstance character, string sessionId)
        {
            var session = new GameSession(GameModeState.Exploration, sessionId);
            Assert.That(session.TryRegisterCharacter(character), Is.True);
            Assert.That(session.TryAddCharacterToActiveParty(character.CharacterId), Is.True);
            return session;
        }

        private static CharacterInstance CreatePartyMember(
            string characterId,
            int maximumHealth,
            int currentHealth,
            CharacterLifeState lifeState)
        {
            return new CharacterInstance
            {
                CharacterId = characterId,
                CurrentHealth = currentHealth,
                LifeState = lifeState,
                Stats = new CharacterStats { MaxHealth = new Stat(maximumHealth) }
            };
        }

        private static ItemDefinition CreateMaterial(
            string id,
            RarityTier rarity = RarityTier.Common,
            int baseValue = 0)
        {
            var material = ScriptableObject.CreateInstance<ItemDefinition>();
            SetAutoProperty(material, "Id", id);
            SetAutoProperty(material, "DisplayName", id);
            SetAutoProperty(material, "ItemType", ItemType.Material);
            SetAutoProperty(material, "MaterialDiscipline", MaterialDiscipline.Alchemy);
            SetAutoProperty(material, "BaseRarity", rarity);
            SetAutoProperty(material, "BaseValue", baseValue);
            SetAutoProperty(material, "IsStackable", true);
            return material;
        }

        private static ConsumableItemDefinition CreateConsumable(string id, ConsumableUseType useType)
        {
            var consumable = ScriptableObject.CreateInstance<ConsumableItemDefinition>();
            SetAutoProperty(consumable, "Id", id);
            SetAutoProperty(consumable, "DisplayName", id);
            SetAutoProperty(consumable, "ItemType", useType == ConsumableUseType.Bomb ? ItemType.Bomb : ItemType.Potion);
            SetAutoProperty(consumable, "BaseRarity", RarityTier.Common);
            SetAutoProperty(consumable, "UseType", useType);
            SetAutoProperty(consumable, "MaxCharges", 5);
            return consumable;
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
