using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.World;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Characters;
using Godmist.Systems.Dungeons;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DungeonRetreatTests
    {
        static GameContentCatalog Catalog => AssetDatabase.LoadAssetAtPath<GameContentCatalog>(
            "Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");

        [TestCase("forest", DungeonLength.Short)]
        [TestCase("forest", DungeonLength.Normal)]
        [TestCase("forest", DungeonLength.Long)]
        [TestCase("catacombs", DungeonLength.Short)]
        [TestCase("catacombs", DungeonLength.Normal)]
        [TestCase("catacombs", DungeonLength.Long)]
        public void AbandonmentClearsInstanceWithoutCompletionOrRewardsAndSurvivesSave(string theme, DungeonLength length)
        {
            var profile = AssetDatabase.LoadAssetAtPath<NewCampaignDefinition>(
                "Assets/_Project/ScriptableObjects/StartingCampaign/NC_Arungard.asset");
            Assert.That(new NewCampaignService().TryCreate(profile, out var session, out var error), Is.True, error);
            var definition = Catalog.DungeonDefinitions.Single(d => d != null && d.Id == "dungeon." + theme + ".expedition");
            var dungeon = new DungeonGeneratorService().Generate(definition, 231023, 1, length);
            Assert.That(session.TrySetActiveDungeon(dungeon), Is.True);
            Assert.That(new DungeonCampService().TryIssueSupplies(session, Catalog), Is.True);
            session.DungeonOffers.SetDanger(definition.Theme, 65);
            var boss = Catalog.DungeonDefinitions.First(d => d != null && d.Theme == definition.Theme && d.BossEncounterDefinition != null);
            Assert.That(session.DungeonOffers.QueueBoss(definition.Theme, boss.Id, 10, DungeonLength.Short), Is.True);
            var gold = session.Gold;
            var xp = session.CharactersById.ToDictionary(p => p.Key, p => p.Value.CurrentExperience);
            var items = session.CharactersById.ToDictionary(p => p.Key,
                p => p.Value.Inventory.Items.Where(i => !DungeonCampService.IsCampingKit(i)).ToArray());
            var questState = session.QuestsById.ToDictionary(p => p.Key, p => p.Value.Status);
            var completed = session.DungeonOffers.CompletedExpeditions;
            var generation = session.DungeonOffers.Generation;
            Assert.That(DungeonAbandonmentSystem.TryAbandon(session, dungeon), Is.True);
            Assert.That(session.ActiveDungeon, Is.Null);
            Assert.That(dungeon.IsCompleted, Is.False);
            Assert.That(session.DungeonOffers.CompletedExpeditions, Is.EqualTo(completed));
            Assert.That(session.DungeonOffers.Generation, Is.EqualTo(generation + 1));
            Assert.That(session.DungeonOffers.GetDanger(definition.Theme), Is.EqualTo(65));
            Assert.That(session.DungeonOffers.BossOpportunities.Single().DefinitionId, Is.EqualTo(boss.Id));
            Assert.That(session.Gold, Is.EqualTo(gold));
            foreach (var character in session.CharactersById.Values)
            {
                Assert.That(character.CurrentExperience, Is.EqualTo(xp[character.CharacterId]));
                CollectionAssert.AreEqual(items[character.CharacterId], character.Inventory.Items);
            }
            foreach (var quest in session.QuestsById) Assert.That(quest.Value.Status, Is.EqualTo(questState[quest.Key]));
            Assert.That(DungeonAbandonmentSystem.TryAbandon(session, dungeon), Is.False, "No double settlement.");
            var snapshot = new GameSessionSaveSnapshotFactory().CreateSnapshot(session, "retreated");
            Assert.That(new GameSessionRestoreFactory().TryCreate(snapshot, Catalog, out var restored, out var failure), Is.True, failure?.ToString());
            Assert.That(restored.ActiveDungeon, Is.Null);
            Assert.That(restored.DungeonOffers.CompletedExpeditions, Is.EqualTo(completed));
            Assert.That(restored.DungeonOffers.GetDanger(definition.Theme), Is.EqualTo(65));
        }

        [Test]
        public void AbandonmentRejectsCombatAndStaleInstanceWithoutMutation()
        {
            var session = new Godmist.Runtime.Game.GameSession(GameModeState.Exploration);
            var dungeon = new DungeonInstance("test.retreat", 1);
            Assert.That(session.TrySetActiveDungeon(dungeon), Is.True);
            var generation = session.DungeonOffers.Generation;
            Assert.That(DungeonAbandonmentSystem.TryAbandon(session, new DungeonInstance("test.other", 1)), Is.False);
            Assert.That(session.TrySetActiveCombat(new CombatSession { CombatId = "test.combat" }), Is.True);
            Assert.That(DungeonAbandonmentSystem.TryAbandon(session, dungeon), Is.False);
            Assert.That(session.ActiveDungeon, Is.SameAs(dungeon));
            Assert.That(session.DungeonOffers.Generation, Is.EqualTo(generation));
            Assert.That(dungeon.IsCompleted, Is.False);
        }
    }
}
