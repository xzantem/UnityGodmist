using System;
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
        void FleshTarget(BattleUnit target)
        {
            target.Character.Definition = Keep(UnityEngine.Object.Instantiate(target.Character.Definition));
            Set(target.Character.Definition, "<Id>k__BackingField", "jerzy");
        }
        SkillData EnemyVfxSkill(string id)
        {
            string path;
            if (id == "skill.enemy.bone_sword") path = "Assets/_Project/ScriptableObjects/Skills/SD_EnemyStrike.asset";
            else if (id.StartsWith("skill.enemy.catacombs.")) path = "Assets/_Project/ScriptableObjects/Catacombs/Skills/SD_Catacombs_" + id.Split('.').Last() + ".asset";
            else
            {
                var suffix = id.Split('.').Last();
                path = "Assets/_Project/ScriptableObjects/Dungeons/Forest/SD_Forest_" + char.ToUpperInvariant(suffix[0]) + suffix.Substring(1) + ".asset";
            }
            var original = AssetDatabase.LoadAssetAtPath<SkillData>(path); Assert.That(original, Is.Not.Null, path);
            var skill = Keep(UnityEngine.Object.Instantiate(original)); Set(skill, "<SkillAccuracy>k__BackingField", 1000f); return skill;
        }
        static List<CombatFeedbackEvent> EnemyCast(CombatSession session, SkillData skill, BattleUnit target)
        {
            var targets = new Dictionary<int, IReadOnlyList<BattleUnit>> { { 0, new[] { target } } };
            for (var mode = 1; mode < skill.TargetModes.Count; mode++) targets[mode] = new[] { session.ActiveUnit };
            var bus = new CombatFeedbackBus(); var events = new List<CombatFeedbackEvent>(); bus.Published += events.Add;
            Assert.That(new SkillExecutionSystem(feedbackBus: bus, statusEffectSystem: new StatusEffectSystem()).TryExecute(
                session, session.ActiveUnit, skill, targets, null, null, null, skipCosts: true), Is.True);
            return events.Where(e => e.TargetVfx.HasValue).ToList();
        }

        [TestCase("fang-contact")] [TestCase("spectral-contact")] [TestCase("ancient-contact")] [TestCase("spectral-landing")]
        public void PassEightEnemyContactsAreSingleFrameAndKeepPixelImportProfile(string id)
        {
            Assert.That(CombatTargetVfxResolver.TryResolve(id, out var clip), Is.True);
            Assert.That(clip.Frames.Length, Is.EqualTo(1)); Assert.That(clip.RepeatSeconds, Is.Zero); Assert.That(clip.Opacity, Is.EqualTo(1));
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip.Frames[0].texture));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point)); Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed)); Assert.That(clip.Frames[0].pixelsPerUnit, Is.EqualTo(32));
            if (id == "spectral-landing") Assert.That(clip.Offset, Is.EqualTo(Vector2.zero));
        }

        [TestCase("skill.enemy.catacombs.bone_club", CombatTargetVfxKind.Blunt, 1)]
        [TestCase("skill.enemy.catacombs.bone_sword", CombatTargetVfxKind.CutBlood, 1)]
        [TestCase("skill.enemy.catacombs.cross_cut", CombatTargetVfxKind.CutBlood, 2)]
        [TestCase("skill.enemy.catacombs.rend", CombatTargetVfxKind.CutBlood, 1)]
        [TestCase("skill.enemy.catacombs.manic_bite", CombatTargetVfxKind.FangContact, 1)]
        [TestCase("skill.enemy.catacombs.spectral_knight_strike", CombatTargetVfxKind.SpectralContact, 1)]
        [TestCase("skill.enemy.forest.bandit_knife", CombatTargetVfxKind.CutBlood, 1)]
        [TestCase("skill.enemy.forest.bandit_backstab", CombatTargetVfxKind.PierceBlood, 1)]
        [TestCase("skill.enemy.forest.poacher_knife", CombatTargetVfxKind.CutBlood, 1)]
        [TestCase("skill.enemy.forest.wolf_bite", CombatTargetVfxKind.FangContact, 1)]
        [TestCase("skill.enemy.bone_sword", CombatTargetVfxKind.CutBlood, 1)]
        public void ImportedPhysicalEnemyContactsFollowActualHitsShieldAndMisses(string id, CombatTargetVfxKind fleshKind, int count)
        {
            var caster = Unit(false); var target = Unit(true, 4, 3); FleshTarget(target); var session = Session(caster, target);
            var skill = EnemyVfxSkill(id);
            var cues = EnemyCast(session, skill, target);
            Assert.That(cues.Count, Is.EqualTo(count)); Assert.That(cues.All(e => e.TargetVfx == fleshKind), Is.True);
            Assert.That(cues.Select(e => e.CueDelaySeconds), Is.EqualTo(Enumerable.Range(0, count).Select(i => i * .4f)));
            target.Character.ShieldPool.AddLayer(10000);
            cues = EnemyCast(session, skill, target);
            var shieldKind = fleshKind is CombatTargetVfxKind.Blunt or CombatTargetVfxKind.SpectralContact ? fleshKind : CombatTargetVfxKind.CutFragments;
            Assert.That(cues.Count, Is.EqualTo(count)); Assert.That(cues.All(e => e.TargetVfx == shieldKind), Is.True);
            target.Character.Stats.Dodge = new Godmist.Core.Types.Stat(1e20f);
            Assert.That(EnemyCast(session, skill, target), Is.Empty);
        }

        [TestCase("enemy.catacombs.swordsman")] [TestCase("enemy.catacombs.archer")]
        [TestCase("enemy.catacombs.defender")] [TestCase("enemy.catacombs.occultist")]
        [TestCase("enemy.catacombs.wraith")] [TestCase("enemy.catacombs.spectral_knight")]
        [TestCase("enemy.forest.infected_tree")]
        public void ShippedNonFleshIdsDoNotNeedSpeciesWordsToAvoidBlood(string id)
        {
            var caster = Unit(false); var target = Unit(true, 4, 3);
            target.Character.Definition = Keep(UnityEngine.Object.Instantiate(target.Character.Definition));
            Set(target.Character.Definition, "<Id>k__BackingField", id);
            var cues = EnemyCast(Session(caster, target), EnemyVfxSkill("skill.enemy.forest.wolf_bite"), target);
            Assert.That(cues.Single().TargetVfx, Is.EqualTo(CombatTargetVfxKind.CutFragments));
        }

        [Test] public void AncientIncantationContactDoesNotInventImmobilizationAndRespectsRealDamageOutcome()
        {
            var caster = Unit(false); var target = Unit(true, 4, 3); var session = Session(caster, target);
            var skill = EnemyVfxSkill("skill.enemy.catacombs.ancient_incantation"); Set(skill.Effects[1], "baseChance", 0f);
            target.Character.Stats.Dodge = new Godmist.Core.Types.Stat(1e20f);
            Assert.That(EnemyCast(session, skill, target).Single().TargetVfx, Is.EqualTo(CombatTargetVfxKind.AncientContact));
            Assert.That(StatusEffectSystem.HasStatus(target, StatusEffectIds.Immobilize), Is.False);
            Set(skill.Effects[0], "damageType", DamageType.Physical);
            Assert.That(EnemyCast(session, skill, target), Is.Empty);
        }

        [TestCase("skill.enemy.forest.wolf_leap", CombatTargetVfxKind.PullDust, 1)]
        [TestCase("skill.enemy.catacombs.disorienting_leap", CombatTargetVfxKind.PullDust, 2)]
        [TestCase("skill.enemy.catacombs.fright", CombatTargetVfxKind.SpectralLanding, 1)]
        public void ImportedLeapsMarkOnlyRealStartAndLandingAndDoNotInventDamage(string id, CombatTargetVfxKind ground, int count)
        {
            var caster = Unit(false, 2, 4); var target = Unit(true, 4, 4); var session = Session(caster, target); var before = caster.Position;
            var cues = EnemyCast(session, EnemyVfxSkill(id), target);
            Assert.That(caster.Position, Is.Not.EqualTo(before));
            var landings = cues.Where(e => CombatTargetVfxFeedback.IsGround(e.TargetVfx.Value)).ToArray();
            Assert.That(landings.Length, Is.EqualTo(count)); Assert.That(landings.All(e => e.TargetVfx == ground), Is.True);
            Assert.That(landings.Last().Position, Is.EqualTo(caster.Position));
            if (count == 2) Assert.That(landings.First().Position, Is.EqualTo(before));
            if (id.EndsWith("fright"))
            {
                var contact = cues.Single(e => e.TargetVfx == CombatTargetVfxKind.SpectralContact);
                Assert.That(contact.Position, Is.EqualTo(target.Position)); Assert.That(contact.ActionId, Is.EqualTo(landings[0].ActionId));
            }
            else Assert.That(cues.All(e => CombatTargetVfxFeedback.IsGround(e.TargetVfx.Value)), Is.True);
        }

        [TestCase(false)] [TestCase(true)]
        public void ChargeSuccessWithoutMovementOrPartialStopCannotMarkRequestedEndpoint(bool partial)
        {
            var caster = Unit(false, 2, 4); var target = Unit(true, 4, 4); var session = Session(caster, target); var before = caster.Position;
            var skill = EnemyVfxSkill("skill.enemy.forest.wolf_leap"); Set(skill, "<RequiresLineOfSight>k__BackingField", false);
            session.Grid.SetBlockedEdge(partial ? new GridPosition(3, 4) : before, partial ? target.Position : new GridPosition(3, 4));
            var cues = EnemyCast(session, skill, target);
            if (!partial) { Assert.That(caster.Position, Is.EqualTo(before)); Assert.That(cues, Is.Empty); }
            else { Assert.That(caster.Position, Is.EqualTo(new GridPosition(3, 4))); Assert.That(cues.Single().Position, Is.EqualTo(caster.Position)); }
        }

        [Test] public void ImmobilizedOrHiddenLeaperHasNoDestinationBeacon()
        {
            var caster = Unit(false, 2, 4); var target = Unit(true, 4, 4); var session = Session(caster, target); var skill = EnemyVfxSkill("skill.enemy.forest.wolf_leap");
            caster.Character.PassiveEffects.Add(Status(StatusEffectIds.Immobilize));
            var events = new List<CombatFeedbackEvent>(); var bus = new CombatFeedbackBus(); bus.Published += events.Add;
            Assert.That(new SkillExecutionSystem(feedbackBus: bus).TryExecute(session, caster, skill,
                new Dictionary<int, IReadOnlyList<BattleUnit>> { { 0, new[] { target } }, { 1, new[] { caster } } }, null, null, null, skipCosts: true), Is.False);
            Assert.That(events.Any(e => e.TargetVfx.HasValue), Is.False);
            caster.Character.PassiveEffects.Clear(); caster.Character.PassiveEffects.Add(Status(StatusEffectIds.Invisibility));
            var cues = EnemyCast(session, skill, target); Assert.That(caster.Position, Is.Not.EqualTo(new GridPosition(2, 4))); Assert.That(cues, Is.Empty);
        }

        [Test] public void SavageRoarShowsExactEmptyCellsAndOneSoundCuePerRecipientEvenWhenStatusesFail()
        {
            var caster = Unit(false, 8, 8); var first = Unit(true, 9, 8); var second = Unit(true, 8, 9);
            var session = Session(caster, first, second); session.Grid = new BattleGrid(17, 17);
            var skill = EnemyVfxSkill("skill.enemy.catacombs.savage_roar"); foreach (var effect in skill.Effects) Set(effect, "baseChance", 0f);
            var cues = ExecuteSelected(session, skill, new[] { first, second });
            var heads = cues.Where(e => e.TargetVfx == CombatTargetVfxKind.Shout).ToArray();
            var floor = cues.Where(e => e.TargetVfx == CombatTargetVfxKind.PullDust).ToArray();
            Assert.That(heads.Select(e => e.Target), Is.EquivalentTo(new[] { first, second }));
            Assert.That(cues.Take(2).All(e => e.TargetVfx == CombatTargetVfxKind.Shout), Is.True);
            var expected = new CombatTargetingService().GetPositionsInRange(session, caster.Position, skill.TargetModes[0].AbsoluteRange);
            var area = new HashSet<GridPosition>(expected);
            var contour = expected.Where(p => !area.Contains(new(p.X - 1, p.Y, p.Level)) || !area.Contains(new(p.X + 1, p.Y, p.Level))
                || !area.Contains(new(p.X, p.Y - 1, p.Level)) || !area.Contains(new(p.X, p.Y + 1, p.Level)));
            Assert.That(floor.Select(e => e.Position), Is.EquivalentTo(contour));
            Assert.That(cues.Count, Is.LessThan(128));
            Assert.That(floor.Any(e => e.Position != first.Position && e.Position != second.Position), Is.True);
            Assert.That(first.Character.PassiveEffects.Concat(second.Character.PassiveEffects).Any(e => e.EffectId == StatusEffectIds.Stun || e.EffectId == StatusEffectIds.Bleed), Is.False);
            Assert.That(cues.Select(e => e.ActionId).Distinct().Count(), Is.EqualTo(1));

            var root = Keep(new GameObject("consecutive roar budget")); var owner = root.AddComponent<CombatUnitViewSpawner>(); owner.enabled = false;
            foreach (var cue in cues) Assert.That(owner.TryPlayTargetVfx(session, cue, Time.time + 2), Is.True);
            CombatTargetVfxResolver.TryResolve("heal", out var art);
            while (Effects(owner).Count < 128)
                Assert.That(owner.TryPlayWorldEffect(session, caster.Position, art.Frames[0], Vector2.zero, 1), Is.True);
            foreach (var effect in Effects(owner)) effect.GetType().GetField("Ends").SetValue(effect, Time.time - 1);
            foreach (var cue in cues)
                Assert.That(owner.TryPlayTargetVfx(session, new CombatFeedbackEvent(cue.Target, CombatFeedbackType.TargetEffect,
                    source: caster, position: cue.Position, targetVfx: cue.TargetVfx, actionId: "next-roar"), Time.time + 2), Is.True);
            Assert.That(Effects(owner).Count, Is.EqualTo(cues.Count));
        }
    }
}
