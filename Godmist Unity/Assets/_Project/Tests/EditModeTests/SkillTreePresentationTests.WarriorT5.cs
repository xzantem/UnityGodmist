using System.Collections;
using System.Linq;
using Godmist.Data.Characters;
using Godmist.Runtime.Characters;
using Godmist.Systems.Characters;
using Godmist.Views.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public partial class SkillTreePresentationTests
    {
        [UnityTest]
        public IEnumerator WarriorFifthRowsAreInspectableAndPurchaseUsesTheExistingGate()
        {
            yield return new EnterPlayMode();
            var root=Workspace();
            try
            {
                var definition=AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Wladimir_Test.asset");
                var character=new CharacterInstanceFactory().Create(definition);
                new CharacterProgressionSystem().TrySetLevel(character,21,out _);
                var session=Session(character);var tree=character.ClassData.SkillTrees[0];
                var progression=new SkillTreeProgressionSystem();
                for(var point=0;point<20;point++)
                {
                    var next=tree.Nodes.First(n=>n.Tier<=4 && string.IsNullOrEmpty(progression.GetInvestmentBlockReason(character,tree.Id,n.Id)));
                    Assert.That(progression.TryInvest(character,tree.Id,next.Id),Is.True);
                }
                Assert.That(character.AvailableSkillPoints,Is.EqualTo(1));
                var view=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),root.transform).GetComponent<SkillTreeProgressionPanelView>();
                view.Open(session,character.CharacterId);
                foreach(var node in tree.Nodes.Where(n=>n.Tier==5))
                {
                    var card=Card(view,node.Id);Assert.That(card,Is.Not.Null,node.Id);
                    card.GetComponent<Button>().onClick.Invoke();
                    Assert.That(((TMP_Text)Field(view,"detailsTitle")).text,Is.EqualTo(node.DisplayName));
                }
                var duel=tree.Nodes.Single(n=>n.Id=="node.warrior.commander.t5_3");
                Card(view,duel.Id).GetComponent<Button>().onClick.Invoke();
                var invest=(Button)Field(view,"investButton");Assert.That(invest.interactable,Is.True);
                invest.onClick.Invoke();Assert.That(character.AvailableSkillPoints,Is.Zero);
                Assert.That(SkillTreeProgressionSystem.GetInvestedRanks(character,tree.Id),Is.EqualTo(21));
                var tabs=(Transform)Field(view,"treeTabRoot");tabs.GetComponentsInChildren<Button>().Last().onClick.Invoke();
                var second=character.ClassData.SkillTrees[1];
                foreach(var node in second.Nodes.Where(n=>n.Tier==5))
                {
                    Card(view,node.Id).GetComponent<Button>().onClick.Invoke();
                    Assert.That(((TMP_Text)Field(view,"detailsTitle")).text,Is.EqualTo(node.DisplayName));
                    Assert.That(((Button)Field(view,"investButton")).interactable,Is.False);
                }
                Assert.That(character.AvailableSkillPoints,Is.Zero);
                yield return null;yield return null;Canvas.ForceUpdateCanvases();
                var scroll=(ScrollRect)Field(view,"treeScroll");
                Assert.That(scroll.verticalScrollbar.gameObject.activeSelf,
                    Is.EqualTo(scroll.content.rect.height>scroll.viewport.rect.height+.01f));
            }
            finally{Object.DestroyImmediate(root);}
            yield return new ExitPlayMode();
        }
    }
}
