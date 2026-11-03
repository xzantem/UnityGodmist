using System;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Data.NPC;
using Godmist.Data.World;
using Godmist.Editor;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Items;
using Godmist.Systems.Characters;
using Godmist.Systems.Items;
using Godmist.Systems.NPC;
using Godmist.Systems.SaveLoad;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class ReturnScrollTests
    {
        static ConsumableItemDefinition Scroll => AssetDatabase.LoadAssetAtPath<ConsumableItemDefinition>(ReturnScrollSetup.ScrollPath);
        static GameContentCatalog Catalog => AssetDatabase.LoadAssetAtPath<GameContentCatalog>(ReturnScrollSetup.Root + "World/GC_MainContentCatalog.asset");
        static string Folder => Path.GetFullPath("../LocalHandoffs/return-scroll-20261103");
        [Serializable] class Snapshot { public string[] paths, contents; }
        static Snapshot Capture()
        {
            var paths = new[] { ReturnScrollSetup.ScrollPath, ReturnScrollSetup.MerchantPath }.Concat(ReturnScrollSetup.ChestPaths).ToArray();
            return new Snapshot { paths = paths, contents = paths.Select(p => EditorJsonUtility.ToJson(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p))).ToArray() };
        }

        [Test, Explicit("Approved native authoring of scroll price, merchant supply and chest entries.")]
        public void AuthorReturnScrollSupply()
        {
            Directory.CreateDirectory(Folder);
            Assert.That(File.Exists(Path.Combine(Folder, "before.json")), Is.False);
            File.WriteAllText(Path.Combine(Folder, "before.json"), JsonUtility.ToJson(Capture(), true));
            ReturnScrollSetup.Apply();
            var after = JsonUtility.ToJson(Capture(), true);
            File.WriteAllText(Path.Combine(Folder, "after.json"), after);
            ReturnScrollSetup.Apply();
            Assert.That(JsonUtility.ToJson(Capture(), true), Is.EqualTo(after), "Native authoring must be idempotent.");
        }

        sealed class AvailabilityRandom : System.Random
        {
            readonly double value;
            public AvailabilityRandom(double value) : base(127) { this.value = value; }
            public override double NextDouble() => value;
        }

        static Godmist.Runtime.Game.GameSession NewCampaign()
        {
            var profile = AssetDatabase.LoadAssetAtPath<NewCampaignDefinition>(ReturnScrollSetup.Root + "StartingCampaign/NC_Arungard.asset");
            Assert.That(new NewCampaignService().TryCreate(profile, out var session, out var error), Is.True, error);
            // City scene registration uses this factory; NewCampaignService
            // creates the party but does not spawn/register scene NPCs itself.
            foreach (var definition in Catalog.NpcDefinitions.Where(d => d != null))
                Assert.That(session.TryRegisterNpc(new NpcInstanceFactory().Create(definition, 1, Catalog)), Is.True);
            return session;
        }

        [Test]
        public void InitialEnchanterHasOneScrollCosts200AndPurchasePersists()
        {
            var session = NewCampaign();
            var merchant = session.NpcsById.Values.Single(n => n.Definition.GetRole<EnchanterRoleData>() != null);
            var scroll = merchant.ShopInventory.Items.Single(i => i.Definition == Scroll);
            Assert.That(scroll, Is.TypeOf<ConsumableItemInstance>());
            Assert.That(Scroll.BaseRarity, Is.EqualTo(RarityTier.Rare));
            Assert.That(Scroll.Icon, Is.Not.Null);
            Assert.That(Scroll.BaseValue, Is.EqualTo(200));
            Assert.That(Scroll.IsStackable, Is.True);
            var trade = new TradeService();
            Assert.That(trade.GetPriceToBuyFromTrader(merchant, scroll), Is.EqualTo(200));
            session.TryAddGold(200);
            var owner = session.CharactersById[session.PlayerParty.ActiveLeaderId];
            Assert.That(trade.TryBuyFromTrader(session, merchant, owner, scroll.InstanceId, out var paid), Is.True);
            Assert.That(paid, Is.EqualTo(200));
            Assert.That(merchant.ShopInventory.Items.Any(i => i.Definition == Scroll), Is.False);
            var snapshot = new GameSessionSaveSnapshotFactory().CreateSnapshot(session, "scroll-purchase");
            Assert.That(new GameSessionRestoreFactory().TryCreate(snapshot, Catalog, out var restored, out var failure), Is.True, failure?.ToString());
            var restoredMerchant = restored.NpcsById[merchant.NpcId];
            Assert.That(restoredMerchant.ShopInventory.Items.Any(i => i.Definition == Scroll), Is.False, "Save/load cannot grant initial stock again.");
            var restoredScroll = restored.CharactersById[owner.CharacterId].Inventory.Items.OfType<ConsumableItemInstance>().Single(i => i.Definition == Scroll);
            Assert.That(restoredScroll.InstanceId, Is.EqualTo(scroll.InstanceId));
            Assert.That(restoredScroll.CurrentCharges, Is.EqualTo(1));
        }

        [Test]
        public void OptionalStockHasAuthoredChanceAndCannotRefillOnReadOrSaveLoad()
        {
            var session = NewCampaign();
            var merchant = session.NpcsById.Values.Single(n => n.Definition.GetRole<EnchanterRoleData>() != null);
            var role = merchant.Definition.GetRole<TraderRoleData>();
            var group = role.RotatingStock.Single(g => g.Id == ReturnScrollSetup.GroupId);
            Assert.That(group.AvailabilityChance, Is.EqualTo(.2f));
            Assert.That(group.GuaranteedInitially, Is.True);
            Assert.That(group.Candidates.Single().Limit, Is.EqualTo(1));
            Assert.That(role.RotatingStock.Where(g => g != group).All(g => g.AvailabilityChance == 1f), Is.True);
            new TraderStockService(new AvailabilityRandom(.99)).Refresh(merchant, 1, catalog: Catalog);
            Assert.That(merchant.ShopInventory.Items.Any(i => i.Definition == Scroll), Is.False);
            var snapshot = new GameSessionSaveSnapshotFactory().CreateSnapshot(session, "scroll-stock-absent");
            Assert.That(new GameSessionRestoreFactory().TryCreate(snapshot, Catalog, out var restored, out var failure), Is.True, failure?.ToString());
            Assert.That(restored.NpcsById[merchant.NpcId].ShopInventory.Items.Any(i => i.Definition == Scroll), Is.False);
            new TraderStockService(new AvailabilityRandom(0)).Refresh(merchant, 1, catalog: Catalog);
            Assert.That(merchant.ShopInventory.Items.Count(i => i.Definition == Scroll), Is.EqualTo(1));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void ProductionChestHasIndependentFourPercentOneScroll(int index)
        {
            var table = AssetDatabase.LoadAssetAtPath<LootTableData>(ReturnScrollSetup.ChestPaths[index]);
            var entry = table.Entries.Single(e => e.ItemDefinition == Scroll);
            Assert.That(entry.DropChance, Is.EqualTo(.04f));
            Assert.That(entry.Amount.Min, Is.EqualTo(1));
            Assert.That(entry.Amount.Max, Is.EqualTo(1));
            foreach (var budget in new[] { 0, 1, 1000 })
            {
                var dropped = new LootTableSystem(new AvailabilityRandom(0), Catalog.EquipmentPartDefinitions).RollChestLoot(table, budget);
                Assert.That(dropped.Count(i => i.Definition == Scroll), Is.EqualTo(1), "Equipment normalization must not alter utility drops.");
                var missed = new LootTableSystem(new AvailabilityRandom(.99), Catalog.EquipmentPartDefinitions).RollChestLoot(table, budget);
                Assert.That(missed.Any(i => i.Definition == Scroll), Is.False);
            }
        }

        [Test]
        public void DeferredSuccessfulReturnConsumesExactlyOnceAndFailureKeepsScroll()
        {
            var owner = new CharacterInstance { CharacterId = "test.scroll.owner" };
            var scroll = new ConsumableItemInstance(Scroll);
            owner.Inventory.Items.Add(scroll);
            var service = new ConsumableService();
            Assert.That(ConsumableService.IsCombatUsable(scroll), Is.False);
            Assert.That(service.TryUseInExploration(owner, scroll, out _, _ => false), Is.False);
            Assert.That(scroll.CurrentCharges, Is.EqualTo(1));
            Action completed = null;
            Assert.That(service.TryUseInExploration(owner, scroll, out var effect,
                commit => { completed = commit; return true; }), Is.True);
            Assert.That(effect, Is.EqualTo(ExplorationConsumableEffect.ReturnToArungard));
            Assert.That(owner.Inventory.Items, Does.Contain(scroll), "Loading has not finished yet.");
            Assert.That(scroll.CurrentCharges, Is.EqualTo(1));
            completed(); completed();
            Assert.That(scroll.CurrentCharges, Is.Zero);
            Assert.That(owner.Inventory.Items, Has.No.Member(scroll));
            Assert.That(service.TryUseInExploration(owner, scroll, out _, _ => true), Is.False);
        }

        [Test]
        public void AuthoredScrollTooltipExplainsExplorationAndResumeRules()
        {
            var node = new GameObject("Return scroll tooltip");
            try
            {
                var view = node.AddComponent<UIWorkspaceTooltipView>();
                var body = (string)typeof(UIWorkspaceTooltipView).GetMethod("BuildItemBody",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(view, new object[] { new ConsumableItemInstance(Scroll), null });
                Assert.That(body, Does.Contain("poza walką").And.Contain("Zachowuje wyprawę do wznowienia"));
            }
            finally { UnityEngine.Object.DestroyImmediate(node); }
        }
    }
}
