using System;
using System.Collections.Generic;
using System.Linq;
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
        static SkillData Imported(string asset) => AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_" + asset + ".asset");
        static List<CombatFeedbackEvent> ExecuteSelected(CombatSession session, SkillData skill,
            IReadOnlyList<BattleUnit> targets, GridPosition? aim = null, GridPosition? teleport = null)
        {
            var cues = new List<CombatFeedbackEvent>(); var bus = new CombatFeedbackBus(); bus.Published += cues.Add;
            Assert.That(new SkillExecutionSystem(feedbackBus: bus, statusEffectSystem: new StatusEffectSystem()).TryExecute(
                session, session.ActiveUnit, skill, new Dictionary<int, IReadOnlyList<BattleUnit>> { { 0, targets } }, teleport, null,
                new Dictionary<int, GridPosition?> { { 0, aim } }, skipCosts: true), Is.True);
            return cues.Where(c => c.Type == CombatFeedbackType.TargetEffect).ToList();
        }
        static SummonedCombatMapObject Field(CombatSession session, GridPosition position)
        {
            var skill = Imported("PaladinJudgementField");
            var field = new SummonedCombatMapObject(Guid.NewGuid().ToString(), position,
                session.ActiveUnit.Character.CharacterId, skill.SkillId, 0, skill.Effects[0].SummonedObjectDefinition);
            session.SummonedObjects.Add(field); session.MapObjects.Add(field); return field;
        }
        static CombatAuraRuntime FireAura(BattleUnit unit, int radius = 1)
        {
            var aura = new CombatAuraRuntime { AuraId = "skill.mage.fire_aura.aura.0." + unit.Character.CharacterId,
                RemainingTurns = 3, Radius = radius, IncludesCaster = false };
            unit.ActiveAuras.Add(aura); return aura;
        }

        [TestCase("judgement", 4)] [TestCase("aura-embers", 4)]
        [TestCase("pull-dust", 1)] [TestCase("shout", 1)] [TestCase("knife-recover", 1)]
        public void PassFiveSeparatesPersistentAreaLoopsFromExecutionFreezes(string id, int count)
        {
            Assert.That(CombatTargetVfxResolver.TryResolve(id, out var clip), Is.True);
            Assert.That(clip.Frames.Length, Is.EqualTo(count));
            if (id == "shout")
            {
                Assert.That(clip.AboveHead, Is.True);
                Assert.That(clip.Offset.y, Is.EqualTo(-clip.ContentHeight - 4f / 32f).Within(.001f));
            }
            Assert.That(clip.FrameAt(clip.FrameSeconds * 1.01f, true), Is.SameAs(clip.Frames[count == 1 ? 0 : 1]));
            foreach (var frame in clip.Frames)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(frame.texture));
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point)); Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
                Assert.That(frame.pixelsPerUnit, Is.EqualTo(32));
            }
        }

        [Test] public void WhirlwindShowsAdjacentEmptyCellsAndAllThreeRealHitsPerEnemy()
        {
            var caster = Unit(true, 4, 4); var first = Unit(false, 5, 4); var second = Unit(false, 3, 3);
            var skill = Keep(UnityEngine.Object.Instantiate(Imported("WarriorWhirlwind")));
            Set(skill, "<SkillAccuracy>k__BackingField", 1000f); // Isolate the guaranteed-hit sequence from the imported79 accuracy.
            var cues = ExecuteSelected(Session(caster, first, second), skill, new[] { first, second });
            var ground = cues.Where(c => c.TargetVfx == CombatTargetVfxKind.SmokeGround).ToList();
            var hits = cues.Where(c => c.TargetVfx is CombatTargetVfxKind.CutBlood or CombatTargetVfxKind.CutFragments).ToList();
            Assert.That(ground.Count, Is.EqualTo(8)); Assert.That(ground.Select(c => c.Position), Has.No.Member(caster.Position));
            Assert.That(ground.Select(c => c.Position), Has.Member(new GridPosition(4, 5)));
            Assert.That(hits.Count, Is.EqualTo(6));
            foreach (var target in new[] { first, second })
                Assert.That(hits.Where(c => c.Target == target).Select(c => c.CueDelaySeconds), Is.EqualTo(new[] { 0f, .4f, .8f }));
        }

        [Test] public void WhirlwindImportedAccuracyEmitsContactsForRealHitsAndLeavesMissGaps()
        {
            var caster = Unit(true, 4, 4); var target = Unit(false, 5, 4); var session = Session(caster, target);
            var bus = new CombatFeedbackBus(); var hitDelays = new List<float>(); var cues = new List<CombatFeedbackEvent>(); var index = 0;
            bus.Published += cue =>
            {
                if (cue.Source == caster && cue.Target == target && cue.Type is CombatFeedbackType.Damage or CombatFeedbackType.Miss)
                { if (cue.Type == CombatFeedbackType.Damage) hitDelays.Add(index * .4f); index++; }
                if (cue.TargetVfx is CombatTargetVfxKind.CutBlood or CombatTargetVfxKind.CutFragments) cues.Add(cue);
            };
            Assert.That(new SkillExecutionSystem(feedbackBus: bus).TryExecute(session, caster, Imported("WarriorWhirlwind"),
                new Dictionary<int, IReadOnlyList<BattleUnit>> { { 0, new[] { target } } }, null, null, null, skipCosts: true), Is.True);
            Assert.That(index, Is.EqualTo(3)); Assert.That(cues.Select(c => c.CueDelaySeconds), Is.EqualTo(hitDelays));
        }

        [TestCase(0f, false)] [TestCase(1f, true)]
        public void ShoutWaveDoesNotInventSuccessfulStun(float chance, bool stunned)
        {
            var caster = Unit(true); var target = Unit(false, 4, 3); var effect = Effect(EffectType.ApplyStatus);
            Set(effect, "statusId", "stun"); Set(effect, "baseChance", chance); Set(effect, "alwaysHits", true);
            var cues = Cast(Session(caster, target), Skill("skill.warrior.shout", effect), target);
            Assert.That(cues.Single().TargetVfx, Is.EqualTo(CombatTargetVfxKind.Shout));
            Assert.That(StatusEffectSystem.HasStatus(target, "stun"), Is.EqualTo(stunned));
        }

        [TestCase(false)] [TestCase(true)]
        public void HookTrailUsesCommittedCellsAndStopsAtBlocker(bool blocked)
        {
            var caster = Unit(true, 1, 1); var target = Unit(false, 7, 4); var session = Session(caster, target);
            var committed = new List<GridPosition> { target.Position };
            session.SummonedObjectEnteredCell += moved => { if (moved == target) committed.Add(moved.Position); };
            if (blocked) session.Grid.SetBlocked(new GridPosition(5, 3));
            var pull = Effect(EffectType.Pull); Set(pull, "baseChance", 1f); Set(pull, "alwaysHits", true); Set(pull, "displacementDistance", 3);
            var cues = Cast(session, Skill("skill.scout.hook", pull), target);
            Assert.That(cues.Select(c => c.Position), Is.EqualTo(committed));
            Assert.That(cues.All(c => c.TargetVfx == CombatTargetVfxKind.PullDust), Is.True);
            if (blocked) Assert.That(committed, Has.No.Member(new GridPosition(5, 3)));
        }

        [TestCase(0f)] [TestCase(1f)]
        public void FailedHookHasNoDisplacementCue(float chance)
        {
            var caster = Unit(true); var target = Unit(false, 6, 3); var session = Session(caster, target); var before = target.Position;
            session.Grid.SetBlocked(new GridPosition(5, 3));
            var pull = Effect(EffectType.Pull); Set(pull, "baseChance", chance); Set(pull, "alwaysHits", true); Set(pull, "displacementDistance", 3);
            Assert.That(Cast(session, Skill("skill.scout.hook", pull), target), Is.Empty); Assert.That(target.Position, Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void SwapHasExactlyTwoGroundSnapshotsOnlyWhenItMoves(bool immobilized)
        {
            var caster = Unit(true); var target = Unit(false, 4, 3); var oldCaster = caster.Position; var oldTarget = target.Position;
            if (immobilized) caster.Character.PassiveEffects.Add(Status("immobilize"));
            var effect = Effect(EffectType.SwapPositions); Set(effect, "baseChance", 1f); Set(effect, "alwaysHits", true);
            var skill = Skill("skill.scout.swap", effect); var session = Session(caster, target);
            if (immobilized)
            {
                var bus = new CombatFeedbackBus(); var emitted = new List<CombatFeedbackEvent>(); bus.Published += emitted.Add;
                Assert.That(new SkillExecutionSystem(feedbackBus: bus).TryExecute(session, caster, skill,
                    new Dictionary<int, IReadOnlyList<BattleUnit>> { { 0, new[] { target } } }), Is.False);
                Assert.That(emitted.Any(c => c.Type == CombatFeedbackType.TargetEffect), Is.False);
                Assert.That(caster.Position, Is.EqualTo(oldCaster)); Assert.That(target.Position, Is.EqualTo(oldTarget)); return;
            }
            var cues = Cast(session, skill, target);
            Assert.That(cues.Count, Is.EqualTo(immobilized ? 0 : 2));
            if (!immobilized)
            {
                Assert.That(cues.Select(c => c.Position), Is.EquivalentTo(new[] { oldCaster, oldTarget }));
                Assert.That(caster.Position, Is.EqualTo(oldTarget)); Assert.That(target.Position, Is.EqualTo(oldCaster));
            }
        }

        [Test] public void ShadowShowsOnlyDepartureSnapshotAndDoesNotTrackInvisibleDestination()
        {
            var caster = Unit(true); var session = Session(caster); var before = caster.Position; var destination = new GridPosition(4, 3);
            var cues = ExecuteSelected(session, Imported("ScoutShadow"), new[] { caster }, teleport: destination);
            Assert.That(caster.Position, Is.EqualTo(destination)); Assert.That(StatusEffectSystem.IsInvisible(caster), Is.True);
            Assert.That(cues.Select(c => c.Position), Is.EqualTo(new[] { before }));
            Assert.That(cues.Single().TargetVfx, Is.EqualTo(CombatTargetVfxKind.SmokeGround));
        }

        [Test] public void HookKnifeRecoveryShowsPickupWithoutFakeDamageOrMovement()
        {
            var caster = Unit(true, 1, 1); caster.MaxKnives = 3; caster.AvailableKnives = 1;
            var session = Session(caster); var position = new GridPosition(3, 1);
            session.MapObjects.Add(new KnifeCombatMapObject("ground", position, caster.Character.CharacterId, 1));
            var cues = ExecuteSelected(session, Imported("ScoutHook"), Array.Empty<BattleUnit>(), position);
            Assert.That(caster.AvailableKnives, Is.EqualTo(2)); Assert.That(caster.Position, Is.EqualTo(new GridPosition(1, 1)));
            Assert.That(cues.Single().TargetVfx, Is.EqualTo(CombatTargetVfxKind.KnifeRecover)); Assert.That(cues.Single().Position, Is.EqualTo(position));
        }

        [Test] public void GroundingWaveMarksActualRelativeAreaAroundChosenCenter()
        {
            var caster = Unit(true, 1, 1); var session = Session(caster); var skill = Imported("PaladinGroundingWave"); var aim = new GridPosition(5, 5);
            var cues = new List<CombatFeedbackEvent>(); var bus = new CombatFeedbackBus(); bus.Published += cues.Add;
            CombatTargetVfxFeedback.PublishArea(bus, session, caster, skill, new Dictionary<int, GridPosition?> { { 0, aim } });
            var expected = new CombatTargetingService().GetRelativeRangePositions(session, caster, skill.TargetModes[0], aim)
                .Where(p => session.Grid.IsWalkable(p));
            Assert.That(cues.Select(c => c.Position), Is.EquivalentTo(expected)); Assert.That(cues.Select(c => c.Position), Has.No.Member(caster.Position));
        }

        [Test] public void FieldGeometryMatchesGameplayIncludingEmptyFloorAndExistingWalls()
        {
            var caster = Unit(true, 4, 4); var session = Session(caster); var field = Field(session, new GridPosition(4, 4));
            session.Grid.SetBlockedEdge(new GridPosition(4, 4), new GridPosition(5, 4));
            var expected = from x in Enumerable.Range(0, 9) from y in Enumerable.Range(0, 9)
                           let p = new GridPosition(x, y) where session.Grid.IsWalkable(p) && PaladinCombatSystem.FieldContains(session, field, p) select p;
            Assert.That(CombatPersistentVfxGeometry.FieldCells(session, field), Is.EquivalentTo(expected));
            field.RemainingDurationTurns = 0; Assert.That(CombatPersistentVfxGeometry.FieldCells(session, field), Is.Empty);
        }

        [Test] public void FieldTicksMarkOnlyActualEnemiesAndPersistentRefreshNeverInventsDamage()
        {
            var caster = Unit(true, 4, 4); var enemy = Unit(false, 5, 4); var ally = Unit(true, 4, 5); var outside = Unit(false, 8, 8);
            var session = Session(caster, enemy, ally, outside); var field = Field(session, caster.Position);
            Assert.That(PaladinCombatSystem.FieldContains(session, field, enemy.Position), Is.True);
            var cues = new List<CombatFeedbackEvent>(); var bus = new CombatFeedbackBus(); bus.Published += cues.Add;
            var executor = new SummonedObjectEffectExecutor(feedbackBus: bus);
            executor.ApplyPersistentEffects(session, field); Assert.That(cues, Is.Empty); Assert.That(enemy.Character.CurrentHealth, Is.EqualTo(10000));
            executor.ExecuteTurn(session, field);
            Assert.That(cues.Where(c => c.Type == CombatFeedbackType.TargetEffect).Select(c => c.Target), Is.EqualTo(new[] { enemy }));
            Assert.That(enemy.Character.CurrentHealth, Is.LessThan(10000)); Assert.That(ally.Character.CurrentHealth, Is.EqualTo(10000));
        }

        [Test] public void PersistentAreasReuseRenderersAnimateMoveHideAndCleanUpWithoutPlayMode()
        {
            var root = Keep(new GameObject("Persistent area verification")); var owner = root.AddComponent<CombatUnitViewSpawner>(); owner.enabled = false;
            var unit = Unit(true, 4, 4); var session = Session(unit); var aura = FireAura(unit);
            owner.RefreshPersistentAreaVfx(new[] { session }, 0);
            var first = root.GetComponentsInChildren<SpriteRenderer>(); Assert.That(first.Length, Is.EqualTo(8));
            var ids = first.Select(r => r.GetInstanceID()).ToArray(); var initial = first.Select(r => r.sprite).ToArray();
            owner.RefreshPersistentAreaVfx(new[] { session }, .25f);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Select(r => r.GetInstanceID()), Is.EquivalentTo(ids));
            Assert.That(first.Zip(initial, (r, s) => r.sprite != s).All(v => v), Is.True);
            unit.Position = new GridPosition(6, 6); owner.RefreshPersistentAreaVfx(new[] { session }, .26f);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Length, Is.EqualTo(8));
            unit.Character.PassiveEffects.Add(Status("invisibility")); owner.RefreshPersistentAreaVfx(new[] { session }, .27f);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>().All(r => r.forceRenderingOff), Is.True);
            aura.RemainingTurns = 0; owner.RefreshPersistentAreaVfx(new[] { session }, .28f);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>(), Is.Empty);
            Field(session, new GridPosition(4, 4)); owner.RefreshPersistentAreaVfx(new[] { session }, .3f);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Length, Is.GreaterThan(0));
            owner.GetType().GetMethod("OnDisable", Private).Invoke(owner, null);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>(), Is.Empty);
        }

        [Test] public void PersistentAreaBudgetIsBoundedAndEndedSessionsReleaseEverything()
        {
            var root = Keep(new GameObject("Area renderer budget")); var owner = root.AddComponent<CombatUnitViewSpawner>(); owner.enabled = false;
            var units = new[] { Unit(true, 5, 5), Unit(true, 14, 5), Unit(true, 5, 14), Unit(true, 14, 14) };
            var session = Session(units); session.Grid = new BattleGrid(25, 25);
            foreach (var unit in units) FireAura(unit, 3);
            owner.RefreshPersistentAreaVfx(new[] { session }, 0);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Length, Is.EqualTo(128));
            owner.RefreshPersistentAreaVfx(Array.Empty<CombatSession>(), 1);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>(), Is.Empty);
        }
    }
}
