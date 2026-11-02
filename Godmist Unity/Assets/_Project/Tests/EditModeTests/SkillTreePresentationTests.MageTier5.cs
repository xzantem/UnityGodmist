using System.Collections;
using System.Linq;
using Godmist.Data.Characters;
using Godmist.Systems.Characters;
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
        [UnityTest]
        public IEnumerator MageTierFiveCanBeInspectedPurchasedAndEquippedInBothTrees()
        {
            yield return new EnterPlayMode();
            var root=Workspace();
            try
            {
                foreach(var treeId in new[]{"tree.mage.warlock","tree.mage.fire"})
                {
                    var c=new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Mage.asset"));
                    c.AvailableSkillPoints=40;
                    var tree=c.ClassData.SkillTrees.Single(t=>t.Id==treeId);
                    var session=Session(c);var progression=new SkillTreeProgressionSystem();
                    var view=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),root.transform).GetComponent<SkillTreeProgressionPanelView>();
                    view.Open(session,c.CharacterId);
                    ((Transform)Field(view,"treeTabRoot")).GetComponentsInChildren<Button>()[c.ClassData.SkillTrees.IndexOf(tree)].onClick.Invoke();
                    Assert.That(view.GetComponentsInChildren<SkillTreeNodeView>().Length,Is.EqualTo(25));
                    foreach(var node in tree.Nodes.Where(n=>n.Tier==5))
                    {
                        Card(view,node.Id).GetComponent<Button>().onClick.Invoke();
                        var text=((TMP_Text)Field(view,"detailsBody")).text;
                        Assert.That(text,Is.Not.Empty);Assert.That(text,Does.Not.Contain("Ranga 1:"));
                        Assert.That(text,Does.Contain(node.GrantedSkill==null?"Następne:":"Koszt:"));
                        Assert.That((bool)Field(Card(view,node.Id),"activeShape"),Is.EqualTo(node.GrantedSkill!=null));
                    }
                    for(int i=0;i<20;i++)
                    {
                        var node=tree.Nodes.First(n=>n.Tier<5&&string.IsNullOrEmpty(progression.GetInvestmentBlockReason(c,tree.Id,n.Id)));
                        Assert.That(progression.TryInvest(c,tree.Id,node.Id),Is.True);
                    }
                    session.NotifyCharacterProgressionChanged(c.CharacterId);
                    var active=tree.Nodes.Single(n=>n.Tier==5&&n.GrantedSkill!=null);
                    Card(view,active.Id).GetComponent<Button>().onClick.Invoke();
                    var buy=(Button)Field(view,"investButton");Assert.That(buy.interactable,Is.True);buy.onClick.Invoke();
                    Assert.That(c.GetKnownSkills(),Has.Member(active.GrantedSkill));
                    var basic=c.GetActiveSkills().First().SkillId;
                    Assert.That(c.ActiveSkillLoadout.TryInitialize(c.GetKnownSkills().Select(s=>s.SkillId).ToList(),basic,new[]{basic,active.GrantedSkill.SkillId}),Is.True);
                    Assert.That(c.GetActiveSkills(),Has.Member(active.GrantedSkill));
                    Object.DestroyImmediate(view.gameObject);
                }
                yield return null;
            }
            finally{Object.DestroyImmediate(root);}
            yield return new ExitPlayMode();
        }
    }
}
