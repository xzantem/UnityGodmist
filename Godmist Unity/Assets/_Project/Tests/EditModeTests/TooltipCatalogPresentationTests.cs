using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Effects;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class TooltipCatalogPresentationTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        static string Plain(string text) => Regex.Replace(text ?? "", "<[^>]*>", "");
        static string CombatBody(UIWorkspaceTooltipView view, SkillData skill) =>
            (string)typeof(UIWorkspaceTooltipView).GetMethod("BuildSkillDescription", Hidden).Invoke(view, new object[] { skill, null });
        static void Set(object target, string field, object value) => target.GetType().GetField(field, Hidden).SetValue(target, value);

        [Test]
        public void EntireSkillCatalogUsesOneMechanicsBlockAcrossCombatLoadoutAndSkillTree()
        {
            var node = new GameObject("Catalog tooltip audit", typeof(RectTransform), typeof(UIWorkspaceTooltipView));
            var report = new StringBuilder();
            var skills = AssetDatabase.FindAssets("t:SkillData", new[] { "Assets/_Project/ScriptableObjects" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(skill => skill != null).OrderBy(skill => skill.SkillId).ToArray();
            try
            {
                var view = node.GetComponent<UIWorkspaceTooltipView>();
                Assert.That(skills.Length, Is.GreaterThanOrEqualTo(92));
                foreach (var skill in skills)
                {
                    string combat;
                    try { combat = Plain(CombatBody(view, skill)); }
                    catch (Exception error) { throw new Exception("Failed tooltip: " + skill.SkillId, error); }
                    var loadout = Plain(view.BuildLoadoutSkillDetails(skill, null));
                    Assert.That(combat, Is.Not.Empty, skill.SkillId);
                    Assert.That(combat, Does.Not.Contain("Modyfikator ").And.Not.Contain("wartość statystyki")
                        .And.Not.Contain("addytywnie").And.Not.Contain("mnożnikowo"), skill.SkillId);
                    Assert.That(combat, Does.Not.Contain("Wywołuje dodatkowy efekt.").And.Not.Contain("Nakłada unikalne wzmocnienie."), skill.SkillId);
                    Assert.That(loadout, Does.Not.Contain("Wywołuje dodatkowy efekt."), skill.SkillId);
                    var lines = combat.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
                    Assert.That(lines.Distinct().Count(), Is.EqualTo(lines.Length), "Repeated line in " + skill.SkillId);
                    var mechanics = (IEnumerable<string>)typeof(UIWorkspaceTooltipView).GetMethod("BuildSkillMechanics", Hidden)
                        .Invoke(view, new object[] { skill, null });
                    foreach (var mechanic in mechanics.Where(text => !string.IsNullOrWhiteSpace(text)))
                    {
                        Assert.That(combat, Does.Contain(Plain(mechanic)), skill.SkillId + " combat");
                        Assert.That(loadout, Does.Contain(Plain(mechanic)), skill.SkillId + " loadout/tree");
                    }
                    report.AppendLine("## " + skill.SkillId + " — " + skill.DisplayName).AppendLine(combat)
                        .AppendLine(Plain(UIWorkspaceTooltipView.BuildLoadoutSkillCost(skill, null))).AppendLine();
                }
                var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../out/tooltip-catalog-rendered.md"));
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.WriteAllText(output, report.ToString());
                TestContext.WriteLine($"Audited {skills.Length} skills; rendered copy: {output}");
            }
            finally { Object.DestroyImmediate(node); }
        }

        [TestCase("SD_WarriorCut")]
        [TestCase("SD_MageEnergyOrb")]
        public void OrdinaryAttacksDescribeTheirDamageOnceWithoutGenericAuthoredIntro(string asset)
        {
            var skill = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/" + asset + ".asset");
            Assert.That(skill, Is.Not.Null);
            var node = new GameObject("Ordinary attack tooltip", typeof(RectTransform), typeof(UIWorkspaceTooltipView));
            try
            {
                var body = Plain(CombatBody(node.GetComponent<UIWorkspaceTooltipView>(), skill));
                Assert.That(body, Does.Not.Contain("Zadaje losowe obrażenia"));
                Assert.That(Regex.Matches(body, "Zadaje").Count, Is.EqualTo(1));
                Assert.That(Regex.Matches(body, "Cel:").Count, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(node); }
        }

        [Test]
        public void AllEffectTypesHaveConcretePresentationIncludingLegacyAndWarriorCommands()
        {
            var node = new GameObject("Effect coverage", typeof(RectTransform), typeof(UIWorkspaceTooltipView));
            var skill = ScriptableObject.CreateInstance<SkillData>();
            Set(skill, "<SkillId>k__BackingField", "skill.coverage");
            try
            {
                foreach (EffectType type in Enum.GetValues(typeof(EffectType)))
                {
                    var effect = new SkillEffectData(); Set(effect, "effectType", type);
                    Set(skill, "effects", new List<SkillEffectData> { effect });
                    var body = CombatBody(node.GetComponent<UIWorkspaceTooltipView>(), skill);
                    Assert.That(body, Is.Not.Empty.And.Not.Contain("Wywołuje dodatkowy efekt."), type.ToString());
                }
            }
            finally { Object.DestroyImmediate(node); Object.DestroyImmediate(skill); }
        }

        [Test]
        public void KnownStatusesHaveDescriptionsAndTypedOnHitDoesNotRepeatGenericUniqueBuffText()
        {
            var node = new GameObject("Status audit", typeof(RectTransform), typeof(UIWorkspaceTooltipView));
            try
            {
                var view = node.GetComponent<UIWorkspaceTooltipView>();
                foreach (var id in StatusEffectIds.KnownIds)
                {
                    var effect = new PassiveEffectRuntime { EffectId = id, RemainingDuration = 3 };
                    Assert.That(view.BuildPresentedStatusPayload(new CombatStatusPresentation(effect, CombatStatusKind.Status, 1)),
                        Is.Not.Empty.And.Not.Contain("Brak dodatkowego działania."), id);
                }
                var onHit = new PassiveEffectRuntime { EffectId = "test.onhit" };
                onHit.SetPayload(new UniqueCombatBuffPayload { BuffType = UniqueBuffType.OnHitStatus });
                onHit.SetPayload(new OnHitStatusPayload { StatusId = StatusEffectIds.Bleed, Chance = .6f, DurationTurns = 3 });
                var body = view.BuildPresentedStatusPayload(new CombatStatusPresentation(onHit, CombatStatusKind.Buff, 1));
                Assert.That(body, Does.Contain("60%").And.Not.Contain("unikalny efekt"));
            }
            finally { Object.DestroyImmediate(node); }
        }

        [Test]
        public void PassiveDescriptionsDoNotRepeatAlreadyStatedActivationButKeepMissingRules()
        {
            var definition = new PassiveEffectDefinition();
            Set(definition, "<Description>k__BackingField", "Przy ataku przywraca 5 punktów zdrowia.");
            var trigger = new PassiveEffectTriggerDefinition();
            Set(trigger, "<Type>k__BackingField", PassiveEffectTriggerType.OnAttack);
            definition.Triggers.Add(trigger);
            var formatter = typeof(UIWorkspaceTooltipView).GetMethod("AppendPassiveDescriptions", BindingFlags.Static | BindingFlags.NonPublic);
            var result = new StringBuilder();
            formatter.Invoke(null, new object[] { result, new[] { definition } });
            Assert.That(result.ToString(), Does.Contain("Przy ataku").And.Not.Contain("Aktywacja:"));
            Set(definition, "<Description>k__BackingField", "Przywraca 5 punktów zdrowia.");
            result.Clear(); formatter.Invoke(null, new object[] { result, new[] { definition } });
            Assert.That(result.ToString(), Does.Contain("Aktywacja: przy ataku"));
        }

        [Test]
        public void DirectionalSkillsKeepTheirSpecificGeometryWithoutAuthoredMechanicsBlock()
        {
            var node = new GameObject("Directional tooltip", typeof(RectTransform), typeof(UIWorkspaceTooltipView));
            try
            {
                var view = node.GetComponent<UIWorkspaceTooltipView>();
                var rays = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_MageEnergySplit.asset");
                Assert.That(CombatBody(view, rays), Does.Contain("trzy promienie: jeden prosty i dwa ukośne"));
                var pull = AssetDatabase.FindAssets("t:SkillData", new[] { "Assets/_Project/ScriptableObjects" })
                    .Select(guid => AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid)))
                    .Single(skill => skill.SkillId == "skill.paladin.grounding_wave");
                Assert.That(CombatBody(view, pull), Does.Contain("w kierunku środka wybranego obszaru"));
            }
            finally { Object.DestroyImmediate(node); }
        }

        [Test]
        public void EverySkillConditionHasPlayerFacingTextInsteadOfTechnicalEnumNames()
        {
            var node = new GameObject("Condition coverage", typeof(RectTransform), typeof(UIWorkspaceTooltipView));
            var skill = ScriptableObject.CreateInstance<SkillData>();
            Set(skill, "<SkillId>k__BackingField", "skill.condition.coverage");
            try
            {
                foreach (EffectConditionType type in Enum.GetValues(typeof(EffectConditionType)))
                {
                    var condition = new SkillEffectConditionData(); Set(condition, "conditionType", type);
                    Set(condition, "statusId", StatusEffectIds.Bleed);
                    var effect = new SkillEffectData(); Set(effect, "conditions", new List<SkillEffectConditionData> { condition });
                    Set(skill, "effects", new List<SkillEffectData> { effect });
                    Assert.That(CombatBody(node.GetComponent<UIWorkspaceTooltipView>(), skill), Does.Not.Contain(type.ToString()), type.ToString());
                }
            }
            finally { Object.DestroyImmediate(node); Object.DestroyImmediate(skill); }
        }
    }
}
