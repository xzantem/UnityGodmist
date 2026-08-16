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
            Assert.That(system.TryInvest(character, "tree.test", "node.second"), Is.True);
        }

        [Test]
        public void GetRequiredRanksForTier_UsesConfirmedThresholds()
        {
            Assert.That(SkillTreeProgressionSystem.GetRequiredRanksForTier(2), Is.EqualTo(0));
            Assert.That(SkillTreeProgressionSystem.GetRequiredRanksForTier(3), Is.EqualTo(10));
            Assert.That(SkillTreeProgressionSystem.GetRequiredRanksForTier(5), Is.EqualTo(20));
            Assert.That(SkillTreeProgressionSystem.GetRequiredRanksForTier(7), Is.EqualTo(30));
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
