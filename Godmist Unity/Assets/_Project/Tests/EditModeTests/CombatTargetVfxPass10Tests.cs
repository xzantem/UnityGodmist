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
        [TestCase("toxic-splash")] [TestCase("toxic-ground")] [TestCase("root-contact")]
        [TestCase("flare-flash")] [TestCase("flare-ground")] [TestCase("growth-burst")] [TestCase("growth-ground")] [TestCase("trap-snap")]
        public void PassTenNatureArtIsStaticAndKeepsPixelImportProfile(string id)
        {
            Assert.That(CombatTargetVfxResolver.TryResolve(id, out var clip), Is.True);
            Assert.That(clip.Frames.Length, Is.EqualTo(1)); Assert.That(clip.RepeatSeconds, Is.Zero);
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip.Frames[0].texture));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point)); Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(clip.Frames[0].pixelsPerUnit, Is.EqualTo(32));
            if (id == "flare-flash") Assert.That(clip.AboveHead, Is.True);
            if (id.EndsWith("-ground")) Assert.That(clip.Offset, Is.EqualTo(Vector2.zero));
        }

        [TestCase("skill.enemy.catacombs.vomit")]
        [TestCase("skill.enemy.forest.poacher_poison_bag")]
        [TestCase("skill.enemy.forest.druid_poison_pulse")]
        public void ToxicExecutionShowsAreaButFailedEffectsDoNotInventPoisonContact(string id)
        {
            var caster = Unit(false); var target = Unit(true, 4, 3); var skill = EnemyVfxSkill(id);
            foreach (var effect in skill.Effects) Set(effect, "baseChance", 0f);
            var cues = ExecuteSelected(Session(caster, target), skill, new[] { target }, target.Position);
            Assert.That(cues.Any(e => e.TargetVfx == CombatTargetVfxKind.ToxicGround), Is.True);
            Assert.That(cues.Any(e => e.TargetVfx == CombatTargetVfxKind.ToxicContact), Is.False);
            Assert.That(StatusEffectSystem.HasStatus(target, StatusEffectIds.Poison), Is.False);
        }

        [Test] public void PoisonBagUsesActualTwoByTwoFootprintIncludingEmptyCellsAndRejectsNonWalkableFloor()
        {
            var caster = Unit(false, 2, 3); var target = Unit(true, 4, 4); var session = Session(caster, target);
            session.Grid = new BattleGrid(9, 9, true);
            for (var x = 0; x < 9; x++) for (var y = 0; y < 9; y++) session.Grid.SetWalkable(new GridPosition(x, y));
            session.Grid.SetWalkable(new GridPosition(5, 5), false);
            var cues = ExecuteSelected(session, EnemyVfxSkill("skill.enemy.forest.poacher_poison_bag"), new[] { target }, target.Position);
            Assert.That(cues.Where(e => e.TargetVfx == CombatTargetVfxKind.ToxicGround).Select(e => e.Position),
                Is.EquivalentTo(new[] { new GridPosition(4, 4), new GridPosition(5, 4), new GridPosition(4, 5) }));
            Assert.That(cues.All(e => !string.IsNullOrEmpty(e.ActionId)), Is.True);
        }

        [TestCase(false)] [TestCase(true)]
        public void BindingRootsContactRequiresActualImmobilizationEvenWhenDefenseDebuffIsSeparate(bool immune)
        {
            var caster = Unit(false); var target = Unit(true, 4, 3); var skill = EnemyVfxSkill("skill.enemy.forest.tree_binding_roots");
            target.Character.Resistances.SuppressionResistance = immune ? 1 : 0;
            Set(skill.Effects[1], "baseChance", 0f);
            var cues = EnemyCast(Session(caster, target), skill, target);
            Assert.That(StatusEffectSystem.IsImmobilized(target), Is.EqualTo(!immune));
            Assert.That(cues.Count(e => e.TargetVfx == CombatTargetVfxKind.RootContact), Is.EqualTo(immune ? 0 : 1));
        }

        [Test] public void DarkRootsKeepMagicalContactWhenPoisonFailsWithoutInventingPoison()
        {
            var caster = Unit(false); var target = Unit(true, 4, 3);
            var skill = EnemyVfxSkill("skill.enemy.forest.tree_dark_roots"); Set(skill.Effects[1], "baseChance", 0f);
            target.Character.Stats.Dodge = new Godmist.Core.Types.Stat(1e20f);
            var cues = EnemyCast(Session(caster, target), skill, target);
            Assert.That(cues.Single().TargetVfx, Is.EqualTo(CombatTargetVfxKind.RootContact));
            Assert.That(target.Character.CurrentHealth, Is.LessThan(10000));
            Assert.That(StatusEffectSystem.HasStatus(target, StatusEffectIds.Poison), Is.False);
        }

        [Test] public void HealingPulseMarksActualRecipientsAndAreaWithoutFakeHealingAtFullHp()
        {
            var caster = Unit(false); var wounded = Unit(false, 4, 3); var full = Unit(false, 3, 4);
            wounded.Character.CurrentHealth = 9000;
            var cues = ExecuteSelected(Session(caster, wounded, full), EnemyVfxSkill("skill.enemy.forest.druid_healing_pulse"), new[] { caster, wounded, full });
            Assert.That(cues.Where(e => e.TargetVfx == CombatTargetVfxKind.Heal).Select(e => e.Target), Is.EquivalentTo(new[] { wounded }));
            Assert.That(cues.Any(e => e.TargetVfx == CombatTargetVfxKind.GrowthGround), Is.True);
            Assert.That(wounded.Character.CurrentHealth, Is.GreaterThan(9000));
            Assert.That(cues.Select(e => e.ActionId).Distinct().Count(), Is.EqualTo(1));
        }

        static bool CastPlacement(CombatSession session, SkillData skill, GridPosition cell, CombatFeedbackBus bus) =>
            new SkillExecutionSystem(statusEffectSystem: new StatusEffectSystem(), feedbackBus: bus).TryExecute(session, session.ActiveUnit, skill,
                new Dictionary<int, IReadOnlyList<BattleUnit>> { { 0, new[] { session.ActiveUnit } } }, null, cell, null, skipCosts: true);

        [TestCase("skill.enemy.forest.druid_healing_growth", CombatTargetVfxKind.GrowthGround)]
        [TestCase("skill.enemy.forest.druid_poison_growth", CombatTargetVfxKind.ToxicGround)]
        public void GrowthCreationMarksOnlyActualSummonedCellAndNeverClaimsImmediateHealing(string id, CombatTargetVfxKind kind)
        {
            var caster = Unit(false); var session = Session(caster); var skill = EnemyVfxSkill(id); var cell = new GridPosition(5, 3);
            var bus = new CombatFeedbackBus(); var cues = new List<CombatFeedbackEvent>(); bus.Published += e => { if (e.TargetVfx.HasValue) cues.Add(e); };
            Assert.That(CastPlacement(session, skill, cell, bus), Is.True);
            var plant = session.Summons.Single().Unit;
            Assert.That(cues.Count, Is.EqualTo(1)); Assert.That(cues[0].Target, Is.SameAs(plant));
            Assert.That(cues[0].Position, Is.EqualTo(cell)); Assert.That(cues[0].TargetVfx, Is.EqualTo(kind));
            cues.Clear(); Assert.That(CastPlacement(session, skill, cell, bus), Is.False); Assert.That(cues, Is.Empty);
            Assert.That(session.Summons.Count, Is.EqualTo(1));
        }

        [Test] public void NatureWrathRequiresOwnedGrowthAndDoesNotFakeContactWithoutIt()
        {
            var caster = Unit(false); var target = Unit(true, 5, 3); var session = Session(caster, target);
            var bus = new CombatFeedbackBus(); var cues = new List<CombatFeedbackEvent>(); bus.Published += e => { if (e.TargetVfx.HasValue) cues.Add(e); };
            var skill = EnemyVfxSkill("skill.enemy.forest.druid_nature_wrath");
            var execution = new SkillExecutionSystem(statusEffectSystem: new StatusEffectSystem(), feedbackBus: bus);
            var targets = new Dictionary<int, IReadOnlyList<BattleUnit>> { { 0, new[] { target } } };
            Assert.That(execution.TryExecute(session, caster, skill, targets, null, null, null, skipCosts: true), Is.False);
            Assert.That(cues, Is.Empty);
            var growth = EnemyVfxSkill("skill.enemy.forest.druid_poison_growth");
            Assert.That(new SummonSystem().TrySummon(session, caster, growth, growth.Effects[0], 0, new GridPosition(4, 4), out _), Is.True);
            Assert.That(execution.TryExecute(session, caster, skill, targets, null, null, null, skipCosts: true), Is.True);
            Assert.That(cues.Single().TargetVfx, Is.EqualTo(CombatTargetVfxKind.RootContact));
        }

        [Test] public void FlareMarksBothResolvedModesOnceAndCleanseOnlyWhenStunWasRemoved()
        {
            var caster = Unit(false); var ally = Unit(false, 3, 4); var enemy = Unit(true, 4, 3); var hidden = Unit(true, 4, 4);
            ally.Character.PassiveEffects.Add(Status(StatusEffectIds.Stun)); hidden.Character.PassiveEffects.Add(Status(StatusEffectIds.Invisibility));
            var skill = EnemyVfxSkill("skill.enemy.forest.shooter_flare"); Set(skill.Effects[0], "baseChance", 0f); Set(skill.Effects[1], "baseChance", 0f);
            var session = Session(caster, ally, enemy, hidden); var bus = new CombatFeedbackBus(); var cues = new List<CombatFeedbackEvent>();
            bus.Published += e => { if (e.TargetVfx.HasValue) cues.Add(e); };
            var execution = new SkillExecutionSystem(statusEffectSystem: new StatusEffectSystem(), feedbackBus: bus);
            var targets = new Dictionary<int, IReadOnlyList<BattleUnit>> { { 0, new[] { enemy } }, { 1, new[] { caster, ally } } };
            Assert.That(execution.TryExecute(session, caster, skill, targets, null, null, null, skipCosts: true), Is.True);
            Assert.That(cues.Where(e => e.TargetVfx == CombatTargetVfxKind.FlareHead).Select(e => e.Target), Is.EquivalentTo(new[] { caster, ally, enemy }));
            Assert.That(cues.Where(e => e.TargetVfx == CombatTargetVfxKind.FlareGround).Select(e => e.Position), Is.EquivalentTo(new[] { caster.Position, ally.Position, enemy.Position }));
            Assert.That(cues.Where(e => e.TargetVfx == CombatTargetVfxKind.HolyGrace).Select(e => e.Target), Is.EquivalentTo(new[] { ally }));
            Assert.That(StatusEffectSystem.HasStatus(enemy, StatusEffectIds.Stun), Is.False);
            cues.Clear(); Assert.That(execution.TryExecute(session, caster, skill, targets, null, null, null, skipCosts: true), Is.True);
            Assert.That(cues.Any(e => e.TargetVfx == CombatTargetVfxKind.HolyGrace), Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void AncientWhipDustFollowsOnlyActualDisplacementBeforeBlockerAndHasNoDamageContactWhenBlocked(bool blockedAtStart)
        {
            var caster = Unit(false, 2, 3); var target = Unit(true, 3, 3); var session = Session(caster, target);
            session.Grid.SetBlockedEdge(new GridPosition(blockedAtStart ? 3 : 4, 3), new GridPosition(blockedAtStart ? 4 : 5, 3));
            var skill = EnemyVfxSkill("skill.enemy.catacombs.ancient_whip"); Set(skill.Effects[1], "baseChance", 0f);
            var cues = EnemyCast(session, skill, target);
            Assert.That(target.Position, Is.EqualTo(new GridPosition(blockedAtStart ? 3 : 4, 3)));
            var dust = cues.Where(e => e.TargetVfx == CombatTargetVfxKind.PullDust).ToList();
            Assert.That(dust.Select(e => e.Position), Is.EquivalentTo(blockedAtStart ? new GridPosition[0] : new[] { new GridPosition(3, 3), new GridPosition(4, 3) }));
            Assert.That(cues.Count(e => e.TargetVfx == CombatTargetVfxKind.CutFragments), Is.EqualTo(blockedAtStart ? 0 : 1));
            Assert.That(StatusEffectSystem.HasStatus(target, StatusEffectIds.Bleed), Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void BearTrapHasNoPlacementCueAndRealActivationUsesJawFreezeWithoutRequiringSerializedTrigger(bool hidden)
        {
            var caster = Unit(false); var enemy = Unit(true, 5, 3); var ally = Unit(false, 4, 4); var session = Session(caster, enemy, ally);
            var skill = EnemyVfxSkill("skill.enemy.forest.poacher_bear_trap"); var cell = new GridPosition(4, 3);
            var definition = Keep(UnityEngine.Object.Instantiate(skill.Effects[0].SummonedObjectDefinition));
            Assert.That(definition.TriggerSprite, Is.Null); Set(definition, "hiddenUntilTriggered", hidden);
            Set(skill.Effects[0], "summonedObjectDefinition", definition);
            var bus = new CombatFeedbackBus(); var events = new List<CombatFeedbackEvent>(); bus.Published += events.Add;
            Assert.That(CastPlacement(session, skill, cell, bus), Is.True);
            Assert.That(events.Any(e => e.TargetVfx.HasValue || e.Type == CombatFeedbackType.MapObjectTriggered), Is.False);
            var trap = session.SummonedObjects.Single(); Assert.That(trap.Definition.HiddenUntilTriggered, Is.EqualTo(hidden));
            var executor = new SummonedObjectEffectExecutor(statusEffectSystem: new StatusEffectSystem(), feedbackBus: bus);
            Assert.That(executor.ExecuteEntry(session, trap, ally), Is.False);
            Assert.That(events.Any(e => e.Type == CombatFeedbackType.MapObjectTriggered), Is.False);
            Assert.That(executor.ExecuteEntry(session, trap, enemy), Is.True);
            Assert.That(events.Count(e => e.Type == CombatFeedbackType.MapObjectTriggered), Is.EqualTo(1));
            var root = Keep(new GameObject("authored trap activation")); var owner = root.AddComponent<CombatUnitViewSpawner>(); owner.enabled = false;
            Assert.That(owner.TryPlayMapObjectTrigger(session, trap), Is.True);
            Assert.That(Effects(owner).Count, Is.EqualTo(1));
            foreach (var effect in Effects(owner)) Assert.That(EffectTime(effect, "Ends") - Time.time, Is.EqualTo(.5f).Within(.02));
            typeof(CombatUnitViewSpawner).GetMethod("OnDisable", Private).Invoke(owner, null); Assert.That(Effects(owner).Count, Is.Zero);
        }

        [Test] public void PlagueExtendsOnlyExistingPoisonAndItsLargeContourFitsTheRendererBudgetIncludingHoles()
        {
            var caster = Unit(false, 20, 20); var poisoned = Unit(true, 21, 20); var fresh = Unit(true, 20, 21);
            var session = Session(caster, poisoned, fresh); session.Grid = new BattleGrid(40, 40, true);
            for (var x = 0; x < 40; x++) for (var y = 0; y < 40; y++) session.Grid.SetWalkable(new GridPosition(x, y));
            session.Grid.SetWalkable(new GridPosition(20, 24), false);
            var poison = Status(StatusEffectIds.Poison); poisoned.Character.PassiveEffects.Add(poison);
            var skill = EnemyVfxSkill("skill.enemy.forest.tree_plague_growth");
            var targets = new CombatTargetingService().GetTargets(session, caster, skill.TargetModes[0], null);
            Assert.That(targets, Is.EquivalentTo(new[] { poisoned }));
            var cues = ExecuteSelected(session, skill, targets);
            Assert.That(poison.RemainingDuration, Is.EqualTo(4));
            Assert.That(StatusEffectSystem.HasStatus(fresh, StatusEffectIds.Poison), Is.False);
            var floor = cues.Where(e => e.TargetVfx == CombatTargetVfxKind.ToxicGround).Select(e => e.Position).ToList();
            Assert.That(floor.Contains(new GridPosition(20, 29)), Is.True);
            Assert.That(floor.Contains(new GridPosition(20, 28)), Is.False);
            Assert.That(floor.Contains(new GridPosition(20, 23)), Is.True);
            Assert.That(floor.Contains(new GridPosition(20, 24)), Is.False);
            Assert.That(cues.Count, Is.LessThan(128));
            var root = Keep(new GameObject("large plague budget")); var owner = root.AddComponent<CombatUnitViewSpawner>(); owner.enabled = false;
            var deadline = Time.time + 2;
            foreach (var cue in cues) Assert.That(owner.TryPlayTargetVfx(session, cue, deadline), Is.True);
            Assert.That(Effects(owner).Count, Is.EqualTo(floor.Count + 1));
            foreach (var effect in Effects(owner)) Assert.That(EffectTime(effect, "Ends"), Is.EqualTo(deadline));
            typeof(CombatUnitViewSpawner).GetMethod("OnDisable", Private).Invoke(owner, null); Assert.That(Effects(owner).Count, Is.Zero);
        }
    }
}
