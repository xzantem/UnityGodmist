using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Serialization;
using Godmist.Data.Characters;
using Godmist.Data.Quests;
using Godmist.Data.World;
using Godmist.Runtime.Game;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Quests;
using Godmist.Systems.Characters;
using Godmist.Systems.Dungeons;
using Godmist.Systems.Quests;
using Godmist.Systems.SaveLoad;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class MissionBoardGenerationTests
    {
        const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        const string Output="../LocalHandoffs/mission-board-pass-20261020";
        [Test]
        public void KillJobsNeverRepeatDestinationEvenWithDifferentQuotas()
        {
            for (var seed = 0; seed < 100; seed++)
            {
                var session = Session(seed: seed); var service = Service(session, seed);
                Assert.That(service.EnsureMissionBoard(), Is.True);
                var jobs = Jobs(session).Where(q => q.BoardContract?.ObjectiveType == QuestObjectiveType.KillEnemy).ToArray();
                Assert.That(jobs.Select(q => q.BoardContract.Theme).Distinct().Count(), Is.EqualTo(jobs.Length), "seed " + seed);
            }
            var old = Session(); var fix = Service(old); fix.EnsureMissionBoard();
            var accepted = Configure(old, QuestObjectiveType.KillEnemy, DungeonTheme.Forest, DungeonLength.Unspecified, tier: 1);
            Assert.That(fix.TryStartQuest(accepted.QuestId), Is.True);
            Configure(old, QuestObjectiveType.KillEnemy, DungeonTheme.Forest, DungeonLength.Unspecified, tier: 3, required: 24, slot: 1);
            Assert.That(fix.EnsureMissionBoard(), Is.True);
            Assert.That(accepted.Status, Is.EqualTo(QuestStatus.Active));
            Assert.That(Jobs(old).Count(q => q.BoardContract?.ObjectiveType == QuestObjectiveType.KillEnemy
                && q.BoardContract.Theme == DungeonTheme.Forest), Is.EqualTo(1));
        }
        GameContentCatalog Catalog=>AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
        static void Set(object target,string name,object value)=>target.GetType().GetField(name,Flags).SetValue(target,value);
        static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,Flags).GetValue(target);
        GameSession Session(int level=1,int seed=1)
        {
            var session=new GameSession(GameModeState.Exploration,campaignId:"missions."+seed);
            var hero=new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset"));
            hero.Level=level;session.TryRegisterCharacter(hero);session.TryAddCharacterToActiveParty(hero.CharacterId);session.TrySetActiveLeader(hero.CharacterId);
            return session;
        }
        QuestService Service(GameSession session,int seed=1)=>new(session,nextContentCatalog:Catalog,nextRandom:new System.Random(seed));
        QuestInstance[] Jobs(GameSession session)=>CatacombsMissionBoard.QuestIds.Select(id=>session.QuestsById[id]).ToArray();
        void CheckUnique(GameSession session)
        {
            var keys=session.DungeonOffers.Offers.Select(o=>
            {
                Assert.That(Catalog.TryGetDungeon(o.DefinitionId,out var d),Is.True);
                return (d.Theme,DungeonOfferSystem.OfferLevel(DungeonOfferSystem.PartyLevel(session),o),o.Length);
            }).ToArray();
            Assert.That(keys.Length,Is.EqualTo(6));Assert.That(keys.Distinct().Count(),Is.EqualTo(6));
        }
        QuestInstance Configure(GameSession session,QuestObjectiveType type,DungeonTheme theme,DungeonLength length,int level=1,int tier=1,int required=8,int slot=0)
        {
            var quest=Jobs(session)[slot];
            var contract=new RadiantBoardContract(quest.Definition.StartStageId,type,theme,length,level,type==QuestObjectiveType.ExitDungeon?1:required,
                type==QuestObjectiveType.ExitDungeon?(int)length:tier,session.DungeonOffers.CompletedExpeditions);
            Assert.That(quest.TrySetBoardContract(contract),Is.True);
            Assert.That(quest.TrySetRadiantRewardOffer(new RadiantRewardOffer(level,1200*contract.RewardMultiplier,40*contract.RewardMultiplier,2*contract.RewardMultiplier,null)),Is.True);
            return quest;
        }
        void Complete(GameSession session)
        {
            Catalog.TryGetDungeon("dungeon.forest.expedition",out var d);
            DungeonOfferSystem.CompleteExpedition(session,new DungeonInstance(d,2,1,DungeonLength.Short),Catalog.DungeonDefinitions);
        }

        [TestCase(1)] [TestCase(2)] [TestCase(10)] [TestCase(48)] [TestCase(49)] [TestCase(50)]
        public void ExpeditionOffersNeverDuplicateThemeActualLevelAndLength(int level)
        {
            for(int seed=0;seed<32;seed++)
            {
                var session=Session(level,seed);
                Assert.That(DungeonOfferSystem.EnsureOffers(session,Catalog.DungeonDefinitions),Is.True);CheckUnique(session);
                var first=session.DungeonOffers.Offers.ToArray();
                Assert.That(DungeonOfferSystem.EnsureOffers(session,Catalog.DungeonDefinitions),Is.True);
                Assert.That(session.DungeonOffers.Offers,Is.EqualTo(first));
                session.CharactersById.Values.First().Level=50;
                Assert.That(DungeonOfferSystem.EnsureOffers(session,Catalog.DungeonDefinitions),Is.True);CheckUnique(session);
            }
        }
        [Test]
        public void DuplicateLegacyBoardRepairsWithoutDiscardingQueuedBossSelectionOrSeeds()
        {
            var session=Session(10);DungeonOfferSystem.EnsureOffers(session,Catalog.DungeonDefinitions);
            var offers=session.DungeonOffers.Offers.ToArray();var seeds=offers.Select(o=>o.Seed).ToArray();
            var boss=offers[0];Assert.That(boss.FixedLevel,Is.GreaterThan(0));
            Catalog.TryGetDungeon(boss.DefinitionId,out var bossDefinition);
            var regular=Catalog.DungeonDefinitions.First(d=>d!=null && d.Theme==bossDefinition.Theme && d.IsAvailableAsOffer && d.FixedOfferLevels.Count==0);
            offers[1]=new DungeonExpeditionOffer(regular.Id,offers[1].Seed,0,length:boss.Length);
            session.DungeonOffers.TrySelect(1);
            Assert.That(session.DungeonOffers.TryRestore(session.DungeonOffers.Generation,1,offers),Is.True);
            var queued=session.DungeonOffers.BossOpportunities.ToArray();
            Assert.That(DungeonOfferSystem.EnsureOffers(session,Catalog.DungeonDefinitions),Is.True);CheckUnique(session);
            Assert.That(session.DungeonOffers.Offers[0],Is.SameAs(boss));
            Assert.That(session.DungeonOffers.SelectedIndex,Is.EqualTo(1));
            Assert.That(session.DungeonOffers.Offers.Select(o=>o.Seed),Is.EqualTo(seeds));
            Assert.That(session.DungeonOffers.BossOpportunities,Is.EqualTo(queued));
        }
        [Test]
        public void ClearContractTitlesOmitSizeLabelsButRetainExactTargetsAndRewardTiers()
        {
            foreach(var theme in new[]{DungeonTheme.Forest,DungeonTheme.Catacombs})
            for(int tier=1;tier<=3;tier++)
            {
                var count=tier==1?8:tier==2?16:36;
                var contract=new RadiantBoardContract("stage.contract",QuestObjectiveType.KillEnemy,theme,
                    DungeonLength.Unspecified,1,count,tier,0);
                Assert.That(contract.DisplayName,Is.EqualTo(theme==DungeonTheme.Forest?"Oczyść: Las":"Oczyść: Katakumby"));
                Assert.That(contract.Description,Does.Contain(count.ToString()));
                Assert.That(contract.RewardMultiplier,Is.EqualTo(tier));
            }
        }
        [TestCase(1)] [TestCase(5)] [TestCase(10)] [TestCase(20)] [TestCase(48)] [TestCase(50)]
        public void NewBoardsIncludeBothThemesAndNeverRequireLevelsAboveParty(int level)
        {
            bool higherExpeditionStillOffered=false;
            for(int seed=0;seed<64;seed++)
            {
                var session=Session(level,seed);var service=Service(session,seed);
                Assert.That(service.EnsureMissionBoard(),Is.True);
                var jobs=Jobs(session);
                Assert.That(jobs.Select(q=>q.BoardContract.Theme).Distinct().Count(),Is.EqualTo(2),"seed "+seed);
                Assert.That(jobs.All(q=>q.BoardContract.Level<=level),Is.True,"seed "+seed);
                foreach(var contract in jobs.Select(q=>q.BoardContract).Where(c=>c.ObjectiveType==QuestObjectiveType.ExitDungeon))
                    Assert.That(session.DungeonOffers.Offers.Any(o=>Catalog.TryGetDungeon(o.DefinitionId,out var dungeon)
                        && dungeon.Theme==contract.Theme && o.Length==contract.Length
                        && DungeonOfferSystem.OfferLevel(level,o)==contract.Level),Is.True);
                higherExpeditionStillOffered|=session.DungeonOffers.Offers.Any(o=>DungeonOfferSystem.OfferLevel(level,o)>level);
            }
            if(level<50) Assert.That(higherExpeditionStillOffered,Is.True,"Harder expeditions remain optional; only mission targets are capped.");
        }
        [Test]
        public void ExistingSingleThemeBoardRepairsOnceWithoutRerollingOtherSlots()
        {
            var session=Session();var service=Service(session);service.EnsureMissionBoard();
            Configure(session,QuestObjectiveType.KillEnemy,DungeonTheme.Catacombs,DungeonLength.Unspecified,slot:0);
            Configure(session,QuestObjectiveType.ExitDungeon,DungeonTheme.Catacombs,DungeonLength.Short,slot:1);
            Configure(session,QuestObjectiveType.ExitDungeon,DungeonTheme.Catacombs,DungeonLength.Normal,slot:2);
            var before=Jobs(session).Select(q=>q.BoardContract).ToArray();
            Assert.That(service.EnsureMissionBoard(),Is.True);
            var after=Jobs(session).Select(q=>q.BoardContract).ToArray();
            Assert.That(after.Select(c=>c.Theme).Distinct().Count(),Is.EqualTo(2));
            Assert.That(after.Where((c,i)=>ReferenceEquals(c,before[i])).Count(),Is.EqualTo(2));
            var rewards=Jobs(session).Select(q=>q.RadiantRewardOffer).ToArray();
            Assert.That(service.EnsureMissionBoard(),Is.True);
            Assert.That(Jobs(session).Select(q=>q.BoardContract),Is.EqualTo(after));
            Assert.That(Jobs(session).Select(q=>q.RadiantRewardOffer),Is.EqualTo(rewards));
        }
        [Test]
        public void ClaimingOnlyForestJobDoesNotRerollRemainingCatacombsJobs()
        {
            var session=Session();var service=Service(session);service.EnsureMissionBoard();
            Configure(session,QuestObjectiveType.KillEnemy,DungeonTheme.Catacombs,DungeonLength.Unspecified,slot:0);
            Configure(session,QuestObjectiveType.ExitDungeon,DungeonTheme.Catacombs,DungeonLength.Short,slot:1);
            var forest=Configure(session,QuestObjectiveType.KillEnemy,DungeonTheme.Forest,DungeonLength.Unspecified,slot:2);
            var contracts=Jobs(session).Select(q=>q.BoardContract).ToArray();
            forest.TrySetStatus(QuestStatus.Completed);Assert.That(service.TryTurnInRadiantQuest(forest.QuestId),Is.True);
            Assert.That(service.EnsureMissionBoard(),Is.True);
            Assert.That(Jobs(session).Select(q=>q.BoardContract),Is.EqualTo(contracts));
            Assert.That(forest.Status,Is.EqualTo(QuestStatus.TurnedIn));
        }
        [Test]
        public void ExistingTooHighUnacceptedContractIsReplacedWhileAcceptedAndPendingRewardsStay()
        {
            var session=Session();var service=Service(session);service.EnsureMissionBoard();
            for(int slot=0;slot<3;slot++)
                Configure(session,QuestObjectiveType.ExitDungeon,DungeonTheme.Catacombs,DungeonLength.Short,level:4,slot:slot);
            var jobs=Jobs(session);
            Assert.That(service.TryStartQuest(jobs[0].QuestId),Is.True);
            jobs[1].TrySetStatus(QuestStatus.Completed);
            var accepted=jobs[0].BoardContract;var pending=jobs[1].BoardContract;var pendingReward=jobs[1].RadiantRewardOffer;
            Assert.That(service.EnsureMissionBoard(),Is.True);
            Assert.That(jobs[0].BoardContract,Is.SameAs(accepted));Assert.That(jobs[0].Status,Is.EqualTo(QuestStatus.Active));
            Assert.That(jobs[1].BoardContract,Is.SameAs(pending));Assert.That(jobs[1].RadiantRewardOffer,Is.SameAs(pendingReward));
            Assert.That(jobs[2].BoardContract.Level,Is.EqualTo(1));Assert.That(jobs[2].BoardContract.Theme,Is.EqualTo(DungeonTheme.Forest));
        }
        [Test]
        public void ThreeStableRandomJobsUseCurrentExpeditionsAndAllKillTiersWithUpfrontRewards()
        {
            var kinds=new HashSet<QuestObjectiveType>();var tiers=new HashSet<int>();var themes=new HashSet<DungeonTheme>();
            for(int seed=0;seed<48;seed++)
            {
                var session=Session(1,seed);var service=Service(session,seed);Assert.That(service.EnsureMissionBoard(),Is.True);
                var jobs=Jobs(session);Assert.That(jobs.Length,Is.EqualTo(3));
                var contracts=jobs.Select(q=>q.BoardContract).ToArray();var rewards=jobs.Select(q=>q.RadiantRewardOffer).ToArray();
                Assert.That(service.EnsureMissionBoard(),Is.True);
                Assert.That(jobs.Select(q=>q.BoardContract),Is.EqualTo(contracts));Assert.That(jobs.Select(q=>q.RadiantRewardOffer),Is.EqualTo(rewards));
                foreach(var quest in jobs)
                {
                    Assert.That(CatacombsMissionBoard.CanOffer(session,quest.QuestId),Is.True);
                    var c=quest.BoardContract;kinds.Add(c.ObjectiveType);themes.Add(c.Theme);
                    Assert.That(quest.RadiantRewardOffer.Honor,Is.EqualTo(2*c.RewardMultiplier));
                    Assert.That(quest.EffectiveRadiantRewardOffer,Is.SameAs(quest.RadiantRewardOffer));
                    if(c.ObjectiveType==QuestObjectiveType.KillEnemy)
                    {
                        Assert.That(c.Level,Is.EqualTo(DungeonOfferSystem.PartyLevel(session)),"Clear contracts retain the existing party-level baseline.");
                        tiers.Add(c.RewardMultiplier);
                        Assert.That(c.RequiredAmount,Is.InRange(c.RewardMultiplier==1?8:c.RewardMultiplier==2?16:24,c.RewardMultiplier==1?12:c.RewardMultiplier==2?24:36));
                    }
                    else Assert.That(session.DungeonOffers.Offers.Any(o=>
                    {
                        Catalog.TryGetDungeon(o.DefinitionId,out var d);
                        return d.Theme==c.Theme && o.Length==c.Length && DungeonOfferSystem.OfferLevel(1,o)==c.Level;
                    }),Is.True,"Completion contract must refer to an actual current offer.");
                }
            }
            Assert.That(kinds.Count,Is.EqualTo(2));Assert.That(tiers.Count,Is.EqualTo(3));Assert.That(themes.Count,Is.EqualTo(2));
        }
        [Test]
        public void OnlyUnacceptedAndPreviouslyClaimedSlotsRerollAfterCompletedDungeon()
        {
            var session=Session();var service=Service(session);service.EnsureMissionBoard();var jobs=Jobs(session);
            Configure(session,QuestObjectiveType.KillEnemy,DungeonTheme.Forest,DungeonLength.Unspecified,tier:3,required:36);
            Assert.That(service.TryStartQuest(jobs[0].QuestId),Is.True);
            service.TryAddObjectiveProgress(jobs[0].QuestId,RadiantBoardContract.ObjectiveId,1);
            jobs[1].TrySetStatus(QuestStatus.Completed);
            var active=jobs[0].BoardContract;var completed=jobs[1].BoardContract;var pending=jobs[2].BoardContract;
            var completedReward=jobs[1].RadiantRewardOffer;
            Complete(session);Assert.That(service.EnsureMissionBoard(),Is.True);
            Assert.That(jobs[0].BoardContract,Is.SameAs(active));Assert.That(jobs[1].BoardContract,Is.SameAs(completed));
            jobs[0].TryGetObjectiveProgress(RadiantBoardContract.ObjectiveId,out var retainedProgress);Assert.That(retainedProgress.CurrentAmount,Is.EqualTo(1));
            Assert.That(jobs[1].RadiantRewardOffer,Is.SameAs(completedReward));Assert.That(jobs[2].BoardContract,Is.Not.SameAs(pending));
            Assert.That(service.TryTurnInRadiantQuest(jobs[1].QuestId),Is.True);Assert.That(jobs[1].Status,Is.EqualTo(QuestStatus.TurnedIn));
            Assert.That(service.EnsureMissionBoard(),Is.True);Assert.That(jobs[1].BoardContract,Is.SameAs(completed));
            Assert.That(CatacombsMissionBoard.CanOffer(session,jobs[1].QuestId),Is.False);
            Complete(session);service.EnsureMissionBoard();
            Assert.That(jobs[0].BoardContract,Is.SameAs(active));Assert.That(jobs[1].Status,Is.EqualTo(QuestStatus.Available));
            Assert.That(jobs[1].BoardContract,Is.Not.SameAs(completed));
        }
        [Test]
        public void AbandoningOrReopeningExpeditionsDoesNotResetJobs()
        {
            var session=Session();var service=Service(session);service.EnsureMissionBoard();var contracts=Jobs(session).Select(q=>q.BoardContract).ToArray();
            session.DungeonOffers.CompleteExpedition();service.EnsureMissionBoard();
            Assert.That(session.DungeonOffers.CompletedExpeditions,Is.Zero);Assert.That(Jobs(session).Select(q=>q.BoardContract),Is.EqualTo(contracts));
        }
        [Test]
        public void ForestKillContractCountsCorrectThemeAndLevelAndIgnoresReinforcements()
        {
            var session=Session();var service=Service(session);service.EnsureMissionBoard();
            var quest=Configure(session,QuestObjectiveType.KillEnemy,DungeonTheme.Forest,DungeonLength.Unspecified,level:2);
            service.TryStartQuest(quest.QuestId);var progress=new QuestObjectiveProgressSystem(session,service);
            progress.ReportEnemyDefeated("dungeon.catacombs.expedition",2,"enemy.catacombs.skeleton",false);
            progress.ReportEnemyDefeated("dungeon.forest.expedition",1,"enemy.forest.wolf",false);
            progress.ReportEnemyDefeated("dungeon.forest.expedition",2,"enemy.forest.wolf",true);
            quest.TryGetObjectiveProgress(RadiantBoardContract.ObjectiveId,out var amount);Assert.That(amount.CurrentAmount,Is.Zero);
            for(int i=0;i<8;i++)progress.ReportEnemyDefeated("dungeon.forest.expedition",2,"enemy.forest.wolf",false);
            Assert.That(quest.Status,Is.EqualTo(QuestStatus.Completed));Assert.That(amount.CurrentAmount,Is.EqualTo(8));
        }
        [TestCase(DungeonLength.Short)] [TestCase(DungeonLength.Normal)] [TestCase(DungeonLength.Long)]
        public void CompletionRequiresSelectedThemeAndLengthAndDoesNotScaleRewardAgain(DungeonLength length)
        {
            var session=Session();var service=Service(session);service.EnsureMissionBoard();
            var quest=Configure(session,QuestObjectiveType.ExitDungeon,DungeonTheme.Forest,length,level:2);
            var reward=quest.RadiantRewardOffer;service.TryStartQuest(quest.QuestId);var progress=new QuestObjectiveProgressSystem(session,service);
            Catalog.TryGetDungeon("dungeon.forest.expedition",out var forest);Catalog.TryGetDungeon("dungeon.catacombs.expedition",out var cat);
            progress.ReportDungeonExited(new DungeonInstance(cat,1,2,length));
            progress.ReportDungeonExited(new DungeonInstance(forest,1,1,length));
            progress.ReportDungeonExited(new DungeonInstance(forest,1,2,length==DungeonLength.Short?DungeonLength.Long:DungeonLength.Short));
            Assert.That(quest.Status,Is.EqualTo(QuestStatus.Active));
            progress.ReportDungeonExited(new DungeonInstance(forest,1,2,length));
            Assert.That(quest.Status,Is.EqualTo(QuestStatus.Completed));Assert.That(quest.EffectiveRadiantRewardOffer,Is.SameAs(reward));
            var gold=session.Gold;Assert.That(service.TryTurnInRadiantQuest(quest.QuestId),Is.True);
            Assert.That(session.Gold-gold,Is.EqualTo(reward.Gold));Assert.That(service.TryTurnInRadiantQuest(quest.QuestId),Is.False);
        }
        [Test]
        public void SaveRestoreKeepsTargetsPartialProgressPendingRewardsAndBoardCycle()
        {
            var session=Session();var service=Service(session);service.EnsureMissionBoard();Complete(session);service.EnsureMissionBoard();
            var quest=Configure(session,QuestObjectiveType.KillEnemy,DungeonTheme.Forest,DungeonLength.Unspecified,tier:3,required:36);
            service.TryStartQuest(quest.QuestId);service.TryAddObjectiveProgress(quest.QuestId,RadiantBoardContract.ObjectiveId,5);
            Jobs(session)[1].TrySetStatus(QuestStatus.Completed);
            var save=new GameSessionSaveSnapshotFactory().CreateSnapshot(session,"Mission roundtrip");
            Assert.That(new GameSessionRestoreFactory().TryCreate(JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(save)),Catalog,out var restored),Is.True);
            var resumed=Service(restored,999);Assert.That(resumed.EnsureMissionBoard(),Is.True);
            Assert.That(restored.DungeonOffers.CompletedExpeditions,Is.EqualTo(1));
            foreach(var original in Jobs(session))
            {
                var next=restored.QuestsById[original.QuestId];
                Assert.That(next.DisplayName,Is.EqualTo(original.DisplayName));Assert.That(next.Status,Is.EqualTo(original.Status));
                Assert.That(next.BoardContract.RequiredAmount,Is.EqualTo(original.BoardContract.RequiredAmount));
                Assert.That(next.RadiantRewardOffer.Gold,Is.EqualTo(original.RadiantRewardOffer.Gold));
            }
            restored.QuestsById[quest.QuestId].TryGetObjectiveProgress(RadiantBoardContract.ObjectiveId,out var progress);
            Assert.That(progress.CurrentAmount,Is.EqualTo(5));
        }
        [Test]
        public void BonusItemAfterClaimHasOneOwnerAndSurvivesSaveRestore()
        {
            for(int seed=0;seed<40;seed++)
            {
                var session=Session(1,seed);var service=Service(session,seed);service.EnsureMissionBoard();
                var quest=Jobs(session).FirstOrDefault(q=>q.RadiantRewardOffer.BonusItem!=null);if(quest==null)continue;
                var item=quest.RadiantRewardOffer.BonusItem;quest.TrySetStatus(QuestStatus.Completed);
                Assert.That(service.TryTurnInRadiantQuest(quest.QuestId),Is.True);Assert.That(quest.RadiantRewardOffer.BonusItem,Is.Null);
                var save=new GameSessionSaveSnapshotFactory().CreateSnapshot(session,"Claimed bonus");
                Assert.That(new GameSessionRestoreFactory().TryCreate(JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(save)),Catalog,out var restored),Is.True);
                Assert.That(restored.PendingLoot.Any(i=>i.InstanceId==item.InstanceId),Is.True);
                var claimed=restored.QuestsById[quest.QuestId];var contract=claimed.BoardContract;
                Assert.That(Service(restored).EnsureMissionBoard(),Is.True);
                Assert.That(claimed.Status,Is.EqualTo(QuestStatus.TurnedIn));Assert.That(claimed.BoardContract,Is.SameAs(contract));return;
            }
            Assert.Fail("Deterministic sample should contain a bonus item.");
        }
        [Test]
        public void LegacyActiveAndCompletedQuestsKeepTheirObjectivesAndUnclaimedRewards()
        {
            var session=Session();var service=Service(session);
            foreach(var id in CatacombsMissionBoard.QuestIds){Catalog.TryGetQuest(id,out var definition);service.TryRegisterQuest(definition);}
            var jobs=Jobs(session);service.TryMakeQuestAvailable(jobs[0].QuestId);service.TryStartQuest(jobs[0].QuestId);
            service.TryAddObjectiveProgress(jobs[0].QuestId,jobs[0].CurrentStage.Objectives[0].ObjectiveId,3);jobs[1].TrySetStatus(QuestStatus.Completed);
            var reward=jobs[1].RadiantRewardOffer;Assert.That(service.EnsureMissionBoard(),Is.True);
            Assert.That(jobs[0].BoardContract,Is.Null);Assert.That(jobs[1].BoardContract,Is.Null);Assert.That(jobs[1].RadiantRewardOffer,Is.SameAs(reward));
            Assert.That(jobs[2].BoardContract,Is.Not.Null);
            var save=new GameSessionSaveSnapshotFactory().CreateSnapshot(session,"Legacy accepted");
            Assert.That(new GameSessionRestoreFactory().TryCreate(JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(save)),Catalog,out var restored),Is.True);
            Assert.That(restored.QuestsById[jobs[0].QuestId].Status,Is.EqualTo(QuestStatus.Active));
        }
        [Test]
        public void CorruptDynamicObjectiveIsRejectedWithoutChangingContentAssets()
        {
            var session=Session();Service(session).EnsureMissionBoard();var save=new GameSessionSaveSnapshotFactory().CreateSnapshot(session,"Invalid contract");
            save.Quests[0].BoardContract.RequiredAmount=999;
            LogAssert.Expect(LogType.Error,"Odtwarzanie GameSession odrzucone przy odtwarzaniu questów.");
            Assert.That(new GameSessionRestoreFactory().TryCreate(save,Catalog,out _),Is.False);
        }

        [TestCase(1)] [TestCase(48)]
        public void NativeMissionBoardShowsThreeJobsAndExactRewardsWithoutMultiplierLabels(int level)
        {
            Directory.CreateDirectory(Output);var session=Session(level);var service=Service(session,12);service.EnsureMissionBoard();
            var root=new GameObject("Mission canvas",typeof(RectTransform),typeof(Canvas));
            var cameraObject=new GameObject("Mission preview camera",typeof(Camera));var camera=cameraObject.GetComponent<Camera>();
            var target=new RenderTexture(1920,1080,24);var image=new Texture2D(1920,1080,TextureFormat.RGBA32,false);var previous=RenderTexture.active;
            try
            {
                var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.orthographicSize=540;camera.cullingMask=1<<30;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.03f,.025f);camera.targetTexture=target;
                var panel=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CatacombsMissions.prefab"),root.transform);panel.SetActive(true);
                var view=panel.GetComponent<CatacombsMissionBoardView>();Set(view,"session",session);Set(view,"service",service);
                foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
                int index=0;
                foreach(var quest in Jobs(session))
                {
                    Set(view,"selectedQuestId",quest.QuestId);typeof(CatacombsMissionBoardView).GetMethod("Refresh",Flags).Invoke(view,null);
                    foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
                    Canvas.ForceUpdateCanvases();
                    Assert.That(Get<TextMeshProUGUI>(view,"titleText").text,Is.EqualTo(quest.DisplayName));
                    Assert.That(Get<TextMeshProUGUI>(view,"experienceText").text,Is.EqualTo(quest.RadiantRewardOffer.Experience.ToString("N0")));
                    Assert.That(Get<TextMeshProUGUI>(view,"goldText").text,Is.EqualTo(quest.RadiantRewardOffer.Gold.ToString("N0")));
                    Assert.That(Get<TextMeshProUGUI>(view,"metadataText").text,Does.Not.Contain("×"));
                    Assert.That(Get<Transform>(view,"questList").Cast<Transform>().Count(t=>t.gameObject.activeSelf),Is.EqualTo(3));
                    camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();
                    File.WriteAllBytes(Path.Combine(Output,"mission-L"+level+"-"+(index++)+".png"),image.EncodeToPNG());
                }
            }
            finally{camera.targetTexture=null;RenderTexture.active=previous;Object.DestroyImmediate(root);Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(target);Object.DestroyImmediate(image);}
        }
    }
}
