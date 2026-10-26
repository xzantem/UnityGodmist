using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Items;
using Godmist.Data.NPC;
using Godmist.Data.World;
using Godmist.Editor;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;
using Godmist.Systems.Characters;
using Godmist.Systems.Items;
using Godmist.Systems.SaveLoad;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class DungeonFoodTests
    {
        static ConsumableItemDefinition Food => AssetDatabase.LoadAssetAtPath<ConsumableItemDefinition>(DungeonFoodSetup.FoodPath);
        static GameContentCatalog Catalog => AssetDatabase.LoadAssetAtPath<GameContentCatalog>(DungeonFoodSetup.CatalogPath);
        static ConsumableItemInstance Portion(string id = null) => (ConsumableItemInstance)new ItemInstanceFactory().Create(Food, id);
        static CharacterInstance Character(int max = 350) => new()
        {
            CharacterId = Guid.NewGuid().ToString(), LifeState = CharacterLifeState.Alive, CurrentHealth = 1,
            Stats = new CharacterStats { MaxHealth = new Stat(max) }, ResourcePool = new ResourcePool(ResourceType.Mana, 20, 100, 10)
        };
        [Test, Explicit("Apply approved food definition, table entries, catalog and camp description through Editor API.")]
        public void ApplyContent()
        {
            DungeonFoodSetup.Apply(); DungeonFoodSetup.Apply();
            Assert.That(Catalog.ItemDefinitions.Count(d => d?.Id == DungeonFoodRules.ItemId), Is.EqualTo(1));
            Assert.That(Food.Icon, Is.Not.Null); Assert.That(Food.Icon.texture.width, Is.EqualTo(32)); Assert.That(Food.Icon.texture.height, Is.EqualTo(32));
            Assert.That(Food.MaxCharges, Is.EqualTo(1)); Assert.That(Food.IsReusable, Is.False); Assert.That(Food.ItemType, Is.EqualTo(ItemType.Food));
            var kit = AssetDatabase.LoadAssetAtPath<ItemDefinition>(DungeonFoodSetup.KitPath);
            Assert.That(kit.Description, Does.Contain("60%")); Assert.That(kit.Description, Does.Not.Contain("40%"));
        }
        [TestCase(101)] [TestCase(350)] [TestCase(550)]
        public void EatingRestoresTenPercentOfOneLivingOwnerAndConsumesExactlyOnePortion(int max)
        {
            var character = Character(max); var other = Character(max); var item = Portion(); var spare = Portion();
            character.Inventory.Items.AddRange(new[] { item, spare });
            Assert.That(new ConsumableService().TryUseInExploration(character, item, out var effect), Is.True);
            Assert.That(effect, Is.EqualTo(ExplorationConsumableEffect.RestoreHealthPercent));
            Assert.That(character.CurrentHealth, Is.EqualTo(1 + Mathf.CeilToInt(max * .1f))); Assert.That(other.CurrentHealth, Is.EqualTo(1));
            Assert.That(character.ResourcePool.CurrentValue, Is.EqualTo(20)); Assert.That(character.Inventory.Items, Is.EqualTo(new[] { spare }));
            Assert.That(item.CurrentCharges, Is.Zero); Assert.That(spare.CurrentCharges, Is.EqualTo(1));
            Assert.That(new ConsumableService().TryUseInExploration(character, item, out _), Is.False);
        }
        [TestCase(true)] [TestCase(false)]
        public void FullHealthAndDeathNeverWasteFoodOrRevive(bool full)
        {
            var character = Character(); var item = Portion(); character.Inventory.Items.Add(item);
            character.CurrentHealth = full ? 350 : 0; character.LifeState = full ? CharacterLifeState.Alive : CharacterLifeState.Dead;
            Assert.That(new ConsumableService().TryUseInExploration(character, item, out _), Is.False);
            Assert.That(character.Inventory.Items, Has.Member(item)); Assert.That(item.CurrentCharges, Is.EqualTo(1));
            Assert.That(character.CurrentHealth, Is.EqualTo(full ? 350 : 0));
        }
        [Test]
        public void OverhealClampsAndAnotherCharactersPortionCannotBeUsed()
        {
            var character = Character(); var owner = Character(); var item = Portion(); owner.Inventory.Items.Add(item);
            Assert.That(new ConsumableService().TryUseInExploration(character, item, out _), Is.False);
            owner.CurrentHealth = 349; Assert.That(new ConsumableService().TryUseInExploration(owner, item, out _), Is.True);
            Assert.That(owner.CurrentHealth, Is.EqualTo(350)); Assert.That(character.CurrentHealth, Is.EqualTo(1));
        }
        [Test]
        public void FoodCannotBecomeCombatActionOrBeUsedByCombatInventoryEvenWithRuntimePayload()
        {
            var character = Character(); var item = Portion(); character.Inventory.Items.Add(item);
            item.RuntimeEffects.Add(new ConsumableEffectRuntime { EffectType = ConsumableRuntimeEffectType.RestoreHealth, Value = .1f });
            var actor = new BattleUnit { Character = character, IsPlayerSide = true, CurrentActionPoints = 100, MaxActionPoints = 100 };
            var session = new CombatSession { ActiveUnit = actor }; session.Units.Add(actor);
            Assert.That(ConsumableService.GetCombatItems(session), Is.Empty); Assert.That(ConsumableService.IsCombatUsable(item), Is.False);
            Assert.That(ConsumableService.CanUseInCombat(session, actor, item), Is.False); Assert.That(ConsumableRuntimeActionBuilder.Build(item), Is.Null);
            var executed = false;
            Assert.That(new ConsumableService().TryExecuteInCombat(session, actor, item, () => { executed = true; return true; }), Is.False);
            Assert.That(executed, Is.False); Assert.That(character.CurrentHealth, Is.EqualTo(1)); Assert.That(item.CurrentCharges, Is.EqualTo(1));
            Assert.That(ItemIconPresentation.GetIcon(item), Is.SameAs(Food.Icon), "Food must not be composed into a potion icon.");
        }
        [Test]
        public void FoodIsNotAnAlchemyBaseOrRefillablePotion()
        {
            var character = Character(); var item = Portion(); item.CurrentCharges = 0; character.Inventory.Items.Add(item);
            var game = new GameSession(GameModeState.Exploration); game.TryRegisterCharacter(character); game.TryAddCharacterToActiveParty(character.CharacterId);
            var npc = new NpcInstance { Definition = AssetDatabase.LoadAssetAtPath<NpcDefinition>("Assets/_Project/ScriptableObjects/NPC/ND_Arungard_Alchemist.asset") };
            var preview = new PotionRefillService().CreatePreview(game, npc, new PotionRefillRequest { ConsumableInstanceId = item.InstanceId });
            Assert.That(preview.CanRefill, Is.False); Assert.That(preview.Consumable, Is.Null);
            Assert.That(AlchemyRuntimeRules.IsValidConsumableType(ConsumableUseType.Food), Is.False);
        }
        [Test]
        public void TooltipShowsHealingAndExplorationRestrictionWithoutPotionChargeClutter()
        {
            var root = new GameObject("Food tooltip"); root.SetActive(false);
            try
            {
                var view = root.AddComponent<UIWorkspaceTooltipView>();
                var body = (string)typeof(UIWorkspaceTooltipView).GetMethod("BuildItemBody", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(view, new object[] { Portion(), null });
                Assert.That(body, Does.Contain("10% maks. HP")); Assert.That(body, Does.Contain("Tylko poza walką")); Assert.That(body, Does.Not.Contain("Ładunki"));
            }
            finally { Object.DestroyImmediate(root); }
        }
        [Test]
        public void SaveRoundTripPreservesPortionsAndRestoredFoodStillUsesImpliedHeal()
        {
            var character = new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Mage.asset"));
            character.CurrentHealth = 1; var food = Portion("food.inventory"); character.Inventory.Items.Add(food);
            var game = new GameSession(GameModeState.Exploration); game.TryRegisterCharacter(character); game.TryAddCharacterToActiveParty(character.CharacterId);
            game.Stash.Items.Add(Portion("food.stash"));
            var snapshot = new GameSessionSaveSnapshotFactory().CreateSnapshot(game, "Food");
            Assert.That(new GameSessionRestoreFactory().TryCreate(snapshot, Catalog, out var restored), Is.True);
            var owner = restored.CharactersById[character.CharacterId]; var portion = owner.Inventory.Items.OfType<ConsumableItemInstance>().Single(i => i.InstanceId == "food.inventory");
            Assert.That(portion.ConsumableDefinition.UseType, Is.EqualTo(ConsumableUseType.Food)); Assert.That(portion.CurrentCharges, Is.EqualTo(1));
            Assert.That(portion.RuntimeEffects, Is.Empty); var hp = owner.CurrentHealth;
            Assert.That(new ConsumableService().TryUseInExploration(owner, portion, out _), Is.True);
            Assert.That(owner.CurrentHealth, Is.EqualTo(hp + Mathf.CeilToInt(owner.Stats.MaxHealth.GetValue() * .1f)));
            Assert.That(restored.Stash.Items.Single().InstanceId, Is.EqualTo("food.stash"));
        }
        [TestCase("Forest", "Dungeons/Forest/Loot")] [TestCase("Catacombs", "Catacombs/Loot")]
        public void ProductionTablesAddIndependentSinglePortionWithoutChangingOtherEntries(string biome, string folder)
        {
            foreach (var bracket in new[] { "1_10", "11_20" }) foreach (var kind in new[] { "Enemy", "Chest" })
            {
                var path = $"Assets/_Project/ScriptableObjects/{folder}/LT_{biome}_{kind}_{bracket}_Production.asset";
                var table = AssetDatabase.LoadAssetAtPath<LootTableData>(path);
                var food = table.Entries.Single(e => e.ItemDefinition == Food);
                Assert.That(food.DropChance, Is.EqualTo(kind == "Enemy" ? .35f : .5f)); Assert.That(food.Amount.Min, Is.EqualTo(1)); Assert.That(food.Amount.Max, Is.EqualTo(1));
                Assert.That(table.Entries[0].ItemDefinition, Is.TypeOf<LootBagItemDefinition>());
                Assert.That(table.Entries[0].DropChance, Is.EqualTo(kind == "Enemy" ? .5f : 1f));
            }
        }
        [Test]
        public void ExistingPrerolledEncounterLootIsNeverFilledOrRerolledByNewFoodTables()
        {
            var definition = Catalog.DungeonDefinitions.Single(d => d != null && d.Id == "dungeon.forest.expedition");
            var dungeon = new Godmist.Systems.Dungeons.DungeonGeneratorService().Generate(definition, 241026, 10, DungeonLength.Short);
            new Godmist.Systems.Dungeons.DungeonEncounterPlacementSystem().Place(dungeon);
            var encounter = dungeon.Floors.SelectMany(f => f.Encounters).First(e => !e.IsReinforcement);
            var saved = new CombatRewardResult(); // Previously saved reward, with no food.
            Assert.That(dungeon.TryStorePreRolledRewards(encounter.InstanceId, saved), Is.True);
            var preparation = new Godmist.Systems.Dungeons.DungeonEncounterRewardPreparationSystem();
            preparation.Prepare(dungeon, Catalog.EquipmentPartDefinitions); preparation.Prepare(dungeon, Catalog.EquipmentPartDefinitions);
            Assert.That(dungeon.TryGetPreRolledRewards(encounter.InstanceId, out var actual), Is.True);
            Assert.That(actual, Is.SameAs(saved)); Assert.That(actual.Items, Is.Empty); Assert.That(actual.EnemyRewards, Is.Empty);
        }
        [Test, Explicit("Capture actual Unity-imported 32px icon through GPU; no source image edits.")]
        public void RenderImportedIcon()
        {
            var target = new RenderTexture(32, 32, 0, RenderTextureFormat.ARGB32); var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false); var previous = RenderTexture.active;
            try
            {
                target.Create(); Graphics.Blit(Food.Icon.texture, target); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 32, 32), 0, 0); texture.Apply();
                File.WriteAllBytes("../LocalHandoffs/dungeon-food-recovery-20261024/ration-imported-32.png", texture.EncodeToPNG());
                Assert.That(texture.GetPixels32().Count(p => p.a > 0), Is.GreaterThan(100));
            }
            finally { RenderTexture.active = previous; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(texture); }
        }
    }
}
