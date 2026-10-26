using System.Collections.Generic;
using System.Reflection;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Systems.Characters;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class SkillTreeProgressionSystemTests
    {
        [Test]
        public void TryInvest_RequiresTierThresholdAndPrerequisiteRanks()
        {
            var first = Node("node.first", 1, 2);
            var second = Node("node.second", 1, 1, new SkillTreeNodeRequirement());
            Set(second.Requirements[0], "NodeId", "node.first");
            Set(second.Requirements[0], "MinimumRanks", 2);
            var character = Character(Tree(first, second), 12);
            var system = new SkillTreeProgressionSystem();

            Assert.That(system.TryInvest(character, "tree.test", "node.second"), Is.False);
            Assert.That(system.TryInvest(character, "tree.test", "node.first"), Is.True);
            Assert.That(system.TryInvest(character, "tree.test", "node.first"), Is.True);
            Assert.That(system.TryInvest(character, "tree.test", "node.first"), Is.False);
            Assert.That(system.TryInvest(character, "tree.test", "node.second"), Is.True);
        }

        [Test]
        public void TryInvest_RequiresAvailableSkillPoints()
        {
            var character = Character(
                Tree(Node("node.only", 1, 1)),
                points: 0);

            Assert.That(
                new SkillTreeProgressionSystem().TryInvest(
                    character,
                    "tree.test",
                    "node.only"),
                Is.False);
            Assert.That(character.SkillTreeProgress, Is.Empty);
        }

        [Test]
        public void TryInvest_NormalizesLegacyZeroMaxRanksToOne()
        {
            var character = Character(Tree(Node("node.legacy", 1, 0)), points: 2);
            var system = new SkillTreeProgressionSystem();

            Assert.That(
                system.TryInvest(character, "tree.test", "node.legacy"),
                Is.True);
            Assert.That(
                system.TryInvest(character, "tree.test", "node.legacy"),
                Is.False);
            Assert.That(
                character.SkillTreeProgress[0].NodeInvestments[0].Ranks,
                Is.EqualTo(1));
        }

        [Test]
        public void TryInvest_StopsAtFiftyPointsPerTree()
        {
            var nodes = new SkillTreeNodeDefinition[51];
            for (var index = 0; index < nodes.Length; index++)
            {
                nodes[index] = Node($"node.{index}", 1, 1);
            }

            var character = Character(Tree(nodes), points: 51);
            var system = new SkillTreeProgressionSystem();

            for (var index = 0; index < SkillTreeProgressionSystem.MaximumPointsPerTree; index++)
            {
                Assert.That(
                    system.TryInvest(character, "tree.test", $"node.{index}"),
                    Is.True,
                    $"Expected investment {index} to succeed.");
            }

            Assert.That(
                system.TryInvest(character, "tree.test", "node.50"),
                Is.False);
            Assert.That(character.AvailableSkillPoints, Is.EqualTo(1));
            Assert.That(
                character.SkillTreeProgress[0].NodeInvestments,
                Has.Count.EqualTo(SkillTreeProgressionSystem.MaximumPointsPerTree));
        }

        [Test]
        public void TryInvest_UnlocksTierBoundariesOnlyAtTenTwentyAndThirty()
        {
            var nodes = new SkillTreeNodeDefinition[34];
            for (var index = 0; index < 30; index++)
            {
                nodes[index] = Node($"node.feeder.{index}", 1, 1);
            }

            nodes[30] = Node("node.tier3", 3, 1);
            nodes[31] = Node("node.tier5", 5, 1);
            nodes[32] = Node("node.tier7", 7, 1);
            nodes[33] = Node("node.unused", 1, 1);

            var character = Character(Tree(nodes), points: 40);
            var system = new SkillTreeProgressionSystem();

            for (var index = 0; index < 9; index++)
            {
                Assert.That(
                    system.TryInvest(
                        character,
                        "tree.test",
                        $"node.feeder.{index}"),
                    Is.True);
            }

            Assert.That(
                system.TryInvest(character, "tree.test", "node.tier3"),
                Is.False);
            Assert.That(
                system.TryInvest(
                    character,
                    "tree.test",
                    "node.feeder.9"),
                Is.True);
            Assert.That(
                system.TryInvest(character, "tree.test", "node.tier3"),
                Is.True);

            for (var index = 10; index < 18; index++)
            {
                Assert.That(
                    system.TryInvest(
                        character,
                        "tree.test",
                        $"node.feeder.{index}"),
                    Is.True);
            }

            Assert.That(
                system.TryInvest(character, "tree.test", "node.tier5"),
                Is.False);
            Assert.That(
                system.TryInvest(
                    character,
                    "tree.test",
                    "node.feeder.18"),
                Is.True);
            Assert.That(
                system.TryInvest(character, "tree.test", "node.tier5"),
                Is.True);

            for (var index = 19; index < 27; index++)
            {
                Assert.That(
                    system.TryInvest(
                        character,
                        "tree.test",
                        $"node.feeder.{index}"),
                    Is.True);
            }

            Assert.That(
                system.TryInvest(character, "tree.test", "node.tier7"),
                Is.False);
            Assert.That(
                system.TryInvest(
                    character,
                    "tree.test",
                    "node.feeder.27"),
                Is.True);
            Assert.That(
                system.TryInvest(character, "tree.test", "node.tier7"),
                Is.True);
        }

        [Test]
        public void TryInvest_KeepsTreeBudgetsIndependent()
        {
            var firstTreeNodes = new SkillTreeNodeDefinition[51];
            for (var index = 0; index < firstTreeNodes.Length; index++)
            {
                firstTreeNodes[index] = Node($"node.first.{index}", 1, 1);
            }

            var firstTree = Tree(firstTreeNodes);
            var secondTree = Tree(Node("node.second", 1, 1));
            Set(secondTree, "Id", "tree.second");
            var character = Character(firstTree, points: 51);
            character.ClassData.SkillTrees.Add(secondTree);
            var system = new SkillTreeProgressionSystem();

            for (var index = 0; index < SkillTreeProgressionSystem.MaximumPointsPerTree; index++)
            {
                Assert.That(
                    system.TryInvest(
                        character,
                        "tree.test",
                        $"node.first.{index}"),
                    Is.True);
            }

            Assert.That(
                system.TryInvest(character, "tree.test", "node.first.50"),
                Is.False);
            Assert.That(
                system.TryInvest(character, "tree.second", "node.second"),
                Is.True);
            Assert.That(character.AvailableSkillPoints, Is.EqualTo(0));
            Assert.That(character.SkillTreeProgress, Has.Count.EqualTo(2));
        }

        [Test]
        public void GetRequiredRanksForTier_UsesConfirmedThresholds()
        {
            Assert.That(SkillTreeProgressionSystem.GetRequiredRanksForTier(2), Is.EqualTo(0));
            Assert.That(SkillTreeProgressionSystem.GetRequiredRanksForTier(3), Is.EqualTo(10));
            Assert.That(SkillTreeProgressionSystem.GetRequiredRanksForTier(5), Is.EqualTo(20));
            Assert.That(SkillTreeProgressionSystem.GetRequiredRanksForTier(7), Is.EqualTo(30));
        }

        [Test]
        public void GetInvestmentBlockReason_DescribesTierPrerequisiteRankAndPointLocks()
        {
            var prerequisite = Node("node.prerequisite", 1, 2);
            var dependent = Node("node.dependent", 3, 1, new SkillTreeNodeRequirement());
            Set(dependent.Requirements[0], "NodeId", "node.prerequisite");
            Set(dependent.Requirements[0], "MinimumRanks", 2);
            var character = Character(Tree(prerequisite, dependent), points: 1);
            var system = new SkillTreeProgressionSystem();

            Assert.That(
                system.GetInvestmentBlockReason(character, "tree.test", "node.dependent"),
                Does.Contain("10 pkt"));

            for (var index = 0; index < 10; index++)
            {
                character.SkillTreeProgress ??= new List<SkillTreeProgress>();
                var progress = character.SkillTreeProgress.Count == 0
                    ? new SkillTreeProgress { TreeId = "tree.test" }
                    : character.SkillTreeProgress[0];
                if (character.SkillTreeProgress.Count == 0) character.SkillTreeProgress.Add(progress);
                progress.NodeInvestments.Add(new SkillTreeNodeInvestment { NodeId = $"filler.{index}", Ranks = 1 });
            }

            Assert.That(
                system.GetInvestmentBlockReason(character, "tree.test", "node.dependent"),
                Does.Contain("node.prerequisite"));

            character.AvailableSkillPoints = 0;
            Assert.That(
                system.GetInvestmentBlockReason(character, "tree.test", "node.prerequisite"),
                Does.Contain("Brak dostępnych"));
        }

        [TestCase("tree.scout.assassin", "% szansy krytycznej", "obrażeń wręcz")]
        [TestCase("tree.scout.blade_dancer", "zachowanie sztyletu", "siły zatrucia")]
        [TestCase("tree.warrior.commander", "% szansy krytycznej", "nieuchronne")]
        [TestCase("tree.warrior.brawler", "wściekłości", "HP")]
        public void GlobalPassiveSummary_ShowsBothCombatEffects(string id, string first, string second)
        {
            var tree = Tree();
            try
            {
                Set(tree, "Id", id);
                foreach (var points in new[] { 0, 10, 19 })
                {
                    var summary = Godmist.Views.UI.SkillTreeProgressionPanelView.GetGlobalPassiveSummary(tree, points);
                    Assert.That(summary, Does.Contain(first).And.Contain(second));
                    Assert.That(summary, Does.Not.Contain("brak efektu"));
                }
                Assert.That(Godmist.Views.UI.SkillTreeProgressionPanelView.GetGlobalPassiveSummary(tree, 10),
                    Is.Not.EqualTo(Godmist.Views.UI.SkillTreeProgressionPanelView.GetGlobalPassiveSummary(tree, 19)));
            }
            finally { Object.DestroyImmediate(tree); }
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator OpenProgressionViews_RefreshAfterInvestment_AndUnsubscribeOnClose()
        {
            yield return new UnityEngine.TestTools.EnterPlayMode();
            var node = Node("node.first", 1, 1);
            var grantedSkill = ScriptableObject.CreateInstance<SkillData>();
            Set(grantedSkill, "SkillId", "skill.ui.unlock");
            Set(node, "GrantedSkill", grantedSkill);
            var tree = Tree(node);
            var character = Character(tree, 2);
            character.CharacterId = "ui.test";
            character.Level = 1;
            character.Stats = new CharacterStats();
            character.Resistances = new ResistanceSet();
            var session = new Godmist.Runtime.Game.GameSession();
            session.TryRegisterCharacter(character);
            session.TryAddCharacterToActiveParty(character.CharacterId);
            var objects = new List<GameObject>();
            try
            {
                var workspace = new GameObject("UI test workspace", typeof(RectTransform));
                objects.Add(workspace);
                var bounds = (RectTransform)workspace.transform;
                bounds.sizeDelta = new Vector2(1920, 1080);
                workspace.AddComponent<Godmist.Views.UI.Workspace.UIWorkspaceWindowManager>();
                GameObject View(string name)
                {
                    var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_" + name + " Variant.prefab");
                    Assert.That(prefab, Is.Not.Null);
                    var instance = Object.Instantiate(prefab, workspace.transform);
                    objects.Add(instance);
                    return instance;
                }
                var info = View("CharacterInfoWindow").GetComponent<Godmist.Views.UI.Workspace.CharacterInfoWindowView>();
                var loadout = View("SkillLoadoutWindow").GetComponent<Godmist.Views.UI.SkillLoadoutModalView>();
                var panel = View("SkillTreeWindow").GetComponent<Godmist.Views.UI.SkillTreeProgressionPanelView>();
                info.gameObject.SetActive(true);
                info.Initialize(session, character.CharacterId);
                loadout.Open(session, character.CharacterId);
                ((RectTransform)panel.transform).anchoredPosition = new Vector2(1500, 1000);
                panel.Open(session, character.CharacterId);
                var corners = new Vector3[4];
                ((RectTransform)panel.transform).GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var local = bounds.InverseTransformPoint(corner);
                    Assert.That(local.x, Is.InRange(bounds.rect.xMin - .01f, bounds.rect.xMax + .01f));
                    Assert.That(local.y, Is.InRange(bounds.rect.yMin - .01f, bounds.rect.yMax + .01f));
                }
                var cardsField = typeof(Godmist.Views.UI.SkillLoadoutModalView).GetField("knownCards", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(((System.Collections.IList)cardsField.GetValue(loadout)).Count, Is.Zero);
                var textField = typeof(Godmist.Views.UI.Workspace.CharacterInfoWindowView).GetField("statsText", BindingFlags.Instance | BindingFlags.NonPublic);
                var stats = textField.GetValue(info);
                var textProperty = stats.GetType().GetProperty("text");
                textProperty.SetValue(stats, "stale");
                var titleField = typeof(Godmist.Views.UI.SkillLoadoutModalView).GetField("workspaceTitleText", BindingFlags.Instance | BindingFlags.NonPublic);
                var title = titleField.GetValue(loadout);
                textProperty.SetValue(title, "stale");
                var notifications = 0;
                session.CharacterProgressionChanged += _ => notifications++;
                var invest = typeof(Godmist.Views.UI.SkillTreeProgressionPanelView).GetMethod("TryInvest", BindingFlags.Instance | BindingFlags.NonPublic);
                invest.Invoke(panel, new object[] { tree, node });
                Assert.That(notifications, Is.EqualTo(1));
                Assert.That(((System.Collections.IList)cardsField.GetValue(loadout)).Count, Is.EqualTo(1));
                Assert.That(textProperty.GetValue(stats), Is.Not.EqualTo("stale"));
                Assert.That(textProperty.GetValue(title), Is.Not.EqualTo("stale"));
                invest.Invoke(panel, new object[] { tree, node });
                Assert.That(notifications, Is.EqualTo(1), "Failed investment must not publish a change.");
                loadout.Close();
                info.gameObject.SetActive(false);

                textProperty.SetValue(title, "closed");
                textProperty.SetValue(stats, "closed");
                session.NotifyCharacterProgressionChanged(character.CharacterId);
                Assert.That(textProperty.GetValue(title), Is.EqualTo("closed"));
                Assert.That(textProperty.GetValue(stats), Is.EqualTo("closed"));
            }
            finally
            {
                for (var i = objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(objects[i]);
                Object.DestroyImmediate(grantedSkill);
                Object.DestroyImmediate(character.ClassData);
                Object.DestroyImmediate(tree);
            }
            yield return new UnityEngine.TestTools.ExitPlayMode();
        }

        private static CharacterInstance Character(SkillTreeDefinition tree, int points)
        {
            var classData = ScriptableObject.CreateInstance<CharacterClassData>();
            Set(classData, "SkillTrees", new List<SkillTreeDefinition> { tree });
            return new CharacterInstance { ClassData = classData, AvailableSkillPoints = points };
        }

        private static SkillTreeDefinition Tree(params SkillTreeNodeDefinition[] nodes)
        {
            var tree = ScriptableObject.CreateInstance<SkillTreeDefinition>();
            Set(tree, "Id", "tree.test");
            Set(tree, "Nodes", new List<SkillTreeNodeDefinition>(nodes));
            return tree;
        }

        private static SkillTreeNodeDefinition Node(string id, int tier, int ranks,
            params SkillTreeNodeRequirement[] requirements)
        {
            var node = new SkillTreeNodeDefinition();
            Set(node, "Id", id); Set(node, "Tier", tier); Set(node, "MaxRanks", ranks);
            Set(node, "Requirements", new List<SkillTreeNodeRequirement>(requirements));
            return node;
        }

        private static void Set<T>(object target, string name, T value)
        {
            target.GetType().GetField($"<{name}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }
    }
}
