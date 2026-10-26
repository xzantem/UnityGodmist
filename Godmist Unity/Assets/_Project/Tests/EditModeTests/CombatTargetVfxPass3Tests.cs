using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using Godmist.Views.Characters;
using Godmist.Views.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Godmist.Tests.EditMode
{
    public sealed partial class CombatTargetVfxTests
    {
        [TestCase("skill.scout.aimed_throw")] [TestCase("skill.scout.poisoned_dagger")]
        [TestCase("skill.scout.quick_strikes")] [TestCase("skill.scout.ricochet")]
        [TestCase("skill.enemy.forest.shooter_shot")] [TestCase("skill.enemy.forest.shooter_piercing_bolt")]
        [TestCase("skill.enemy.forest.archer_arrow")] [TestCase("skill.enemy.forest.archer_arrow_rain")]
        [TestCase("skill.enemy.catacombs.bone_bow")] [TestCase("skill.enemy.catacombs.burning_arrow")]
        public void RangedContactsRequireHitAndUseFragmentsForShield(string id)
        {
            var caster = Unit(true); var target = Unit(false, 4, 3);
            // This branch tests flesh; the generic fixture is a shipped skeleton.
            FleshTarget(target);
            var skill = Skill(id, Effect(EffectType.DealDamage));
            var session = Session(caster, target);
            Assert.That(Cast(session, skill, target).Count(e => e.TargetVfx == CombatTargetVfxKind.PierceBlood), Is.EqualTo(1));
            target.Character.ShieldPool.AddLayer(10000);
            Assert.That(Cast(session, skill, target).Count(e => e.TargetVfx == CombatTargetVfxKind.CutFragments), Is.EqualTo(1));
            target.Character.Stats.Dodge = new Stat(1e20f);
            Assert.That(Cast(session, skill, target).Any(e => e.TargetVfx is CombatTargetVfxKind.PierceBlood or CombatTargetVfxKind.CutFragments), Is.False);
        }

        [TestCase("skill.mage.energy_orb")] [TestCase("skill.mage.energy_split")]
        public void EnergyContactRequiresRealHit(string id)
        {
            var caster = Unit(true); var target = Unit(false, 4, 3); var session = Session(caster, target);
            var skill = Skill(id, Effect(EffectType.DealDamage));
            Assert.That(Cast(session, skill, target).Count(c => c.TargetVfx == CombatTargetVfxKind.EnergyImpact), Is.EqualTo(1));
            target.Character.Stats.Dodge = new Stat(1e20f);
            Assert.That(Cast(session, skill, target).Any(c => c.TargetVfx == CombatTargetVfxKind.EnergyImpact), Is.False);
        }

        [Test] public void QuickStrikesPublishesEachRealHitWithSeparatedStaticWindows()
        {
            var caster = Unit(true); var target = Unit(false, 4, 3); var effect = Effect(EffectType.DealDamage);
            Set(effect, "damageHitCount", 3);
            var cues = Cast(Session(caster, target), Skill("skill.scout.quick_strikes", effect), target);
            Assert.That(cues.Count, Is.EqualTo(3));
            Assert.That(cues.Select(c => c.CueDelaySeconds), Is.EqualTo(new[] { 0f, .4f, .8f }));
            Assert.That(cues.All(c => c.Position == target.Position), Is.True);
        }

        [Test] public void PiercingSkeletonUsesFragmentsAndNotFleshCue()
        {
            var caster = Unit(true); var target = Unit(false, 4, 3);
            var definition = Keep(Object.Instantiate(target.Character.Definition));
            Set(definition, "<Id>k__BackingField", "enemy.catacombs.skeleton_slayer"); target.Character.Definition = definition;
            var cues = Cast(Session(caster, target), Skill("skill.scout.poisoned_dagger", Effect(EffectType.DealDamage)), target);
            Assert.That(cues.Single().TargetVfx, Is.EqualTo(CombatTargetVfxKind.CutFragments));
        }

        [Test] public void ExecutionForwardsSelectedDirectionToGroundSnapshotBeforeDamage()
        {
            var caster = Unit(true, 4, 4); var target = Unit(false, 5, 4); var session = Session(caster, target);
            var skill = Skill("skill.mage.energy_split", Effect(EffectType.DealDamage));
            var mode = new SkillTargetModeData(); Set(mode, "targetType", TargetType.AreaAroundCaster);
            Set(mode, "absoluteRange", new SkillRangeData(RangeShape.ThreeRays, 1, 3));
            Set(skill, "targetModes", new List<SkillTargetModeData> { mode });
            var cues = new List<CombatFeedbackEvent>(); var bus = new CombatFeedbackBus(); bus.Published += cues.Add;
            Assert.That(new SkillExecutionSystem(feedbackBus: bus).TryExecute(session, caster, skill,
                new Dictionary<int, IReadOnlyList<BattleUnit>> { { 0, new[] { target } } }, null, null,
                new Dictionary<int, GridPosition?> { { 0, new GridPosition(7, 4) } }, skipCosts: true), Is.True);
            var ground = cues.Where(c => c.TargetVfx == CombatTargetVfxKind.EnergyGround).ToList();
            Assert.That(ground.Count, Is.EqualTo(9));
            Assert.That(cues.FindIndex(c => c.TargetVfx == CombatTargetVfxKind.EnergyImpact),
                Is.GreaterThan(cues.FindLastIndex(c => c.TargetVfx == CombatTargetVfxKind.EnergyGround)));
        }

        [Test] public void RicochetContactOrderFollowsActualTargetsAndDoesNotInventLinks()
        {
            var caster = Unit(true); var first = Unit(false, 4, 3); var next = Unit(false, 4, 4);
            var cues = Cast(Session(caster, first, next), Skill("skill.scout.ricochet", Effect(EffectType.DealDamage)), next, first);
            Assert.That(cues.Select(c => c.Target), Is.EqualTo(new[] { next, first }));
            Assert.That(cues.Select(c => c.CueDelaySeconds), Is.EqualTo(new[] { 0f, .16f }));
        }

        [TestCase(0f, 0)] [TestCase(1f, 1)]
        public void SmokeGroundShowsRecipientButHeadCueRequiresSuccessfulDebuff(float chance, int headCount)
        {
            var caster = Unit(true); var target = Unit(false, 4, 3); var effect = Effect(EffectType.ModifyStat);
            Set(effect, "baseChance", chance);
            Set(effect, "modifierValue", -20f);
            var skill = Skill("skill.scout.smoke", effect);
            var mode = new SkillTargetModeData(); Set(mode, "targetType", TargetType.All);
            Set(skill, "targetModes", new List<SkillTargetModeData> { mode });
            var cues = Cast(Session(caster, target), skill, target);
            Assert.That(cues.Count(c => c.TargetVfx == CombatTargetVfxKind.SmokeGround), Is.EqualTo(1));
            Assert.That(cues.Count(c => c.TargetVfx == CombatTargetVfxKind.SmokeHead), Is.EqualTo(headCount));
        }

        [TestCase("skill.mage.energy_split", RangeShape.ThreeRays, CombatTargetVfxKind.EnergyGround, 0)]
        [TestCase("skill.mage.energy_split", RangeShape.ThreeRays, CombatTargetVfxKind.EnergyGround, 1)]
        [TestCase("skill.mage.fire_arrow", RangeShape.Line, CombatTargetVfxKind.FireGround, 0)]
        [TestCase("skill.enemy.forest.shooter_piercing_bolt", RangeShape.Rectangle, CombatTargetVfxKind.ArrowGround, 1)]
        public void DirectionalGroundMatchesGameplayCellsIncludingEmptyAndRotated(string id, RangeShape shape, CombatTargetVfxKind kind, int rotation)
        {
            var caster = Unit(true, 4, 4); var session = Session(caster); var aim = new GridPosition(7, 4);
            var mode = new SkillTargetModeData(); Set(mode, "targetType", TargetType.AreaAroundCaster);
            var range = new SkillRangeData(shape, 1, 3, 2); Set(mode, "absoluteRange", range);
            var skill = Skill(id); Set(skill, "targetModes", new List<SkillTargetModeData> { mode });
            var cues = new List<CombatFeedbackEvent>(); var bus = new CombatFeedbackBus(); bus.Published += cues.Add;
            CombatTargetVfxFeedback.PublishArea(bus, session, caster, skill,
                new Dictionary<int, GridPosition?> { { 0, aim } }, new Dictionary<int, int> { { 0, rotation } });
            var expected = new CombatTargetingService().GetDirectionalRangePositions(session, caster, range, aim, rotationQuarterTurns: rotation);
            Assert.That(cues.Select(c => c.Position), Is.EquivalentTo(expected));
            Assert.That(cues.Select(c => c.Position).Distinct().Count(), Is.EqualTo(cues.Count));
            Assert.That(cues.All(c => c.TargetVfx == kind), Is.True);
            if (shape == RangeShape.ThreeRays && rotation == 0)
            {
                Assert.That(cues.Count, Is.EqualTo(9));
                Assert.That(cues.Select(c => c.Position), Has.No.Member(new GridPosition(7, 5)), "three rays must not fill a triangle");
            }
            if (shape == RangeShape.Line) Assert.That(cues.Count, Is.EqualTo(3));
        }

        [Test] public void ArrowRainUsesSelectedTileAndExistingFloorNotCasterOrCastRange()
        {
            var caster = Unit(false, 1, 1); var session = Session(caster); var aim = new GridPosition(6, 6);
            var mode = new SkillTargetModeData(); Set(mode, "targetType", TargetType.AreaAroundTile);
            Set(mode, "relativeRange", new SkillRangeData(RangeShape.Radial, 0, 2));
            var skill = Skill("skill.enemy.forest.archer_arrow_rain"); Set(skill, "targetModes", new List<SkillTargetModeData> { mode });
            var cues = new List<CombatFeedbackEvent>(); var bus = new CombatFeedbackBus(); bus.Published += cues.Add;
            var selection = new Dictionary<int, GridPosition?> { { 0, aim } };
            CombatTargetVfxFeedback.PublishArea(bus, session, caster, skill, selection);
            Assert.That(cues.Count, Is.EqualTo(21)); Assert.That(cues.Select(c => c.Position), Has.Member(aim));
            Assert.That(cues.Select(c => c.Position), Has.No.Member(caster.Position));
            session.Grid = new BattleGrid(9, 9, true); session.Grid.SetWalkable(aim); cues.Clear();
            CombatTargetVfxFeedback.PublishArea(bus, session, caster, skill, selection);
            Assert.That(cues.Select(c => c.Position), Is.EqualTo(new[] { aim }));
        }

        [UnityTest] public IEnumerator DelayedContactRemainsStaticAtSnapshotThenCleansUp()
        {
            yield return new EnterPlayMode(); var root = new GameObject("Delayed static target cue");
            var oldStep = Time.maximumDeltaTime; Time.maximumDeltaTime = .05f;
            try
            {
                var owner = root.AddComponent<CombatUnitViewSpawner>(); owner.enabled = false;
                var unit = Unit(true); var session = Session(unit); var snapshot = unit.Position;
                var cue = new CombatFeedbackEvent(unit, CombatFeedbackType.TargetEffect, position: snapshot,
                    targetVfx: CombatTargetVfxKind.PierceBlood, cueDelaySeconds: .4f);
                Assert.That(owner.TryPlayTargetVfx(session, cue), Is.True);
                var renderer = root.GetComponentsInChildren<SpriteRenderer>().Single(); var sprite = renderer.sprite;
                var point = renderer.transform.position; Assert.That(renderer.forceRenderingOff, Is.True);
                unit.Position = new GridPosition(7, 7);
                var visibleFrames = 0; var end = Time.time + 1f;
                while (Time.time < end)
                {
                    owner.SendMessage("TickWorldEffects");
                    if (renderer != null)
                    {
                        Assert.That(renderer.sprite, Is.SameAs(sprite)); Assert.That(renderer.transform.position, Is.EqualTo(point));
                        if (!renderer.forceRenderingOff && renderer.enabled) visibleFrames++;
                    }
                    yield return null;
                }
                Assert.That(visibleFrames, Is.GreaterThan(0)); Assert.That(renderer == null, Is.True);
                owner.TryPlayTargetVfx(session, cue); owner.SendMessage("OnDisable");
                Assert.That(root.GetComponentsInChildren<SpriteRenderer>().All(r => !r.enabled), Is.True);
            }
            finally { Time.maximumDeltaTime = oldStep; Object.Destroy(root); }
            yield return new ExitPlayMode();
        }
    }
}
