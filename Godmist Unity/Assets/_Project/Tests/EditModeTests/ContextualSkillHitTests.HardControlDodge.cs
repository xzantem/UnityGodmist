using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Systems.Combat;
using Godmist.Views.UI;
using NUnit.Framework;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Godmist.Tests.EditMode
{
    public sealed partial class ContextualSkillHitTests
    {
        [Test, Explicit("Native refresh of the four descriptions accompanying the approved hard CC rule."), Order(-10)]
        public void AuthorHardControlGlossaryDescriptions()
        {
            var glossary = AssetDatabase.LoadAssetAtPath<CombatTooltipGlossary>(
                "Assets/_Project/ScriptableObjects/UI/CombatTooltipGlossary.asset");
            var folder = Path.GetFullPath("../LocalHandoffs/hard-control-dodge-20261103");
            Directory.CreateDirectory(folder);
            Assert.That(File.Exists(Path.Combine(folder, "glossary-before.json")), Is.False);
            File.WriteAllText(Path.Combine(folder, "glossary-before.json"), EditorJsonUtility.ToJson(glossary));
            var data = new SerializedObject(glossary);
            var entries = data.FindProperty("entries");
            var count = 0;
            for (var i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                var id = entry.FindPropertyRelative("Id").stringValue;
                if (id is not (StatusEffectIds.Stun or StatusEffectIds.Freeze or StatusEffectIds.Sleep or StatusEffectIds.Paralysis)) continue;
                entry.FindPropertyRelative("Description").stringValue = CombatTooltipGlossary.BuiltInEntries.Single(e => e.Id == id).Description;
                count++;
            }
            Assert.That(count, Is.EqualTo(4));
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(glossary);
            File.WriteAllText(Path.Combine(folder, "glossary-after.json"), EditorJsonUtility.ToJson(glossary));
        }

        [TestCase(StatusEffectIds.Stun)]
        [TestCase(StatusEffectIds.Freeze)]
        [TestCase(StatusEffectIds.Sleep)]
        [TestCase(StatusEffectIds.Paralysis)]
        public void HardControlGlossaryExplainsZeroDodge(string status)
        {
            var authored = AssetDatabase.LoadAssetAtPath<CombatTooltipGlossary>(
                "Assets/_Project/ScriptableObjects/UI/CombatTooltipGlossary.asset");
            var fallback = Keep(ScriptableObject.CreateInstance<CombatTooltipGlossary>());
            Assert.That(authored.TryGet(status, out var production), Is.True);
            Assert.That(fallback.TryGet(status, out var builtIn), Is.True);
            Assert.That(production.Description, Does.Contain("Unik wynosi 0."));
            Assert.That(builtIn.Description, Does.Contain("Unik wynosi 0."));
        }

        static void ApplyControl(Godmist.Runtime.Combat.BattleUnit target, string status)
        {
            Assert.That(new StatusEffectSystem().ApplyStatus(null, target, status,
                1f, 2, 1, 1f, 0f, "test.hard-control", publishFeedback: false,
                sharedRoll: 0f), Is.True);
        }

        [TestCase(StatusEffectIds.Stun, true)]
        [TestCase(StatusEffectIds.Stun, false)]
        [TestCase(StatusEffectIds.Freeze, true)]
        [TestCase(StatusEffectIds.Freeze, false)]
        [TestCase(StatusEffectIds.Sleep, true)]
        [TestCase(StatusEffectIds.Sleep, false)]
        [TestCase(StatusEffectIds.Paralysis, true)]
        [TestCase(StatusEffectIds.Paralysis, false)]
        public void HardControlSuppressesDodgeForBothSidesWithoutMutatingStat(string status, bool playerTarget)
        {
            var source = Unit(!playerTarget);
            var target = Unit(playerTarget, 2, 1);
            source.Character.WeaponAccuracy = 50;
            target.Character.Stats.Dodge = new Stat(20);
            var skill = Skill(Damage());
            Set(skill, "<SkillAccuracy>k__BackingField", 100f);
            var baseline = 75f * 75f / 95f / 100f;
            Assert.That(CombatDamageSystem.CalculateSkillHitChance(source, target, skill), Is.EqualTo(baseline).Within(.00001));

            ApplyControl(target, status);
            Assert.That(CombatDamageSystem.ResolveTargetDodge(target), Is.Zero);
            Assert.That(target.Character.Stats.Dodge.GetValue(), Is.EqualTo(20));
            Assert.That(CombatDamageSystem.CalculateSkillHitChance(source, target, skill), Is.EqualTo(.75f));
            // Paralysis retains its existing damage immunity; this test checks
            // only the hit threshold, not whether damage is permitted afterwards.
            for (var seed = 0; seed < 128; seed++)
            {
                Random.InitState(seed);
                var roll = Random.value;
                var next = Random.value;
                Random.InitState(seed);
                var hit = CombatDamageSystem.RollHit(source, target, 100f, out var dodged, skill);
                Assert.That(hit, Is.EqualTo(roll <= .75f));
                Assert.That(dodged, Is.False);
                Assert.That(Random.value, Is.EqualTo(next), "One existing roll only.");
            }

            Assert.That(new StatusEffectSystem().RemoveStatus(target, status), Is.True);
            Assert.That(CombatDamageSystem.CalculateSkillHitChance(source, target, skill), Is.EqualTo(baseline).Within(.00001));
        }

        [TestCase(StatusEffectIds.Immobilize)]
        [TestCase(StatusEffectIds.Chill)]
        [TestCase(StatusEffectIds.Slow)]
        public void SoftControlDoesNotSuppressDodge(string status)
        {
            var target = Unit(false);
            target.Character.Stats.Dodge = new Stat(20);
            ApplyControl(target, status);
            Assert.That(CombatDamageSystem.ResolveTargetDodge(target), Is.EqualTo(20));
        }

        [TestCase(StatusEffectIds.Stun)]
        [TestCase(StatusEffectIds.Freeze)]
        [TestCase(StatusEffectIds.Sleep)]
        public void PreviewAndActualPhysicalHitUseSameControlledThreshold(string status)
        {
            var source = Unit(true);
            var target = Unit(false, 2, 1);
            source.Character.WeaponAccuracy = 50;
            target.Character.Stats.Dodge = new Stat(200);
            var skill = Skill(Damage());
            Set(skill, "<SkillAccuracy>k__BackingField", 100f);
            ApplyControl(target, status);
            var preview = CombatSkillPreviewCalculator.CalculateTargetPreview(source, target, skill);
            Assert.That(preview.HitChance, Is.EqualTo(.75f));
            Random.InitState(SeedForInterval(.6f, .7f));
            var result = new CombatDamageSystem().ResolveAndApplyDamage(Session(source, target),
                source, target, 10, DamageType.Physical, 100f);
            Assert.That(result.Hit, Is.True, "This roll would miss against the original dodge.");
            Assert.That(result.HealthDamage, Is.GreaterThan(0));
            Assert.That(target.Character.Stats.Dodge.GetValue(), Is.EqualTo(200));
            if (status == StatusEffectIds.Freeze)
            {
                Assert.That(StatusEffectSystem.IsFrozen(target), Is.False);
                Assert.That(CombatDamageSystem.ResolveTargetDodge(target), Is.EqualTo(200),
                    "The shattering hit uses zero dodge; subsequent hits use restored dodge.");
            }
        }

        [Test]
        public void ParalysisStillPreventsDamage()
        {
            var source = Unit(true);
            var target = Unit(false, 2, 1);
            ApplyControl(target, StatusEffectIds.Paralysis);
            var before = target.Character.CurrentHealth;
            var result = new CombatDamageSystem().ResolveAndApplyDamage(Session(source, target),
                source, target, 10, DamageType.Physical, 100f);
            Assert.That(result.HealthDamage, Is.Zero);
            Assert.That(target.Character.CurrentHealth, Is.EqualTo(before));
        }

        [Test]
        public void DodgeReturnsOnlyAfterLastActiveHardControlEnds()
        {
            var target = Unit(false);
            target.Character.Stats.Dodge = new Stat(20);
            ApplyControl(target, StatusEffectIds.Stun);
            ApplyControl(target, StatusEffectIds.Sleep);
            var statuses = new StatusEffectSystem();
            Assert.That(statuses.RemoveStatus(target, StatusEffectIds.Stun), Is.True);
            Assert.That(CombatDamageSystem.ResolveTargetDodge(target), Is.Zero);
            statuses.ProcessTurnEnd(target);
            Assert.That(CombatDamageSystem.ResolveTargetDodge(target), Is.Zero);
            statuses.ProcessTurnEnd(target);
            Assert.That(StatusEffectSystem.IsSleeping(target), Is.False);
            Assert.That(CombatDamageSystem.ResolveTargetDodge(target), Is.EqualTo(20));
        }

        [Test]
        public void NonDamageEffectUsesControlledHitThreshold()
        {
            var source = Unit(true);
            var target = Unit(false, 2, 1);
            source.Character.WeaponAccuracy = 50;
            target.Character.Stats.Dodge = new Stat(200);
            ApplyControl(target, StatusEffectIds.Stun);
            var effect = Modifier();
            Set(effect, "alwaysHits", false);
            var skill = Skill(effect);
            Set(skill, "<SkillAccuracy>k__BackingField", 100f);
            Random.InitState(SeedForInterval(.6f, .7f));
            Assert.That(Cast(Session(source, target), skill, target), Is.True);
            Assert.That(Modified(target, skill), Is.True);
        }
    }
}
