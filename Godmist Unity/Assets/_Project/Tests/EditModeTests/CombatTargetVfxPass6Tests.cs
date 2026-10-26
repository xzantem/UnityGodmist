using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
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
        [TestCase("destruction-mark", 4)] [TestCase("weakpoint-mark", 4)]
        [TestCase("destruction-mark-hit", 1)] [TestCase("weakpoint-mark-hit", 1)]
        [TestCase("sacrifice", 1)] [TestCase("transfusion", 1)]
        public void PassSixKeepsExecutionStaticAndMarksAnimated(string id, int count)
        {
            Assert.That(CombatTargetVfxResolver.TryResolve(id, out var clip), Is.True);
            Assert.That(clip.Frames.Length, Is.EqualTo(count));
            Assert.That(clip.FrameAt(clip.FrameSeconds * 1.01f, true), Is.SameAs(clip.Frames[count == 1 ? 0 : 1]));
            foreach (var frame in clip.Frames)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(frame.texture));
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
                Assert.That(frame.pixelsPerUnit, Is.EqualTo(32));
            }
        }

        static IList Effects(CombatUnitViewSpawner owner) => (IList)typeof(CombatUnitViewSpawner).GetField("worldEffects", Private).GetValue(owner);
        static float EffectTime(object effect, string field) => (float)effect.GetType().GetField(field).GetValue(effect);

        [Test] public void DriverCorrelatesCastEffectsWithoutExtendingUnrelatedHealingTicks()
        {
            var root = Keep(new GameObject("cast timing integration"));
            var host = root.AddComponent<CombatSandboxBootstrap>(); var spawner = root.AddComponent<CombatUnitViewSpawner>(); spawner.enabled = false;
            var unit = Unit(true); var session = Session(unit);
            using var driver = new CombatExecutionDriver(host,spawner,null,null,null,null,null,4,4,0,0,0,null,null,new(),null,null,null);
            typeof(CombatExecutionDriver).GetProperty("Session").SetValue(driver,session);
            var handle = typeof(CombatExecutionDriver).GetMethod("HandlePoseFeedback", Private);
            handle.Invoke(driver, new object[] { new CombatFeedbackEvent(unit, CombatFeedbackType.SkillUsed,
                skillPresentation: new CombatSkillPresentation(unit, Imported("MageFocus")), actionId: "cast") });
            handle.Invoke(driver, new object[] { new CombatFeedbackEvent(unit, CombatFeedbackType.TargetEffect,
                targetVfx: CombatTargetVfxKind.ArcaneFocus, actionId: "cast") });
            Assert.That(EffectTime(Effects(spawner)[0], "Ends") - Time.time, Is.EqualTo(CombatExecutionDriver.SkillPoseDuration).Within(.02));
            handle.Invoke(driver, new object[] { new CombatFeedbackEvent(unit, CombatFeedbackType.TargetEffect,
                targetVfx: CombatTargetVfxKind.Heal) });
            Assert.That(EffectTime(Effects(spawner)[1], "Ends") - Time.time, Is.LessThan(1));
            Assert.That(driver.IsSkillPresentationInProgress, Is.True);
        }

        [Test] public void IntrinsicCastEffectsStayForPoseEvenWithOldShortAuthoredDuration()
        {
            Assert.That(CombatPoseResolver.TryResolveAction("enemy.forest.bandit_leader", "trap", Vector2.down, out var pose), Is.True);
            Assert.That(pose.Effects.Length, Is.GreaterThan(0));
            Assert.That(pose.EffectDurations.Any(d => d > 0 && d < 2), Is.True);
            var root = Keep(new GameObject("intrinsic VFX duration")); var body = root.AddComponent<SpriteRenderer>(); body.sprite = pose.Actor;
            using var player = new CombatPosePlayer(body); var now = Time.time; player.Play(pose, 2);
            Assert.That(player.Tick(now + 1.9f), Is.True);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Where(r => r != body).All(r => r.enabled), Is.True);
            Assert.That(player.Tick(now + 2.01f), Is.False);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Where(r => r != body).All(r => !r.enabled), Is.True);
        }

        [Test] public void AuthoredTargetHeadLayersAlsoUseFullCastDeadline()
        {
            var root = Keep(new GameObject("target head timing integration")); var host = root.AddComponent<CombatSandboxBootstrap>();
            var spawner = root.AddComponent<CombatUnitViewSpawner>(); spawner.enabled = false;
            var caster = Unit(true); var ally = Unit(true, 4, 3); var session = Session(caster, ally);
            caster.Character.Definition = Keep(UnityEngine.Object.Instantiate(caster.Character.Definition));
            Set(caster.Character.Definition, "<Id>k__BackingField", "enemy.forest.bandit_leader");
            var views = (Dictionary<BattleUnit, CharacterView>)typeof(CombatUnitViewSpawner).GetField("views", Private).GetValue(spawner);
            foreach (var unit in session.Units)
            {
                var child = new GameObject("actor"); child.transform.SetParent(root.transform);
                var body = child.AddComponent<SpriteRenderer>(); var view = child.AddComponent<CharacterView>();
                Set(view, "bodyRenderer", body); view.Bind(unit); views.Add(unit, view);
            }
            using var driver = new CombatExecutionDriver(host,spawner,null,null,null,null,null,4,4,0,0,0,null,null,new(),null,null,null);
            typeof(CombatExecutionDriver).GetProperty("Session").SetValue(driver,session);
            var skill = Skill("skill.enemy.forest.bandit_leader_order");
            typeof(CombatExecutionDriver).GetMethod("HandlePoseFeedback", Private).Invoke(driver, new object[] {
                new CombatFeedbackEvent(caster, CombatFeedbackType.SkillUsed,
                    skillPresentation: new CombatSkillPresentation(caster, skill, ally.Position, targets: new[] { ally }), actionId: "order") });
            Assert.That(views[ally].GetComponentInChildren<CombatTargetEffectPlayer>(), Is.Null, "no successful-buff cue from cast intent alone");
            typeof(CombatExecutionDriver).GetMethod("HandlePoseFeedback", Private).Invoke(driver, new object[] {
                new CombatFeedbackEvent(ally, CombatFeedbackType.TargetEffect, source: caster,
                    targetVfx: CombatTargetVfxKind.BanditOrder, actionId: "order") });
            var player = views[ally].GetComponentInChildren<CombatTargetEffectPlayer>(); Assert.That(player, Is.Not.Null);
            Assert.That((float)typeof(CombatTargetEffectPlayer).GetField("ends", Private).GetValue(player) - Time.time,
                Is.EqualTo(CombatExecutionDriver.SkillPoseDuration).Within(.02));
        }

        [TestCase(0f)] [TestCase(.8f)] [TestCase(1.6f)]
        public void EveryHitFreezeEndsAtCastDeadlineRatherThanItsOwnClipTime(float delay)
        {
            var root = Keep(new GameObject("cast VFX deadline")); var owner = root.AddComponent<CombatUnitViewSpawner>(); owner.enabled = false;
            var unit = Unit(true); var session = Session(unit); var deadline = Time.time + 2;
            Assert.That(owner.TryPlayTargetVfx(session, new CombatFeedbackEvent(unit, CombatFeedbackType.TargetEffect,
                targetVfx: CombatTargetVfxKind.Fire, cueDelaySeconds: delay, actionId: "one"), deadline), Is.True);
            var effect = Effects(owner)[0];
            Assert.That(EffectTime(effect, "Ends"), Is.EqualTo(deadline));
            Assert.That(EffectTime(effect, "Starts"), Is.EqualTo(Time.time + delay).Within(.01));
        }

        [Test] public void TickWithoutCastKeepsItsShortCueAndLateCastCueCannotOutlivePose()
        {
            var root = Keep(new GameObject("non-cast VFX")); var owner = root.AddComponent<CombatUnitViewSpawner>(); owner.enabled = false;
            var unit = Unit(true); var session = Session(unit);
            var cue = new CombatFeedbackEvent(unit, CombatFeedbackType.TargetEffect, targetVfx: CombatTargetVfxKind.Heal);
            Assert.That(owner.TryPlayTargetVfx(session, cue), Is.True);
            Assert.That(EffectTime(Effects(owner)[0], "Ends") - Time.time, Is.LessThan(1));
            var late = new CombatFeedbackEvent(unit, CombatFeedbackType.TargetEffect, targetVfx: CombatTargetVfxKind.Fire, cueDelaySeconds: 3, actionId: "late");
            Assert.That(owner.TryPlayTargetVfx(session, late, Time.time + 2), Is.False);
            Assert.That(Effects(owner).Count, Is.EqualTo(1));
        }

        [Test] public void RepeatedStaticHitsReuseRendererButDifferentActionsRemainIndependent()
        {
            var root = Keep(new GameObject("static hit reuse")); var owner = root.AddComponent<CombatUnitViewSpawner>(); owner.enabled = false;
            var unit = Unit(true); var session = Session(unit); var deadline = Time.time + 2;
            foreach (var delay in new[] { 0f, .4f, .8f })
                owner.TryPlayTargetVfx(session, new CombatFeedbackEvent(unit, CombatFeedbackType.TargetEffect,
                    targetVfx: CombatTargetVfxKind.CutBlood, cueDelaySeconds: delay, actionId: "one"), deadline);
            Assert.That(Effects(owner).Count, Is.EqualTo(1));
            owner.TryPlayTargetVfx(session, new CombatFeedbackEvent(unit, CombatFeedbackType.TargetEffect,
                targetVfx: CombatTargetVfxKind.CutBlood, actionId: "two"), deadline);
            Assert.That(Effects(owner).Count, Is.EqualTo(2));
            typeof(CombatUnitViewSpawner).GetMethod("OnDisable", Private).Invoke(owner, null);
            Assert.That(Effects(owner).Count, Is.Zero);
        }

        [Test] public void RealSkillFeedbackCarriesOneActionIdAcrossAreaAndDelayedContacts()
        {
            var caster = Unit(true); var target = Unit(false, 4, 3); var session = Session(caster, target);
            var skill = Keep(UnityEngine.Object.Instantiate(Imported("WarriorWhirlwind"))); Set(skill, "<SkillAccuracy>k__BackingField", 1000f);
            var cues = new List<CombatFeedbackEvent>(); var bus = new CombatFeedbackBus(); bus.Published += cues.Add;
            Assert.That(new SkillExecutionSystem(feedbackBus: bus).TryExecute(session, caster, skill,
                new Dictionary<int, IReadOnlyList<BattleUnit>> { { 0, new[] { target } } }, null, null, null, skipCosts: true), Is.True);
            var start = cues.Single(c => c.Type == CombatFeedbackType.SkillUsed);
            Assert.That(start.ActionId, Is.Not.Null.And.Not.Empty);
            Assert.That(cues.Where(c => c.Type == CombatFeedbackType.TargetEffect).All(c => c.ActionId == start.ActionId), Is.True);
            Assert.That(cues.Count(c => c.TargetVfx is CombatTargetVfxKind.CutFragments or CombatTargetVfxKind.CutBlood), Is.EqualTo(3));
        }

        [TestCase("destruction-mark")] [TestCase("weakpoint-mark")]
        public void MarkLoopsFollowActualCounterExpiryDeathAndInvisibility(string id)
        {
            var root = Keep(new GameObject("mark lifecycle")); var body = root.AddComponent<SpriteRenderer>();
            CombatTargetVfxResolver.TryResolve("heal", out var art); body.sprite = art.Frames[0];
            var unit = Unit(false); using var player = new CombatStatusVfxPlayer(body);
            if (id == "destruction-mark") unit.WarriorState.OrderTurns = 3; else unit.ScoutState.WeakPointTurns = 5;
            player.Tick(unit.Character, unit);
            var renderer = root.GetComponentsInChildren<SpriteRenderer>().Single(r => r != body);
            Assert.That(renderer.enabled, Is.True); Assert.That(renderer.gameObject.name, Does.EndWith(id));
            Assert.That(renderer.transform.localPosition.y, Is.GreaterThan(body.sprite.bounds.max.y));
            unit.Character.PassiveEffects.Add(Status(StatusEffectIds.Invisibility)); player.Tick(unit.Character, unit);
            Assert.That(renderer.enabled, Is.False);
            unit.Character.PassiveEffects.Clear(); unit.Character.LifeState = CharacterLifeState.Dead; player.Tick(unit.Character, unit);
            Assert.That(renderer.enabled, Is.False);
            unit.Character.LifeState = CharacterLifeState.Alive; unit.WarriorState.OrderTurns = unit.ScoutState.WeakPointTurns = 0;
            player.Tick(unit.Character, unit); Assert.That(renderer.enabled, Is.False);
        }

        [Test] public void PassiveDuplicateSharesPresentationIdWithoutSharingGameplayActionOrStartingAnotherPose()
        {
            var caster = Unit(true); var target = Unit(false, 4, 3); var session = Session(caster, target);
            var bus = new CombatFeedbackBus(); var events = new List<CombatFeedbackEvent>(); bus.Published += events.Add;
            var executor = new SkillExecutionSystem(feedbackBus: bus);
            var skill = Skill("skill.warrior.cut", Effect(EffectType.DealDamage));
            Assert.That(executor.TryExecute(session, caster, skill, new Dictionary<int, IReadOnlyList<BattleUnit>> { { 0, new[] { target } } },
                null, null, null, skipCosts: true, isPassiveDuplicate: true, presentationActionId: "parent"), Is.True);
            Assert.That(events.Any(c => c.Type == CombatFeedbackType.SkillUsed), Is.False);
            Assert.That(events.Single(c => c.Type == CombatFeedbackType.TargetEffect).ActionId, Is.EqualTo("parent"));
        }

        [Test] public void MarksShareTwoRendererBudgetAndYieldToControlAndDamageStatuses()
        {
            var root = Keep(new GameObject("mark budget")); var body = root.AddComponent<SpriteRenderer>(); var unit = Unit(false);
            unit.WarriorState.OrderTurns = 3; unit.ScoutState.WeakPointTurns = 5;
            using var player = new CombatStatusVfxPlayer(body); player.Tick(unit.Character, unit);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Count(r => r != body && r.enabled), Is.EqualTo(2));
            unit.Character.PassiveEffects.Add(Status(StatusEffectIds.Stun)); unit.Character.PassiveEffects.Add(Status(StatusEffectIds.Burn));
            player.Tick(unit.Character, unit);
            var effects = root.GetComponentsInChildren<SpriteRenderer>().Where(r => r != body && r.enabled).ToArray();
            Assert.That(effects.Length, Is.EqualTo(2)); Assert.That(effects.Any(r => r.gameObject.name.Contains("mark")), Is.False);
        }

        [TestCase("WarriorDestruction", CombatTargetVfxKind.DestructionMark)]
        [TestCase("ScoutWeakPoint", CombatTargetVfxKind.WeakPointMark)]
        public void ImportedMarksShowOnlyWhenTheirGameplayRollSucceeds(string asset, CombatTargetVfxKind kind)
        {
            var caster = Unit(true); var target = Unit(false, 4, 3); var session = Session(caster, target);
            var skill = Keep(UnityEngine.Object.Instantiate(Imported(asset))); Set(skill.Effects[0], "baseChance", 1f);
            Assert.That(ExecuteSelected(session, skill, new[] { target }).Single().TargetVfx, Is.EqualTo(kind));
            target.WarriorState.OrderTurns = target.ScoutState.WeakPointTurns = 0; Set(skill.Effects[0], "baseChance", -1f);
            Assert.That(ExecuteSelected(session, skill, new[] { target }), Is.Empty);
            Assert.That(target.WarriorState.OrderTurns + target.ScoutState.WeakPointTurns, Is.Zero);
        }

        [Test] public void SacrificeShowsDonorAndCasterOnlyForSuccessfulNonSelfTransfer()
        {
            var caster = Unit(true); var ally = Unit(true, 4, 3); var session = Session(caster, ally);
            var skill = Imported("MageSacrifice"); var cues = ExecuteSelected(session, skill, new[] { ally });
            Assert.That(cues.Count, Is.EqualTo(2));
            Assert.That(cues.Single(c => c.Target == ally).TargetVfx, Is.EqualTo(CombatTargetVfxKind.Sacrifice));
            Assert.That(cues.Single(c => c.Target == caster).TargetVfx, Is.EqualTo(CombatTargetVfxKind.ArcaneFocus));
            var denied = new List<CombatFeedbackEvent>(); var bus = new CombatFeedbackBus(); bus.Published += denied.Add;
            Assert.That(new SkillExecutionSystem(feedbackBus: bus).TryExecute(session, caster, skill,
                new Dictionary<int, IReadOnlyList<BattleUnit>> { { 0, new[] { caster } } }, null, null, null, skipCosts: true), Is.False);
            Assert.That(denied.Any(c => c.TargetVfx.HasValue), Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void FocusSeparatesRealHealthCostFromSuccessfulResourceGain(bool full)
        {
            var caster = Unit(true); caster.Character.ResourcePool = new ResourcePool(ResourceType.Mana, full ? 10000 : 0, 10000, 0);
            var before = caster.Character.CurrentHealth;
            var cues = ExecuteSelected(Session(caster), Imported("MageFocus"), new[] { caster });
            Assert.That(caster.Character.CurrentHealth, Is.LessThan(before));
            Assert.That(cues.Count(c => c.TargetVfx == CombatTargetVfxKind.Transfusion), Is.EqualTo(1));
            Assert.That(cues.Count(c => c.TargetVfx == CombatTargetVfxKind.ArcaneFocus), Is.EqualTo(full ? 0 : 1));
            caster.Character.CurrentHealth = 1;
            Assert.That(ExecuteSelected(Session(caster), Imported("MageFocus"), new[] { caster }), Is.Empty);
        }

        [TestCase(false)] [TestCase(true)]
        public void TransfusionContactRequiresHitAndHealingRequiresActualMissingHealth(bool injured)
        {
            var caster = Unit(true); var target = Unit(false, 4, 3); var session = Session(caster, target);
            if (injured) caster.Character.CurrentHealth = 5000;
            var skill = Keep(UnityEngine.Object.Instantiate(Imported("PaladinTransfusion"))); Set(skill, "<SkillAccuracy>k__BackingField", 1000f);
            var before = caster.Character.CurrentHealth;
            var cues = ExecuteSelected(session, skill, new[] { target });
            Assert.That(cues.Count(c => c.Target == target && c.TargetVfx == CombatTargetVfxKind.Transfusion), Is.EqualTo(1));
            Assert.That(cues.Count(c => c.Target == caster && c.TargetVfx == CombatTargetVfxKind.Heal), Is.EqualTo(injured ? 1 : 0));
            Assert.That(caster.Character.CurrentHealth >= before, Is.True);
            // Magical damage is guaranteed by current gameplay rules. Isolate a
            // miss with a physical copy; do not change the imported skill's balance.
            Set(skill.Effects[0], "damageType", DamageType.Physical);
            target.Character.Stats.Dodge = new Godmist.Core.Types.Stat(1e20f);
            Assert.That(ExecuteSelected(session, skill, new[] { target }), Is.Empty);
        }

        [Test] public void RitualCuesActualCostAndRegistrationThenOnlyPositiveRecoveryTicks()
        {
            var caster = Unit(true); var ally = Unit(true, 4, 3); var session = Session(caster, ally); ally.Character.CurrentHealth = 1;
            var skill = Imported("MageRitual"); var cues = ExecuteSelected(session, skill, new[] { caster, ally });
            Assert.That(cues.Count(c => c.TargetVfx == CombatTargetVfxKind.Transfusion), Is.EqualTo(1));
            Assert.That(cues.Count(c => c.TargetVfx == CombatTargetVfxKind.HolyGrace), Is.EqualTo(2));
            Assert.That(cues.Any(c => c.TargetVfx == CombatTargetVfxKind.Heal), Is.False);
            var effect = ally.Character.PassiveEffects.Single(e => e.EffectId.StartsWith("skill.mage.ritual.periodic_recovery."));
            var bus = new CombatFeedbackBus(); var ticks = new List<CombatFeedbackEvent>(); bus.Published += ticks.Add;
            var processor = new PeriodicRecoveryPassiveEffectProcessor(new CombatRecoverySystem(feedbackBus: bus));
            processor.ProcessTurn(session, ally, effect, out _);
            Assert.That(ticks.Count(c => c.TargetVfx == CombatTargetVfxKind.Heal), Is.EqualTo(1));
            Assert.That(ticks.Where(c => c.TargetVfx.HasValue).All(c => c.ActionId == null), Is.True);
            ticks.Clear(); ally.Character.CurrentHealth = 10000; processor.ProcessTurn(session, ally, effect, out _);
            Assert.That(ticks.Any(c => c.TargetVfx.HasValue), Is.False);
        }

        [Test] public void LegacyPeriodicRecoveryWithoutEffectIdKeepsHealingWithoutRitualCue()
        {
            var unit = Unit(true); unit.Character.CurrentHealth = 5000;
            var effect = new PassiveEffectRuntime { EffectId = null }; effect.SetPayload(new PeriodicRecoveryPayload { HealthPercent = .1f });
            var bus = new CombatFeedbackBus(); var events = new List<CombatFeedbackEvent>(); bus.Published += events.Add;
            new PeriodicRecoveryPassiveEffectProcessor(new CombatRecoverySystem(feedbackBus: bus)).ProcessTurn(Session(unit), unit, effect, out _);
            Assert.That(unit.Character.CurrentHealth, Is.EqualTo(6000));
            Assert.That(events.Any(c => c.TargetVfx.HasValue), Is.False);
        }
    }
}
