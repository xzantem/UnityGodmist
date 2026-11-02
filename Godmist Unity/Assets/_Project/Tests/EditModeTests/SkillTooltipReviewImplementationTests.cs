using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Godmist.Core.Enums;
using Godmist.Core.Constants;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Items;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public partial class SkillTooltipReviewImplementationTests
    {
        const string Root="Assets/_Project/ScriptableObjects/";
        static BattleUnit Unit(string hero="Paladin",bool player=true)
        {
            var character=new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Root+"Characters/CD_"+(hero=="Warrior"?"Wladimir_Test":hero=="Scout"?"Jerzy":hero)+".asset"),AssetDatabase.LoadAssetAtPath<CharacterClassData>(Root+"Classes/CCD_"+hero+".asset"));
            character.CharacterId=Guid.NewGuid().ToString();character.Stats.MinAttack=new Stat(40);character.Stats.MaxAttack=new Stat(60);
            character.Stats.MaxHealth=new Stat(1000);character.CurrentHealth=100;character.Stats.CritChance=new Stat(0);character.Stats.CritDamage=new Stat(1.5f);
            return new BattleUnit{Character=character,IsPlayerSide=player,Position=new GridPosition(player?1:2,2),MaxActionPoints=10000,CurrentActionPoints=10000,CurrentActionValue=100};
        }
        static void Rank(BattleUnit u,string tree,string node,int rank)
        {
            var p=u.Character.SkillTreeProgress.Find(p=>p.TreeId==tree);
            if(p==null){p=new SkillTreeProgress{TreeId=tree};u.Character.SkillTreeProgress.Add(p);}
            p.NodeInvestments.RemoveAll(i=>i.NodeId==node);p.NodeInvestments.Add(new SkillTreeNodeInvestment{NodeId=node,Ranks=rank});
        }
        static CombatSession Session(params BattleUnit[] units)
        {
            var s=new CombatSession{Grid=new BattleGrid(12,12),Units=units.ToList(),ActiveUnit=units[0]};
            foreach(var u in units)u.WarriorState.Session=s;return s;
        }
        static SkillData Skill(string name)=>AssetDatabase.LoadAssetAtPath<SkillData>(Root+"Skills/SD_"+name+".asset");
        static string Plain(string text)=>Regex.Replace(text,"<[^>]*>","");
        [Test]
        public void BurningCriticalChanceBelongsToOwnerAndStacksWithOverflow()
        {
            var mage=Unit("Mage");var ally=Unit("Mage");var enemy=Unit("Warrior",false);var session=Session(mage,ally,enemy);
            new StatusEffectSystem().ApplyStatus(mage,enemy,StatusEffectIds.Burn,1,3,1,.1f,0,"test");
            Rank(mage,MageCombatSystem.Fire,"node.mage.fire.weakness",1);
            Rank(ally,MageCombatSystem.Fire,"node.mage.fire.weakness",2);
            var ctx=new PassiveEffectCombatContext(session:session,source:mage,target:enemy,actionId:"cast");
            Assert.That(MageCombatSystem.CalculateMagicCriticalChance(ctx),Is.EqualTo(.15f).Within(.00001));
            mage.MageState.OverflowActionId="cast";mage.MageState.OverflowCritChance=.2f;
            Assert.That(MageCombatSystem.CalculateMagicCriticalChance(ctx),Is.EqualTo(.35f).Within(.00001));
            Rank(mage,MageCombatSystem.Fire,"node.mage.fire.weakness",2);
            Assert.That(MageCombatSystem.CalculateMagicCriticalChance(ctx),Is.EqualTo(.6f).Within(.00001));
            Rank(mage,MageCombatSystem.Fire,"node.mage.fire.weakness",0);mage.MageState.OverflowCritChance=0;
            Assert.That(MageCombatSystem.CalculateMagicCriticalChance(ctx),Is.Zero,"A teammate must never grant the chance.");
        }
        [Test]
        public void SingleTargetHealingUsesResolvedRecipientsAndDoesNotTriggerOnTicksOrAura()
        {
            var caster=Unit();var target=Unit();target.Position=new GridPosition(2,2);var session=Session(caster,target);
            Rank(caster,PaladinCombatSystem.Priest,"node.paladin.priest.healer_focus",2);
            Rank(caster,PaladinCombatSystem.Priest,"node.paladin.priest.encouragement",1);
            var group=Skill("PaladinGroupHeal");var effect=group.Effects.First();
            var single=new Dictionary<int,IReadOnlyList<BattleUnit>>{{0,new[]{target}}};
            Assert.That(PaladinCombatSystem.IsSingleHealingTarget(group,single),Is.True);
            single[0]=new[]{caster,target};Assert.That(PaladinCombatSystem.IsSingleHealingTarget(group,single),Is.False);
            Assert.That(PaladinCombatSystem.CalculateHealing(caster,group,effect,true),Is.EqualTo(PaladinCombatSystem.CalculateHealing(caster,group,effect,false)*1.3f).Within(.001));
            PaladinCombatSystem.Execute(session,caster,target,group,effect,new CombatRecoverySystem(),true);
            Assert.That(target.CurrentActionValue,Is.LessThan(100));
            target.CurrentActionValue=100;
            new CombatRecoverySystem().Heal(target,10);Assert.That(target.CurrentActionValue,Is.EqualTo(100));
            Rank(caster,PaladinCombatSystem.Priest,"node.paladin.priest.healing_aura",1);
            PaladinCombatSystem.TurnStart(session,caster,new CombatRecoverySystem());Assert.That(target.CurrentActionValue,Is.EqualTo(100));
            target.Character.CurrentHealth=1000;
            PaladinCombatSystem.Execute(session,caster,target,group,effect,new CombatRecoverySystem(),true);
            Assert.That(target.CurrentActionValue,Is.EqualTo(100),"Overhealing is not an actual heal.");
        }
        [Test]
        public void TransfusionUsesSingleTargetHealingTalentsWithoutConsumingHealDiscount()
        {
            var source=Unit();var enemy=Unit("Warrior",false);enemy.Character.CurrentHealth=1000;
            var session=Session(source,enemy);
            Rank(source,PaladinCombatSystem.Priest,"node.paladin.priest.healer_focus",2);
            Rank(source,PaladinCombatSystem.Priest,"node.paladin.priest.encouragement",1);
            source.PaladinState.HealingDiscount=true;source.CurrentActionValue=0;
            var random=UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(7);
                Assert.That(new SkillExecutionSystem(statusEffectSystem:new StatusEffectSystem()).TryExecute(session,source,Skill("PaladinTransfusion"),enemy),Is.True);
                var dealt=1000-enemy.Character.CurrentHealth;
                Assert.That(source.Character.CurrentHealth-100,Is.EqualTo(Mathf.FloorToInt(dealt*2*1.3f)));
                Assert.That(source.CurrentActionValue,Is.Zero);
                Assert.That(source.PendingEquipmentActionValueReduction,Is.EqualTo(.2f*CombatTurnSystem.CalculateBaseActionValue(source)));
                Assert.That(source.PaladinState.HealingDiscount,Is.True);
            }
            finally{UnityEngine.Random.state=random;}
        }

        [Test]
        public void GuaranteedCriticalIsIncludedOnceAndDoubleDamageRemainsSeparate()
        {
            var scout=Unit("Scout");var aimed=Skill("ScoutAimedThrow");var effect=aimed.Effects.First(e=>e.EffectType==EffectType.DealDamage);
            var range=CombatSkillPreviewCalculator.CalculateSingleHitRange(scout,aimed,effect);
            Assert.That(range.Minimum,Is.EqualTo(60));Assert.That(range.Maximum,Is.EqualTo(90));
            var quick=Skill("ScoutQuickStrikes");var e=quick.Effects.First(e=>e.EffectType==EffectType.DealDamage);
            var trace=new CombatValueExplanation();var hit=CombatSkillPreviewCalculator.CalculateSingleHitRange(scout,quick,e,trace);
            Assert.That(hit.Minimum,Is.EqualTo(20));Assert.That(hit.Maximum,Is.EqualTo(30));
            Assert.That(trace.Rows.Last().Value,Is.EqualTo("20–30"));
        }
        [Test]
        public void CriticalAvoidancePreviewIsPureAndDoesNotPromiseGuaranteedCriticalDamage()
        {
            var source=Unit("Scout");var target=Unit("Warrior",false);Session(source,target);
            target.Character.Stats.PhysicalDefense=new Stat(0);
            target.Character.Equipment.Armor=new EquipmentItemInstance{UpgradeProgress=1,SocketedGaldurytes=new(){new GalduryteItemInstance{IsIdentified=true,Effects=new(){new GalduryteEffectInstance{EffectType=GalduryteEffectType.CriticalAvoidance,Value=.5f}}}}};
            var skill=Skill("ScoutAimedThrow");var random=UnityEngine.Random.state;
            Assert.That(CombatDamageSystem.PreviewCriticalChance(source.WarriorState.Session,source,target,skill,DamageType.Physical),Is.EqualTo(.5f));
            Assert.That(UnityEngine.Random.state,Is.EqualTo(random));
            var range=CombatSkillPreviewCalculator.CalculateTargetPreview(source,target,skill).Damage;
            Assert.That(range.Minimum,Is.EqualTo(24));Assert.That(range.Maximum,Is.EqualTo(54));
        }
        [Test]
        public void RicochetDisplaysSevenSeventeenTwentySevenWithoutRolling()
        {
            var source=Unit("Scout");source.Character.Stats.CritChance=new Stat(.07f);var skill=Skill("ScoutRicochet");var random=UnityEngine.Random.state;
            Assert.That(CombatDamageSystem.PreviewUntargetedCriticalChance(source,skill,0),Is.EqualTo(.07f).Within(.00001));
            Assert.That(CombatDamageSystem.PreviewUntargetedCriticalChance(source,skill,1),Is.EqualTo(.17f).Within(.00001));
            Assert.That(CombatDamageSystem.PreviewUntargetedCriticalChance(source,skill,2),Is.EqualTo(.27f).Within(.00001));
            Assert.That(UnityEngine.Random.state,Is.EqualTo(random));
        }
        [Test]
        public void SingleRecipientGroupHealingPreviewUsesTheSameTalentAsExecution()
        {
            var source=Unit();Session(source);
            Rank(source,PaladinCombatSystem.Priest,"node.paladin.priest.healer_focus",2);
            var skill=Skill("PaladinGroupHeal");var expected=PaladinCombatSystem.HealingPowerRange(source,skill,skill.Effects.First(),true);
            var preview=CombatSkillPreviewCalculator.CalculateHealingPreview(source.WarriorState.Session,source,source,skill);
            Assert.That(preview.Minimum,Is.EqualTo(expected.Minimum));Assert.That(preview.Maximum,Is.EqualTo(expected.Maximum));
        }

        [Test]
        public void EveryReviewedSkillAndRankBuildsWithoutChangingTheCharacter()
        {
            var old=UIWorkspaceTooltipView.ActiveInstance;
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab"));
            try
            {
                var view=root.GetComponent<UIWorkspaceTooltipView>();Assert.That(view,Is.Not.Null);
                var total=0;
                foreach(var hero in new[]{"Warrior","Scout","Mage","Paladin"})
                {
                    var unit=Unit(hero);var character=unit.Character;var before=JsonUtility.ToJson(character);
                    foreach(var skill in character.Skills.Concat(character.ClassData.SkillTrees.SelectMany(t=>t.Nodes).Select(n=>n.GrantedSkill)).Where(s=>s!=null).Distinct())
                    {
                        var rich=view.BuildLoadoutSkillDetails(skill,character);
                        var body=Plain(rich);
                        foreach(System.Text.RegularExpressions.Match link in Regex.Matches(rich,"calculation\\.[a-z0-9]+"))
                        {
                            var arguments=new object[]{link.Value,null};
                            Assert.That((bool)typeof(UIWorkspaceTooltipView).GetMethod("TryGetGlossaryEntry",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(view,arguments),Is.True,skill.SkillId+" missing calculation "+link.Value);
                            Assert.That(((Godmist.Views.UI.CombatTooltipGlossary.Entry)arguments[1]).Description,Is.Not.Empty);
                        }
                        Assert.That(body,Does.Not.Contain("{X"),skill.SkillId);
                        if(skill.SkillId=="skill.scout.poisoned_dagger")Assert.That(body,Does.Not.Contain("odporność"),body);
                        if(skill.SkillId=="skill.mage.offering")Assert.That(Plain(UIWorkspaceTooltipView.BuildLoadoutSkillCost(skill,character)),Does.Not.Contain("Podtrzymanie"));
                    }
                    foreach(var tree in character.ClassData.SkillTrees)
                    {
                        Assert.That(SkillTreeProgressionPanelView.GetGlobalPassiveSummary(tree,0),Is.EqualTo("brak premii."));
                        foreach(var node in tree.Nodes.Where(n=>n.GrantedSkill==null))
                        {
                            for(var rank=0;rank<=node.EffectiveMaxRanks;rank++)
                            {
                                var body=Plain(SkillTreeNodeView.BuildDetails(node,rank,view,character));
                                Assert.That(body,Does.Not.Contain("{X"),node.Id);
                                Assert.That(body,Does.Not.Contain("{furia}"),node.Id);
                                Assert.That(body,Does.Not.Contain("Aktualne →"),node.Id);
                                Assert.That(body,Does.Not.Contain("punktów Złości"),node.Id);
                                if(rank==0)Assert.That(body,Does.StartWith("Następne:"),node.Id);
                                if(rank>0&&rank<node.EffectiveMaxRanks)Assert.That(body,Does.Contain("Aktualne:").And.Contain("Następne:"),node.Id);
                                total++;
                            }
                        }
                    }
                    Assert.That(JsonUtility.ToJson(character),Is.EqualTo(before),"Preview mutated "+hero);
                }
                Assert.That(total,Is.GreaterThan(350));
            }
            finally{Object.DestroyImmediate(root);typeof(UIWorkspaceTooltipView).GetProperty("ActiveInstance").SetValue(null,old);}
        }
        [Test]
        public void CalculationTablesKeepOnlySourcesAndResultAndResolveNestedAttack()
        {
            var old=UIWorkspaceTooltipView.ActiveInstance;
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab"));
            try
            {
                var view=root.GetComponent<UIWorkspaceTooltipView>();var source=Unit("Warrior");
                WarriorCombatSystem.Variable(source,source,GameplayVariableType.DamageDealtPercent,.2f,3,"test.bonus","Okrzyk Wojenny");
                var rich=view.BuildLoadoutSkillDetails(Skill("WarriorCut"),source.Character);
                var entries=(Dictionary<string,CombatTooltipGlossary.Entry>)typeof(UIWorkspaceTooltipView).GetField("calculationEntries",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(view);
                var entry=entries.Values.First(e=>e.Title.StartsWith("Obrażenia:"));
                Assert.That(entry.Description,Does.Contain("×100%").And.Contain("#8ED59B").And.Contain("#E7C77B"));
                Assert.That(Plain(entry.Description),Does.Not.Contain("zaokrągleniu").And.Not.Contain("Po modyfikatorach").And.Not.Contain("Źródła statystyk").And.Not.Contain("Premie wyposażenia").And.Not.Contain("Zadawane obrażenia"));
                Assert.That(entry.Description,Does.Not.Contain("umiejętności ×"));
                var nested=Regex.Match(entry.Description,"calculation\\.[a-z0-9]+").Value;
                Assert.That(entries[nested].Title,Is.EqualTo("Atak postaci"));
                Assert.That(entries[nested].Description,Does.Contain("Atak bazowy"));
                var row=typeof(UIWorkspaceTooltipView).GetMethod("Row",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
                Assert.That((string)row.Invoke(null,new object[]{"Osłabienie ×","0,8"}),Does.Contain("×80%").And.Contain("#F08C85"));
                Assert.That((string)row.Invoke(null,new object[]{"Koszt zasobu","-20%"}),Does.Contain("#8ED59B"));
                Assert.That((string)row.Invoke(null,new object[]{"Koszt — wyposażenie ×","0,8"}),Does.Contain("×80%").And.Contain("#8ED59B"));
                Assert.That((string)row.Invoke(null,new object[]{"Otrzymywane obrażenia","+20%"}),Does.Contain("#F08C85"));
                Assert.That((string)row.Invoke(null,new object[]{"Szansa krytyczna leczenia","0%"}),Is.Empty);
                Assert.That((string)row.Invoke(null,new object[]{"Premia wyposażenia","+0 pp"}),Is.Empty);
                Assert.That((string)row.Invoke(null,new object[]{"Szansa krytyczna leczenia","5%"}),Does.Contain("5%"));
                Assert.That((string)row.Invoke(null,new object[]{"Leczenie","0"}),Does.Contain("0").And.Contain("#E7C77B"));
                Assert.That((string)row.Invoke(null,new object[]{"Obrażenia umiejętności ×","0"}),Does.Contain("×0%"));
            }
            finally{Object.DestroyImmediate(root);typeof(UIWorkspaceTooltipView).GetProperty("ActiveInstance").SetValue(null,old);}
        }
        [Test]
        public void KickHasSeparateBreakdownsForDistanceChanceModifierAndDuration()
        {
            var old=UIWorkspaceTooltipView.ActiveInstance;
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab"));
            try
            {
                var view=root.GetComponent<UIWorkspaceTooltipView>();var source=Unit("Warrior");
                var before=JsonUtility.ToJson(source.Character);var random=UnityEngine.Random.state;
                var rich=view.BuildLoadoutSkillDetails(Skill("WarriorKick"),source.Character);
                Assert.That(Plain(rich),Does.Contain("zmniejszenie uniku"));
                var entries=(Dictionary<string,CombatTooltipGlossary.Entry>)typeof(UIWorkspaceTooltipView).GetField("calculationEntries",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(view);
                var distance=entries.Values.Single(e=>e.Title=="Odległość odepchnięcia");
                Assert.That(Plain(distance.Description),Does.Contain("1 pole").And.Not.Contain("Czas").And.Not.Contain("Szansa"));
                var chance=entries.Values.Single(e=>e.Title=="Szansa nałożenia: Unik");
                Assert.That(Plain(chance.Description),Does.Contain("80%").And.Not.Contain("15").And.Not.Contain("Czas"));
                var modifier=entries.Values.Single(e=>e.Title=="Unik — zmiana");
                Assert.That(Plain(modifier.Description),Does.Contain("15").And.Not.Contain("-15").And.Not.Contain("80%").And.Not.Contain("Czas"));
                Assert.That(modifier.Description,Does.Not.Contain("<color="));
                var duration=entries.Values.Single(e=>e.Title=="Czas trwania: Unik");
                Assert.That(Plain(duration.Description),Does.Contain("1 tur").And.Not.Contain("80%").And.Not.Contain("15"));
                foreach(var entry in new[]{distance,chance,modifier,duration})Assert.That(rich,Does.Contain(entry.Id));
                Assert.That(JsonUtility.ToJson(source.Character),Is.EqualTo(before));Assert.That(UnityEngine.Random.state,Is.EqualTo(random));
            }
            finally{Object.DestroyImmediate(root);typeof(UIWorkspaceTooltipView).GetProperty("ActiveInstance").SetValue(null,old);}
        }
        [Test,Explicit("Authorized Editor API changes to hero skill display names only.")]
        public void ApplyAuthorizedNames(){Godmist.Editor.SkillTooltipReviewSetup.ApplyNames();Godmist.Editor.SkillTooltipReviewSetup.ApplyGlobalPassiveTooltip();}
    }
}
