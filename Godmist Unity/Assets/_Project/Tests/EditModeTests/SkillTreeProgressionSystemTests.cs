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
