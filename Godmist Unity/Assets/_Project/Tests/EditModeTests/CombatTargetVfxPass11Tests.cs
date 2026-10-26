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
    public sealed partial class CombatTargetVfxTests
    {
        BattleUnit VfxBoss(bool king)
        {
            var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(king ? VS16CatacombsBossSetup.KingPath : VS16CatacombsBossSetup.SlayerPath);
            Assert.That(definition, Is.Not.Null);
            var c = new CharacterInstanceFactory().Create(definition, definition.ClassData, 10); c.CharacterId += Guid.NewGuid();
            c.Stats.MaxHealth = new Stat(10000); c.CurrentHealth = 10000;
            c.Stats.MinAttack = new Stat(100); c.Stats.MaxAttack = new Stat(100); c.Stats.CritChance = new Stat(0); c.Stats.Dodge = new Stat(0);
            return new BattleUnit { Character = c, Position = new GridPosition(7, 7), CurrentActionPoints = 10000, MaxActionPoints = 10000 };
        }
        static CombatSession BossSession(params BattleUnit[] units)
        { var s = Session(units); s.Grid = new BattleGrid(17, 17); return s; }
        static CombatFeedbackBus RecordBoss(List<CombatFeedbackEvent> events)
        { var bus = new CombatFeedbackBus(); bus.Published += events.Add; return bus; }
        static bool BossCast(CombatSession session, SkillData skill, BattleUnit target, CombatFeedbackBus bus, GridPosition? aim = null, GridPosition? placement = null) =>
            new SkillExecutionSystem(statusEffectSystem: new StatusEffectSystem(), feedbackBus: bus).TryExecute(session, session.ActiveUnit, skill,
                new Dictionary<int, IReadOnlyList<BattleUnit>> { { 0, new[] { target } } }, null, placement,
                new Dictionary<int, GridPosition?> { { 0, aim } }, skipCosts: true);
        SpriteRenderer BossBody(GameObject root)
        {
            var texture = Resources.Load<Texture2D>("Godmist/CharacterAnimations/reimond/reimond-s-idle-00-64");
            var body = root.AddComponent<SpriteRenderer>(); body.sprite = Keep(Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(.5f, .125f), 32));
            body.sprite.name = "reimond-s-idle-00-64"; return body;
        }

        [TestCase("royal-command", 4)] [TestCase("slayer-ward", 4)] [TestCase("hammer-charge", 4)]
        [TestCase("royal-command-hit", 1)] [TestCase("slayer-ward-hit", 1)] [TestCase("hammer-charge-hit", 1)]
        [TestCase("shadow-impact", 1)] [TestCase("shadow-ground", 1)]
        public void PassElevenSeparatesBossStatePulsesFromStaticExecutionAndKeepsPixelImport(string id, int frames)
        {
            Assert.That(CombatTargetVfxResolver.TryResolve(id, out var clip), Is.True); Assert.That(clip.Frames.Length, Is.EqualTo(frames));
            Assert.That(clip.RepeatSeconds, Is.EqualTo(frames == 4 ? 4.8f : 0));
            Assert.That(clip.Frames.Select(f => f.name).Distinct().Count(), Is.EqualTo(frames));
            foreach (var frame in clip.Frames)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(frame.texture));
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point)); Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed)); Assert.That(frame.pixelsPerUnit, Is.EqualTo(32));
            }
            if (id.StartsWith("royal-command")) Assert.That(clip.AboveHead, Is.True);
        }

        [Test] public void RoyalCommandActivationAndPulseFollowActualExclusiveMarkExpiryAndVisibility()
        {
            var boss = VfxBoss(true); var first = Unit(true, 8, 7); var second = Unit(true, 7, 8); var session = BossSession(boss, first, second);
            var events = new List<CombatFeedbackEvent>(); var bus = RecordBoss(events); var skill = EnemyVfxSkill(DeadKingBossSystem.CommandId);
            Assert.That(BossCast(session, skill, first, bus), Is.True);
            Assert.That(events.Single(e => e.TargetVfx == CombatTargetVfxKind.RoyalCommand).Target, Is.SameAs(first));
            Assert.That(CombatBossVfxFeedback.HasRoyalCommand(first), Is.True);
            var root = Keep(new GameObject("actual royal mark")); var body = BossBody(root); using var player = new CombatStatusVfxPlayer(body);
            player.Tick(first.Character, first); Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Count(r => r != body && r.enabled), Is.EqualTo(1));
            Assert.That(BossCast(session, skill, second, bus), Is.True);
            Assert.That(CombatBossVfxFeedback.HasRoyalCommand(first), Is.False); Assert.That(CombatBossVfxFeedback.HasRoyalCommand(second), Is.True);
            player.Tick(first.Character, first); Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Any(r => r != body && r.enabled), Is.False);
            player.Tick(second.Character, second); Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Any(r => r != body && r.enabled), Is.True);
            second.Character.PassiveEffects.Add(Status(StatusEffectIds.Invisibility)); player.Tick(second.Character, second);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Any(r => r != body && r.enabled), Is.False);
            second.Character.PassiveEffects.Clear(); player.Tick(second.Character, second);
            Assert.That(CombatBossVfxFeedback.HasRoyalCommand(second), Is.False);
        }

        [Test] public void KingCleaveShowsTwoResolvedCellsAndActualFleshContactWithoutChangingMissRules()
        {
            var caster = Unit(false); var target = Unit(true, 4, 3); FleshTarget(target); var session = BossSession(caster, target);
            var skill = EnemyVfxSkill("skill.enemy.catacombs.dead_king_cleave");
            var cues = ExecuteSelected(session, skill, new[] { target }, target.Position);
            Assert.That(cues.Count(e => e.TargetVfx == CombatTargetVfxKind.SmokeGround), Is.EqualTo(2));
            Assert.That(cues.Count(e => e.TargetVfx == CombatTargetVfxKind.CutBlood), Is.EqualTo(1));
            target.Character.Stats.Dodge = new Stat(1e20f);
            cues = ExecuteSelected(session, skill, new[] { target }, target.Position);
            Assert.That(cues.All(e => e.TargetVfx == CombatTargetVfxKind.SmokeGround), Is.True);
        }

        [Test] public void KingWaveKeepsExactSixCellLineIncludingEmptyCellsAndSeparateFailedSlow()
        {
            var caster = Unit(false); var target = Unit(true, 6, 3); var session = BossSession(caster, target);
            var skill = EnemyVfxSkill("skill.enemy.catacombs.dead_king_wave"); Set(skill.Effects[1], "baseChance", 0f);
            var cues = ExecuteSelected(session, skill, new[] { target }, target.Position);
            Assert.That(cues.Where(e => e.TargetVfx == CombatTargetVfxKind.SpectralLanding).Select(e => e.Position),
                Is.EquivalentTo(Enumerable.Range(4, 6).Select(x => new GridPosition(x, 3))));
            Assert.That(cues.Count(e => e.TargetVfx == CombatTargetVfxKind.SpectralContact), Is.EqualTo(1));
        }

        [Test] public void KingScreamShowsOneAudibleContactPerRecipientEvenWhenBothStatusesFail()
        {
            var caster = Unit(false); var one = Unit(true, 4, 3); var two = Unit(true, 3, 4); var session = BossSession(caster, one, two);
            var skill = EnemyVfxSkill("skill.enemy.catacombs.dead_king_scream"); foreach (var e in skill.Effects) Set(e, "baseChance", 0f);
            var cues = ExecuteSelected(session, skill, new[] { one, two });
            Assert.That(cues.Where(e => e.TargetVfx == CombatTargetVfxKind.ShadowImpact).Select(e => e.Target), Is.EquivalentTo(new[] { one, two }));
            Assert.That(cues.Count(e => e.TargetVfx == CombatTargetVfxKind.ShadowGround), Is.EqualTo(2));
            Assert.That(StatusEffectSystem.HasStatus(one, StatusEffectIds.Stun) || StatusEffectSystem.HasStatus(one, StatusEffectIds.Bleed), Is.False);
        }

        [Test] public void SpectralBladeArrivalIsAtCommittedTeleportEndpointAndIsSeparateFromContact()
        {
            var boss = VfxBoss(true); var target = Unit(true, 10, 7); var session = BossSession(boss, target);
            var before = boss.Position; var skill = EnemyVfxSkill(DeadKingBossSystem.BladeId); Set(skill.Effects[1], "baseChance", 0f);
            var cues = ExecuteSelected(session, skill, new[] { target });
            Assert.That(boss.Position, Is.Not.EqualTo(before));
            Assert.That(cues.Single(e => e.TargetVfx == CombatTargetVfxKind.SpectralLanding).Position, Is.EqualTo(boss.Position));
            Assert.That(cues.Single(e => e.TargetVfx == CombatTargetVfxKind.SpectralContact).Target, Is.SameAs(target));
            Assert.That(cues.Select(e => e.ActionId).Distinct().Count(), Is.EqualTo(1));
        }

        [Test] public void GuardMarksAllActualSpawnPointsIncludingTwoOwnerFollowups()
        {
            var boss = VfxBoss(true); var session = BossSession(boss); var events = new List<CombatFeedbackEvent>(); var bus = RecordBoss(events);
            using var owner = new DeadKingBossSystem(session, new CombatDamageSystem(), new StatusEffectSystem(), feedback: bus);
            Assert.That(BossCast(session, EnemyVfxSkill(CatacombsBossSystem.GuardSkillId), boss, bus, placement: new GridPosition(8, 7)), Is.True);
            Assert.That(session.Summons.Count, Is.EqualTo(3));
            var cues = events.Where(e => e.TargetVfx == CombatTargetVfxKind.SpectralReveal).ToList();
            Assert.That(cues.Select(e => e.Target), Is.EquivalentTo(session.Summons.Select(s => s.Unit)));
            Assert.That(cues.Select(e => e.Position), Is.EquivalentTo(session.Summons.Select(s => s.Unit.Position)));
            Assert.That(cues.Select(e => e.ActionId).Distinct().Count(), Is.EqualTo(1));
        }

        [Test] public void SlayerDefenseAndTaskActivationRequireCommittedBossStateAndPulsesUseExistingBudget()
        {
            var boss = VfxBoss(false); var session = BossSession(boss); var events = new List<CombatFeedbackEvent>(); var bus = RecordBoss(events);
            // The dummy ModifyActionValue effect reorders real turn participants.
            // A test with no initialized timeline loses ActiveUnit after Defense.
            new CombatTurnSystem().InitializeTurnOrder(session);
            using var owner = new SkeletonSlayerBossSystem(session, new CombatDamageSystem(), new StatusEffectSystem(), feedback: bus);
            Assert.That(BossCast(session, EnemyVfxSkill(SkeletonSlayerBossSystem.DefenseId), boss, bus), Is.True);
            Assert.That(events.Count(e => e.TargetVfx == CombatTargetVfxKind.SlayerWard), Is.EqualTo(1));
            Assert.That(session.ActiveUnit, Is.SameAs(boss));
            var root = Keep(new GameObject("boss state pulse")); var body = BossBody(root); using var player = new CombatStatusVfxPlayer(body);
            player.Tick(boss.Character, boss); Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Any(r => r.name == "Status VFX slayer-ward" && r.enabled), Is.True);
            boss.SkeletonSlayerState.DefenseTurns = 0; player.Tick(boss.Character, boss);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Any(r => r != body && r.enabled), Is.False);
            boss.Character.CurrentHealth = 5000; owner.CheckPhases(); owner.BeginTurn(boss); events.Clear();
            var task = EnemyVfxSkill(SkeletonSlayerBossSystem.TaskId);
            Assert.That(BossCast(session, task, boss, bus), Is.True,
                $"phase={boss.SkeletonSlayerState.PhaseTwo};pending={boss.SkeletonSlayerState.PhaseTwoPending};hp={boss.Character.CurrentHealth}/{boss.Character.Stats.MaxHealth.GetValue()};stun={StatusEffectSystem.IsHardCrowdControlled(boss)};taskUsed={boss.SkeletonSlayerState.TaskUsedThisTurn};accelerate={boss.SkeletonSlayerState.AccelerateNextTurn};gate={session.CanUseBossSkill(boss, task)};active={session.ActiveUnit == boss}");
            Assert.That(events.Count(e => e.TargetVfx == CombatTargetVfxKind.Rally), Is.EqualTo(1));
            Assert.That(boss.SkeletonSlayerState.AccelerateNextTurn, Is.True);
            events.Clear(); Assert.That(BossCast(session, EnemyVfxSkill(SkeletonSlayerBossSystem.TaskId), boss, bus), Is.False);
            Assert.That(events.Any(e => e.TargetVfx.HasValue), Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void DelayedHammerContactsMatchRealHitsOnlyAfterCountdownAndShareImpactPoseDeadline(bool miss)
        {
            var boss = VfxBoss(false); var target = Unit(true, 7, 8); var session = BossSession(boss, target);
            if (miss) target.Character.Stats.Dodge = new Stat(1e20f);
            var events = new List<CombatFeedbackEvent>(); var bus = RecordBoss(events);
            using var owner = new SkeletonSlayerBossSystem(session, new CombatDamageSystem(feedbackBus: bus), new StatusEffectSystem(), feedback: bus);
            boss.Character.CurrentHealth = 5000; owner.CheckPhases(); owner.BeginTurn(boss); events.Clear();
            Assert.That(BossCast(session, EnemyVfxSkill(SkeletonSlayerBossSystem.HammerId), target, bus), Is.True);
            Assert.That(events.Any(e => e.TargetVfx == CombatTargetVfxKind.HammerCharge), Is.True);
            events.Clear(); owner.BeginTurn(boss); Assert.That(events.Any(e => e.TargetVfx.HasValue), Is.False);
            owner.BeginTurn(boss);
            var contacts = events.Where(e => e.TargetVfx == CombatTargetVfxKind.Blunt).ToList();
            var hits = events.Count(e => e.Type == CombatFeedbackType.Damage && e.Target == target && e.Amount > 0);
            Assert.That(contacts.Count, Is.EqualTo(hits)); Assert.That(contacts.Count, miss ? Is.Zero : Is.InRange(1, 5));
            Assert.That(events.Count(e => e.TargetVfx == CombatTargetVfxKind.Tremor), Is.EqualTo(9));
            Assert.That(contacts.All(e => e.CueDelaySeconds >= 0 && e.CueDelaySeconds <= 1.6f), Is.True);
            Assert.That(boss.SkeletonSlayerState.HammerTurns, Is.Zero); Assert.That(boss.SkeletonSlayerState.Markers, Is.Empty);
            AssertBossProcDeadline(session, events);
        }

        void AssertBossProcDeadline(CombatSession session, List<CombatFeedbackEvent> events)
        {
            var root = Keep(new GameObject("delayed boss cast timing")); var host = root.AddComponent<CombatSandboxBootstrap>();
            var spawner = root.AddComponent<CombatUnitViewSpawner>(); spawner.enabled = false;
            Set(spawner, "battleUnitPrefab", AssetDatabase.LoadAssetAtPath<CharacterView>("Assets/_Project/Prefabs/Characters/PF_BattleUnit.prefab"));
            Set(spawner, "unitsRoot", root.transform);
            using var driver = new CombatExecutionDriver(host, spawner, null, null, null, null, null, 4, 4, 0, 0, 0, null, null, new(), null, null, null);
            typeof(CombatExecutionDriver).GetProperty("Session").SetValue(driver, session);
            var handle = typeof(CombatExecutionDriver).GetMethod("HandlePoseFeedback", Private);
            foreach (var e in events.Where(e => e.Type == CombatFeedbackType.ProcTriggered || e.TargetVfx.HasValue)) handle.Invoke(driver, new object[] { e });
            Assert.That(driver.IsSkillPresentationInProgress, Is.True);
            Assert.That(Effects(spawner).Count, Is.GreaterThan(0));
            foreach (var effect in Effects(spawner)) Assert.That(EffectTime(effect, "Ends") - Time.time, Is.EqualTo(2).Within(.03));
        }

        [Test] public void InterruptedHammerClearsChargePulseAndNeverPublishesLateImpacts()
        {
            var boss = VfxBoss(false); var target = Unit(true, 7, 8); var session = BossSession(boss, target);
            var events = new List<CombatFeedbackEvent>(); var bus = RecordBoss(events); var damage = new CombatDamageSystem();
            using var owner = new SkeletonSlayerBossSystem(session, damage, new StatusEffectSystem(), feedback: bus);
            boss.Character.CurrentHealth = 5000; owner.CheckPhases(); owner.BeginTurn(boss);
            Assert.That(BossCast(session, EnemyVfxSkill(SkeletonSlayerBossSystem.HammerId), target, bus), Is.True);
            var root = Keep(new GameObject("interrupt pulse cleanup")); var body = BossBody(root); using var player = new CombatStatusVfxPlayer(body);
            player.Tick(boss.Character, boss); Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Any(r => r.name == "Status VFX hammer-charge" && r.enabled), Is.True);
            damage.ApplyDamage(session, target, boss, 1000, DamageType.Magical);
            player.Tick(boss.Character, boss); Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Any(r => r.name == "Status VFX hammer-charge" && r.enabled), Is.False);
            events.Clear(); owner.BeginTurn(boss); owner.BeginTurn(boss);
            Assert.That(events.Any(e => e.TargetVfx == CombatTargetVfxKind.Blunt || e.TargetVfx == CombatTargetVfxKind.Tremor), Is.False);
            Assert.That(boss.SkeletonSlayerState.Markers, Is.Empty);
        }

        [Test] public void ShadowResolutionWaitsTwoTurnsKeepsSafeCellsEmptyAndPublishesThreeRealContacts()
        {
            var boss = VfxBoss(true); var danger = Unit(true, 1, 1); var safe = Unit(true, 2, 1); var session = BossSession(boss, danger, safe);
            var events = new List<CombatFeedbackEvent>(); var bus = RecordBoss(events);
            using var owner = new DeadKingBossSystem(session, new CombatDamageSystem(), new StatusEffectSystem(), feedback: bus);
            boss.Character.CurrentHealth = 5000; owner.CheckPhases(); owner.BeginTurn(boss); events.Clear();
            Assert.That(BossCast(session, EnemyVfxSkill(DeadKingBossSystem.ShadowId), boss, bus), Is.True);
            Assert.That(boss.DeadKingState.SafeFields.Count, Is.EqualTo(10));
            danger.Position = boss.DeadKingState.DarkFields.First(); safe.Position = boss.DeadKingState.SafeFields.First();
            var safeHp = safe.Character.CurrentHealth; var safeCells = boss.DeadKingState.SafeFields.ToArray();
            events.Clear(); owner.BeginTurn(boss); Assert.That(events.Any(e => e.TargetVfx.HasValue), Is.False);
            owner.BeginTurn(boss);
            var contact = events.Where(e => e.TargetVfx == CombatTargetVfxKind.ShadowImpact).ToList();
            Assert.That(contact.Select(e => e.Target), Is.EquivalentTo(new[] { danger, danger, danger }));
            Assert.That(contact.Select(e => e.CueDelaySeconds), Is.EqualTo(new[] { 0f, .4f, .8f }));
            Assert.That(safe.Character.CurrentHealth, Is.EqualTo(safeHp));
            Assert.That(events.Where(e => e.TargetVfx == CombatTargetVfxKind.ShadowGround).Any(e => safeCells.Contains(e.Position)), Is.False);
            Assert.That(events.Count(e => e.TargetVfx.HasValue), Is.LessThan(128));
            Assert.That(boss.DeadKingState.Markers, Is.Empty); Assert.That(StatusEffectSystem.IsStunned(boss), Is.True);
            AssertBossProcDeadline(session, events);
        }

        [Test] public void RuneCreationDisarmAndTimedStrikeUseActualObjectsAndOneCastCorrelation()
        {
            var boss = VfxBoss(true); var target = Unit(true, 1, 1); var session = BossSession(boss, target);
            var skill = EnemyVfxSkill(CatacombsBossSystem.VengeanceSkillId);
            boss.Character.Skills = new List<SkillData> { skill };
            boss.Character.ActiveSkillLoadout.TryInitialize(boss.Character.Skills.Select(s => s.SkillId), skill.SkillId);
            var events = new List<CombatFeedbackEvent>(); var bus = RecordBoss(events);
            var system = new CatacombsBossSystem(new SummonedObjectSystem(), new CombatDamageSystem(), feedbackBus: bus);
            boss.Character.CurrentHealth = 7000; system.OnEnemyHealthChanged(session, boss, 10000);
            Assert.That(events, Is.Empty); Assert.That(system.TryExecuteVengeance(session, boss, out _), Is.True);
            var created = events.Where(e => e.TargetVfx == CombatTargetVfxKind.SpectralLanding).ToList();
            Assert.That(created.Select(e => e.Position), Is.EquivalentTo(session.SummonedObjects.Select(r => r.Position)));
            Assert.That(created.Count, Is.EqualTo(5)); Assert.That(events.First().Type, Is.EqualTo(CombatFeedbackType.SkillUsed));
            Assert.That(created.All(e => e.ActionId == events.First().ActionId && !string.IsNullOrEmpty(e.ActionId)), Is.True);
            var disarmed = session.SummonedObjects.First(); target.Position = disarmed.Position; events.Clear(); system.OnUnitEnteredCell(session, target);
            Assert.That(events.Single().Position, Is.EqualTo(disarmed.Position)); Assert.That(session.SummonedObjects.Count, Is.EqualTo(4));
            events.Clear(); var rune = session.SummonedObjects.First(); rune.RemainingDurationTurns = 2;
            system.ExecuteRuneTurn(session, rune); Assert.That(events, Is.Empty);
            rune.RemainingDurationTurns = 1; system.ExecuteRuneTurn(session, rune);
            Assert.That(events.Single(e => e.TargetVfx.HasValue).TargetVfx, Is.EqualTo(CombatTargetVfxKind.SpectralContact));
            Assert.That(events.Single(e => e.TargetVfx.HasValue).Target, Is.SameAs(target));
            events.Clear(); boss.Character.Stats.MinAttack = new Stat(0); boss.Character.Stats.MaxAttack = new Stat(0);
            system.ExecuteRuneTurn(session, rune); Assert.That(events.Any(e => e.TargetVfx.HasValue), Is.False);
        }

        [Test] public void DelayedBossContactKeepsPreDamageCellWhenAReactionMovesTheTarget()
        {
            var boss = VfxBoss(true); var target = Unit(true, 8, 7); var cell = target.Position;
            var events = new List<CombatFeedbackEvent>(); var bus = RecordBoss(events); target.Position = new GridPosition(9, 7);
            CombatBossVfxFeedback.PublishContact(bus, boss, target, new CombatDamageResult(true, false, 10),
                CombatTargetVfxKind.ShadowImpact, 2, "delayed-impact", cell);
            Assert.That(events.Single().Position, Is.EqualTo(cell)); Assert.That(events.Single().CueDelaySeconds, Is.EqualTo(.8f));
            CombatBossVfxFeedback.PublishContact(bus, boss, target, new CombatDamageResult(false, false, 0),
                CombatTargetVfxKind.ShadowImpact, 3, "delayed-impact", cell);
            Assert.That(events.Count, Is.EqualTo(1));
        }
    }
}
