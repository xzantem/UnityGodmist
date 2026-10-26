using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Godmist.Data.Skills;
using Godmist.Runtime.Effects;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class CombatTooltipGlossaryTests
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;

        [Test]
        public void FallbackLookupCoversSlowAndClassResourceTerms()
        {
            var glossary=ScriptableObject.CreateInstance<CombatTooltipGlossary>();
            try
            {
                Assert.That(glossary.TryGet("slow",out var slow),Is.True);
                Assert.That(slow.Title,Is.EqualTo("Spowolnienie"));
                Assert.That(glossary.TryGet("stamina",out var stamina),Is.True);
                Assert.That(stamina.Id,Is.EqualTo("momentum"));
                Assert.That(glossary.TryGet("damage.physical",out var physical),Is.True);
                Assert.That(physical.Description,Does.Contain("obronę fizyczną"));
            }
            finally{Object.DestroyImmediate(glossary);}
        }

        [Test]
        public void AuthoredEntryWinsAndFallbackFillsItsPolishAliases()
        {
            var glossary=ScriptableObject.CreateInstance<CombatTooltipGlossary>();
            var authored=new CombatTooltipGlossary.Entry{Id="health",Title="Własne zdrowie",Description="Autorska reguła."};
            typeof(CombatTooltipGlossary).GetField("entries",Hidden).SetValue(glossary,new List<CombatTooltipGlossary.Entry>{authored});
            try
            {
                Assert.That(glossary.TryGet("health",out var result),Is.True);
                Assert.That(result,Is.SameAs(authored));
                var text=glossary.Linkify("Własne zdrowie i zdrowiu.");
                Assert.That(text,Does.Contain("<link=\"health\"><color=#7EDBFF>Własne zdrowie</color></link>"));
                Assert.That(text,Does.Not.Contain("<link=\"health\"><color=#7EDBFF>zdrowiu</color></link>"),
                    "Repeated mentions keep their text but link each glossary entry only once.");
                Assert.That(glossary.Linkify("zdrowiu."),Does.Contain("<link=\"health\"><color=#7EDBFF>zdrowiu</color></link>"),
                    "The fallback alias must still work when it is the first mention.");

                typeof(CombatTooltipGlossary).GetField("entries",Hidden).SetValue(glossary,new List<CombatTooltipGlossary.Entry>
                {
                    new CombatTooltipGlossary.Entry{Id="debuff",Title="Osłabienie"},
                    new CombatTooltipGlossary.Entry{Id="weakening",Title="Osłabienie"}
                });
                Assert.That(glossary.Linkify("Osłabienie."),Does.Contain("<link=\"weakening\">")
                    .And.Not.Contain("<link=\"debuff\">") );
            }
            finally{Object.DestroyImmediate(glossary);}
        }

        [Test]
        public void PolishWordsKeepSentencePunctuationAndAvoidTechnicalIds()
        {
            var text="Zdrowie. AP i skill.ap, a obrażenia fizyczne.";
            var linked=CombatTooltipGlossary.LinkifyFallback(text);
            Assert.That(linked,Does.Contain("<link=\"health\"><color=#7EDBFF>Zdrowie</color></link>.")
                .And.Contain("<link=\"ap\"><color=#7EDBFF>AP</color></link> i skill.ap,")
                .And.Contain("<link=\"damage.physical\"><color=#7EDBFF>obrażenia fizyczne</color></link>"));
            Assert.That(linked,Does.Not.Contain("skill.<link"));
            Assert.That(CombatTooltipGlossary.LinkifyFallback(linked),Is.EqualTo(linked));
        }

        [Test]
        public void NullSerializedGlossaryLinksStatusAndLoadoutSkillMechanics()
        {
            var node=new GameObject("glossary fallback tooltip");
            var tooltip=node.AddComponent<UIWorkspaceTooltipView>();
            var skill=ScriptableObject.CreateInstance<SkillData>();
            try
            {
                var resolver=typeof(UIWorkspaceTooltipView).GetMethod("TryGetGlossaryEntry",Hidden);
                var args=new object[]{"slow",null};
                Assert.That((bool)resolver.Invoke(tooltip,args),Is.True);
                Assert.That(((CombatTooltipGlossary.Entry)args[1]).Title,Is.EqualTo("Spowolnienie"));
                SetSkill(skill,"<SkillId>k__BackingField","skill.tooltip.test");
                SetSkill(skill,"<DisplayName>k__BackingField","Testowa umiejętność");
                SetSkill(skill,"<TooltipDescription>k__BackingField","Zadaje obrażenia fizyczne i kosztuje AP.");
                var body=tooltip.BuildLoadoutSkillDetails(skill,null);
                Assert.That(body,Does.Contain("<link=\"damage.physical\">").And.Contain("<link=\"ap\">") );
            }
            finally{Object.DestroyImmediate(skill);Object.DestroyImmediate(node);}
        }

        [Test]
        public void RuntimeStatusTitleKeepsTypedSkillLabelAndLinksStatusEntry()
        {
            var formatter=typeof(UIWorkspaceTooltipView).GetMethod("FormatStatusTitle",BindingFlags.Static|BindingFlags.NonPublic);
            var title=(string)formatter.Invoke(null,new object[]{"slow",null,"Umiejętność testowa"});
            Assert.That(title,Is.EqualTo("Umiejętność testowa"));
            title=(string)formatter.Invoke(null,new object[]{"slow","Spowolnienie",null});
            Assert.That(title,Is.EqualTo("<link=\"slow\"><color=#7EDBFF>Spowolnienie</color></link>"));
        }

        [Test]
        public void FallbackStatusCopyMatchesRuntimeTargetingLanguage()
        {
            var glossary=ScriptableObject.CreateInstance<CombatTooltipGlossary>();
            try
            {
                Assert.That(glossary.TryGet("paralysis",out var paralysis),Is.True);
                Assert.That(paralysis.Description,Does.Contain("Nie może być celem umiejętności")
                    .And.Contain("nie otrzymuje obrażeń"));
                Assert.That(glossary.TryGet("invisibility",out var invisibility),Is.True);
                Assert.That(invisibility.Description,Does.Contain("obszarowym celowaniem").And.Not.Contain("celowanie All"));
            }
            finally{Object.DestroyImmediate(glossary);}

            var appendStatusRule=typeof(UIWorkspaceTooltipView).GetMethod("AppendStatusRule",BindingFlags.Static|BindingFlags.NonPublic);
            var result=new StringBuilder();
            appendStatusRule.Invoke(null,new object[]{result,new PassiveEffectRuntime{EffectId="invisibility"}});
            Assert.That(result.ToString(),Does.Contain("obszarowym celowaniem").And.Not.Contain("celowanie All"));
        }

        [Test]
        public void NestedEntryCanExcludeItsOwnLinkAndKeepRelatedLinks()
        {
            var text=CombatTooltipGlossary.LinkifyFallback("Spowolnienie ogranicza szybkość i może wpływać na obrażenia.","slow");
            Assert.That(text,Does.Not.Contain("<link=\"slow\">")
                .And.Contain("<link=\"speed\">").And.Contain("<link=\"damage\">") );
        }

        [Test]
        public void NestedGlossaryBranchAllowsAtoBtoAWithoutSteadyHoverStorm()
        {
            var canvas=new GameObject("nested glossary canvas",typeof(RectTransform),typeof(Canvas));
            var rootObject=new GameObject("nested glossary root");
            rootObject.transform.SetParent(canvas.transform,false);
            var root=rootObject.AddComponent<UIWorkspaceTooltipView>();
            try
            {
                var create=typeof(UIWorkspaceTooltipView).GetMethod(
                    "CreateNestedTooltip",
                    Hidden,
                    binder:null,
                    types:new[]{typeof(CombatTooltipGlossary.Entry)},
                    modifiers:null);
                Assert.That(create,Is.Not.Null);
                var entryA=new CombatTooltipGlossary.Entry{Id="health",Title="Health",Description="A"};
                var entryB=new CombatTooltipGlossary.Entry{Id="mana",Title="Mana",Description="B"};

                create.Invoke(root,new object[]{entryA});
                var rootNested=typeof(UIWorkspaceTooltipView).GetField("nested",Hidden);
                var first=(List<UIWorkspaceTooltipView>)rootNested.GetValue(root);
                Assert.That(first,Has.Count.EqualTo(1));
                var child=first[0];
                create.Invoke(child,new object[]{entryB});
                var childNested=(List<UIWorkspaceTooltipView>)rootNested.GetValue(child);
                Assert.That(childNested,Has.Count.EqualTo(1));
                var grandchild=childNested[0];

                // The same entry is a valid new card after traversing another
                // entry. Only the current link hover, not an ancestor ID, owns
                // duplicate suppression.
                create.Invoke(grandchild,new object[]{entryA});
                var grandchildNested=(List<UIWorkspaceTooltipView>)rootNested.GetValue(grandchild);
                Assert.That(grandchildNested,Has.Count.EqualTo(1));
                Assert.That(typeof(UIWorkspaceTooltipView).GetField("glossaryEntryId",Hidden).GetValue(grandchildNested[0]),Is.EqualTo("health"));

                var rootEntryField=typeof(UIWorkspaceTooltipView).GetField("glossaryEntryId",Hidden);
                rootEntryField.SetValue(root,"health");
                typeof(UIWorkspaceTooltipView).GetMethod("HideLocal",Hidden).Invoke(root,null);
                Assert.That(rootEntryField.GetValue(root),Is.Null,
                    "A hidden glossary card must not leak its self-link exclusion into the next presentation.");

                // A steady pending link is consumed once; repeating the same
                // callback does not create another card for the same hover.
                var pendingRootObject=new GameObject("nested glossary pending root");
                pendingRootObject.transform.SetParent(canvas.transform,false);
                var pendingRoot=pendingRootObject.AddComponent<UIWorkspaceTooltipView>();
                try
                {
                    typeof(UIWorkspaceTooltipView).GetField("pendingLinkId",Hidden).SetValue(pendingRoot,"health");
                    typeof(UIWorkspaceTooltipView).GetField("pendingLinkOwner",Hidden).SetValue(pendingRoot,pendingRoot);
                    typeof(UIWorkspaceTooltipView).GetField("pendingLinkShownAt",Hidden).SetValue(pendingRoot,Time.unscaledTime-1f);
                    var pending=typeof(UIWorkspaceTooltipView).GetMethod("TryShowPendingNested",Hidden);
                    pending.Invoke(pendingRoot,new object[]{pendingRoot,"health"});
                    var pendingNested=(List<UIWorkspaceTooltipView>)rootNested.GetValue(pendingRoot);
                    Assert.That(pendingNested,Has.Count.EqualTo(1));
                    pending.Invoke(pendingRoot,new object[]{pendingRoot,"health"});
                    Assert.That(pendingNested,Has.Count.EqualTo(1));

                    typeof(UIWorkspaceTooltipView).GetField("pendingLinkId",Hidden).SetValue(pendingRoot,"health");
                    typeof(UIWorkspaceTooltipView).GetMethod("HideLocal",Hidden).Invoke(pendingRoot,null);
                    Assert.That(typeof(UIWorkspaceTooltipView).GetField("pendingLinkId",Hidden).GetValue(pendingRoot),Is.Null,
                        "Hiding a tooltip must clear a stale pending glossary link.");
                }
                finally { Object.DestroyImmediate(pendingRootObject); }
            }
            finally{Object.DestroyImmediate(canvas);}
        }

        private static void SetSkill(SkillData skill,string name,object value) =>
            typeof(SkillData).GetField(name,Hidden).SetValue(skill,value);
    }
}
