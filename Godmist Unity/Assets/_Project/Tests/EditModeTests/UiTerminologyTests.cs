using System;
using System.Globalization;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Core.Utilities;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Items;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class UiTerminologyTests
    {
        [Test]
        public void EveryStatResistanceVariableAndGalduryteHasANonTechnicalName()
        {
            foreach (CharacterStatType value in Enum.GetValues(typeof(CharacterStatType)))
                Assert.That(UiTerms.Stat(value), Is.Not.EqualTo(value.ToString()));
            foreach (ResistanceType value in Enum.GetValues(typeof(ResistanceType)))
                Assert.That(UiTerms.Resistance(value), Is.Not.EqualTo(value.ToString()));
            foreach (GameplayVariableType value in Enum.GetValues(typeof(GameplayVariableType)))
                Assert.That(UiTerms.Variable(value), Is.Not.EqualTo(value.ToString()));
            foreach (GalduryteEffectType value in Enum.GetValues(typeof(GalduryteEffectType)))
                Assert.That(GalduryteRuntimeText.GetEffectName(value), Is.Not.EqualTo(value.ToString()));
        }

        [TestCase(ResourceType.Mana, "Maksymalna mana", "Regeneracja many", "Generowanie many")]
        [TestCase(ResourceType.Rage, "Maksymalna furia", "Regeneracja furii", "Generowanie furii")]
        [TestCase(ResourceType.Momentum, "Maksymalny impet", "Regeneracja impetu", "Generowanie impetu")]
        [TestCase(ResourceType.None, "Maksymalny zasób", "Regeneracja zasobu", "Generowanie zasobu")]
        public void ResourceContextIsExplicitAndGenericByDefault(ResourceType resource, string max, string regen, string generation)
        {
            Assert.That(UiTerms.Stat(CharacterStatType.MaxResource, resource), Is.EqualTo(max));
            Assert.That(UiTerms.Stat(CharacterStatType.ResourceRegeneration, resource), Is.EqualTo(regen));
            Assert.That(UiTerms.Variable(GameplayVariableType.ResourceGenerationPercent, resource: resource), Is.EqualTo(generation));
            Assert.That(GalduryteRuntimeText.GetEffectName(GalduryteEffectType.MaxResourcePercent), Is.EqualTo("Maksymalny zasób"));
        }

        [TestCase("Przebicie obrony fizycznej", "Penetracja fizyczna")]
        [TestCase("Penetracja obrony magicznej", "Penetracja magiczna")]
        [TestCase("Moc leczenia", "Siła leczenia")]
        [TestCase("Szansa kryt.", "Szansa krytyczna")]
        [TestCase("Wściekłość: 50 PW", "Furia: 50 furii")]
        [TestCase("Zużywa całą aktualną wściekłość", "Zużywa całą aktualną furię")]
        [TestCase("Płonięcie", "Podpalenie")]
        [TestCase("+10 p.p. i +20%", "+10 pp i +20%")]
        [TestCase("+10 pp", "+10 pp")]
        [TestCase("Koszt: 20% AP", "Koszt: 20% PA")]
        public void LegacyCopyUsesTheApprovedVocabularyAndIsIdempotent(string old, string expected)
        {
            var actual = UiTerms.Present(old);
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(UiTerms.Present(actual), Is.EqualTo(actual));
        }

        [Test]
        public void RichTextIdentifiersAndNumbersArePreserved()
        {
            const string old = "<link=\"skill.AP.PP\"><sprite name=\"AP\">+10 p.p.</link> AP +12,5% 3/5";
            Assert.That(UiTerms.Present(old), Is.EqualTo("<link=\"skill.AP.PP\"><sprite name=\"AP\">+10 pp</link> PA +12,5% 3/5"));
            Assert.That(UiTerms.Present("skill.AP i folder/AP oraz HP < 10"), Is.EqualTo("skill.AP i folder/AP oraz HP < 10"));
        }

        [TestCase(CharacterStatType.CritChance, StatModifierOperation.FlatBase, .1f, "Szansa krytyczna: +10 pp")]
        [TestCase(CharacterStatType.CritChance, StatModifierOperation.Percent, .1f, "Szansa krytyczna: +10%")]
        [TestCase(CharacterStatType.Dodge, StatModifierOperation.FlatAbsolute, 4f, "Unik: +4")]
        [TestCase(CharacterStatType.Dodge, StatModifierOperation.Percent, .2f, "Unik: +20%")]
        [TestCase(CharacterStatType.PhysicalDefensePenetration, StatModifierOperation.FlatAbsolute, .15f, "Penetracja fizyczna: +15%")]
        public void UnitsRespectModifierOperations(CharacterStatType stat, StatModifierOperation operation, float value, string expected)
            => Assert.That(UIWorkspaceTooltipView.FormatStatusStatModifier(stat, operation, value), Is.EqualTo(expected));

        [Test]
        public void StatusUsesRecipientResourceAndNeverMutatesItsSerializedState()
        {
            var unit = new BattleUnit { Character = new CharacterInstance { ResourcePool = new(ResourceType.Rage, 50, 100, 0) } };
            var effect = new PassiveEffectRuntime { EffectId = "paladin.test", RemainingDuration = 2,
                StatModifiers = new() { new() { StatType = CharacterStatType.MaxResource, Operation = StatModifierOperation.FlatAbsolute, Value = 10 } } };
            var json = JsonUtility.ToJson(effect);
            var root = new GameObject("terminology test");
            try
            {
                var view = root.AddComponent<UIWorkspaceTooltipView>();
                var presentation = new CombatStatusPresentation(effect, CombatStatusKind.Buff, 1, owner: unit);
                Assert.That(view.BuildPresentedStatusPayload(presentation), Does.Contain("Maksymalna furia: +10"));
                effect.EffectId = "consumable.test.modifier";
                Assert.That(view.BuildPresentedStatusPayload(presentation), Does.Contain("Maksymalny zasób: +10").And.Not.Contain("furia"));
                effect.EffectId = "paladin.test";
                Assert.That(JsonUtility.ToJson(effect), Is.EqualTo(json));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase(TargetType.Caster, "Maksymalna mana")]
        [TestCase(TargetType.Single, "Maksymalny zasób")]
        public void SkillEffectUsesClassResourceOnlyWhenRecipientIsKnown(TargetType target, string expected)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            var root = new GameObject("skill terminology test");
            try
            {
                var data = new SerializedObject(skill);
                data.FindProperty("<SkillId>k__BackingField").stringValue = "skill.mage.test";
                var modes = data.FindProperty("targetModes"); modes.arraySize = 1;
                modes.GetArrayElementAtIndex(0).FindPropertyRelative("targetType").enumValueIndex = (int)target;
                var effects = data.FindProperty("effects"); effects.arraySize = 1;
                var effect = effects.GetArrayElementAtIndex(0);
                effect.FindPropertyRelative("effectType").enumValueIndex = (int)EffectType.ModifyStat;
                effect.FindPropertyRelative("modifierStatType").enumValueIndex = (int)CharacterStatType.MaxResource;
                effect.FindPropertyRelative("modifierValue").floatValue = 10;
                effect.FindPropertyRelative("modifierOperation").enumValueIndex = (int)StatModifierOperation.FlatAbsolute;
                data.ApplyModifiedPropertiesWithoutUndo();
                var body = root.AddComponent<UIWorkspaceTooltipView>().BuildLoadoutSkillDetails(skill, null);
                Assert.That(System.Text.RegularExpressions.Regex.Replace(body, "<[^>]*>", string.Empty), Does.Contain(expected));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(skill); }
        }

        [Test]
        public void AuthoredGlossaryEntriesArePresentedWithoutChangingSource()
        {
            var entry = new CombatTooltipGlossary.Entry { Id = "burn", Title = "Płonięcie", Description = "Koszt: 10 AP", Aliases = new() { "płonięcia" } };
            var copy = CombatTooltipGlossary.PresentEntry(entry);
            Assert.That(copy.Title, Is.EqualTo("Podpalenie"));
            Assert.That(copy.Description, Is.EqualTo("Koszt: 10 PA"));
            Assert.That(copy.Aliases, Does.Contain("Płonięcie"));
            Assert.That(entry.Title, Is.EqualTo("Płonięcie"));
            Assert.That(entry.Aliases.Count, Is.EqualTo(1));
        }

        [Test]
        public void CanonicalTermsStillLinkToExistingGlossaryIdentifiers()
        {
            var glossary = ScriptableObject.CreateInstance<CombatTooltipGlossary>();
            try
            {
                var linked = glossary.Linkify("Podpalenie oraz PA");
                Assert.That(linked, Does.Contain("burn").And.Contain("ap"));
            }
            finally { UnityEngine.Object.DestroyImmediate(glossary); }
        }

        [TestCase("ap", "Punkty akcji")]
        [TestCase("upkeep", "Podtrzymanie")]
        [TestCase("dot-resistance", "Odporność na krwawienie, zatrucie i podpalenie")]
        public void LegacyGlossaryTitlesUseTheSameVocabulary(string id, string expected)
        {
            var entry = CombatTooltipGlossary.BuiltInEntries.First(item => item.Id == id);
            Assert.That(entry.Title, Is.EqualTo(expected));
        }

        [Test]
        public void PolishDecimalsAndPercentagePointsDoNotDependOnSystemCulture()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                Assert.That(UIWorkspaceTooltipView.FormatStatusStatModifier(CharacterStatType.Speed, StatModifierOperation.Percent, .125f), Is.EqualTo("Szybkość: +12,5%"));
                Assert.That(UIWorkspaceTooltipView.FormatStatusStatModifier(CharacterStatType.CritChance, StatModifierOperation.FlatAbsolute, .125f), Is.EqualTo("Szansa krytyczna: +12,5 pp"));
                Assert.That(GalduryteRuntimeText.FormatEffectRange(GalduryteEffectType.AccuracyPercent, .015f, .025f), Is.EqualTo("Szansa trafienia: +1,5 pp–2,5 pp"));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [Test]
        public void ExplorationStatusUsesTheSameResourceContextAsCombat()
        {
            var character = new CharacterInstance { ResourcePool = new(ResourceType.Momentum, 50, 100, 0) };
            var effect = new PassiveEffectRuntime { EffectId = "skill.scout.test", RemainingDuration = 2,
                StatModifiers = new() { new() { StatType = CharacterStatType.MaxResource, Operation = StatModifierOperation.FlatAbsolute, Value = 10 } } };
            var root = new GameObject("exploration terminology test");
            try
            {
                var body = root.AddComponent<UIWorkspaceTooltipView>().BuildPresentedStatusPayload(
                    new CombatStatusPresentation(effect, CombatStatusKind.Buff, 1, explorationCharacter: character));
                Assert.That(body, Does.Contain("Maksymalny impet: +10"));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
