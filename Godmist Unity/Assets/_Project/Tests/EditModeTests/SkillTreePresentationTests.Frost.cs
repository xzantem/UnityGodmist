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
        [UnityTest] public IEnumerator FrostTreeCanBeInspectedPurchasedAndAllThreeSpellsEquipped()
        {
            yield return new EnterPlayMode();var root=Workspace();
            try
            {
                var c=new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Mage.asset"));
                c.AvailableSkillPoints=60;var tree=c.ClassData.SkillTrees.Single(t=>t.Id=="tree.mage.frost");var session=Session(c);
                var view=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),root.transform).GetComponent<SkillTreeProgressionPanelView>();view.Open(session,c.CharacterId);
                ((Transform)Field(view,"treeTabRoot")).GetComponentsInChildren<Button>()[2].onClick.Invoke();
                Assert.That(view.GetComponentsInChildren<SkillTreeNodeView>().Length,Is.EqualTo(25));
                foreach(var pair in new[]{("t4_1","t3_2"),("t4_2","t3_1")})
                {
                    var child=tree.Nodes.Single(n=>n.Id=="node.mage.frost."+pair.Item1);
                    var parent=tree.Nodes.Single(n=>n.Id=="node.mage.frost."+pair.Item2);
                    Assert.That(child.Column,Is.EqualTo(parent.Column),"Shield upgrades must align with their requirements");
                    Assert.That(child.Requirements.Single().NodeId,Is.EqualTo(parent.Id));
                }
                foreach(var node in tree.Nodes)
                {
                    Card(view,node.Id).GetComponent<Button>().onClick.Invoke();var text=((TMP_Text)Field(view,"detailsBody")).text;
                    Assert.That(text,Is.Not.Empty,node.Id);Assert.That(text,Does.Not.Contain("Ranga 1:"),node.Id);
                    Assert.That(text,Does.Contain(node.GrantedSkill==null?"Następne:":"Koszt:"),node.Id);
                    Assert.That((bool)Field(Card(view,node.Id),"activeShape"),Is.EqualTo(node.GrantedSkill!=null),node.Id);
                }
                var progression=new SkillTreeProgressionSystem();
                for(int i=0;i<44;i++)
                {
                    var node=tree.Nodes.First(n=>string.IsNullOrEmpty(progression.GetInvestmentBlockReason(c,tree.Id,n.Id)));
                    Assert.That(progression.TryInvest(c,tree.Id,node.Id),Is.True,node.Id);
                }
                session.NotifyCharacterProgressionChanged(c.CharacterId);
                var basic=c.GetActiveSkills().First().SkillId;var spells=tree.Nodes.Where(n=>n.GrantedSkill!=null).Select(n=>n.GrantedSkill).ToArray();
                Assert.That(c.ActiveSkillLoadout.TryInitialize(c.GetKnownSkills().Select(s=>s.SkillId).ToList(),basic,new[]{basic}.Concat(spells.Select(s=>s.SkillId)).ToArray()),Is.True);
                foreach(var spell in spells)Assert.That(c.GetActiveSkills(),Has.Member(spell));
                Object.DestroyImmediate(view.gameObject);yield return null;
            }
            finally{Object.DestroyImmediate(root);}
            yield return new ExitPlayMode();
        }
    }
}
