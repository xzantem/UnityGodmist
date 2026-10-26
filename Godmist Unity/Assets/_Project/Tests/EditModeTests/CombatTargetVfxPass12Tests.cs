using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Editor;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Views.Characters;
using Godmist.Views.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class CombatTargetVfxTests
    {
        SkillData BossSkill12(string id)
        {
            var suffix = id.Replace("skill.enemy.forest.", "").Replace("bandit_leader_", "BanditLeader_").Replace("ent_", "Ent_");
            var skill = Keep(UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<SkillData>(
                "Assets/_Project/ScriptableObjects/Dungeons/Forest/SD_Forest_" + suffix + ".asset")));
            Set(skill, "<SkillAccuracy>k__BackingField", 1000f); return skill;
        }
        BattleUnit ForestBoss12(bool ent)
        {
            var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(ent ? ForestEnemiesSetup.EntPath : VS16CatacombsBossSetup.BanditPath);
            var unit = new BattleUnit { Character = new CharacterInstanceFactory().Create(definition, definition.ClassData, 10),
                Position = new GridPosition(4, 4), IsPlayerSide = false, CurrentActionPoints = 10000, MaxActionPoints = 10000 };
            unit.Character.CharacterId += Guid.NewGuid(); unit.Character.Stats.MaxHealth = new Stat(10000); unit.Character.CurrentHealth = 10000;
            unit.Character.Stats.MinAttack = unit.Character.Stats.MaxAttack = new Stat(100);
            unit.Character.Stats.PhysicalDefense = unit.Character.Stats.MagicalDefense = unit.Character.Stats.CritChance = new Stat(0);
            unit.Character.ResourcePool.CurrentValue = 100; return unit;
        }

        [TestCase("bark-ward", 1)] [TestCase("wood-impact", 1)] [TestCase("wood-ground", 1)]
        [TestCase("mine-burst", 1)] [TestCase("sanctuary-flame", 4)]
        public void PassTwelveImportsCrispStaticContactsAndDistinctFireLoop(string id, int frames)
        {
            Assert.That(CombatTargetVfxResolver.TryResolve(id, out var clip), Is.True);
            Assert.That(clip.Frames.Length, Is.EqualTo(frames));
            Assert.That(clip.Frames.Select(f => f.name).Distinct().Count(), Is.EqualTo(frames));
            foreach (var frame in clip.Frames)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(frame.texture));
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point)); Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed)); Assert.That(frame.pixelsPerUnit, Is.EqualTo(32));
            }
            if (frames > 1) Assert.That(clip.FrameAt(.2f, true), Is.Not.SameAs(clip.FrameAt(0, true)));
        }

        [TestCase("ent_branch", CombatTargetVfxKind.WoodGround)]
        [TestCase("ent_fist", CombatTargetVfxKind.Tremor)]
        [TestCase("ent_ignition", CombatTargetVfxKind.FireGround)]
        [TestCase("bandit_leader_fire", CombatTargetVfxKind.FireGround)]
        public void LastBossAreasMatchResolvedRotatedGeometryIncludingEmptyCells(string suffix, CombatTargetVfxKind kind)
        {
            var caster = Unit(false, 4, 4); var session = Session(caster); var skill = BossSkill12("skill.enemy.forest." + suffix);
            var mode = skill.TargetModes[0]; var targeting = new CombatTargetingService(); var aim = new GridPosition(5, 4);
            var cues = new List<CombatFeedbackEvent>(); var bus = RecordBoss(cues);
            for (var turn = 0; turn < 4; turn++)
            {
                cues.Clear();
                var expected = mode.TargetType == TargetType.AreaAroundTile
                    ? targeting.GetRelativeRangePositions(session, caster, mode, aim, turn)
                    : targeting.GetDirectionalRangePositions(session, caster, mode.AbsoluteRange, aim, rotationQuarterTurns: turn);
                CombatTargetVfxFeedback.PublishArea(bus, session, caster, skill,
                    new Dictionary<int, GridPosition?> { { 0, aim } }, new Dictionary<int, int> { { 0, turn } }, actionId: "area12");
                Assert.That(cues.Select(e => e.Position), Is.EquivalentTo(expected.Where(session.Grid.IsWalkable)), suffix + " rotation " + turn);
                Assert.That(cues, Is.Not.Empty); Assert.That(cues.All(e => e.TargetVfx == kind && e.ActionId == "area12"), Is.True);
            }
        }

        [TestCase("ent_fist", CombatTargetVfxKind.WoodImpact)] [TestCase("ent_branch", CombatTargetVfxKind.WoodImpact)]
        [TestCase("ent_ignition", CombatTargetVfxKind.Fire)] [TestCase("bandit_leader_fire", CombatTargetVfxKind.Fire)]
        [TestCase("bandit_leader_strike", CombatTargetVfxKind.PierceBlood)]
        public void LastBossContactsRequireActualHitAndKeepImpactSnapshot(string suffix, CombatTargetVfxKind kind)
        {
            var caster = Unit(false); var target = Unit(true, 4, 3); FleshTarget(target);
            var skill = BossSkill12("skill.enemy.forest." + suffix); var effect = skill.Effects.First(e => e.EffectType == EffectType.DealDamage);
            var cues = new List<CombatFeedbackEvent>(); var bus = RecordBoss(cues); var before = target.Position;
            target.Position = new GridPosition(6, 3);
            CombatTargetVfxFeedback.PublishImpact(bus, caster, target, skill, effect, false, before, 0, actionId: "miss");
            Assert.That(cues, Is.Empty);
            CombatTargetVfxFeedback.PublishImpact(bus, caster, target, skill, effect, true, before, 100, actionId: "hit");
            Assert.That(cues.Single().TargetVfx, Is.EqualTo(kind)); Assert.That(cues[0].Position, Is.EqualTo(before));
        }

        [Test] public void BarkDeduplicatesCompoundBuffAndRootsConfirmOnlyAppliedStatus()
        {
            var caster = Unit(false); var target = Unit(true, 4, 3); var session = Session(caster, target);
            var bark = BossSkill12(ForestEntBossSystem.BarkSkillId); var cues = ExecuteSelected(session, bark, new[] { caster });
            Assert.That(cues.Count(e => e.TargetVfx == CombatTargetVfxKind.BarkWard), Is.EqualTo(1));
            var roots = BossSkill12("skill.enemy.forest.ent_roots"); Set(roots.Effects[0], "baseChance", 0f);
            cues = ExecuteSelected(session, roots, new[] { target }); Assert.That(cues, Is.Empty);
            Set(roots.Effects[0], "baseChance", 1f); cues = ExecuteSelected(session, roots, new[] { target });
            Assert.That(cues.Count(e => e.TargetVfx == CombatTargetVfxKind.RootContact), Is.EqualTo(1));
            Assert.That(StatusEffectSystem.HasStatus(target, StatusEffectIds.Immobilize), Is.True);
        }

        [Test] public void CommanderOrderUsesOneApprovedHeadMarkOnlyOnRealGuardBuff()
        {
            var boss = ForestBoss12(false); var guard = Unit(false, 5, 4); var hero = Unit(true, 4, 5); var session = Session(boss, guard, hero);
            new CombatTurnSystem().InitializeTurnOrder(session); session.ActiveUnit = boss;
            using var owner = new BanditLeaderBossSystem(session, new CombatDamageSystem(), new StatusEffectSystem(), new CombatRecoverySystem(), new SummonedObjectSystem());
            var events = new List<CombatFeedbackEvent>(); var bus = RecordBoss(events); var skill = BossSkill12(BanditLeaderBossSystem.OrderId);
            Assert.That(BossCast(session, skill, guard, bus), Is.True);
            Assert.That(events.Count(e => e.TargetVfx == CombatTargetVfxKind.BanditOrder), Is.EqualTo(1));
            Assert.That(events.Single(e => e.TargetVfx.HasValue).Target, Is.SameAs(guard));
            events.Clear(); session.ActiveUnit = boss; Assert.That(BossCast(session, skill, guard, bus), Is.False); Assert.That(events, Is.Empty);
        }

        [TestCase(false, false)] [TestCase(false, true)] [TestCase(true, false)] [TestCase(true, true)]
        public void LeaderTrapsStayHiddenUntilEntryOrDetonationAndUseActualThreeByThree(bool mine, bool detonate)
        {
            var boss = ForestBoss12(false); var ally = Unit(false, 3, 3); var victim = Unit(true, 6, 4); var near = Unit(true, 5, 5); var far = Unit(true, 8, 8);
            var session = Session(boss, ally, victim, near, far); var events = new List<CombatFeedbackEvent>(); var bus = RecordBoss(events);
            var skill = BossSkill12(mine ? BanditLeaderBossSystem.MineId : BanditLeaderBossSystem.BearId);
            var cell = new GridPosition(5, 4);
            Assert.That(BossCast(session, skill, boss, bus, placement: cell), Is.True);
            Assert.That(events.Any(e => e.TargetVfx.HasValue || e.Type == CombatFeedbackType.MapObjectTriggered), Is.False);
            var trap = session.SummonedObjects.Single(); Assert.That(trap.Definition.HiddenUntilTriggered, Is.True);
            victim.Position = cell; // placement requires an empty cell; then the victim enters
            var executor = new SummonedObjectEffectExecutor(statusEffectSystem: new StatusEffectSystem(), feedbackBus: bus);
            events.Clear(); Assert.That(executor.ExecuteEntry(session, trap, ally), Is.False); Assert.That(events, Is.Empty);
            if (detonate) executor.ExecuteDetonation(session, trap); else Assert.That(executor.ExecuteEntry(session, trap, victim), Is.True);
            var proc = events.Single(e => e.EffectName == CombatBossVfxFeedback.TrapResolutionCue);
            Assert.That(events.IndexOf(proc), Is.Zero);
            var ground = mine ? CombatTargetVfxKind.MineBurst : CombatTargetVfxKind.TrapGround;
            var cells = events.Where(e => e.TargetVfx == ground).Select(e => e.Position).ToArray();
            Assert.That(cells.Length, Is.EqualTo(9)); Assert.That(cells, Is.EquivalentTo(session.Grid.Positions.Where(p =>
                CombatTargetingService.IsPositionWithinRange(cell, p, trap.Definition.EffectRange))));
            var contacts = events.Where(e => e.TargetVfx == (mine ? CombatTargetVfxKind.Blunt : CombatTargetVfxKind.TrapContact)).ToArray();
            Assert.That(contacts.Select(e => e.Target), Is.EquivalentTo(new[] { victim, near }));
            Assert.That(events.Where(e => e.TargetVfx.HasValue).All(e => e.ActionId == proc.ActionId), Is.True);
            var root = Keep(new GameObject("trap freeze")); var spawner = root.AddComponent<CombatUnitViewSpawner>(); spawner.enabled = false;
            var deadline = Time.time + 2;
            foreach (var cue in events.Where(e => e.TargetVfx.HasValue)) Assert.That(spawner.TryPlayTargetVfx(session, cue, deadline), Is.True);
            foreach (var effect in Effects(spawner)) Assert.That(EffectTime(effect, "Ends"), Is.EqualTo(deadline));
            typeof(CombatUnitViewSpawner).GetMethod("OnDisable", Private).Invoke(spawner, null); Assert.That(Effects(spawner).Count, Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void EntBranchDustUsesOnlyCommittedDisplacement(bool blocked)
        {
            var caster = Unit(false, 4, 4); var target = Unit(true, 5, 4); var session = Session(caster, target);
            if (blocked) session.Units.Add(Unit(true, 6, 4));
            var before = target.Position;
            var cues = ExecuteSelected(session, BossSkill12("skill.enemy.forest.ent_branch"), new[] { target }, new GridPosition(5, 4));
            var dust = cues.Where(e => e.TargetVfx == CombatTargetVfxKind.PullDust).Select(e => e.Position).ToArray();
            if (blocked) { Assert.That(target.Position, Is.EqualTo(before)); Assert.That(dust, Is.Empty); }
            else
            {
                Assert.That(target.Position, Is.Not.EqualTo(before)); Assert.That(dust.First(), Is.EqualTo(before)); Assert.That(dust.Last(), Is.EqualTo(target.Position));
                Assert.That(dust.All(p => p.Y == before.Y && p.X >= before.X && p.X <= target.Position.X), Is.True);
            }
        }

        [TestCase("health", true)] [TestCase("shield", true)] [TestCase("paralysis", false)] [TestCase("zero", false)]
        public void MineContactRequiresActualHealthOrShieldLoss(string mode, bool contact)
        {
            var boss = ForestBoss12(false); var target = Unit(true, 5, 4); var session = Session(boss, target);
            var skill = BossSkill12(BanditLeaderBossSystem.MineId);
            var trap = new SummonedCombatMapObject("mine.loss", target.Position, boss.Character.CharacterId,
                BanditLeaderBossSystem.MineId, 0, skill.Effects[0].SummonedObjectDefinition);
            session.SummonedObjects.Add(trap); session.MapObjects.Add(trap);
            if (mode == "shield") target.Character.ShieldPool.AddLayer(10000);
            if (mode == "paralysis") target.Character.PassiveEffects.Add(Status(StatusEffectIds.Paralysis));
            if (mode == "zero") boss.Character.Stats.MinAttack = boss.Character.Stats.MaxAttack = new Stat(0);
            var events = new List<CombatFeedbackEvent>(); var bus = RecordBoss(events);
            new SummonedObjectEffectExecutor(feedbackBus: bus).ExecuteDetonation(session, trap);
            Assert.That(events.Any(e => e.TargetVfx == CombatTargetVfxKind.Blunt), Is.EqualTo(contact));
            Assert.That(events.Count(e => e.TargetVfx == CombatTargetVfxKind.MineBurst), Is.EqualTo(9), "physical footprint remains independent of victim damage");
        }

        [Test] public void SanctuaryUsesExistingExactMarkersAndOnlyActualFireHitsWithOncePerTurnGate()
        {
            var boss = ForestBoss12(true); var hero = Unit(true, 3, 4); var session = Session(boss, hero);
            var events = new List<CombatFeedbackEvent>(); var bus = RecordBoss(events);
            using var owner = new ForestEntBossSystem(session, new CombatDamageSystem(), new StatusEffectSystem(), new CombatRecoverySystem(), feedback: bus);
            boss.Character.CurrentHealth = 5000; owner.CheckPhases(); owner.BeginTurn(boss);
            Assert.That(BossCast(session, BossSkill12(ForestEntBossSystem.SanctuarySkillId), boss, bus), Is.True);
            var state = boss.ForestEntState;
            Assert.That(state.Warning, Is.True); Assert.That(state.Markers.Select(m => m.Position), Is.EquivalentTo(state.Fields));
            Assert.That(events.Any(e => e.TargetVfx.HasValue), Is.False);
            hero.Position = state.Fields.First(); events.Clear(); session.NotifyUnitEnteredCell(hero); Assert.That(events, Is.Empty);
            hero.Position = session.Grid.Positions.First(p => !state.Fields.Contains(p)); owner.BeginTurn(boss); events.Clear();
            hero.Position = state.Fields.First(); session.NotifyUnitEnteredCell(hero);
            Assert.That(events.Count(e => e.TargetVfx == CombatTargetVfxKind.Fire), Is.EqualTo(1));
            Assert.That(events.Single(e => e.TargetVfx == CombatTargetVfxKind.Fire).Position, Is.EqualTo(hero.Position));
            events.Clear(); session.NotifyUnitEnteredCell(hero); Assert.That(events.Any(e => e.TargetVfx.HasValue), Is.False);
            Assert.That(state.Markers.All(m => m.Definition.ObjectId == "object.forest.ent.fire"), Is.True);
            var root = Keep(new GameObject("one renderer per real sanctuary marker")); var spawner = root.AddComponent<CombatUnitViewSpawner>(); spawner.enabled = false;
            Set(spawner, "summonedObjectsRoot", root.transform);
            typeof(CombatUnitViewSpawner).GetMethod("EnsureSummonedObjectViews", Private).Invoke(spawner, new object[] { session });
            typeof(CombatUnitViewSpawner).GetMethod("SynchronizeSummonedObjectViews", Private).Invoke(spawner, new object[] { session });
            Assert.That(CombatTargetVfxResolver.TryResolve("sanctuary-flame", out var flame), Is.True);
            var renderers = root.GetComponentsInChildren<SpriteRenderer>();
            Assert.That(renderers.Length, Is.EqualTo(state.Fields.Count));
            Assert.That(renderers.All(r => flame.Frames.Contains(r.sprite) && r.transform.localScale == Vector3.one), Is.True);
            Assert.That(Effects(spawner).Count, Is.Zero, "no duplicate temporary fire renderers");
            owner.BeginTurn(boss); owner.BeginTurn(boss); owner.BeginTurn(boss);
            Assert.That(state.Fields, Is.Empty); Assert.That(state.Markers, Is.Empty); Assert.That(session.SummonedObjects, Is.Empty);
            typeof(CombatUnitViewSpawner).GetMethod("RemoveMissingSummonedObjectViews", Private).Invoke(spawner, new object[] { session });
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>(), Is.Empty);
        }
    }
}
