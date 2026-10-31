using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;

namespace Godmist.Tests.EditMode
{
    public class SkillTreePassiveTooltipContentTests
    {
        static void Set(object owner, string field, object value) => owner.GetType()
            .GetField("<" + field + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
        static string Plain(string text) => Regex.Replace(text, "<[^>]*>", "");
        static SkillTreeNodeDefinition Node(string text, int ranks = 2)
        {
            var node = new SkillTreeNodeDefinition();
            Set(node, "Description", text); Set(node, "MaxRanks", ranks);
            return node;
        }

        [Test]
        public void AllPlayablePassiveNodesRenderEveryRankWithoutInternalNotation()
        {
            var trees = AssetDatabase.FindAssets("t:CharacterClassData")
                .Select(guid => AssetDatabase.LoadAssetAtPath<CharacterClassData>(AssetDatabase.GUIDToAssetPath(guid)))
                .SelectMany(character => character.SkillTrees).Where(tree => tree != null).Distinct().OrderBy(tree => tree.Id).ToArray();
            Assert.That(trees.Length, Is.EqualTo(8));
            var report = new StringBuilder("# Tooltipy pasywek — wszystkie rangi\n\n");
            var count = 0;
            foreach (var tree in trees)
            {
                report.AppendLine("## " + tree.DisplayName);
                foreach (var node in tree.Nodes.Where(node => node.GrantedSkill == null))
                {
                    count++;
                    var authored = node.Description;
                    report.AppendLine("\n### " + node.DisplayName);
                    for (var rank = 0; rank <= node.EffectiveMaxRanks; rank++)
                    {
                        var body = Plain(SkillTreeNodeView.BuildTooltip(node, rank, ""));
                        var context = tree.Id + "/" + node.Id + " rank=" + rank;
                        Assert.That(body, Is.Not.Empty, context);
                        foreach (var technical in new[] { "[p]", "DoT", "przedmitygacyj", "zasięg bezwzględny", "(max ", "etc.", "surowca", "losowań", "pokoleń kopii" })
                            Assert.That(body, Does.Not.Contain(technical), context);
                        Assert.That(body, Does.Not.Match(@"\[\d+%\]"), context);
                        Assert.That(body, Does.Contain("Ranga: " + rank + "/" + node.EffectiveMaxRanks), context);
                        report.AppendLine("\n**Ranga " + rank + "**\n\n" + body + "\n");
                    }
                    Assert.That(node.Description, Is.EqualTo(authored), "Presentation must not mutate authored data.");
                }
            }
            Assert.That(count, Is.EqualTo(133));
            Directory.CreateDirectory("out");
            File.WriteAllText("out/passive-tooltip-catalog-rendered.md", report.ToString());
        }

        [Test]
        public void RankComparisonPreservesUnitsInSeparateFullDescriptions()
        {
            var node = Node("Przy ataku obrażenia +10/20/30%, przez 2/3/4 tury. Maksymalnie 5 kumulacji.", 3);
            var body = Plain(SkillTreeNodeView.BuildDetails(node, 1));
            Assert.That(body, Does.Contain("Aktualne:").And.Contain("Następne:").And.Contain("+10%").And.Contain("+20%").And.Contain("2 tury").And.Contain("3 tury"));
            Assert.That(Regex.Matches(body, "Przy ataku").Count, Is.EqualTo(2));
            Assert.That(Regex.Matches(body, "Maksymalnie 5 kumulacji").Count, Is.EqualTo(2));
            Assert.That(body, Does.Not.Contain("30%").And.Not.Contain("4 tury"));
        }

        [Test]
        public void FixedDescriptionHasCurrentAndNextRankSections()
        {
            var body = Plain(SkillTreeNodeView.BuildDetails(Node("Raz na turę odnawia sztylet."), 1));
            Assert.That(Regex.Matches(body, "Raz na turę").Count, Is.EqualTo(2));
        }

        [Test]
        public void AuthoredConditionAndActivationAreNotRepeated()
        {
            var node = Node("Przy ataku, gdy cel krwawi, obrażenia +10/20%.");
            var passive = new PassiveEffectDefinition();
            Set(passive.Tooltip, "ConditionText", "gdy cel krwawi");
            Set(passive.Tooltip, "TriggerText", "przy ataku");
            node.PassiveEffects.Add(passive); node.PassiveEffects.Add(passive);
            var body = Plain(SkillTreeNodeView.BuildDetails(node, 1));
            Assert.That(body, Does.Not.Contain("Warunek:").And.Not.Contain("Aktywacja:"));
            Set(passive.Tooltip, "ConditionText", "tylko w zwarciu");
            body = Plain(SkillTreeNodeView.BuildDetails(node, 1));
            Assert.That(Regex.Matches(body, "Warunek: tylko w zwarciu").Count, Is.EqualTo(1));
        }

        [Test]
        public void EveryPassiveTriggerHasPlayerFacingText()
        {
            foreach (PassiveEffectTriggerType type in Enum.GetValues(typeof(PassiveEffectTriggerType)))
            {
                var trigger = new PassiveEffectTriggerDefinition(); Set(trigger, "Type", type);
                var passive = new PassiveEffectDefinition(); passive.Triggers.Add(trigger);
                var node = Node("Premia +10/20%."); node.PassiveEffects.Add(passive);
                var body = Plain(SkillTreeNodeView.BuildDetails(node, 1));
                Assert.That(body, Does.Not.Contain(type.ToString()), type.ToString());
                Assert.That(body.Contains("Aktywacja:"), Is.EqualTo(type != PassiveEffectTriggerType.AlwaysActive), type.ToString());
            }
        }
    }
}
