using System.Collections;
using System.Linq;
using Godmist.Systems.Characters;
using Godmist.Views.UI;
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
        public IEnumerator ScoutBothTierFiveRowsCanBeInspectedAndBoughtAfterTwentyPoints()
        {
            yield return new EnterPlayMode();
            var root=Workspace();
            try
            {
                var character=Jerzy();character.AvailableSkillPoints=42;
                var session=Session(character);var progression=new SkillTreeProgressionSystem();
                var view=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),root.transform).GetComponent<SkillTreeProgressionPanelView>();
                for(int index=0;index<2;index++)
                {
                    var tree=character.ClassData.SkillTrees[index];
                    for(int point=0;point<20;point++)
                    {
                        var next=tree.Nodes.First(n=>n.Tier<5&&string.IsNullOrEmpty(progression.GetInvestmentBlockReason(character,tree.Id,n.Id)));
                        Assert.That(progression.TryInvest(character,tree.Id,next.Id),Is.True);
                    }
                    view.Open(session,character.CharacterId);
                    ((Transform)Field(view,"treeTabRoot")).GetComponentsInChildren<Button>()[index].onClick.Invoke();
                    foreach(var node in tree.Nodes.Where(n=>n.Tier==5))
                    {
                        var card=Card(view,node.Id);Assert.That(card,Is.Not.Null);card.GetComponent<Button>().onClick.Invoke();
                        Assert.That(((TMP_Text)Field(view,"detailsTitle")).text,Is.EqualTo(node.DisplayName));Assert.That(node.Icon,Is.Not.Null);
                    }
                    var buy=tree.Nodes.First(n=>n.Tier==5&&n.Requirements.Count==0);
                    Card(view,buy.Id).GetComponent<Button>().onClick.Invoke();
                    var invest=(Button)Field(view,"investButton");Assert.That(invest.interactable,Is.True);invest.onClick.Invoke();
                    Assert.That(SkillTreeProgressionSystem.GetInvestedRanks(character,tree.Id),Is.EqualTo(21));
                }
                Assert.That(character.AvailableSkillPoints,Is.Zero);yield return null;Canvas.ForceUpdateCanvases();
            }
            finally{Object.DestroyImmediate(root);}
            yield return new ExitPlayMode();
        }
    }
}
