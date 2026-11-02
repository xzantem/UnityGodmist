using System.Collections;
using System.Linq;
using Godmist.Data.Characters;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Views.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public partial class SkillTreePresentationTests
    {
        [UnityTest]
        public IEnumerator ManAtArmsThirdTabShowsFullGraphAndUsesExistingInvestmentGate()
        {
            yield return new EnterPlayMode();
            var root=Workspace();
            try
            {
                var definition=AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Wladimir_Test.asset");
                var character=new CharacterInstanceFactory().Create(definition);
                new CharacterProgressionSystem().TrySetLevel(character,21,out _);
                var session=Session(character);var tree=character.ClassData.SkillTrees.Single(t=>t.Id==WarriorCombatSystem.ManAtArms);
                var progression=new SkillTreeProgressionSystem();
                for(int point=0;point<20;point++)
                {
                    var next=tree.Nodes.First(n=>n.Tier<5&&string.IsNullOrEmpty(progression.GetInvestmentBlockReason(character,tree.Id,n.Id)));
                    Assert.That(progression.TryInvest(character,tree.Id,next.Id),Is.True);
                }
                var view=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),root.transform).GetComponent<SkillTreeProgressionPanelView>();
                view.Open(session,character.CharacterId);
                var tabs=(Transform)Field(view,"treeTabRoot");
                Assert.That(tabs.GetComponentsInChildren<Button>().Length,Is.EqualTo(3));
                tabs.GetComponentsInChildren<Button>()[2].onClick.Invoke();
                foreach(var n in tree.Nodes)
                {
                    var card=Card(view,n.Id);Assert.That(card,Is.Not.Null,n.Id);
                    card.GetComponent<Button>().onClick.Invoke();
                    Assert.That(((TMP_Text)Field(view,"detailsTitle")).text,Is.EqualTo(n.DisplayName));
                    Assert.That(n.Icon,Is.Not.Null);
                }
                Card(view,"node.warrior.man_at_arms.t5_4").GetComponent<Button>().onClick.Invoke();
                var invest=(Button)Field(view,"investButton");Assert.That(invest.interactable,Is.True);
                invest.onClick.Invoke();
                Assert.That(character.AvailableSkillPoints,Is.Zero);
                Assert.That(SkillTreeProgressionSystem.GetInvestedRanks(character,tree.Id),Is.EqualTo(21));
                Assert.That(SkillTreeProgressionSystem.GetInvestedRanks(character,WarriorCombatSystem.Commander),Is.Zero);
                Assert.That(SkillTreeProgressionSystem.GetInvestedRanks(character,WarriorCombatSystem.Brawler),Is.Zero);
                yield return null;Canvas.ForceUpdateCanvases();
                var scroll=(ScrollRect)Field(view,"treeScroll");
                Assert.That(scroll.content.rect.width,Is.LessThanOrEqualTo(scroll.viewport.rect.width+1));
            }
            finally{Object.DestroyImmediate(root);}
            yield return new ExitPlayMode();
        }
    }
}
