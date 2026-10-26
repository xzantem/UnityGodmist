using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Serialization;
using Godmist.Data.Dungeons;
using Godmist.Data.Quests;
using Godmist.Data.World;
using Godmist.Editor;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Runtime.Quests;
using Godmist.Systems.Dungeons;
using Godmist.Systems.Quests;
using Godmist.Systems.SaveLoad;
using Godmist.Views.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Godmist.Tests.EditMode
{
    public sealed class DungeonLengthTests
    {
        const string Folder="../LocalHandoffs/branch-counts-20261023";
        static GameContentCatalog Catalog=>AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
        readonly List<UnityEngine.Object> temporary=new();
        readonly Report report=new();
        [OneTimeSetUp] public void ApplyProfiles()=>DungeonLengthSetup.ApplyAll();
        [TearDown] public void Cleanup(){foreach(var asset in temporary)UnityEngine.Object.DestroyImmediate(asset);temporary.Clear();}
        [OneTimeTearDown] public void Export(){Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"generated-pacing.json"),JsonUtility.ToJson(report,true));}
        [Serializable] class Row {public string dungeon;public DungeonLength length;public int seed,rooms,encounters,enemies,floors,kits;public float seconds;}
        [Serializable] class Report {public List<Row> rows=new();}

        [TestCase("dungeon.catacombs.expedition")]
        [TestCase("dungeon.catacombs.boss_expedition")]
        [TestCase("dungeon.catacombs.skeleton_slayer_expedition")]
        [TestCase("dungeon.forest.expedition")]
        [TestCase("dungeon.forest.boss_expedition")]
        [TestCase("dungeon.forest.bandit_leader_expedition")]
        public void EveryOfferDefinitionGeneratesAllLengthsAtThirtyToFortyPercentDensity(string id)
        {
            var definition=Catalog.DungeonDefinitions.Single(d=>d!=null && d.Id==id);
            Assert.That(definition.LengthProfiles.Count,Is.EqualTo(3));
            foreach(var length in new[]{DungeonLength.Short,DungeonLength.Normal,DungeonLength.Long})
            for(var seed=0;seed<2;seed++)
            {
                var timer=System.Diagnostics.Stopwatch.StartNew();
                var dungeon=new DungeonGeneratorService().Generate(definition,171016+seed,10,length);
                new DungeonEncounterPlacementSystem().Place(dungeon);
                var scale=(int)length;var forest=definition.Theme==DungeonTheme.Forest;
                Assert.That(dungeon.LengthProfile.MinBranchCount,Is.EqualTo(scale));
                Assert.That(dungeon.LengthProfile.MaxBranchCount,Is.EqualTo(scale+2));
                Assert.That(dungeon.Floors.Count,Is.EqualTo(forest?1:scale));
                Assert.That(dungeon.Floors.Sum(f=>f.Rooms.Count(r=>r.RoomType==DungeonRoomType.Optional)), Is.InRange(scale,scale+2));
                Assert.That(dungeon.CampingKitAllowance,Is.EqualTo(scale-1));
                Assert.That(dungeon.CompletionRewardMultiplier,Is.EqualTo(scale));
                foreach(var floor in dungeon.Floors)
                {
                    var budgetRooms = floor.Rooms.Count(r=>!r.IsBranchTransitRoom);
                    Assert.That(budgetRooms,Is.InRange(14*(forest?scale:1),17*(forest?scale:1)));
                    Assert.That(floor.Encounters.Count/(double)budgetRooms,Is.InRange(.3-1e-6,.4+1e-6));
                    Assert.That(floor.Encounters.Select(e=>e.RoomId).Distinct().Count(),Is.EqualTo(floor.Encounters.Count));
                    DungeonBranchTopologyTests.CheckTopology(dungeon, floor);
                }
                Assert.That(dungeon.Floors.Sum(f=>f.Encounters.Count),Is.InRange(
                    forest?(9*scale+1)/2:5*scale,forest?(11*scale+1)/2:6*scale));
                Assert.That(dungeon.Floors.SelectMany(f=>f.Encounters).Count(e=>e.Definition==definition.BossEncounterDefinition),
                    Is.EqualTo(definition.BossEncounterDefinition==null?0:1));
                timer.Stop();report.rows.Add(new Row{dungeon=id,length=length,seed=seed,rooms=dungeon.Floors.Sum(f=>f.Rooms.Count),
                    encounters=dungeon.Floors.Sum(f=>f.Encounters.Count),enemies=dungeon.Floors.SelectMany(f=>f.Encounters).Sum(e=>e.Enemies.Count),
                    floors=dungeon.Floors.Count,kits=dungeon.CampingKitAllowance,seconds=(float)timer.Elapsed.TotalSeconds});
            }
        }

        [TestCase(DungeonLength.Short)] [TestCase(DungeonLength.Normal)] [TestCase(DungeonLength.Long)]
        public void SuppliesMatchLengthForOneFloorForestAndCannotBeGrantedTwice(DungeonLength length)
        {
            var game=Party();var dungeon=new DungeonInstance("length-test",1,1,length);
            dungeon.TryAddFloor(new DungeonFloorInstance(0));game.TrySetActiveDungeon(dungeon);
            var service=new DungeonCampService();Assert.That(service.TryIssueSupplies(game,Catalog),Is.True);
            Assert.That(game.CharactersById.Values.Sum(c=>c.Inventory.Items.Count(DungeonCampService.IsCampingKit)),Is.EqualTo((int)length-1));
            Assert.That(service.TryIssueSupplies(game,Catalog),Is.False);
            Assert.That(dungeon.CampSuppliesIssued,Is.True,"A zero-kit short expedition must also mark its grant consumed.");
        }

        [TestCase("dungeon.catacombs.expedition")]
        [TestCase("dungeon.forest.expedition")]
        public void LongExpeditionSupportsActualRoomObjectsAndRepeatPlacement(string id)
        {
            var definition=Catalog.DungeonDefinitions.Single(d=>d!=null && d.Id==id);
            var dungeon=new DungeonGeneratorService().Generate(definition,171018,10,DungeonLength.Long);
            new DungeonEncounterPlacementSystem().Place(dungeon);
            var placer=new DungeonObjectPlacementSystem();placer.Place(dungeon,Catalog.EquipmentPartDefinitions);
            Assert.That(dungeon.HasPlacedContent,Is.True);
            var objects=dungeon.Floors.SelectMany(f=>f.Objects).ToArray();
            Assert.That(objects.Length,Is.GreaterThan(0));
            placer.Place(dungeon,Catalog.EquipmentPartDefinitions);
            Assert.That(dungeon.Floors.SelectMany(f=>f.Objects).ToArray(),Is.EqualTo(objects));
        }

        [Test]
        public void BoardLengthIsRandomPerOfferAndStableAcrossReopenSelectionAndJsonRestore()
        {
            var seen=new HashSet<DungeonLength>();
            for(var campaign=0;campaign<16;campaign++)
            {
                var game=new GameSession(campaignId:"length-campaign-"+campaign);
                Assert.That(DungeonOfferSystem.EnsureOffers(game,Catalog.DungeonDefinitions),Is.True);
                game.DungeonOffers.TrySelect(2);
                foreach(var offer in game.DungeonOffers.Offers)seen.Add(offer.Length);
                var before=JsonUtility.ToJson(DungeonOfferPersistence.Capture(game.DungeonOffers));
                DungeonOfferSystem.EnsureOffers(game,Catalog.DungeonDefinitions);
                var restored=new Godmist.Runtime.Dungeons.DungeonOfferBoard();
                Assert.That(DungeonOfferPersistence.Restore(JsonUtility.FromJson<DungeonOfferBoardSaveData>(before),restored),Is.True);
                Assert.That(JsonUtility.ToJson(DungeonOfferPersistence.Capture(restored)),Is.EqualTo(before));
                Assert.That(JsonUtility.ToJson(DungeonOfferPersistence.Capture(game.DungeonOffers)),Is.EqualTo(before));
            }
            Assert.That(seen,Is.EquivalentTo(new[]{DungeonLength.Short,DungeonLength.Normal,DungeonLength.Long}));
        }

        [Test]
        public void QueuedBossLengthSurvivesOtherExpeditionsAndLegacyUpgrade()
        {
            var game=new GameSession(campaignId:"length-boss");
            var board=game.DungeonOffers;
            board.QueueBoss(DungeonTheme.Catacombs,"dungeon.catacombs.boss_expedition",10);
            DungeonOfferSystem.EnsureOffers(game,Catalog.DungeonDefinitions);
            var boss=board.BossOpportunities.Single();
            Assert.That(boss.Length,Is.InRange(DungeonLength.Short,DungeonLength.Long));
            for(var i=0;i<3;i++)
            {
                board.CompleteExpedition();
                DungeonOfferSystem.EnsureOffers(game,Catalog.DungeonDefinitions);
                Assert.That(board.Offers.Single(o=>o.FixedLevel==10).Length,Is.EqualTo(boss.Length));
                var data=JsonUtility.FromJson<DungeonOfferBoardSaveData>(JsonUtility.ToJson(DungeonOfferPersistence.Capture(board)));
                Assert.That(DungeonOfferPersistence.Restore(data,board),Is.True);
                Assert.That(board.BossOpportunities.Single().Length,Is.EqualTo(boss.Length));
            }
            var before=JsonUtility.ToJson(DungeonOfferPersistence.Capture(board));
            var invalid=DungeonOfferPersistence.Capture(board);invalid.BossOpportunities[0].Length=(DungeonLength)99;
            Assert.That(DungeonOfferPersistence.Restore(invalid,board),Is.False);
            Assert.That(JsonUtility.ToJson(DungeonOfferPersistence.Capture(board)),Is.EqualTo(before));
            invalid=DungeonOfferPersistence.Capture(board);invalid.Offers[0].Length=(DungeonLength)(-1);
            Assert.That(DungeonOfferPersistence.Restore(invalid,board),Is.False);
            Assert.That(JsonUtility.ToJson(DungeonOfferPersistence.Capture(board)),Is.EqualTo(before));
        }

        [Test]
        public void LegacyActiveDungeonRetainsGeometryAndOldSupplies()
        {
            var game=Party();var definition=Catalog.DungeonDefinitions.Single(d=>d!=null && d.Id=="dungeon.catacombs.expedition");
            var dungeon=new DungeonGeneratorService().Generate(definition,121017,1);
            game.TrySetActiveDungeon(dungeon);
            var save=new GameSessionSaveSnapshotFactory().CreateSnapshot(game,"legacy-length");
            Assert.That(save.ActiveDungeon.Length,Is.EqualTo(DungeonLength.Unspecified));
            Assert.That(new GameSessionRestoreFactory().TryCreate(save,Catalog,out var restored),Is.True);
            Assert.That(restored.ActiveDungeon.Floors.Count,Is.EqualTo(dungeon.Floors.Count));
            Assert.That(restored.ActiveDungeon.Floors.Sum(f=>f.Rooms.Count),Is.EqualTo(dungeon.Floors.Sum(f=>f.Rooms.Count)));
            Assert.That(restored.ActiveDungeon.CampingKitAllowance,Is.EqualTo(dungeon.Floors.Count));
            save.ActiveDungeon.Length=(DungeonLength)99;
            LogAssert.Expect(LogType.Error,"Nie udało się odtworzyć aktywnego lochu 'dungeon.catacombs.expedition' (seed: 121017).");
            Assert.That(new GameSessionRestoreFactory().TryCreate(save,Catalog,out _),Is.False);
        }

        [Test]
        public void ActiveDungeonSnapshotPreservesLengthGeometryAndSupplyGrant()
        {
            var game=Party();var definition=Catalog.DungeonDefinitions.Single(d=>d!=null && d.Id=="dungeon.forest.expedition");
            var dungeon=new DungeonGeneratorService().Generate(definition,121016,1,DungeonLength.Normal);
            new DungeonEncounterPlacementSystem().Place(dungeon);game.TrySetActiveDungeon(dungeon);
            new DungeonCampService().TryIssueSupplies(game,Catalog);
            var save=new GameSessionSaveSnapshotFactory().CreateSnapshot(game,"length");
            Assert.That(save.ActiveDungeon.Length,Is.EqualTo(DungeonLength.Normal));
            Assert.That(new GameSessionRestoreFactory().TryCreate(JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(save)),Catalog,out var restored),Is.True);
            Assert.That(restored.ActiveDungeon.Length,Is.EqualTo(DungeonLength.Normal));
            Assert.That(restored.ActiveDungeon.CampSuppliesIssued,Is.True);
            Assert.That(restored.ActiveDungeon.Floors.Sum(f=>f.Rooms.Count),Is.EqualTo(dungeon.Floors.Sum(f=>f.Rooms.Count)));
            Assert.That(new DungeonCampService().TryIssueSupplies(restored,Catalog),Is.False);
        }

        [TestCase(DungeonLength.Short)] [TestCase(DungeonLength.Normal)] [TestCase(DungeonLength.Long)]
        public void CompletionQuestMultipliesRecordedRewardOnceAndSurvivesSave(DungeonLength length)
        {
            var game=Party();var definition=Quest(QuestObjectiveType.ExitDungeon);
            var catalog=UnityEngine.Object.Instantiate(Catalog);temporary.Add(catalog);catalog.QuestDefinitions.Add(definition);
            var service=new QuestService(game,nextContentCatalog:catalog,nextRandom:new System.Random(711));
            Assert.That(service.TryRegisterQuest(definition,QuestStatus.Available),Is.True);
            Assert.That(service.TryStartQuest(definition.Id),Is.True);
            var quest=game.QuestsById[definition.Id];var original=quest.RadiantRewardOffer;
            var dungeon=new DungeonInstance("length-quest-dungeon",1,1,length);dungeon.TryMarkCompleted();
            var progress=new QuestObjectiveProgressSystem(game,service);progress.ReportDungeonExited(dungeon);progress.ReportDungeonExited(dungeon);
            Assert.That(quest.Status,Is.EqualTo(QuestStatus.Completed));
            Assert.That(quest.EffectiveRadiantRewardOffer.Experience,Is.EqualTo(original.Experience*(int)length));
            var save=new GameSessionSaveSnapshotFactory().CreateSnapshot(game,"quest-length");
            Assert.That(new GameSessionRestoreFactory().TryCreate(JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(save)),catalog,out var restored),Is.True);
            var restoredService=new QuestService(restored,nextContentCatalog:catalog);
            Assert.That(restored.QuestsById[definition.Id].CompletionLengthMultiplier,Is.EqualTo((int)length));
            QuestRewardResult granted=null;restoredService.QuestRewardsGranted+=(q,r)=>granted=r;
            Assert.That(restoredService.TryTurnInRadiantQuest(definition.Id),Is.True);
            Assert.That(granted.ExperiencePerActiveCharacter,Is.EqualTo(original.Experience*(int)length));
            Assert.That(granted.Gold,Is.EqualTo(original.Gold*(int)length));
            Assert.That(restoredService.TryTurnInRadiantQuest(definition.Id),Is.False);
            Assert.That(restored.QuestsById[definition.Id].CompletionLengthMultiplier,Is.EqualTo(1));
        }

        [Test]
        public void IndependentKillQuestDoesNotUseExpeditionLengthMultiplier()
        {
            var quest=new QuestInstance(Quest(QuestObjectiveType.KillEnemy));
            quest.TrySetRadiantRewardOffer(new RadiantRewardOffer(1,1200,50,2,null));
            quest.TrySetQuestFlag(QuestInstance.CompletionLengthFlag,"3");
            Assert.That(quest.UsesLengthReward,Is.False);
            Assert.That(quest.EffectiveRadiantRewardOffer.Experience,Is.EqualTo(1200));
        }
        [Test] public void LabelsAreExplicit()=>Assert.That(new[]{DungeonLengthPresentation.Name(DungeonLength.Short),DungeonLengthPresentation.Name(DungeonLength.Normal),DungeonLengthPresentation.Name(DungeonLength.Long)},Is.EqualTo(new[]{"Krótka","Normalna","Długa"}));
        GameSession Party()
        {
            var game=new GameSession(GameModeState.Exploration);var hero=EnemyScalingBalanceTests.Hero(0,1,false,true);
            game.TryRegisterCharacter(hero);game.TryAddCharacterToActiveParty(hero.CharacterId);game.TrySetActiveLeader(hero.CharacterId);return game;
        }
        QuestDefinition Quest(QuestObjectiveType type)
        {
            var definition=ScriptableObject.CreateInstance<QuestDefinition>();temporary.Add(definition);
            Set(definition,"Id","quest.length.test");Set(definition,"QuestType",QuestType.Radiant);Set(definition,"StartStageId","exit");
            var objective=new QuestObjectiveDefinition();Set(objective,"ObjectiveId","objective");Set(objective,"ObjectiveType",type);Set(objective,"TargetId","length-quest-dungeon");Set(objective,"RequiredAmount",1);
            var stage=new QuestStageDefinition();Set(stage,"StageId","exit");Set(stage,"Objectives",new List<QuestObjectiveDefinition>{objective});definition.Stages.Add(stage);return definition;
        }
        static void Set(object target,string name,object value)=>target.GetType().GetField("<"+name+">k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
    }
}
