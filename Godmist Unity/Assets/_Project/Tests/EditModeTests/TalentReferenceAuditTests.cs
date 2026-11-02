using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using Godmist.Data.Characters;
using Godmist.Runtime.Characters;
using Godmist.Systems.Characters;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class TalentReferenceAuditTests
    {
        const string Assets="Assets/_Project/ScriptableObjects/";
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static CharacterInstance Hero(string cls,string definition)=>new CharacterInstanceFactory().Create(
            AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Assets+"Characters/CD_"+definition+".asset"),
            AssetDatabase.LoadAssetAtPath<CharacterClassData>(Assets+"Classes/CCD_"+cls+".asset"),1);
        static CombatTooltipGlossary.Entry Resolve(UIWorkspaceTooltipView tooltip,string id)
        {
            var args=new object[]{id,null};
            Assert.That(typeof(UIWorkspaceTooltipView).GetMethod("TryGetGlossaryEntry",Hidden).Invoke(tooltip,args),Is.True,id);
            return (CombatTooltipGlossary.Entry)args[1];
        }
        static string Final(UIWorkspaceTooltipView tooltip,string text)=>(string)typeof(UIWorkspaceTooltipView)
            .GetMethod("LinkifyGlossary",Hidden).Invoke(tooltip,new object[]{text});
        static void ProductionGlossary(UIWorkspaceTooltipView tooltip)=>typeof(UIWorkspaceTooltipView).GetField("glossary",Hidden)
            .SetValue(tooltip,AssetDatabase.LoadAssetAtPath<CombatTooltipGlossary>(Assets+"UI/CombatTooltipGlossary.asset"));

        [TestCase("Warrior","Wladimir_Test",3,75)]
        [TestCase("Scout","Jerzy",3,75)]
        [TestCase("Mage","Mage",3,75)]
        [TestCase("Paladin","Paladin",2,40)]
        public void EveryClassTreeNameResolvesItsRealNodeAndIconAtUnlearnedAndMaximumRank(string cls,string definition,int trees,int count)
        {
            var hero=Hero(cls,definition);var go=new GameObject("Talent audit "+cls);
            var report=new List<string>();
            try
            {
                var tooltip=go.AddComponent<UIWorkspaceTooltipView>();ProductionGlossary(tooltip);
                Assert.That(hero.ClassData.SkillTrees.Count,Is.EqualTo(trees));
                var nodes=hero.ClassData.SkillTrees.SelectMany(t=>t.Nodes).ToArray();Assert.That(nodes.Length,Is.EqualTo(count));
                foreach(var node in nodes)
                {
                    var text=Final(tooltip,tooltip.LinkTalentReferences(node.DisplayName+". "+node.DisplayName+".",hero));
                    Assert.That(text,Does.Contain("<link=\""+node.Id+"\""),node.Id);
                    Assert.That(text,Does.Not.Contain("catacombs.skeleton_slayer.retaliation"),node.Id);
                    foreach(var rank in new[]{0,node.EffectiveMaxRanks})
                    {
                        hero.SkillTreeProgress.Clear();
                        if(rank>0)hero.SkillTreeProgress.Add(new SkillTreeProgress{TreeId=hero.ClassData.SkillTrees.Single(t=>t.Nodes.Contains(node)).Id,
                            NodeInvestments=new(){new SkillTreeNodeInvestment{NodeId=node.Id,Ranks=rank}}});
                        var entry=Resolve(tooltip,node.Id);
                        Assert.That(entry.Title,Is.EqualTo(node.DisplayName));Assert.That(entry.Icon,Is.SameAs(node.Icon??node.GrantedSkill?.Icon));
                        Assert.That(entry.Icon,Is.Not.Null,node.Id);Assert.That(entry.Description,Is.Not.Empty,node.Id);
                        Assert.That(entry.Description,Does.Not.Contain("{X").And.Not.Contain("{furia}"),node.Id);
                        if(node.GrantedSkill==null)Assert.That(entry.Description,Does.Contain(rank==0?"Następne:":"Aktualne:"),node.Id);
                        report.Add(node.Id+"|"+rank+"|"+entry.Title+"|PASS");
                    }
                }
                var dir=Path.Combine(Application.dataPath,"../../LocalHandoffs/skill-tree-reference-audit-20261102");Directory.CreateDirectory(dir);
                File.WriteAllLines(Path.Combine(dir,cls+"-references.txt"),report);
            }
            finally{Object.DestroyImmediate(go);}
        }

        [TestCase("Warrior","Wladimir_Test","Rozkazem Unicestwienia","node.warrior.commander.destruction")]
        [TestCase("Warrior","Wladimir_Test","Młyńca","node.warrior.brawler.whirlwind")]
        [TestCase("Warrior","Wladimir_Test","Pragnienia Śmierci","node.warrior.brawler.death_wish")]
        [TestCase("Warrior","Wladimir_Test","Gniewny Odwet","node.warrior.commander.furious_rising")]
        [TestCase("Scout","Jerzy","Rykoszetu","node.scout.assassin.ricochet")]
        [TestCase("Scout","Jerzy","Szybkich Ciosów","node.scout.blade_dancer.quick_strikes")]
        [TestCase("Mage","Mage","Ognistej Aury","node.mage.fire.fire_aura")]
        [TestCase("Mage","Mage","Przejęcia Mocy","node.mage.warlock.sacrifice")]
        [TestCase("Paladin","Paladin","Pola Osądu","node.paladin.crusader.judgement_field")]
        public void InflectedNamesLinkToClassTalent(string cls,string definition,string term,string id)
        {
            var go=new GameObject("Inflection audit");try
            {
                var tooltip=go.AddComponent<UIWorkspaceTooltipView>();ProductionGlossary(tooltip);
                var text=Final(tooltip,tooltip.LinkTalentReferences(term,Hero(cls,definition)));
                Assert.That(text,Does.Contain("<link=\""+id+"\""));Assert.That(text,Does.Not.Contain("catacombs.skeleton_slayer.retaliation"));
            }finally{Object.DestroyImmediate(go);}
        }

        [Test]
        public void SwitchingClassesReplacesAmbiguousTalentAndLowercaseStatusKeepsItsMeaning()
        {
            var go=new GameObject("Class context");try
            {
                var tooltip=go.AddComponent<UIWorkspaceTooltipView>();ProductionGlossary(tooltip);
                var warrior=Hero("Warrior","Wladimir_Test");var scout=Hero("Scout","Jerzy");
                var armored=warrior.ClassData.SkillTrees.SelectMany(t=>t.Nodes).Single(n=>n.DisplayName=="Przewidywanie");
                var assassin=scout.ClassData.SkillTrees.SelectMany(t=>t.Nodes).Single(n=>n.DisplayName=="Przewidywanie");
                Assert.That(Final(tooltip,tooltip.LinkTalentReferences("Przewidywanie",warrior)),Does.Contain(armored.Id));
                Assert.That(Final(tooltip,tooltip.LinkTalentReferences("Przewidywanie",scout)),Does.Contain(assassin.Id).And.Not.Contain(armored.Id));
                tooltip.LinkTalentReferences("",warrior);
                Assert.That(Final(tooltip,"unieruchomienie"),Does.Contain("<link=\"immobilize\"").And.Not.Contain("node.warrior"));
                Assert.That(Final(tooltip,"Unieruchomienie"),Does.Contain("node.warrior.commander.immobilize"));
                Assert.That(Final(tooltip,"Odwet"),Does.Contain("catacombs.skeleton_slayer.retaliation"));
                tooltip.LinkTalentReferences("",Hero("Paladin","Paladin"));
                Assert.That(Final(tooltip,"Leczenie krytyczne"),Does.Contain("<link=\"healing\""));
            }finally{Object.DestroyImmediate(go);}
        }
    }
}
