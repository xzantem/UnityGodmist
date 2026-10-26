using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using Godmist.Views.Characters;
using Godmist.Views.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed partial class CombatTargetVfxTests
    {
        [TestCase("bone-ward")] [TestCase("spectral-departure")] [TestCase("spectral-reveal")] [TestCase("life-siphon")]
        public void PassNineSupportArtIsStaticAndKeepsPixelImportProfile(string id)
        {
            Assert.That(CombatTargetVfxResolver.TryResolve(id, out var clip), Is.True);
            Assert.That(clip.Frames.Length, Is.EqualTo(1)); Assert.That(clip.RepeatSeconds, Is.Zero);
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip.Frames[0].texture));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point)); Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(clip.Frames[0].pixelsPerUnit, Is.EqualTo(32));
        }

        [TestCase("skill.enemy.catacombs.battle_fervor", CombatTargetVfxKind.Rally)]
        [TestCase("skill.enemy.catacombs.bone_fervor", CombatTargetVfxKind.Rally)]
        [TestCase("skill.enemy.catacombs.blood_scent", CombatTargetVfxKind.DeathWish)]
        [TestCase("skill.enemy.catacombs.quiver_of_envy", CombatTargetVfxKind.Tonic)]
        [TestCase("skill.enemy.forest.archer_hawkeye", CombatTargetVfxKind.Tonic)]
        public void ImportedCompoundEnemyBuffsGiveOneCuePerRealRecipient(string id, CombatTargetVfxKind kind)
        {
            var caster = Unit(false); var ally = Unit(false, 4, 3); var skill = EnemyVfxSkill(id);
            var cues = EnemyCast(Session(caster, ally), skill, caster);
            Assert.That(cues.Count, Is.EqualTo(1)); Assert.That(cues[0].Target, Is.SameAs(caster));
            Assert.That(cues[0].TargetVfx, Is.EqualTo(kind)); Assert.That(cues[0].ActionId, Is.Not.Null.And.Not.Empty);
        }

        [Test] public void BoneGuardianMarksBothActualParticipantsAndRejectsInvalidSelfProtection()
        {
            var caster = Unit(false); var ally = Unit(false, 4, 3); var session = Session(caster, ally);
            var skill = EnemyVfxSkill("skill.enemy.catacombs.bone_guardian");
            var cues = EnemyCast(session, skill, ally);
            Assert.That(cues.Select(e => e.Target), Is.EquivalentTo(new[] { caster, ally }));
            Assert.That(cues.All(e => e.TargetVfx == CombatTargetVfxKind.BoneWard), Is.True);
            Assert.That(EnemyCast(session, skill, caster), Is.Empty);
        }

        [TestCase(false)] [TestCase(true)]
        public void WolfRecoveryCueRequiresActualHealthRestoration(bool full)
        {
            var wolf = Unit(false); wolf.Character.CurrentHealth = full ? 10000 : 9000;
            var cues = EnemyCast(Session(wolf), EnemyVfxSkill("skill.enemy.forest.wolf_lick_wounds"), wolf);
            Assert.That(cues.Count, Is.EqualTo(full ? 0 : 1));
            if (!full) { Assert.That(wolf.Character.CurrentHealth, Is.GreaterThan(9000)); Assert.That(cues[0].TargetVfx, Is.EqualTo(CombatTargetVfxKind.Heal)); }
        }

        [TestCase(false)] [TestCase(true)]
        public void LifeDrainSeparatesContactFromRealAllyRecoveryAndShieldAbsorption(bool absorbed)
        {
            var caster = Unit(false); var wounded = Unit(false, 3, 4); var full = Unit(false, 2, 3); var target = Unit(true, 4, 3);
            caster.Character.CurrentHealth = 9000; wounded.Character.CurrentHealth = 9000;
            if (absorbed) target.Character.ShieldPool.AddLayer(10000);
            var cues = EnemyCast(Session(caster, wounded, full, target), EnemyVfxSkill("skill.enemy.catacombs.life_drain"), target);
            var contact = cues.Single(e => e.Target == target);
            Assert.That(contact.TargetVfx, Is.EqualTo(absorbed ? CombatTargetVfxKind.SpectralContact : CombatTargetVfxKind.LifeSiphon));
            var heals = cues.Where(e => e.TargetVfx == CombatTargetVfxKind.Heal).ToList();
            Assert.That(heals.Select(e => e.Target), Is.EquivalentTo(absorbed ? new BattleUnit[0] : new[] { caster, wounded }));
            Assert.That(full.Character.CurrentHealth, Is.EqualTo(10000));
            Assert.That(cues.Select(e => e.ActionId).Distinct().Count(), Is.EqualTo(1));
            Assert.That(heals.All(e => e.CueDelaySeconds == contact.CueDelaySeconds), Is.True);
        }

        [TestCase("skill.enemy.catacombs.dematerialize", CombatTargetVfxKind.SpectralDeparture)]
        [TestCase("skill.enemy.forest.bandit_ambush", CombatTargetVfxKind.SmokeGround)]
        public void DisappearanceRecordsLastVisibleOriginOnlyAndDoesNotRepeatWhileHidden(string id, CombatTargetVfxKind kind)
        {
            var caster = Unit(false); var session = Session(caster); var origin = caster.Position; var skill = EnemyVfxSkill(id);
            var cues = EnemyCast(session, skill, caster);
            Assert.That(StatusEffectSystem.IsInvisible(caster), Is.True);
            Assert.That(cues.Count, Is.EqualTo(1)); Assert.That(cues[0].TargetVfx, Is.EqualTo(kind));
            caster.Position = new GridPosition(5, 5); Assert.That(cues[0].Position, Is.EqualTo(origin));
            Assert.That(EnemyCast(session, skill, caster), Is.Empty);
            var root = Keep(new GameObject("departure snapshot")); var owner = root.AddComponent<CombatUnitViewSpawner>(); owner.enabled = false;
            var deadline = Time.time + 2;
            Assert.That(owner.TryPlayTargetVfx(session, cues[0], deadline), Is.True);
            var effect = Effects(owner)[0];
            Assert.That(effect.GetType().GetField("Target").GetValue(effect), Is.Null);
            Assert.That(effect.GetType().GetField("Position").GetValue(effect), Is.EqualTo(origin));
            Assert.That(EffectTime(effect, "Ends"), Is.EqualTo(deadline));
            typeof(CombatUnitViewSpawner).GetMethod("OnDisable", Private).Invoke(owner, null);
            Assert.That(Effects(owner).Count, Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void MaterializationWaitsForActualRevealBeforeShowingRealRecovery(bool full)
        {
            var caster = Unit(false); caster.Character.CurrentHealth = full ? 10000 : 9000;
            caster.Character.PassiveEffects.Add(Status(StatusEffectIds.Invisibility));
            var bus = new CombatFeedbackBus(); var cues = new List<CombatFeedbackEvent>();
            bus.Published += e => { if (!e.TargetVfx.HasValue) return; Assert.That(StatusEffectSystem.IsInvisible(caster), Is.False); cues.Add(e); };
            var skill = EnemyVfxSkill("skill.enemy.catacombs.materialize");
            Assert.That(new SkillExecutionSystem(feedbackBus: bus, statusEffectSystem: new StatusEffectSystem()).TryExecute(
                Session(caster), caster, skill, new Dictionary<int, IReadOnlyList<BattleUnit>> { { 0, new[] { caster } } },
                null, null, null, skipCosts: true), Is.True);
            Assert.That(cues.Count(e => e.TargetVfx == CombatTargetVfxKind.SpectralReveal), Is.EqualTo(1));
            Assert.That(cues.Count(e => e.TargetVfx == CombatTargetVfxKind.Heal), Is.EqualTo(full ? 0 : 1));
            Assert.That(cues.All(e => !string.IsNullOrEmpty(e.ActionId)), Is.True);
        }

        [Test] public void MaterializationRejectsAnAlreadyVisibleCasterWithoutFakeRecoveryOrReveal()
        {
            var caster = Unit(false); caster.Character.CurrentHealth = 9000;
            var bus = new CombatFeedbackBus(); var cues = new List<CombatFeedbackEvent>(); bus.Published += cues.Add;
            var skill = EnemyVfxSkill("skill.enemy.catacombs.materialize");
            Assert.That(new SkillExecutionSystem(feedbackBus: bus, statusEffectSystem: new StatusEffectSystem()).TryExecute(
                Session(caster), caster, skill, new Dictionary<int, IReadOnlyList<BattleUnit>> { { 0, new[] { caster } } },
                null, null, null, skipCosts: true), Is.False);
            Assert.That(caster.Character.CurrentHealth, Is.EqualTo(9000));
            Assert.That(cues.Where(e => e.TargetVfx.HasValue), Is.Empty);
        }

        [Test] public void FailedVisibilityEffectsCannotRevealHiddenActorsOrInventDeparture()
        {
            var caster = Unit(false); var effect = Effect(EffectType.ApplyStatus); Set(effect, "statusId", StatusEffectIds.Invisibility);
            var bus = new CombatFeedbackBus(); var cues = new List<CombatFeedbackEvent>(); bus.Published += cues.Add;
            CombatTargetVfxFeedback.PublishVisibilityTransition(bus, caster, caster,
                EnemyVfxSkill("skill.enemy.catacombs.dematerialize"), effect, false, false, caster.Position, "failed");
            Assert.That(cues, Is.Empty);
            caster.Character.PassiveEffects.Add(Status(StatusEffectIds.Invisibility));
            var root = Keep(new GameObject("hidden support")); var owner = root.AddComponent<CombatUnitViewSpawner>(); owner.enabled = false;
            foreach (var kind in new[] { CombatTargetVfxKind.BoneWard, CombatTargetVfxKind.Heal, CombatTargetVfxKind.SpectralReveal, CombatTargetVfxKind.LifeSiphon })
                Assert.That(owner.TryPlayTargetVfx(Session(caster), new CombatFeedbackEvent(caster, CombatFeedbackType.TargetEffect, targetVfx: kind)), Is.False);
            Assert.That(Effects(owner).Count, Is.Zero);
        }
    }
}
