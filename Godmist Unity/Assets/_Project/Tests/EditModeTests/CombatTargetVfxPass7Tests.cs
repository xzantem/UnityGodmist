using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
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
        [TestCase("death-wish", 4)] [TestCase("offering", 4)] [TestCase("fatigue", 4)]
        [TestCase("death-wish-hit", 1)] [TestCase("offering-hit", 1)] [TestCase("fatigue-hit", 1)] [TestCase("tonic", 1)]
        public void PassSevenSeparatesSparseLoopsFromFullStrengthStaticContacts(string id, int frames)
        {
            Assert.That(CombatTargetVfxResolver.TryResolve(id, out var clip), Is.True);
            Assert.That(clip.Frames.Length, Is.EqualTo(frames));
            Assert.That(clip.RepeatSeconds, frames == 4 ? Is.GreaterThan(clip.Duration) : Is.Zero);
            Assert.That(clip.Opacity, Is.EqualTo(frames == 4 ? .65f : 1f));
            foreach (var frame in clip.Frames)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(frame.texture));
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point)); Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
                Assert.That(frame.pixelsPerUnit, Is.EqualTo(32));
            }
        }

        [TestCase("burn")] [TestCase("bleed")] [TestCase("stun")] [TestCase("destruction-mark")] [TestCase("heal")]
        public void EarlierCatalogsRetainFullOpacityAndContinuousStatusTiming(string id)
        {
            Assert.That(CombatTargetVfxResolver.TryResolve(id, out var clip), Is.True);
            Assert.That(clip.Opacity, Is.EqualTo(1)); Assert.That(clip.RepeatSeconds, Is.Zero);
        }

        static List<CombatFeedbackEvent> ExecuteToggle(CombatSession session, Godmist.Data.Skills.SkillData skill, SkillExecutionSystem executor, List<CombatFeedbackEvent> events)
        {
            events.Clear();
            Assert.That(executor.TryExecute(session, session.ActiveUnit, skill,
                new Dictionary<int, IReadOnlyList<BattleUnit>> { { 0, new[] { session.ActiveUnit } } }, null, null, null, skipCosts: true), Is.True);
            return events.Where(e => e.TargetVfx.HasValue).ToList();
        }

        [TestCase("WarriorDeathWish", "death-wish", CombatTargetVfxKind.DeathWish)]
        [TestCase("MageOffering", "offering", CombatTargetVfxKind.Offering)]
        [TestCase("MageTiringSpells", "fatigue", CombatTargetVfxKind.Fatigue)]
        public void ImportedToggleShowsOneActivationAndStopsItsLoopOnManualDeactivation(string asset, string loop, CombatTargetVfxKind kind)
        {
            var unit = Unit(true); unit.Character.ResourcePool = new ResourcePool(ResourceType.Rage, 1000, 1000, 0);
            var session = Session(unit); var skill = Imported(asset);
            var bus = new CombatFeedbackBus(); var events = new List<CombatFeedbackEvent>(); bus.Published += events.Add;
            var executor = new SkillExecutionSystem(feedbackBus: bus, statusEffectSystem: new StatusEffectSystem());
            var cues = ExecuteToggle(session, skill, executor, events);
            Assert.That(cues.Count, Is.EqualTo(1)); Assert.That(cues[0].TargetVfx, Is.EqualTo(kind));
            Assert.That(cues[0].ActionId, Is.EqualTo(events.Single(e => e.Type == CombatFeedbackType.SkillUsed).ActionId));
            var root = Keep(new GameObject("toggle loop")); var body = root.AddComponent<SpriteRenderer>();
            using var player = new CombatStatusVfxPlayer(body); player.Tick(unit.Character, unit);
            var effect = root.GetComponentsInChildren<SpriteRenderer>().Single(r => r != body);
            Assert.That(effect.enabled, Is.True); Assert.That(effect.gameObject.name, Does.EndWith(loop));
            Assert.That(ExecuteToggle(session, skill, executor, events), Is.Empty);
            Assert.That(unit.ActiveToggleSkillIds.Contains(skill.SkillId), Is.False);
            player.Tick(unit.Character, unit); Assert.That(effect.enabled, Is.False);
        }

        [Test] public void DeathWishCannotEmitActivationWithoutUpkeepAndLosesLoopWhenUpkeepFails()
        {
            var unit = Unit(true); var skill = Imported("WarriorDeathWish"); var session = Session(unit);
            unit.Character.ResourcePool = new ResourcePool(ResourceType.Rage, 0, 1000, 0);
            var bus = new CombatFeedbackBus(); var events = new List<CombatFeedbackEvent>(); bus.Published += events.Add;
            var executor = new SkillExecutionSystem(feedbackBus: bus);
            Assert.That(executor.TryExecute(session, unit, skill, unit), Is.False);
            Assert.That(events.Any(e => e.TargetVfx.HasValue), Is.False);
            unit.Character.ResourcePool.CurrentValue = 1000;
            unit.Character.Skills.Add(skill);
            foreach (var known in unit.Character.GetActiveSkills().ToArray()) unit.Character.ActiveSkillLoadout.TryRemoveSkill(known.SkillId);
            Assert.That(unit.Character.ActiveSkillLoadout.TryAddSkill(skill.SkillId, unit.Character.GetKnownSkills().Select(s => s.SkillId)), Is.True);
            ExecuteToggle(session, skill, executor, events);
            var root = Keep(new GameObject("upkeep loop")); var body = root.AddComponent<SpriteRenderer>();
            using var player = new CombatStatusVfxPlayer(body); player.Tick(unit.Character, unit);
            var effect = root.GetComponentsInChildren<SpriteRenderer>().Single(r => r != body);
            unit.Character.ResourcePool.CurrentValue = 0; executor.ProcessToggleUpkeep(session, unit);
            Assert.That(unit.ActiveToggleSkillIds.Contains(skill.SkillId), Is.False);
            player.Tick(unit.Character, unit); Assert.That(effect.enabled, Is.False);
        }

        [Test] public void TogglePulseAnimatesThenRestsWithoutAllocatingNewRenderers()
        {
            var unit = Unit(true); unit.ActiveToggleSkillIds.Add("skill.mage.tiring");
            var root = Keep(new GameObject("sparse pulse")); var body = root.AddComponent<SpriteRenderer>();
            using var player = new CombatStatusVfxPlayer(body); player.Tick(unit.Character, unit);
            var renderer = root.GetComponentsInChildren<SpriteRenderer>().Single(r => r != body);
            CombatTargetVfxResolver.TryResolve("fatigue", out var clip);
            var starts = (float[])typeof(CombatStatusVfxPlayer).GetField("started", Private).GetValue(player);
            starts[0] = Time.time - .31f; player.Tick(unit.Character, unit);
            Assert.That(renderer.sprite, Is.SameAs(clip.Frames[1])); Assert.That(renderer.enabled, Is.True);
            starts[0] = Time.time - 2f; player.Tick(unit.Character, unit); Assert.That(renderer.enabled, Is.False);
            starts[0] = Time.time - clip.RepeatSeconds - .31f; player.Tick(unit.Character, unit);
            Assert.That(renderer.enabled, Is.True); Assert.That(renderer.sprite, Is.SameAs(clip.Frames[1]));
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Count(r => r != body), Is.EqualTo(1));
        }

        [Test] public void ToggleLoopsRespectControlPriorityInvisibilityDeathAndCombatUnbinding()
        {
            var unit = Unit(true); unit.ActiveToggleSkillIds.Add(WarriorCombatSystem.DeathWish); unit.ActiveToggleSkillIds.Add("skill.mage.tiring");
            var root = Keep(new GameObject("toggle visibility")); var body = root.AddComponent<SpriteRenderer>();
            using var player = new CombatStatusVfxPlayer(body); player.Tick(unit.Character, unit);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Count(r => r != body && r.enabled), Is.EqualTo(2));
            unit.Character.PassiveEffects.Add(Status(StatusEffectIds.Stun)); unit.Character.PassiveEffects.Add(Status(StatusEffectIds.Burn));
            player.Tick(unit.Character, unit);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Where(r => r != body && r.enabled)
                .All(r => !r.gameObject.name.EndsWith("fatigue") && !r.gameObject.name.EndsWith("death-wish")), Is.True);
            unit.Character.PassiveEffects.Clear(); unit.Character.PassiveEffects.Add(Status(StatusEffectIds.Invisibility)); player.Tick(unit.Character, unit);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Any(r => r != body && r.enabled), Is.False);
            unit.Character.PassiveEffects.Clear(); unit.Character.LifeState = CharacterLifeState.Dead; player.Tick(unit.Character, unit);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Any(r => r != body && r.enabled), Is.False);
            unit.Character.LifeState = CharacterLifeState.Alive; player.Tick(unit.Character);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Any(r => r != body && r.enabled), Is.False);
        }

        [Test] public void OfferingFlagWithoutItsActualBuffDoesNotInventAPersistentPowerEffect()
        {
            var unit = Unit(true); unit.ActiveToggleSkillIds.Add("skill.mage.offering");
            var root = Keep(new GameObject("offering payload gate")); var body = root.AddComponent<SpriteRenderer>();
            using var player = new CombatStatusVfxPlayer(body); player.Tick(unit.Character, unit);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Count(r => r != body), Is.Zero);
        }

        [Test] public void TonicShowsBoostNowAndCrashOnlyAtActualScheduledTurn()
        {
            var unit = Unit(true); var session = Session(unit); var skill = Imported("ScoutTonic");
            unit.Character.ResourcePool = new ResourcePool(ResourceType.Momentum, 1000, 1000, 0);
            var bus = new CombatFeedbackBus(); var events = new List<CombatFeedbackEvent>(); bus.Published += events.Add;
            var executor = new SkillExecutionSystem(feedbackBus: bus);
            Assert.That(executor.TryExecute(session, unit, skill, unit), Is.True);
            Assert.That(events.Count(e => e.TargetVfx == CombatTargetVfxKind.Tonic), Is.EqualTo(1));
            Assert.That(events.Any(e => e.TargetVfx == CombatTargetVfxKind.Fatigue), Is.False);
            Assert.That(session.ScheduledEffects.Count, Is.EqualTo(1));
            var turns = session.ScheduledEffects[0].RemainingCasterTurns;
            for (var turn = 1; turn < turns; turn++)
            { events.Clear(); executor.ProcessScheduledEffects(session, unit); Assert.That(events.Any(e => e.TargetVfx.HasValue), Is.False); }
            events.Clear(); executor.ProcessScheduledEffects(session, unit);
            var crash = events.Single(e => e.TargetVfx.HasValue);
            Assert.That(crash.TargetVfx, Is.EqualTo(CombatTargetVfxKind.Fatigue)); Assert.That(crash.ActionId, Is.Null);
            Assert.That(session.ScheduledEffects, Is.Empty);
            events.Clear(); executor.ProcessScheduledEffects(session, unit); Assert.That(events.Any(e => e.TargetVfx.HasValue), Is.False);
        }

        [Test] public void RecastingTonicReplacesPendingCrashAndCancelledCrashHasNoCue()
        {
            var unit = Unit(true); var session = Session(unit); var skill = Imported("ScoutTonic");
            ExecuteSelected(session, skill, new[] { unit }); ExecuteSelected(session, skill, new[] { unit });
            Assert.That(session.ScheduledEffects.Count, Is.EqualTo(1)); session.ScheduledEffects.Clear();
            var bus = new CombatFeedbackBus(); var events = new List<CombatFeedbackEvent>(); bus.Published += events.Add;
            new SkillExecutionSystem(feedbackBus: bus).ProcessScheduledEffects(session, unit);
            Assert.That(events.Any(e => e.TargetVfx.HasValue), Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void TiringProcCueMatchesActualRandomApplicationsAndCarriesHitDelayAndParentPresentation(bool duplicate)
        {
            var caster = Unit(true); var target = Unit(false, 4, 3); var session = Session(caster, target);
            caster.ActiveToggleSkillIds.Add("skill.mage.tiring");
            var damage = Effect(EffectType.DealDamage); Set(damage, "damageType", DamageType.Magical); Set(damage, "damageHitCount", 3);
            var skill = Skill("skill.mage.energy_orb", damage); var successes = 0; var failures = 0;
            for (var attempt = 0; attempt < 12; attempt++)
            {
                target.Character.PassiveEffects.RemoveAll(e => e.EffectId == "mage.tiring");
                var bus = new CombatFeedbackBus(); var events = new List<CombatFeedbackEvent>(); bus.Published += events.Add;
                Assert.That(new SkillExecutionSystem(feedbackBus: bus).TryExecute(session, caster, skill,
                    new Dictionary<int, IReadOnlyList<BattleUnit>> { { 0, new[] { target } } }, null, null, null,
                    skipCosts: true, isPassiveDuplicate: duplicate, presentationActionId: "parent"), Is.True);
                var cues = events.Where(e => e.TargetVfx == CombatTargetVfxKind.Fatigue).ToArray();
                var actual = target.Character.PassiveEffects.Any(e => e.EffectId == "mage.tiring");
                Assert.That(cues.Length > 0, Is.EqualTo(actual)); Assert.That(cues.Length, Is.LessThanOrEqualTo(1));
                successes += cues.Length; failures += 1 - cues.Length;
                Assert.That(cues.All(e => e.ActionId == "parent" && e.Target == target && e.Source == caster), Is.True);
                Assert.That(cues.All(e => new[] { 0f, .4f, .8f }.Contains(e.CueDelaySeconds)), Is.True);
            }
            Assert.That(successes, duplicate ? Is.Zero : Is.GreaterThan(0)); Assert.That(failures, Is.GreaterThan(0));
        }

        [TestCase(true)] [TestCase(false)]
        public void TiringDoesNotShowSlowForMissOrCompleteShieldAbsorption(bool miss)
        {
            var caster = Unit(true); var target = Unit(false, 4, 3); caster.ActiveToggleSkillIds.Add("skill.mage.tiring");
            if (miss) target.Character.Stats.Dodge = new Godmist.Core.Types.Stat(1e20f); else target.Character.ShieldPool.AddLayer(10000);
            var cues = Cast(Session(caster, target), Skill("skill.warrior.cut", Effect(EffectType.DealDamage)), target);
            Assert.That(cues.Any(e => e.TargetVfx == CombatTargetVfxKind.Fatigue), Is.False);
            Assert.That(target.Character.PassiveEffects.Any(e => e.EffectId == "mage.tiring"), Is.False);
        }

        [Test] public void OutcomeAndDuplicateSnapshotsPreserveVisualTimingWithoutReplacingGameplayActionId()
        {
            var context = new PassiveEffectCombatContext(actionId: "gameplay-child", presentationActionId: "visual-parent", presentationDelaySeconds: .8f);
            foreach (var copy in new[] { context.WithOutcome(succeeded: true), context.AsPassiveDuplicate() })
            { Assert.That(copy.ActionId, Is.EqualTo("gameplay-child")); Assert.That(copy.PresentationActionId, Is.EqualTo("visual-parent")); Assert.That(copy.PresentationDelaySeconds, Is.EqualTo(.8f)); }
        }
    }
}
