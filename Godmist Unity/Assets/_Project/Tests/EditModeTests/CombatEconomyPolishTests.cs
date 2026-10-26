using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Items;
using Godmist.Data.NPC;
using Godmist.Data.Quests;
using Godmist.Data.Skills;
using Godmist.Data.World;
using Godmist.Editor;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;
using Godmist.Runtime.Quests;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.Items;
using Godmist.Systems.NPC;
using Godmist.Systems.Quests;
using Godmist.Systems.SaveLoad;
using Godmist.Views.Combat;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed partial class CombatEconomyPolishTests
    {
        const string Root="Assets/_Project/ScriptableObjects/";
        const string Output="../LocalHandoffs/combat-economy-ui-20261026";
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static GameContentCatalog Catalog=>AssetDatabase.LoadAssetAtPath<GameContentCatalog>(Root+"World/GC_MainContentCatalog.asset");
        static BattleUnit Hero(string name="CD_Jerzy",bool player=true)=>new() {
            Character=new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Root+"Characters/"+name+".asset")),
            IsPlayerSide=player,CurrentActionPoints=1000,MaxActionPoints=1000,Position=new GridPosition(0,0)
        };
        static SkillData Knife=>AssetDatabase.LoadAssetAtPath<SkillData>(Root+"Skills/SD_KnifeToss.asset");
        [Test,Explicit("Capture baseline before the approved Editor API authoring.")]
        public void CaptureBaseline() {CombatEconomyPolishSetup.CaptureBefore();ExportStats("before");}
        [Test,Explicit("Apply approved secondary stats and presentation assets via Editor API.")]
        public void ApplyApprovedContent() {CombatEconomyPolishSetup.Apply();ExportStats("after");}
        static void ExportStats(string label)
        {
            var text=new StringBuilder("id,level,hp,atkMin,atkMax,physical,magical,dodge,crit,critDamage\n");
            foreach(var def in Catalog.CharacterDefinitions.Where(d=>d!=null&&d.ClassData!=null&&d.Id.StartsWith("enemy.")))
            foreach(var level in new[]{1,3,5,7,10,12,15,20,25,30,35,40,45,48,50})
            {
                var c=new CharacterInstanceFactory().Create(def,null,level);var s=c.Stats;
                text.Append(def.Id).Append(',').Append(level);
                foreach(var value in new[]{s.MaxHealth.GetValue(),s.MinAttack.GetValue(),s.MaxAttack.GetValue(),s.PhysicalDefense.GetValue(),s.MagicalDefense.GetValue(),s.Dodge.GetValue(),s.CritChance.GetValue(),s.CritDamage.GetValue()})
                    text.Append(',').Append(value.ToString("0.#####",CultureInfo.InvariantCulture));
                text.Append('\n');
            }
            Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"stats-"+label+".csv"),text.ToString());
        }
        [TestCase(0,.25f)] [TestCase(.25f,.625f)] [TestCase(.5f,1)] [TestCase(.75f,1.5f)] [TestCase(1,2)]
        [TestCase(-1,.25f)] [TestCase(2,2)]
        public void ManaCurveHasApprovedAnchorsAndClamps(float ap,float expected)=>Assert.That(ManaResourceRule.RegenerationMultiplier(ap),Is.EqualTo(expected).Within(.0001));
        [Test]
        public void RestorationTextCannotBeMistakenForTemporaryMaximumBuff()
        {
            var definition=ScriptableObject.CreateInstance<ConsumableItemDefinition>();
            try {
                typeof(ItemDefinition).GetField("<Id>k__BackingField",Private).SetValue(definition,"test.restoration");
                var item=new ConsumableItemInstance(definition);
                item.RuntimeEffects.Add(new ConsumableEffectRuntime{EffectType=ConsumableRuntimeEffectType.RestoreHealth,Value=.15f});
                item.RuntimeEffects.Add(new ConsumableEffectRuntime{EffectType=ConsumableRuntimeEffectType.RestoreResource,Value=.2f});
                var text=UIWorkspaceTooltipView.BuildPotionEffectSummary(item);
                Assert.That(text,Does.Contain("Przywraca zdrowie").And.Contain("15%").And.Contain("Przywraca zasób").And.Contain("20%"));
            } finally {Object.DestroyImmediate(definition);}
        }
        [Test]
        public void AimedThrowHasHigherRealHitChanceWithoutChangingCostOrDamage()
        {
            var hero=Hero();var enemy=Hero(player:false);
            var aimed=AssetDatabase.LoadAssetAtPath<SkillData>(Root+"Skills/SD_ScoutAimedThrow.asset");
            Assert.That(aimed.SkillAccuracy,Is.EqualTo(120));
            Assert.That(CombatDamageSystem.CalculateSkillHitChance(hero,enemy,aimed),Is.GreaterThan(CombatDamageSystem.CalculateSkillHitChance(hero,enemy,Knife)));
        }
        [TestCase(0)] [TestCase(1)]
        public void KnifeSelectionAndTargetPreviewAgreeWithoutInventory(int knives)
        {
            var hero=Hero();hero.AvailableKnives=knives;var near=Hero(player:false);near.Position=new GridPosition(1,0);
            var far=Hero(player:false);far.Position=new GridPosition(3,0);
            var session=new CombatSession{Grid=new BattleGrid(6,4),ActiveUnit=hero};session.Units.AddRange(new[]{hero,near,far});
            var driver=new CombatExecutionDriver(null,null,null,null,null,null,null,6,4,0,0,0,null,null,null,null,null,null);
            driver.InitializeTargetingPreview(session);var mode=Knife.TargetModes[0];
            Assert.That(driver.GetSelectableSkillPositions(Knife,mode,0),Does.Contain(near.Position));
            Assert.That(driver.GetSkillTargets(Knife,mode,0,near.Position),Does.Contain(near));
            Assert.That(driver.GetSelectableSkillPositions(Knife,mode,0).Contains(far.Position),Is.EqualTo(knives>0));
            Assert.That(driver.GetSkillTargets(Knife,mode,0,far.Position).Contains(far),Is.EqualTo(knives>0));
            Assert.That(hero.AvailableKnives,Is.EqualTo(knives));
        }
        [Test]
        public void DiagnosticBufferIsOptInBoundedAndRetainsOnlyLatestEntries()
        {
            var trace=new CombatDiagnosticRecorder();trace.Record("test",null,null,"off");Assert.That(trace.Count,Is.Zero);
            trace.Enabled=true;
            for(var i=0;i<CombatDiagnosticRecorder.Capacity+5;i++)trace.Record("test",null,null,"sample="+i);
            Assert.That(trace.Count,Is.EqualTo(CombatDiagnosticRecorder.Capacity));
            Assert.That(trace.Read(1),Does.Contain("sample=4004").And.Not.Contain("sample=4003"));
            trace.Enabled=false;trace.Record("test",null,null,"not recorded");Assert.That(trace.Read(),Does.Not.Contain("not recorded"));
            trace.Clear();Assert.That(trace.Count,Is.Zero);
        }
        [Test]
        public void ConsoleTraceAttachesToRunningCombatAndRecordsActualDamageWithoutChangingDamage()
        {
            var game=new GameSession(GameModeState.Exploration);var hero=Hero();var enemy=Hero(player:false);
            var session=new CombatSession{CombatId="trace.test",Grid=new BattleGrid(5,2),ActiveUnit=hero};session.Units.AddRange(new[]{hero,enemy});
            game.TrySetActiveCombat(session);
            var console=new Godmist.Systems.Debugging.DevConsoleCommandService();
            Assert.That(console.Execute("combattrace on",game,session).Success,Is.True);
            Assert.That(session.Diagnostics,Is.SameAs(game.CombatDiagnostics));
            var before=enemy.Character.CurrentHealth;
            var actual=new CombatDamageSystem().ApplyDamage(session,hero,enemy,17,DamageType.Physical);
            Assert.That(enemy.Character.CurrentHealth,Is.EqualTo(before-actual));
            Assert.That(game.CombatDiagnostics.Read(),Does.Contain("|damage|").And.Contain(enemy.Character.CharacterId).And.Contain("hpDamage="+actual));
            Assert.That(console.Execute("combattrace show",game,session).Message,Does.Contain("damage"));
            var count=game.CombatDiagnostics.Count;console.Execute("combattrace off",game,session);
            new CombatDamageSystem().ApplyDamage(session,hero,enemy,17,DamageType.Physical);Assert.That(game.CombatDiagnostics.Count,Is.EqualTo(count));
            Assert.That(console.Execute("combattrace invalid",game,session).Success,Is.False);
        }
        [TestCase(1)] [TestCase(10)] [TestCase(20)]
        public void BlacksmithFreshLegacyAndSaveRestoreKeepOnlyValidMainOffers(int level)
        {
            var catalog=Catalog;var def=catalog.NpcDefinitions.Single(d=>d!=null&&d.GetRole<BlacksmithRoleData>()!=null);
            var npc=new NpcInstanceFactory().Create(def,level,catalog);
            var valid=npc.ShopInventory.Items.OfType<EquipmentItemInstance>().ToArray();
            Assert.That(valid.Length,Is.GreaterThan(0));Assert.That(valid.All(i=>i.PartIds.Count>=3&&i.BaseValue>0&&!TradeService.IsFreeStarter(i)),Is.True);
            var starter=new ItemInstanceFactory().Create(catalog.ItemDefinitions.OfType<EquipmentItemDefinition>().First(d=>d.IsStarterEquipment));
            npc.ShopInventory.Items.Add(starter);npc.ShopInventory.BuybackStock.Add(new BuybackEntry(starter,0));
            Assert.That(TraderStockService.RemoveInvalidEquipmentOffers(npc),Is.EqualTo(1));
            Assert.That(npc.ShopInventory.Items.OfType<EquipmentItemInstance>(),Is.EquivalentTo(valid));
            Assert.That(npc.ShopInventory.BuybackStock.Single().Item,Is.SameAs(starter));
            npc.ShopInventory.Items.Add(new ItemInstanceFactory().Create(starter.Definition));
            var game=new GameSession(GameModeState.Exploration);game.TryRegisterNpc(npc);
            var snapshot=new GameSessionSaveSnapshotFactory().CreateSnapshot(game,"Blacksmith legacy");
            Assert.That(new GameSessionRestoreFactory().TryCreate(snapshot,catalog,out var restored),Is.True);
            Assert.That(restored.NpcsById[npc.NpcId].ShopInventory.Items.Any(TradeService.IsFreeStarter),Is.False);
            Assert.That(restored.NpcsById[npc.NpcId].ShopInventory.BuybackStock.Single().Item.Definition.Id,Is.EqualTo(starter.Definition.Id));
        }
        [Test]
        public void TrackerShowsSelectedQuestClearsWhenUntrackedAndRebindsSession()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<TrackedQuestHudView>(CombatEconomyPolishSetup.TrackerPath);Assert.That(prefab,Is.Not.Null);
            var view=Object.Instantiate(prefab);
            try {
                var game=new GameSession(GameModeState.Exploration);
                var quest=new QuestInstance(AssetDatabase.LoadAssetAtPath<QuestDefinition>(Root+"Quests/QD_InvestigateCatacombs.asset"),QuestStatus.Active);
                game.TryRegisterQuest(quest);game.TrySetTrackedQuest(quest.QuestId);view.Bind(game,new QuestService(game));
                var content=(GameObject)typeof(TrackedQuestHudView).GetField("content",Private).GetValue(view);
                Assert.That(content.activeSelf,Is.True);Assert.That(view.GetComponentsInChildren<TMP_Text>().First().text,Is.EqualTo(quest.DisplayName));
                Assert.That(TrackedQuestHudView.BuildObjectives(quest),Is.Not.Empty);
                view.Bind(new GameSession(GameModeState.Exploration),null);Assert.That(content.activeSelf,Is.False);
                game.TrySetTrackedQuest(quest.QuestId);Assert.That(content.activeSelf,Is.False,"Old campaign must be unsubscribed.");
            } finally {Object.DestroyImmediate(view.gameObject);}
        }
        [Test]
        public void DefenderIsGroundedInPaladinBaselineWithoutChangingHealthAttackOrBosses()
        {
            var paladin=EnemyScalingBalanceTests.Hero(3,1);
            var defender=new CharacterInstanceFactory().Create(Catalog.CharacterDefinitions.Single(d=>d.Id=="enemy.catacombs.defender"));
            Assert.That(defender.Stats.PhysicalDefense.GetValue(),Is.InRange(paladin.Stats.PhysicalDefense.GetValue(),2*paladin.Stats.PhysicalDefense.GetValue()));
            Assert.That(defender.Stats.PhysicalDefense.GetValue(),Is.EqualTo(40));
            foreach(var def in Catalog.CharacterDefinitions.Where(d=>d!=null&&d.ClassData!=null&&d.Id.StartsWith("enemy.")))
            {
                var before=ScriptableObject.CreateInstance<CharacterDefinition>();
                try {
                    EditorJsonUtility.FromJsonOverwrite(File.ReadAllText(Path.Combine(Output,"before-definitions",def.Id+".json")),before);
                    foreach(var level in new[]{1,7,20,35,50}) {
                        var old=new CharacterInstanceFactory().Create(before,null,level).Stats;
                        var current=new CharacterInstanceFactory().Create(def,null,level).Stats;
                        Assert.That(current.MaxHealth.GetValue(),Is.EqualTo(old.MaxHealth.GetValue()),def.Id);
                        Assert.That(current.MinAttack.GetValue(),Is.EqualTo(old.MinAttack.GetValue()),def.Id);
                        Assert.That(current.MaxAttack.GetValue(),Is.EqualTo(old.MaxAttack.GetValue()),def.Id);
                        if(!EnemyArchetypeStatsSetup.Configure(def.Id,new SerializedObject(before)))
                            Assert.That(EditorJsonUtility.ToJson(def),Is.EqualTo(EditorJsonUtility.ToJson(before)),def.Id);
                    }
                } finally {Object.DestroyImmediate(before);}
            }
        }
    }
}
