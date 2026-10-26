using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using Godmist.Views.Characters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed partial class CombatTargetVfxTests
    {
        static List<CombatFeedbackEvent> CastImportedSupport(CombatSession session, string asset, BattleUnit target)
        {
            var skill = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_" + asset + ".asset");
            Assert.That(skill, Is.Not.Null);
            var cues = new List<CombatFeedbackEvent>(); var bus = new CombatFeedbackBus(); bus.Published += cues.Add;
            Assert.That(new SkillExecutionSystem(feedbackBus: bus, statusEffectSystem: new StatusEffectSystem()).TryExecute(
                session, session.ActiveUnit, skill, new Dictionary<int, IReadOnlyList<BattleUnit>> { { 0, new[] { target } } },
                null, null, null, skipCosts: true), Is.True);
            return cues.Where(c => c.Type == CombatFeedbackType.TargetEffect).ToList();
        }

        [TestCase("MageShield", CombatTargetVfxKind.ArcaneWard)]
        [TestCase("PaladinBlessing", CombatTargetVfxKind.HolyGrace)]
        [TestCase("PaladinReflectionShield", CombatTargetVfxKind.HolyGrace)]
        [TestCase("PaladinHolyRegeneration", CombatTargetVfxKind.HolyGrace)]
        [TestCase("WarriorProtection", CombatTargetVfxKind.HolyGrace)]
        [TestCase("WarriorRegroup", CombatTargetVfxKind.Rally)]
        public void ImportedSupportMarksRecipientOnlyAfterActualApplication(string asset, CombatTargetVfxKind kind)
        {
            var caster = Unit(true); var target = Unit(true, 4, 3); var session = Session(caster, target);
            target.CurrentActionValue = 80;
            var beforeShield = target.Character.ShieldPool.TotalShield;
            var cues = CastImportedSupport(session, asset, target);
            Assert.That(cues.Count, Is.EqualTo(1)); Assert.That(cues[0].Target, Is.SameAs(target));
            Assert.That(cues[0].Position, Is.EqualTo(target.Position)); Assert.That(cues[0].TargetVfx, Is.EqualTo(kind));
            if (asset == "MageShield") Assert.That(target.Character.ShieldPool.TotalShield, Is.GreaterThan(beforeShield));
            if (asset == "PaladinReflectionShield") Assert.That(target.Character.ShieldPool.TotalShield, Is.EqualTo(beforeShield), "dodge buff must not grant an HP shield");
            if (asset == "WarriorRegroup") Assert.That(target.CurrentActionValue, Is.EqualTo(0));
        }

        [Test] public void ImportedMagicFocusMarksCasterOnceAndRegistersAboveHead()
        {
            var caster = Unit(true);
            var cues = CastImportedSupport(Session(caster), "MageMagicFocus", caster);
            Assert.That(cues.Single().TargetVfx, Is.EqualTo(CombatTargetVfxKind.ArcaneFocus));
            Assert.That(CombatTargetVfxResolver.TryResolve("arcane-focus", out var clip), Is.True);
            Assert.That(clip.AboveHead, Is.True); Assert.That(clip.Frames.Length, Is.EqualTo(1));
        }

        [TestCase(false, false, 0)] [TestCase(true, false, 1)]
        [TestCase(false, true, 1)] [TestCase(true, true, 1)]
        public void BandageShowsOneCueForHealingOrCleanseButNoneForNoOp(bool injured, bool bleeding, int expected)
        {
            var caster = Unit(true); var target = Unit(true, 4, 3);
            if (injured) target.Character.CurrentHealth = 5000;
            if (bleeding) target.Character.PassiveEffects.Add(Status("bleed"));
            var cues = CastImportedSupport(Session(caster, target), "WarriorBandage", target);
            Assert.That(cues.Count, Is.EqualTo(expected));
            Assert.That(cues.All(c => c.TargetVfx == CombatTargetVfxKind.Heal && c.Target == target), Is.True);
            Assert.That(StatusEffectSystem.HasStatus(target, "bleed"), Is.False);
            Assert.That(target.Character.CurrentHealth, Is.EqualTo(injured ? 5400 : 10000));
        }

        [TestCase(false, false)] [TestCase(false, true)] [TestCase(true, false)] [TestCase(true, true)]
        public void HolyRegenerationTickRequiresRestoredHealthEvenWhenHealerHasLeft(bool fullHealth, bool healerLeft)
        {
            var caster = Unit(true); var target = Unit(true, 4, 3); var session = Session(caster, target);
            CastImportedSupport(session, "PaladinHolyRegeneration", target);
            target.Character.CurrentHealth = fullHealth ? 10000 : 5000;
            if (healerLeft) session.Units.Remove(caster);
            var cues = new List<CombatFeedbackEvent>(); var bus = new CombatFeedbackBus(); bus.Published += cues.Add;
            PaladinCombatSystem.BeforeTurnEnd(session, target, new CombatRecoverySystem(feedbackBus: bus));
            var healing = cues.Where(c => c.Type == CombatFeedbackType.Heal).ToList();
            var vfx = cues.Where(c => c.Type == CombatFeedbackType.TargetEffect).ToList();
            Assert.That(healing.Count, Is.EqualTo(fullHealth ? 0 : 1)); Assert.That(vfx.Count, Is.EqualTo(healing.Count));
            Assert.That(vfx.All(c => c.TargetVfx == CombatTargetVfxKind.Heal && c.Target == target), Is.True);
            Assert.That(target.Character.CurrentHealth, Is.EqualTo(fullHealth ? 10000 : 6000));
        }

        [Test] public void OptionalRecoveryCueKeepsOrdinaryHealsUnchangedAndRevivalGetsOneCue()
        {
            var target = Unit(true); target.Character.CurrentHealth = 5000;
            var bus = new CombatFeedbackBus(); var cues = new List<CombatFeedbackEvent>(); bus.Published += cues.Add;
            var recovery = new CombatRecoverySystem(feedbackBus: bus);
            recovery.Heal(target, 20); Assert.That(cues.Any(c => c.Type == CombatFeedbackType.TargetEffect), Is.False);
            target.Character.CurrentHealth = 0; target.Character.LifeState = CharacterLifeState.Downed;
            target.DownedTurnsRemaining = 2; cues.Clear(); recovery.Heal(target, 50, targetVfx: CombatTargetVfxKind.Heal);
            Assert.That(target.Character.LifeState, Is.EqualTo(CharacterLifeState.Alive)); Assert.That(target.DownedTurnsRemaining, Is.EqualTo(0));
            Assert.That(cues.Count(c => c.Type == CombatFeedbackType.TargetEffect), Is.EqualTo(1));
            target.Character.LifeState = CharacterLifeState.Dead; cues.Clear();
            Assert.That(recovery.Heal(target, 50, targetVfx: CombatTargetVfxKind.Heal), Is.Zero); Assert.That(cues, Is.Empty);
        }

        [TestCase("skill.paladin.verdict", CombatTargetVfxKind.HolyImpact)]
        [TestCase("skill.paladin.crushing_blow", CombatTargetVfxKind.Blunt)]
        public void PaladinPhysicalContactsFollowHitEvenThroughShieldAndNeverAppearOnMiss(string id, CombatTargetVfxKind kind)
        {
            var caster = Unit(true); var target = Unit(false, 4, 3); var session = Session(caster, target);
            target.Character.ShieldPool.AddLayer(10000);
            var skill = Skill(id, Effect(EffectType.DealDamage)); var cues = Cast(session, skill, target);
            Assert.That(cues.Single().TargetVfx, Is.EqualTo(kind)); Assert.That(target.Character.CurrentHealth, Is.EqualTo(10000));
            target.Character.Stats.Dodge = new Stat(1e20f); Assert.That(Cast(session, skill, target), Is.Empty);
        }

        [TestCase(0f)] [TestCase(1f)]
        public void BlindingLightSeparatesOrderedHitsFromSuccessfulDebuffs(float debuffChance)
        {
            var caster = Unit(true); var first = Unit(false, 4, 3); var second = Unit(false, 4, 4);
            var damage = Effect(EffectType.DealDamage); var blind = Effect(EffectType.ModifyStat);
            Set(blind, "modifierValue", -15f); Set(blind, "baseChance", debuffChance);
            var skill = Skill("skill.paladin.blinding_light", damage, blind);
            var cues = Cast(Session(caster, first, second), skill, second, first);
            var hits = cues.Where(c => c.TargetVfx == CombatTargetVfxKind.HolyImpact).ToList();
            var debuffs = cues.Where(c => c.TargetVfx == CombatTargetVfxKind.HolyBlind).ToList();
            Assert.That(hits.Select(c => c.Target), Is.EqualTo(new[] { second, first }));
            Assert.That(hits.Select(c => c.CueDelaySeconds), Is.EqualTo(new[] { 0f, .16f }));
            Assert.That(debuffs.Count, Is.EqualTo(debuffChance == 0 ? 0 : 2));
            if (debuffs.Count > 0) Assert.That(debuffs.Select(c => c.CueDelaySeconds), Is.EqualTo(hits.Select(c => c.CueDelaySeconds)));
            Assert.That(cues.Any(c => c.TargetVfx.HasValue && CombatTargetVfxFeedback.IsGround(c.TargetVfx.Value)), Is.False);
        }
    }
}
