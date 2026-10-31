using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Items;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class DemonTagBonusTests
    {
        private readonly List<ScriptableObject> assets = new();
        [TearDown] public void Cleanup()
        { foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset); assets.Clear(); }

        private static IEnumerable<TestCaseData> TagMatrix()
        {
            var mappings = new[] {
                (GalduryteEffectType.HumanDamagePercent, CharacterTag.Human),
                (GalduryteEffectType.BeastDamagePercent, CharacterTag.Beast),
                (GalduryteEffectType.UndeadDamagePercent, CharacterTag.Undead),
                (GalduryteEffectType.DemonDamagePercent, CharacterTag.Demon) };
            foreach (var (effect, matchingTag) in mappings)
                foreach (var tag in Enum.GetValues(typeof(CharacterTag)).Cast<CharacterTag>().Append((CharacterTag)999))
                    yield return new TestCaseData(effect, tag, tag == matchingTag ? 1.2f : 1f);
        }

        [TestCaseSource(nameof(TagMatrix))]
        public void GalduryteBonusRequiresItsExplicitTag(GalduryteEffectType effect, CharacterTag tag, float expected)
        {
            var source = Unit(); var target = Unit(tag); Equip(source, effect, .2f);
            Assert.That(EquipmentEffectSystem.Outgoing(source, target, DamageType.Physical, null), Is.EqualTo(expected).Within(.00001f));
        }

        [TestCase("CD_Forest_InfectedEnt")]
        [TestCase("CD_Forest_InfectedEntBurning")]
        public void ActualEntAssetsReceiveUndeadButNotDemonBonus(string assetName)
        {
            var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                "Assets/_Project/ScriptableObjects/Dungeons/Forest/" + assetName + ".asset");
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.Tags, Has.Member(CharacterTag.Giant));
            Assert.That(definition.Tags, Has.Member(CharacterTag.Undead));
            Assert.That(definition.Tags, Has.No.Member(CharacterTag.Demon));
            var target = Unit(); target.Character.Definition = definition;
            var source = Unit(); Equip(source, GalduryteEffectType.DemonDamagePercent, .2f);
            Assert.That(EquipmentEffectSystem.Outgoing(source, target, DamageType.Magical, null), Is.EqualTo(1f));
            Equip(source, GalduryteEffectType.UndeadDamagePercent, .2f);
            Assert.That(EquipmentEffectSystem.Outgoing(source, target, DamageType.Magical, null), Is.EqualTo(1.2f).Within(.00001f));
        }

        [TestCase(DamageType.Physical)] [TestCase(DamageType.Magical)]
        public void CoreDamageCalculationDoesNotApplyDemonBonusToGiant(DamageType type)
        {
            var source = Unit(); var target = Unit(CharacterTag.Giant);
            var session = new CombatSession();
            var baseline = CombatDamageSystem.CalculateIncomingBeforeRedirect(session, source, target, 100, type, null);
            Equip(source, GalduryteEffectType.DemonDamagePercent, .2f);
            Assert.That(CombatDamageSystem.CalculateIncomingBeforeRedirect(session, source, target, 100, type, null), Is.EqualTo(baseline));
            target.Character.Definition.Tags.Add(CharacterTag.Demon);
            Assert.That(CombatDamageSystem.CalculateIncomingBeforeRedirect(session, source, target, 100, type, null), Is.EqualTo(Mathf.RoundToInt(baseline * 1.2f)));
        }

        [TestCase(CharacterTag.Giant)] [TestCase((CharacterTag)999)]
        public void GenericDamageAgainstTagStillWorksForTagsWithoutAGalduryteMapping(CharacterTag tag)
        {
            var source = Unit(); var target = Unit(tag); Equip(source, GalduryteEffectType.DemonDamagePercent, .2f);
            source.Character.PassiveEffects.Add(new PassiveEffectRuntime { VariableModifiers = new() {
                new GameplayVariableModifier { VariableType = GameplayVariableType.DamageAgainstTag,
                    ReferenceId = tag.ToString(), Value = .3f } } });
            Assert.That(EquipmentEffectSystem.Outgoing(source, target, DamageType.Physical, null), Is.EqualTo(1.3f).Within(.00001f));
        }

        [Test] public void MatchingTagsStillStackAndDemonIsCountedOnlyForItsOwnTag()
        {
            var source = Unit(); var target = Unit(CharacterTag.Giant, CharacterTag.Undead, CharacterTag.Demon);
            Equip(source, GalduryteEffectType.DemonDamagePercent, .2f);
            source.Character.Equipment.Weapon.SocketedGaldurytes[0].Effects.Add(
                new GalduryteEffectInstance(GalduryteEffectType.UndeadDamagePercent, GaldurytePowderColor.Pink, GalduryteEffectGrade.S, .1f));
            Assert.That(EquipmentEffectSystem.Outgoing(source, target, DamageType.Physical, null), Is.EqualTo(1.3f).Within(.00001f));
        }

        [Test] public void MissingTargetTagsDoNotGrantARacialBonus()
        {
            var source = Unit(); Equip(source, GalduryteEffectType.DemonDamagePercent, .2f);
            Assert.That(EquipmentEffectSystem.Outgoing(source, Unit(), DamageType.Physical, null), Is.EqualTo(1));
            Assert.That(EquipmentEffectSystem.Outgoing(source, null, DamageType.Physical, null), Is.EqualTo(1));
        }

        private BattleUnit Unit(params CharacterTag[] tags)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>(); assets.Add(definition);
            Set(definition, "Id", Guid.NewGuid().ToString()); Set(definition, "Tags", tags.ToList());
            return new BattleUnit { Character = new CharacterInstance { Definition = definition, CharacterId = definition.Id,
                LifeState = CharacterLifeState.Alive, Stats = new CharacterStats(), Resistances = new ResistanceSet() } };
        }
        private static void Equip(BattleUnit source, GalduryteEffectType type, float value)
        {
            source.Character.Equipment.Weapon = new EquipmentItemInstance { UpgradeProgress = 1,
                SocketedGaldurytes = new() { new GalduryteItemInstance { IsIdentified = true,
                    Effects = new() { new GalduryteEffectInstance(type, GaldurytePowderColor.Pink, GalduryteEffectGrade.S, value) } } } };
        }
        private static void Set(object target, string property, object value) => target.GetType()
            .GetField("<" + property + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
