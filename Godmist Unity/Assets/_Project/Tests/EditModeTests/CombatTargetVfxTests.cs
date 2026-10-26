using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Views.Characters;
using Godmist.Views.Combat;
using Godmist.Views.Exploration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed partial class CombatTargetVfxTests
    {
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        readonly List<Object> created = new();
        UnityEngine.Random.State previousRandom;
        [SetUp] public void Seed() { previousRandom = UnityEngine.Random.state; UnityEngine.Random.InitState(8123); }
        [TearDown] public void Cleanup()
        { foreach (var o in created) if (o != null) Object.DestroyImmediate(o); created.Clear(); UnityEngine.Random.state = previousRandom; }
        T Keep<T>(T value) where T : Object { created.Add(value); return value; }
        static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Private).SetValue(obj, value);
        static BattleUnit Unit(bool player, int x = 3, int y = 3)
        {
            var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_swordsman.asset");
            var character = new CharacterInstanceFactory().Create(definition, definition.ClassData, 1);
            character.CharacterId += Guid.NewGuid();
            character.Stats.MinAttack = new Stat(100); character.Stats.MaxAttack = new Stat(100);
            character.Stats.CritChance = new Stat(0); character.Stats.Dodge = new Stat(0);
            character.Stats.PhysicalDefense = new Stat(0); character.Stats.MaxHealth = new Stat(10000); character.CurrentHealth = 10000;
            return new BattleUnit { Character = character, Position = new(x, y), IsPlayerSide = player, CurrentActionPoints = 10000, MaxActionPoints = 10000 };
        }
        static CombatSession Session(params BattleUnit[] units)
        {
            var session = new CombatSession { Grid = new BattleGrid(9, 9), Units = units.ToList(), ActiveUnit = units[0] };
            foreach (var unit in units) unit.WarriorState.Session = session;
            return session;
        }
        SkillData Skill(string id, params SkillEffectData[] effects)
        {
            var skill = Keep(ScriptableObject.CreateInstance<SkillData>());
            Set(skill, "<SkillId>k__BackingField", id); Set(skill, "<DisplayName>k__BackingField", "VFX test");
            Set(skill, "<SkillAccuracy>k__BackingField", 1000f); Set(skill, "effects", effects.ToList());
            Set(skill, "targetModes", new List<SkillTargetModeData> { new() });
            return skill;
        }
        static SkillEffectData Effect(EffectType type)
        { var effect = new SkillEffectData(); Set(effect, "effectType", type); Set(effect, "alwaysHits", false); return effect; }
        static List<CombatFeedbackEvent> Cast(CombatSession session, SkillData skill, params BattleUnit[] targets)
        {
            var feedback = new CombatFeedbackBus(); var events = new List<CombatFeedbackEvent>(); feedback.Published += events.Add;
            Assert.That(new SkillExecutionSystem(statusEffectSystem: new StatusEffectSystem(), feedbackBus: feedback).TryExecute(
                session, session.ActiveUnit, skill, new Dictionary<int, IReadOnlyList<BattleUnit>> { { 0, targets } }), Is.True);
            return events.Where(e => e.Type == CombatFeedbackType.TargetEffect).ToList();
        }

        [TestCase(false, false, 1)] [TestCase(true, false, 0)] [TestCase(false, true, 1)]
        public void CutContactFollowsRealHitIncludingFullShieldAbsorption(bool miss, bool shield, int expected)
        {
            var caster = Unit(true); var target = Unit(false, 4, 3);
            if (miss) target.Character.Stats.Dodge = new Stat(1e20f);
            if (shield) target.Character.ShieldPool.AddLayer(10000);
            var effects = Cast(Session(caster, target), Skill("skill.warrior.cut", Effect(EffectType.DealDamage)), target);
            Assert.That(effects.Count, Is.EqualTo(expected));
            if (shield)
            {
                Assert.That(target.Character.CurrentHealth, Is.EqualTo(10000));
                Assert.That(effects.Single().TargetVfx, Is.EqualTo(CombatTargetVfxKind.CutFragments));
            }
        }

        [TestCase("enemy.catacombs.skeleton_slayer")] [TestCase("enemy.catacombs.dead_king")]
        [TestCase("enemy.forest.ent")]
        public void NonFleshTargetsUseFragmentsEvenWhenHealthIsDamaged(string id)
        {
            var caster = Unit(true); var target = Unit(false, 4, 3);
            var definition = Keep(Object.Instantiate(target.Character.Definition));
            Set(definition, "<Id>k__BackingField", id); target.Character.Definition = definition;
            var cues = Cast(Session(caster, target), Skill("skill.warrior.cut", Effect(EffectType.DealDamage)), target);
            Assert.That(target.Character.CurrentHealth, Is.LessThan(10000));
            Assert.That(cues.Single().TargetVfx, Is.EqualTo(CombatTargetVfxKind.CutFragments));
        }

        [Test] public void CutSnapshotStaysAtContactBeforeSubsequentPush()
        {
            var caster = Unit(true); var target = Unit(false, 4, 3); var old = target.Position;
            var push = Effect(EffectType.Push); Set(push, "displacementDistance", 1);
            var effects = Cast(Session(caster, target), Skill("skill.warrior.cut", Effect(EffectType.DealDamage), push), target);
            Assert.That(target.Position, Is.Not.EqualTo(old));
            Assert.That(effects.Single().Position, Is.EqualTo(old));
        }

        [Test] public void FireImpactOccursAtAimOnMissWithoutInventingBurnStatusOrArea()
        {
            var caster = Unit(true); var target = Unit(false, 4, 3); target.Character.Stats.Dodge = new Stat(1e20f);
            var effects = Cast(Session(caster, target), Skill("skill.mage.fireball", Effect(EffectType.DealDamage)), target);
            Assert.That(effects.Count, Is.EqualTo(1)); Assert.That(effects[0].TargetVfx, Is.EqualTo(CombatTargetVfxKind.Fire));
            Assert.That(effects[0].Position, Is.EqualTo(target.Position));
            Assert.That(StatusEffectSystem.HasStatus(target, StatusEffectIds.Burn), Is.False);
        }

        [TestCase("skill.paladin.heal", 1)] [TestCase("skill.paladin.group_heal", 2)]
        public void PaladinHealMarksEveryValidRecipientIncludingFullHealth(string id, int count)
        {
            var caster = Unit(true); var first = Unit(true, 4, 3); var second = Unit(true, 3, 4);
            first.Character.CurrentHealth = 9000;
            var mode = new SkillTargetModeData(); Set(mode, "targetingType", TargetingType.Allies);
            var skill = Skill(id, Effect(EffectType.PaladinHeal)); Set(skill, "targetModes", new List<SkillTargetModeData> { mode });
            var targets = count == 1 ? new[] { first } : new[] { first, second };
            var effects = Cast(Session(caster, first, second), skill, targets);
            Assert.That(effects.Select(e => e.Target), Is.EquivalentTo(targets));
            Assert.That(effects.All(e => e.TargetVfx == CombatTargetVfxKind.Heal), Is.True);
        }

        [Test] public void TremorsIncludeEmptyCellsButNotRoundedRadialCornersOrMissingFloor()
        {
            var caster = Unit(false, 4, 4); var session = Session(caster);
            var mode = new SkillTargetModeData(); Set(mode, "targetType", TargetType.AreaAroundCaster);
            Set(mode, "absoluteRange", new SkillRangeData(RangeShape.Radial, 0, 2));
            var skill = Skill("skill.enemy.catacombs.skeleton_slayer_tremors"); Set(skill, "targetModes", new List<SkillTargetModeData> { mode });
            var feedback = new CombatFeedbackBus(); var cues = new List<CombatFeedbackEvent>(); feedback.Published += cues.Add;
            CombatTargetVfxFeedback.PublishArea(feedback, session, caster, skill);
            Assert.That(cues.Count, Is.EqualTo(21));
            Assert.That(cues.Select(e => e.Position), Has.Member(new GridPosition(6, 5)));
            Assert.That(cues.Select(e => e.Position), Has.No.Member(new GridPosition(6, 6)));
            Assert.That(cues.All(e => e.TargetVfx == CombatTargetVfxKind.Tremor), Is.True);
            session.Grid = new BattleGrid(9, 9, true);
            session.Grid.SetWalkable(caster.Position); session.Grid.SetWalkable(new GridPosition(5, 4)); cues.Clear();
            CombatTargetVfxFeedback.PublishArea(feedback, session, caster, skill);
            Assert.That(cues.Select(e => e.Position), Is.EquivalentTo(new[] { caster.Position, new GridPosition(5, 4) }));
        }

        [Test] public void UnmappedSkillsHaveNoIncidentalContactCue()
        { var a = Unit(true); var b = Unit(false, 4, 3); Assert.That(Cast(Session(a, b), Skill("skill.test.unknown", Effect(EffectType.DealDamage)), b), Is.Empty); }

        [TestCase("heal", 1)] [TestCase("cut", 1)] [TestCase("fragments", 1)] [TestCase("fire", 1)]
        [TestCase("tremor", 1)] [TestCase("burn", 4)] [TestCase("bleed", 4)]
        [TestCase("energy-impact", 1)] [TestCase("energy-ground", 1)] [TestCase("fire-ground", 1)]
        [TestCase("pierce", 1)] [TestCase("smoke", 1)] [TestCase("smoke-hit", 1)] [TestCase("arrow-ground", 1)]
        [TestCase("arcane-ward", 1)] [TestCase("holy-grace", 1)] [TestCase("rally", 1)]
        [TestCase("holy-impact", 1)] [TestCase("holy-blind", 1)] [TestCase("arcane-focus", 1)]
        public void SkillFreezesAndAnimatedStatusesUsePixelArtImportAndRegisteredTiming(string id, int count)
        {
            Assert.That(CombatTargetVfxResolver.TryResolve(id, out var clip), Is.True);
            Assert.That(clip.Frames.Length, Is.EqualTo(count));
            Assert.That(clip.FrameAt(clip.FrameSeconds * 1.01f), Is.SameAs(clip.Frames[count == 1 ? 0 : 1]));
            Assert.That(clip.FrameAt(clip.Duration + .001f, true), Is.SameAs(clip.Frames[0]));
            foreach (var sprite in clip.Frames)
            {
                Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(64, 64))); Assert.That(sprite.pixelsPerUnit, Is.EqualTo(32));
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite.texture));
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point)); Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            }
        }

        static PassiveEffectRuntime Status(string id) => new() { EffectId = id, Lifetime = PassiveEffectLifetime.Timed, RemainingDuration = 3 };
        [Test] public void PersistentStatusRenderersCleanUpOnExpiryHiddenBodyDeathAndRebind()
        {
            var root = Keep(new GameObject("Status cleanup test")); var renderer = root.AddComponent<SpriteRenderer>();
            var unit = Unit(true); var burn = Status(StatusEffectIds.Burn); unit.Character.PassiveEffects.Add(burn);
            unit.Character.PassiveEffects.Add(Status(StatusEffectIds.Bleed));
            using var player = new CombatStatusVfxPlayer(renderer); player.Tick(unit.Character, unit);
            var effects = root.GetComponentsInChildren<SpriteRenderer>().Where(r => r != renderer).ToArray();
            Assert.That(effects.Count(r => r.enabled), Is.EqualTo(2));
            burn.RemainingDuration = 0; player.Tick(unit.Character, unit); Assert.That(effects.Count(r => r.enabled), Is.EqualTo(1));
            renderer.forceRenderingOff = true; player.Tick(unit.Character, unit); Assert.That(effects.All(r => !r.enabled), Is.True);
            renderer.forceRenderingOff = false; player.Tick(unit.Character, unit); Assert.That(effects.Count(r => r.enabled), Is.EqualTo(1));
            unit.Character.LifeState = CharacterLifeState.Dead; player.Tick(unit.Character, unit); Assert.That(effects.All(r => !r.enabled), Is.True);
            player.Tick(Unit(true).Character); Assert.That(effects.All(r => !r.enabled), Is.True);
        }

        [TestCase(false)] [TestCase(true)]
        public void BothCharacterViewOwnersPresentStatusAndHideOnDisable(bool exploration)
        {
            var root = Keep(new GameObject("View status integration")); var renderer = root.AddComponent<SpriteRenderer>();
            var unit = Unit(true); unit.Character.PassiveEffects.Add(Status(StatusEffectIds.Burn));
            MonoBehaviour view;
            if (exploration)
            {
                var value = root.AddComponent<ExplorationCharacterView>(); Set(value, "bodyRenderer", renderer);
                typeof(ExplorationCharacterView).GetProperty("RuntimeCharacter").SetValue(value, unit.Character); view = value;
            }
            else { var value = root.AddComponent<CharacterView>(); Set(value, "bodyRenderer", renderer); value.Bind(unit); view = value; }
            view.GetType().GetMethod("LateUpdate", Private).Invoke(view, null);
            var effect = root.GetComponentsInChildren<SpriteRenderer>().Single(r => r != renderer);
            Assert.That(effect.enabled, Is.True);
            view.GetType().GetMethod("OnDisable", Private).Invoke(view, null); Assert.That(effect.enabled, Is.False);
        }

        [Test] public void InvisibleUnitHasNeitherPersistentBeaconNorContactRenderer()
        {
            var root = Keep(new GameObject("Invisible VFX test")); var body = root.AddComponent<SpriteRenderer>();
            var unit = Unit(false); unit.Character.PassiveEffects.Add(Status(StatusEffectIds.Burn));
            unit.Character.PassiveEffects.Add(Status(StatusEffectIds.Invisibility));
            using var player = new CombatStatusVfxPlayer(body); player.Tick(unit.Character, unit);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Count(r => r != body), Is.Zero);
            var spawner = root.AddComponent<CombatUnitViewSpawner>(); spawner.enabled = false;
            Assert.That(spawner.TryPlayTargetVfx(Session(unit), new CombatFeedbackEvent(unit, CombatFeedbackType.TargetEffect,
                targetVfx: CombatTargetVfxKind.Fire)), Is.False);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Count(r => r != body), Is.Zero);
        }

        [TestCase("stun", 4)] [TestCase("poison", 4)] [TestCase("immobilize", 4)]
        [TestCase("freeze", 1)] [TestCase("paralysis", 1)] [TestCase("blunt", 1)]
        public void PassTwoCatalogSeparatesLoopsAndStaticOverlays(string id, int count)
        {
            Assert.That(CombatTargetVfxResolver.TryResolve(id, out var clip), Is.True);
            Assert.That(clip.Frames.Length, Is.EqualTo(count));
            Assert.That(clip.FitBody, Is.EqualTo(id is "freeze" or "paralysis"));
            Assert.That(clip.AboveHead, Is.EqualTo(id == "stun"));
            Assert.That(clip.FrameAt(clip.Duration * 3.25f, true), Is.SameAs(clip.Frames[count == 1 ? 0 : 1]));
        }

        [TestCase("freeze")] [TestCase("paralysis")]
        public void StaticStatusHoldsActorFrameUntilRemovedAndDoesNotOverrideDeath(string id)
        {
            var root = Keep(new GameObject("Static overlay pose test")); var body = root.AddComponent<SpriteRenderer>();
            var texture = Keep(new Texture2D(16, 24));
            var first = Keep(Sprite.Create(texture, new Rect(0, 0, 16, 24), Vector2.zero, 32));
            var second = Keep(Sprite.Create(texture, new Rect(0, 0, 8, 12), Vector2.zero, 32));
            body.sprite = first; var unit = Unit(true); unit.Character.PassiveEffects.Add(Status(id));
            using var player = new CombatStatusVfxPlayer(body); player.Tick(unit.Character, unit);
            var overlay = root.GetComponentsInChildren<SpriteRenderer>().Single(r => r.name == "Status VFX " + id);
            var wash = root.GetComponentsInChildren<SpriteRenderer>().Single(r => r.name == "Hard Status Tint");
            var originalColor = body.color;
            Assert.That(overlay.enabled, Is.True); var image = overlay.sprite;
            body.sprite = second; player.Tick(unit.Character, unit);
            Assert.That(body.sprite, Is.SameAs(first)); Assert.That(overlay.sprite, Is.SameAs(image));
            Assert.That(wash.sprite, Is.SameAs(first)); Assert.That(wash.color.a, Is.InRange(.01f, .99f));
            Assert.That(body.color, Is.EqualTo(originalColor));
            unit.Character.PassiveEffects.Clear(); body.sprite = second; player.Tick(unit.Character, unit);
            Assert.That(body.sprite, Is.SameAs(second)); Assert.That(overlay.enabled, Is.False);
            Assert.That(wash.enabled, Is.False);
            unit.Character.PassiveEffects.Add(Status(id)); body.sprite = first; player.Tick(unit.Character, unit);
            unit.Character.LifeState = CharacterLifeState.Dead; body.sprite = second; player.Tick(unit.Character, unit);
            Assert.That(body.sprite, Is.SameAs(second)); Assert.That(overlay.enabled, Is.False);
        }

        [Test] public void StatusPriorityKeepsTwoRenderersAndPromotesRemainingEffects()
        {
            var root = Keep(new GameObject("Status priority test")); var body = root.AddComponent<SpriteRenderer>(); var unit = Unit(true);
            foreach (var id in new[] { "poison", "bleed", "burn", "immobilize", "stun", "paralysis", "freeze" }) unit.Character.PassiveEffects.Add(Status(id));
            using var player = new CombatStatusVfxPlayer(body);
            void Check(params string[] expected)
            {
                player.Tick(unit.Character, unit);
                var effects = root.GetComponentsInChildren<SpriteRenderer>().Where(r => r != body).ToArray();
                Assert.That(effects.Length, Is.EqualTo(2));
                Assert.That(effects.Where(r => r.enabled).Select(r => r.name.Replace("Status VFX ", "")), Is.EquivalentTo(expected));
            }
            Check("freeze", "stun");
            unit.Character.PassiveEffects.RemoveAll(e => e.EffectId == "freeze"); Check("paralysis", "stun");
            unit.Character.PassiveEffects.RemoveAll(e => e.EffectId == "paralysis"); Check("stun", "immobilize");
            unit.Character.PassiveEffects.RemoveAll(e => e.EffectId is "stun" or "immobilize"); Check("burn", "bleed");
            unit.Character.PassiveEffects.RemoveAll(e => e.EffectId is "burn" or "bleed"); Check("poison");
        }

        [Test] public void StunUsesMeasuredActorHeadInsteadOfTransparentCanvasTop()
        {
            var texture = Resources.Load<Texture2D>("Godmist/CharacterAnimations/reimond/reimond-s-idle-00-64");
            var sprite = Keep(Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(.5f, .125f), 32)); sprite.name = "reimond-s-idle-00-64";
            var root = Keep(new GameObject("Measured head anchor")); var body = root.AddComponent<SpriteRenderer>(); body.sprite = sprite;
            var unit = Unit(true); unit.Character.PassiveEffects.Add(Status("stun"));
            using var player = new CombatStatusVfxPlayer(body); player.Tick(unit.Character, unit);
            var effect = root.GetComponentsInChildren<SpriteRenderer>().Single(r => r != body);
            var bounds = CombatTargetVfxResolver.ActorVisibleBounds(sprite);
            Assert.That(bounds.max.y, Is.LessThan(sprite.bounds.max.y));
            Assert.That(CombatTargetVfxResolver.TryResolve("stun", out var clip), Is.True);
            Assert.That(effect.transform.localPosition.y - clip.ContentHeight * .5f, Is.EqualTo(bounds.max.y + 2f / 32f).Within(.001f));
        }

        [Test] public void HardStatusCrustFitsMeasuredFeetAndHeightWithoutCanvasPadding()
        {
            var texture = Resources.Load<Texture2D>("Godmist/CharacterAnimations/reimond/reimond-s-idle-00-64");
            var sprite = Keep(Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(.5f, .125f), 32)); sprite.name = "reimond-s-idle-00-64";
            var root = Keep(new GameObject("Measured crust registration")); var body = root.AddComponent<SpriteRenderer>(); body.sprite = sprite;
            var unit = Unit(true); unit.Character.PassiveEffects.Add(Status("freeze"));
            using var player = new CombatStatusVfxPlayer(body); player.Tick(unit.Character, unit);
            var crust = root.GetComponentsInChildren<SpriteRenderer>().Single(r => r.name == "Status VFX freeze");
            var bounds = CombatTargetVfxResolver.ActorVisibleBounds(sprite);
            Assert.That(crust.transform.localPosition.y, Is.EqualTo(bounds.min.y).Within(.001f));
            Assert.That(crust.transform.localPosition.x, Is.EqualTo(bounds.center.x).Within(.001f));
            Assert.That(CombatTargetVfxResolver.TryResolve("freeze", out var clip), Is.True);
            Assert.That(crust.transform.localScale.y * clip.ContentHeight, Is.EqualTo(bounds.size.y).Within(.001f));
        }

        [TestCase("skill.warrior.right_hook", false)] [TestCase("skill.warrior.right_hook", true)]
        [TestCase("skill.enemy.catacombs.skeleton_slayer_crushing", false)]
        public void BluntImpactIsAStaticContactOnRealHits(string id, bool miss)
        {
            var caster = Unit(true); var target = Unit(false, 4, 3);
            if (miss) target.Character.Stats.Dodge = new Stat(1e20f);
            var cues = Cast(Session(caster, target), Skill(id, Effect(EffectType.DealDamage)), target);
            Assert.That(cues.Count, Is.EqualTo(miss ? 0 : 1));
            if (!miss) Assert.That(cues.Single().TargetVfx, Is.EqualTo(CombatTargetVfxKind.Blunt));
        }

        [UnityTest] public IEnumerator FrozenAndParalyzedActorsRemainStillInBothViewOwnersThenResume()
        {
            yield return new EnterPlayMode(); var previousStep = Time.maximumDeltaTime; Time.maximumDeltaTime = .05f;
            try
            {
                foreach (var exploration in new[] { false, true })
                    foreach (var id in new[] { "freeze", "paralysis" })
                    {
                        var root = new GameObject("Motion lock " + id); var body = root.AddComponent<SpriteRenderer>();
                        try
                        {
                            var unit = Unit(true); MonoBehaviour view;
                            if (exploration)
                            {
                                var value = root.AddComponent<ExplorationCharacterView>(); Set(value, "bodyRenderer", body); value.BindCombat(unit); view = value;
                            }
                            else { var value = root.AddComponent<CharacterView>(); Set(value, "bodyRenderer", body); value.Bind(unit); view = value; }
                            yield return null; yield return null;
                            var effect = Status(id); unit.Character.PassiveEffects.Add(effect);
                            yield return null; yield return null; var frozen = body.sprite;
                            Assert.That(frozen, Is.Not.Null); var finish = Time.time + .7f;
                            while (Time.time < finish) { Assert.That(body.sprite, Is.SameAs(frozen), exploration + " " + id); yield return null; }
                            unit.Character.PassiveEffects.Remove(effect); var seen = new HashSet<Sprite>(); finish = Time.time + 1.2f;
                            while (Time.time < finish) { seen.Add(body.sprite); yield return null; }
                            Assert.That(seen.Count, Is.GreaterThan(1), "animation did not resume: " + exploration + " " + id);
                        }
                        finally { Object.Destroy(root); }
                        yield return null;
                    }
            }
            finally { Time.maximumDeltaTime = previousStep; }
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator SkillEffectStaysFrozenWhilePersistentStatusAnimatesThenBothCleanUp()
        {
            yield return new EnterPlayMode(); var root = new GameObject("World effect lifecycle");
            // Sampling every authored frame requires a bounded game-time step;
            // editor/GPU startup can otherwise legitimately skip a short frame.
            var oldMaximumDeltaTime = Time.maximumDeltaTime;
            Time.maximumDeltaTime = .05f;
            try
            {
                var spawner = root.AddComponent<CombatUnitViewSpawner>(); spawner.enabled = false;
                var unit = Unit(true); var session = Session(unit);
                Assert.That(CombatTargetVfxResolver.TryResolve("fire", out var clip), Is.True);
                var cue = new CombatFeedbackEvent(unit, CombatFeedbackType.TargetEffect, targetVfx: CombatTargetVfxKind.Fire);
                Assert.That(spawner.TryPlayTargetVfx(session, cue), Is.True);
                var renderer = root.GetComponentsInChildren<SpriteRenderer>().Single();
                var body = root.AddComponent<SpriteRenderer>();
                unit.Character.PassiveEffects.Add(Status(StatusEffectIds.Burn));
                using var status = new CombatStatusVfxPlayer(body); status.Tick(unit.Character, unit);
                var burn = root.GetComponentsInChildren<SpriteRenderer>().Single(r => r.gameObject.name == "Status VFX burn");
                Assert.That(CombatTargetVfxResolver.TryResolve("burn", out var burnClip), Is.True);
                var seen = new HashSet<Sprite>(); var statusSeen = new HashSet<Sprite>();
                var finish = Time.time + Mathf.Max(clip.Duration, burnClip.Duration) + .08f;
                while (Time.time < finish)
                {
                    spawner.SendMessage("TickWorldEffects"); if (renderer != null && renderer.enabled) seen.Add(renderer.sprite);
                    status.Tick(unit.Character, unit); statusSeen.Add(burn.sprite);
                    yield return null;
                }
                spawner.SendMessage("TickWorldEffects"); yield return null;
                Assert.That(seen, Is.EquivalentTo(clip.Frames)); Assert.That(renderer == null, Is.True);
                Assert.That(statusSeen.Count, Is.EqualTo(4));
                unit.Character.PassiveEffects.Clear(); status.Tick(unit.Character, unit); Assert.That(burn.enabled, Is.False);
                spawner.TryPlayTargetVfx(session, cue); spawner.SendMessage("OnDisable");
                Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Where(r => r != body).All(r => !r.enabled), Is.True);
            }
            finally { Time.maximumDeltaTime = oldMaximumDeltaTime; Object.Destroy(root); }
            yield return new ExitPlayMode();
        }
    }
}
