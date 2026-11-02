using System.Collections;
using System.Linq;
using Godmist.Data.Skills;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Godmist.Tests.EditMode
{
    public partial class SkillTreePresentationTests
    {
        [Test]
        public void GuardianProductionTreeHasOnlyFiveTiersAndThreeFunctionalSkills()
        {
            var tree=Jerzy().ClassData.SkillTrees.Single(t=>t.Id==GuardianCombatSystem.Tree);
            Assert.That(tree.Nodes.Count,Is.EqualTo(25));Assert.That(tree.Nodes.Sum(n=>n.MaxRanks),Is.EqualTo(44));
            Assert.That(tree.Nodes.All(n=>n.Tier<=5),Is.True);
            Assert.That(tree.Nodes.Where(n=>n.GrantedSkill!=null).Select(n=>n.GrantedSkill.SkillId),Is.EquivalentTo(new[]{GuardianCombatSystem.Shield,GuardianCombatSystem.Taunt,GuardianCombatSystem.Counter}));
            Assert.That(tree.Nodes.Single(n=>n.DisplayName=="Refleks").Column,Is.EqualTo(2));
            Assert.That(tree.Nodes.Single(n=>n.DisplayName=="Czujność").Column,Is.EqualTo(1));
            foreach(var node in tree.Nodes)
            {
                Assert.That(node.Icon,Is.Not.Null);Assert.That(node.Icon.rect.width,Is.EqualTo(32));
                foreach(var req in node.Requirements)Assert.That(tree.Nodes.Any(n=>n.Id==req.NodeId),Is.True);
                if(node.GrantedSkill!=null)Assert.That(CombatDataValidator.ValidateSkill(node.GrantedSkill).Errors,Is.Empty);
            }
        }
        [UnityTest]
        public IEnumerator GuardianCanBeInspectedPurchasedAndEquippedThroughTierFive()
        {
            yield return new EnterPlayMode();var root=Workspace();
            try
            {
                var c=Jerzy();c.AvailableSkillPoints=40;var tree=c.ClassData.SkillTrees.Single(t=>t.Id==GuardianCombatSystem.Tree);
                var session=Session(c);var progression=new SkillTreeProgressionSystem();
                var view=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),root.transform).GetComponent<SkillTreeProgressionPanelView>();
                view.Open(session,c.CharacterId);var index=c.ClassData.SkillTrees.IndexOf(tree);
                ((Transform)Field(view,"treeTabRoot")).GetComponentsInChildren<Button>()[index].onClick.Invoke();
                Assert.That(view.GetComponentsInChildren<SkillTreeNodeView>().Length,Is.EqualTo(25));
                foreach(var node in tree.Nodes)
                {
                    Card(view,node.Id).GetComponent<Button>().onClick.Invoke();
                    var text=((TMP_Text)Field(view,"detailsBody")).text;
                    Assert.That(text,Is.Not.Empty);Assert.That(text,Does.Not.Contain("Ranga 1:"));
                    if(node.Id=="node.scout.guardian.t4_1")
                    {
                        var hoverRoot=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab"),root.transform);
                        var hover=SkillTreeNodeView.BuildTooltip(node,0,null,hoverRoot.GetComponent<UIWorkspaceTooltipView>(),c);
                        Assert.That(hover,Does.Contain("node.scout.guardian.t3_1"));
                        Assert.That(hover,Does.Not.Contain("catacombs.skeleton_slayer.retaliation"));
                    }
                    Assert.That((bool)Field(Card(view,node.Id),"activeShape"),Is.EqualTo(node.GrantedSkill!=null));
                    if(node.GrantedSkill==null)Assert.That(text,Does.Contain("Następne:"));
                    else Assert.That(text,Does.Contain("Koszt:"));
                }
                for(int i=0;i<20;i++)
                {
                    var node=tree.Nodes.First(n=>string.IsNullOrEmpty(progression.GetInvestmentBlockReason(c,tree.Id,n.Id)));
                    Assert.That(progression.TryInvest(c,tree.Id,node.Id),Is.True);
                }
                session.NotifyCharacterProgressionChanged(c.CharacterId);
                var counter=tree.Nodes.Single(n=>n.GrantedSkill?.SkillId==GuardianCombatSystem.Counter);
                Card(view,counter.Id).GetComponent<Button>().onClick.Invoke();
                var buy=(Button)Field(view,"investButton");Assert.That(buy.interactable,Is.True);buy.onClick.Invoke();
                Assert.That(c.AvailableSkillPoints,Is.EqualTo(19));
                Assert.That(c.GetKnownSkills(),Has.Member(counter.GrantedSkill));
                Assert.That(c.ActiveSkillLoadout.TryInitialize(c.GetKnownSkills().Select(s=>s.SkillId).ToList(),"skill.knife_toss",new[]{"skill.knife_toss",GuardianCombatSystem.Counter}),Is.True);
                Assert.That(c.GetActiveSkills(),Has.Member(counter.GrantedSkill));
                yield return null;
            }
            finally{Object.DestroyImmediate(root);}
            yield return new ExitPlayMode();
        }
    }
}
