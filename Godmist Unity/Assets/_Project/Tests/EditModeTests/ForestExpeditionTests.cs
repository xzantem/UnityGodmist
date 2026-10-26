using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Data.World;
using Godmist.Editor;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Systems.Dungeons;
using Godmist.Systems.Characters;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEditor;

namespace Godmist.Tests.EditMode
{
    public sealed class ForestExpeditionTests
    {
        static DungeonDefinition Load(string path) => AssetDatabase.LoadAssetAtPath<DungeonDefinition>(path);
        static GameContentCatalog Catalog => AssetDatabase.LoadAssetAtPath<GameContentCatalog>(ForestExpeditionsSetup.CatalogPath);
        static DungeonDefinition Regular => Load(ForestExpeditionsSetup.RegularPath);
        static DungeonDefinition Boss => Load(ForestExpeditionsSetup.BossPath);
        static string[] BossIds => new[]{Boss.Id,Load(VS16CatacombsBossSetup.BanditDungeonPath).Id};
        static GameSession Session(int level = 8)
        {
            var session = new GameSession(campaignId: "forest.production");
            session.TryRegisterCharacter(new CharacterInstance { CharacterId = "hero", Level = level });
            session.PlayerParty.TryAddActiveMember("hero");
            return session;
        }

        [Test]
        public void ClearingsStayFreeOfMatureTreeObjectsAcrossExpeditionLengths()
        {
            // Targeted update of the already-authored room catalogue. Full
            // Configure would rebuild unrelated encounters and prop anchors.
            ForestPrototypeSetup.RefreshTreeFootprints();
            var definition = AssetDatabase.LoadAssetAtPath<DungeonDefinition>(
                ForestExpeditionsSetup.RegularPath);
            Assert.That(definition.RoomTemplateCatalog.Templates.SelectMany(t => t.SemanticAnchors)
                .Any(a => a.ObjectDefinition?.Id == "object.forest.tree.prototype"), Is.False);
            foreach (var length in new[] { DungeonLength.Short, DungeonLength.Normal, DungeonLength.Long })
                foreach (int seed in new[] { 42, 127, 171026 })
                {
                    var dungeon = new DungeonGeneratorService().Generate(definition, seed, 10, length);
                    new DungeonObjectPlacementSystem().Place(dungeon);
                    Assert.That(dungeon.Floors.SelectMany(f => f.Objects)
                        .Any(o => o.Definition.Id == "object.forest.tree.prototype"), Is.False,
                        $"No tree instance means no leftover physical or logical tree blocker: {length}, seed {seed}.");
                    Assert.That(dungeon.Floors.SelectMany(f => f.Objects).OfType<ObstacleDungeonObjectInstance>(), Is.Not.Empty,
                        "Retain the other authored forest props.");
                }
        }

        [Test]
        public void RevisedForestSquadsReduceRangedPressureAndKeepLegalSpawns()
        {
            foreach(var roster in new[]
            {
                (Key:"elf_archers",Members:new[]{"elf_poacher","elf_druid","elf_archer"}),
                (Key:"bandit_patrol",Members:new[]{"bandit_thug","bandit_thug","bandit_shooter"})
            })
            {
                var id=roster.Key=="bandit_patrol" ? Godmist.Editor.DungeonPolishSetup.PatrolId
                    : "encounter.forest."+roster.Key+EncounterBalanceSetup.VersionSuffix;
                var encounter=Regular.EncounterDefinitions.Single(e=>e.Id==id);
                var expected=roster.Members.Select(m=>"enemy.forest."+m).ToArray();
                Assert.That(encounter.Enemies.Select(e=>e.CharacterDefinition.Id),Is.EqualTo(expected));
                Assert.That(encounter.Enemies.All(e=>e.ClassData==e.CharacterDefinition.ClassData),Is.True,
                    "The replacement Druid must use its own mana class, not the Archer class.");
                foreach(var pool in Catalog.DungeonDefinitions.Where(d=>d!=null&&d.Theme==DungeonTheme.Forest&&d.IsAvailableAsOffer))
                    Assert.That(pool.EncounterDefinitions.Single(e=>e.Id==id),Is.SameAs(encounter));
                var clone=UnityEngine.Object.Instantiate(encounter);
                var dungeonDefinition=UnityEngine.Object.Instantiate(Regular);
                try
                {
                    EncounterBalanceSetup.ApplyRoster(clone);
                    Assert.That(clone.Enemies.Select(e=>e.CharacterDefinition.Id),Is.EqualTo(expected),
                        "Reauthoring must not restore the old ranged-heavy squad.");
                    Assert.That(clone.Enemies.Select(e=>e.ClassData),Is.EqualTo(encounter.Enemies.Select(e=>e.ClassData)));
                    dungeonDefinition.EncounterDefinitions.Clear();dungeonDefinition.EncounterDefinitions.Add(encounter);
                    var dungeon=new DungeonGeneratorService().Generate(dungeonDefinition,42,10,DungeonLength.Short);
                    new DungeonEncounterPlacementSystem().Place(dungeon);
                    foreach(var floor in dungeon.Floors)
                    foreach(var group in floor.Encounters)
                    {
                        Assert.That(group.Enemies.Select(e=>e.CharacterDefinition.Id),Is.EqualTo(expected));
                        var positions=new DungeonEnemyAnchorResolver().Resolve(floor,dungeonDefinition.RoomTemplateCatalog,group,false);
                        // Progression terminals may intentionally use formation
                        // placement instead of authored enemy anchors.
                        var occupied=positions.Values.ToHashSet();
                        for(var i=0;i<group.Enemies.Count;i++)
                        {
                            var enemy=group.Enemies[i];
                            if(positions.ContainsKey(enemy.InstanceId))continue;
                            var cell=(UnityEngine.Vector2Int)typeof(Godmist.Views.Dungeons.DungeonEncounterSpawner)
                                .GetMethod("GetFormationCell",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)
                                .Invoke(null,new object[]{floor.Map,group.RoomId,group.TriggerArea,i,group.Enemies.Count,group.ExplorationPosition,occupied});
                            positions.Add(enemy.InstanceId,cell);occupied.Add(cell);
                        }
                        Assert.That(positions.Count,Is.EqualTo(expected.Length));
                        Assert.That(positions.Values.Distinct().Count(),Is.EqualTo(expected.Length));
                        Assert.That(positions.Values.All(floor.Map.IsWalkable),Is.True);
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(clone);UnityEngine.Object.DestroyImmediate(dungeonDefinition);}
            }
        }

        [Test]
        public void ShippedDefinitionsSeparateOrdinaryExpeditionsFromEntAndKeepPrototype()
        {
            var prototype = Load(ForestPrototypeSetup.DefinitionPath);
            Assert.That(Regular, Is.Not.Null);
            Assert.That(Boss, Is.Not.Null);
            Assert.That(Regular.Id, Is.EqualTo("dungeon.forest.expedition"));
            Assert.That(Boss.Id, Is.EqualTo("dungeon.forest.boss_expedition"));
            Assert.That(prototype.IsAvailableAsOffer, Is.False);
            Assert.That(Regular.BossEncounterDefinition, Is.Null);
            Assert.That(Regular.FixedOfferLevels, Is.Empty);
            Assert.That(Boss.BossEncounterDefinition, Is.SameAs(prototype.BossEncounterDefinition));
            Assert.That(Boss.FixedOfferLevels, Is.EqualTo(new[] { 10, 20 }));
            foreach (var definition in new[] { Regular, Boss })
            {
                Assert.That(definition.IsAvailableAsOffer, Is.True);
                Assert.That(definition.Theme, Is.EqualTo(DungeonTheme.Forest));
                Assert.That(definition.IsIndoor, Is.False);
                Assert.That(definition.VegetationPresentation, Is.SameAs(prototype.VegetationPresentation));
                Assert.That(definition.RoomTemplateCatalog, Is.SameAs(prototype.RoomTemplateCatalog));
                Assert.That(definition.LootTableCatalog, Is.SameAs(prototype.LootTableCatalog));
                Assert.That(Catalog.DungeonDefinitions.Count(d => d == definition), Is.EqualTo(1));
            }
        }

        [TestCase(false, 10, 42)] [TestCase(false, 20, 219)]
        [TestCase(true, 10, 42)] [TestCase(true, 20, 219)]
        public void GeneratedExpeditionsHaveForestContentAndEntOnlyInBossVariant(bool boss, int level, int seed)
        {
            var definition = boss ? Boss : Regular;
            var dungeon = new DungeonGeneratorService().Generate(definition, seed, level);
            new DungeonObjectPlacementSystem().Place(dungeon);
            new DungeonEncounterPlacementSystem().Place(dungeon);
            var encounters = dungeon.Floors.SelectMany(f => f.Encounters).ToArray();
            Assert.That(encounters.Count(e => e.Definition == Boss.BossEncounterDefinition), Is.EqualTo(boss ? 1 : 0));
            Assert.That(encounters.Any(e => definition.EncounterDefinitions.Contains(e.Definition)), Is.True);
            Assert.That(dungeon.Floors.Sum(f => f.Objects.Count), Is.GreaterThan(0));
        }

        [Test]
        public void OrdinaryBoardIncludesForestButNeverEntBeforeDangerUnlock()
        {
            var session = Session();
            Assert.That(DungeonOfferSystem.EnsureOffers(session, Catalog.DungeonDefinitions), Is.True);
            Assert.That(session.DungeonOffers.Offers.Select(o => o.DefinitionId), Has.Member(Regular.Id));
            Assert.That(session.DungeonOffers.Offers.Select(o => o.DefinitionId), Has.No.Member(Boss.Id));
        }

        [TestCase(8, 10)] [TestCase(20, 20)]
        public void MaximumForestDangerQueuesOneForestBossAtSupportedLevelAndSurvivesSave(int characterLevel, int bossLevel)
        {
            var profile = AssetDatabase.LoadAssetAtPath<NewCampaignDefinition>(
                "Assets/_Project/ScriptableObjects/StartingCampaign/NC_Arungard.asset");
            Assert.That(new NewCampaignService().TryCreate(profile, out var session, out var error), Is.True, error);
            foreach (var hero in session.CharactersById.Values)
                Assert.That(new CharacterProgressionSystem().TrySetLevel(hero, characterLevel, out _), Is.True);
            session.DungeonOffers.MarkMilestoneProcessed(20);
            session.DungeonOffers.SetDanger(DungeonTheme.Forest, 100);
            DungeonOfferSystem.EnsureOffers(session, Catalog.DungeonDefinitions);
            var offer = session.DungeonOffers.Offers.Single(o => BossIds.Contains(o.DefinitionId));
            Assert.That(offer.FixedLevel, Is.EqualTo(bossLevel));
            var snapshot = new GameSessionSaveSnapshotFactory().CreateSnapshot(session, "forest.offers");
            var json = new GameSaveJsonSerializer();
            Assert.That(json.TryDeserialize(json.Serialize(snapshot), out var decoded), Is.True);
            Assert.That(new GameSessionRestoreFactory().TryCreate(decoded, Catalog, out var restored), Is.True);
            DungeonOfferSystem.EnsureOffers(restored, Catalog.DungeonDefinitions);
            Assert.That(restored.DungeonOffers.GetDanger(DungeonTheme.Forest), Is.EqualTo(100));
            Assert.That(restored.DungeonOffers.Offers.Single(o => BossIds.Contains(o.DefinitionId)).DefinitionId,Is.EqualTo(offer.DefinitionId));
            Assert.That(restored.DungeonOffers.Offers.Single(o => BossIds.Contains(o.DefinitionId)).Seed, Is.EqualTo(offer.Seed));
            Assert.That(restored.DungeonOffers.BossOpportunities.Single(b => b.DefinitionId == offer.DefinitionId).Level, Is.EqualTo(bossLevel));
        }

        [Test]
        public void OrdinaryCompletionAdjustsDangerAndSelectedBossCompletionResetsItButCanBeUnlockedAgain()
        {
            var session = Session();
            session.DungeonOffers.SetDanger(DungeonTheme.Forest, 70);
            DungeonOfferSystem.CompleteExpedition(session, new DungeonInstance(Regular, 42, 8), Catalog.DungeonDefinitions);
            Assert.That(session.DungeonOffers.GetDanger(DungeonTheme.Forest), Is.EqualTo(40));
            Assert.That(session.DungeonOffers.GetDanger(DungeonTheme.Catacombs), Is.InRange(10, 20));
            session.DungeonOffers.SetDanger(DungeonTheme.Forest, 100);
            DungeonOfferSystem.EnsureOffers(session, Catalog.DungeonDefinitions);
            var selectedId=session.DungeonOffers.Offers.Single(o=>BossIds.Contains(o.DefinitionId)).DefinitionId;
            var selected=Catalog.DungeonDefinitions.Single(d=>d!=null && d.Id==selectedId);
            DungeonOfferSystem.CompleteExpedition(session, new DungeonInstance(selected, 43, 10), Catalog.DungeonDefinitions);
            Assert.That(session.DungeonOffers.GetDanger(DungeonTheme.Forest), Is.Zero);
            Assert.That(session.DungeonOffers.BossOpportunities.Any(b => BossIds.Contains(b.DefinitionId)), Is.False);
            session.DungeonOffers.SetDanger(DungeonTheme.Forest, 100);
            DungeonOfferSystem.EnsureOffers(session, Catalog.DungeonDefinitions);
            Assert.That(session.DungeonOffers.Offers.Count(o => BossIds.Contains(o.DefinitionId)), Is.EqualTo(1));
        }

        [Test]
        public void ReauthoringKeepsProductionVariantsRegisteredOnceAndOrdinaryBossFree()
        {
            ForestExpeditionsSetup.Configure();
            ForestExpeditionsSetup.RefreshIfAuthored();
            ShippedDefinitionsSeparateOrdinaryExpeditionsFromEntAndKeepPrototype();
        }
    }
}
