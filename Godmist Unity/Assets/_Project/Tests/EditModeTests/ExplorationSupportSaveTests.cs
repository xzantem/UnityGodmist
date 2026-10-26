using System;
using System.Collections.Generic;
using System.IO;
using Godmist.Core.Enums;
using Godmist.Core.Serialization;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Data.World;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.SaveLoad;
using Godmist.Views.Combat;
using Godmist.Views.Exploration;
using Godmist.Views.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class ExplorationSupportSaveTests
    {
        static GameContentCatalog Catalog=>AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
        static GameSaveSummary Summary(GameSaveData data)=>new("test","test","test","test",DateTime.MinValue,DateTime.MinValue,SaveSlotType.Autosave,new GameSavePreview(data));
        [TestCase(DungeonTheme.Forest,"Oczyść: Las")][TestCase(DungeonTheme.Catacombs,"Oczyść: Katakumby")]
        public void SavePreviewUsesContractNameInsteadOfBossSlotTemplate(DungeonTheme theme,string expected)
        {
            var data=new GameSaveData{TrackedQuestId="quest.catacombs.radiant.boss"};
            data.Quests.Add(new QuestSaveData{QuestId=data.TrackedQuestId,HasBoardContract=true,BoardContract=new RadiantBoardContractSaveData {
                ObjectiveType=QuestObjectiveType.KillEnemy,Theme=theme,Level=1,RequiredAmount=25,RewardMultiplier=3,Cycle=0}});
            var json=JsonUtility.ToJson(data);var summary=Summary(data);
            Assert.That(summary.Preview.TrackedQuestDisplayName,Is.EqualTo(expected));
            Assert.That(SaveSummaryPresentation.Facts(summary,Catalog),Does.Contain(expected).And.Not.Contain("Kr"));
            Assert.That(SaveSummaryPresentation.Details(summary,Catalog),Does.Contain(expected).And.Not.Contain("Kr"));
            Assert.That(JsonUtility.ToJson(data),Is.EqualTo(json));
        }
        [Test]
        public void RealLegacyBossAndMalformedContractHaveSeparateSafeFallbacks()
        {
            var data=new GameSaveData{TrackedQuestId="quest.catacombs.radiant.boss"};
            Assert.That(SaveSummaryPresentation.Facts(Summary(data),Catalog),Does.Contain("Umarłego Króla"));
            data.Quests.Add(new QuestSaveData{QuestId=data.TrackedQuestId,HasBoardContract=true,BoardContract=new RadiantBoardContractSaveData()});
            Assert.That(SaveSummaryPresentation.Facts(Summary(data),Catalog),Does.Contain("Zlecenie").And.Not.Contain("Umarłego Króla"));
        }
        [Test,Explicit("Read-only regression against the user's reproduced forest save; never writes it.")]
        public void ReproducedForestSaveShowsItsActualContractWithoutChangingFile()
        {
            var path=Path.Combine(Application.persistentDataPath,"Saves/31815c7d08554a849c0d4d20163e4b17/autosave.json");
            var raw=File.ReadAllText(path);
            Assert.That(new GameSaveJsonSerializer().TryDeserialize(raw,out var data),Is.True);
            Assert.That(data.ActiveDungeon.DungeonId,Is.EqualTo("dungeon.forest.expedition"));
            Assert.That(data.ActiveDungeon.DungeonLevel,Is.EqualTo(1));
            var facts=SaveSummaryPresentation.Facts(Summary(data),Catalog);
            Assert.That(facts,Does.Contain("Oczyść: Las").And.Not.Contain("Umarłego Króla"));
            Assert.That(File.ReadAllText(path),Is.EqualTo(raw));
            Directory.CreateDirectory("../LocalHandoffs/exploration-support-save-20261026");
            File.WriteAllText("../LocalHandoffs/exploration-support-save-20261026/reproduced-save-summary.txt",facts);
        }
        [Test]
        public void RealPaladinHealPlaysOnExistingExplorationBodyWithoutBorrowingOrSnapping()
        {
            var root=new GameObject("Support presentation");
            CombatExecutionDriver preview=null;
            try {
                var spawner=root.AddComponent<CombatUnitViewSpawner>();
                BattleUnit Unit(string file,bool player)=>new(){IsPlayerSide=player,CurrentActionPoints=1000,MaxActionPoints=1000,
                    Character=new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/"+file+".asset"))};
                var caster=Unit("CD_Paladin",true);var target=Unit("CD_Jerzy",true);
                caster.Character.ResourcePool.CurrentValue=caster.Character.ResourcePool.MaxValue;
                caster.Position=new GridPosition(1,1);target.Position=new GridPosition(2,1);target.Character.CurrentHealth=50;
                var prefab=AssetDatabase.LoadAssetAtPath<ExplorationCharacterView>("Assets/_Project/Prefabs/Characters/PF_ExplorationCharacter.prefab");
                var actorView=Object.Instantiate(prefab,root.transform);actorView.Bind(caster.Character);actorView.SetPosition(new Vector2(1.37f,1.42f));
                var targetView=Object.Instantiate(prefab,root.transform);targetView.Bind(target.Character);targetView.SetPosition(new Vector2(2.2f,1.1f));
                spawner.ExplorationView=u=>u==caster?actorView:u==target?targetView:null;
                spawner.ExplorationWorldPosition=u=>spawner.ExplorationView(u)?.transform.position;
                var session=new CombatSession{Grid=new BattleGrid(8,8),ActiveUnit=caster};session.Units.AddRange(new[]{caster,target});
                preview=new CombatExecutionDriver(null,spawner,null,null,null,null,null,8,8,0,0,0,null,null,null,null,null,null);preview.InitializeTargetingPreview(session);
                var bus=new CombatFeedbackBus();bus.Published+=preview.PresentExplorationSupportFeedback;
                var skill=AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_PaladinHeal.asset");
                var before=actorView.Position;var hp=target.Character.CurrentHealth;var mana=caster.Character.ResourcePool.CurrentValue;
                var apCost=CombatActionCostCalculator.CalculateSkillCost(caster,skill);
                var resourceCost=PassiveEffectCombatExecutionSystem.ResolveResourceCost(caster,skill);
                var execution=new SkillExecutionSystem(recoverySystem:new CombatRecoverySystem(feedbackBus:bus),feedbackBus:bus);
                Assert.That(execution.TryExecute(session,caster,skill,new Dictionary<int,IReadOnlyList<BattleUnit>>{[0]=new[]{target}}),Is.True);
                bus.Published-=preview.PresentExplorationSupportFeedback;
                Assert.That(actorView.HasCombatPose,Is.True,"The real cast pose must be active on the exploration body.");
                Assert.That(actorView.Position,Is.EqualTo(before));Assert.That(target.Character.CurrentHealth,Is.GreaterThan(hp));
                Assert.That(caster.CurrentActionPoints,Is.EqualTo(1000-apCost));
                Assert.That(caster.Character.ResourcePool.CurrentValue,Is.EqualTo(mana-resourceCost));
                Assert.That(actorView.GetComponentsInChildren<SpriteRenderer>().Length,Is.GreaterThan(0));
            } finally {Object.DestroyImmediate(root);}
        }
    }
}
