using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Data.World;
using Godmist.Editor;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Game;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.SaveLoad;
using Godmist.Views.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class MageTiringStacksTests
    {
        const string Root = "Assets/_Project/ScriptableObjects/";
        UnityEngine.Random.State random;
        readonly List<SkillData> clones = new();
        [SetUp] public void Before() => random = UnityEngine.Random.state;
        [TearDown] public void After() { UnityEngine.Random.state = random; foreach (var clone in clones) Object.DestroyImmediate(clone); clones.Clear(); }
        static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(Root + path);
        static SkillData Tiring => Load<SkillData>("Skills/SD_MageTiringSpells.asset");
        static SkillData Orb => Load<SkillData>("Skills/SD_MageEnergyOrb.asset");
        static BattleUnit Unit(bool player = true, bool canonical = false)
        {
            var character = new CharacterInstanceFactory().Create(Load<CharacterDefinition>("Characters/CD_" + (player ? "Mage" : "Paladin") + ".asset"));
            if (!canonical) character.CharacterId = Guid.NewGuid().ToString();
            character.SkillTreeProgress.Clear();
            if (!canonical)
            {
                character.Stats.Speed = new Stat(100); character.Stats.MinAttack = new Stat(1); character.Stats.MaxAttack = new Stat(1);
                character.Stats.MaxHealth = new Stat(100000); character.CurrentHealth = 100000;
                character.Stats.MaxResource = new Stat(1000); character.ResourcePool.MaxValue = 1000; character.ResourcePool.CurrentValue = 1000;
                character.Stats.WeaponAccuracy = new Stat(10000); character.WeaponAccuracy = 10000;
                character.Stats.CritChance = new Stat(0); character.Stats.PhysicalDefense = new Stat(0); character.Stats.MagicalDefense = new Stat(0); character.Stats.Dodge = new Stat(0);
                character.BaseResistances = new ResistanceSet { WeakeningResistance = .5f }; character.Resistances = new ResistanceSet { WeakeningResistance = .5f };
            }
            var unit = new BattleUnit { Character = character, IsPlayerSide = player, Position = new GridPosition(player ? 1 : 2, 2), CurrentActionPoints = 100000, MaxActionPoints = 100000 };
            if (player) unit.ActiveToggleSkillIds.Add(MageTiringSpellSystem.SkillId);
            return unit;
        }
        static CombatSession Session(BattleUnit source, params BattleUnit[] targets) => new CombatSession { Grid = new BattleGrid(10, 10), Units = new[] { source }.Concat(targets).ToList(), ActiveUnit = source };
        static PassiveEffectCombatContext Context(BattleUnit source, BattleUnit target, string action, bool duplicate = false, bool reaction = false, DamageType type = DamageType.Magical) =>
            new PassiveEffectCombatContext(source: source, target: target, skill: Orb, skillId: Orb.SkillId, actionId: action,
                damageType: type, isPassiveDuplicate: duplicate, isPassiveReaction: reaction);
        static Godmist.Runtime.Effects.PassiveEffectRuntime Effect(BattleUnit target) => target.Character.PassiveEffects.FirstOrDefault(e => e.EffectId == MageTiringSpellSystem.EffectId);
        static void SeedFor(bool success, float chance = .7f, int skip = 0)
        {
            for (var seed = 0; seed < 10000; seed++)
            {
                UnityEngine.Random.InitState(seed); for (var n = 0; n < skip; n++) _ = UnityEngine.Random.value;
                if ((UnityEngine.Random.value < chance) != success) continue;
                UnityEngine.Random.InitState(seed); return;
            }
            Assert.Fail("Could not find deterministic roll.");
        }
        static bool Apply(BattleUnit source, BattleUnit target, string action)
        {
            SeedFor(true, StatusEffectSystem.CalculateEffectChance(.7f, ModifyResistanceSystem.GetEffectiveResistance(target, ResistanceType.Weakening)));
            return MageTiringSpellSystem.TryApply(source, target, Context(source, target, action));
        }
        SkillData MultiHit(int hits = 3, bool delayed = false)
        {
            var skill = Object.Instantiate(Orb); clones.Add(skill); var data = new SerializedObject(skill);
            var effects = data.FindProperty("effects"); if (delayed) effects.InsertArrayElementAtIndex(1);
            for (var i = 0; i < effects.arraySize; i++)
            {
                var effect = effects.GetArrayElementAtIndex(i); effect.FindPropertyRelative("alwaysHits").boolValue = true;
                effect.FindPropertyRelative("damageHitCount").intValue = hits;
                effect.FindPropertyRelative("damageValueSource").enumValueIndex = (int)DamageValueSource.MaxAttack;
                if (delayed) effect.FindPropertyRelative("delayCasterTurns").intValue = i + 1;
            }
            data.ApplyModifiedPropertiesWithoutUndo(); return skill;
        }
        [Test, Explicit("Update only the user-approved skill description through Editor API.")]
        public void ApplyContent()
        {
            var icon = Tiring.Icon; MageTiringStacksSetup.Apply();
            Assert.That(Tiring.TooltipDescription, Is.EqualTo(MageTiringSpellSystem.Description));
            Assert.That(Tiring.IsToggle, Is.True); Assert.That(Tiring.ActionCost, Is.Zero); Assert.That(Tiring.ResourceCost, Is.Zero);
            Assert.That(Tiring.Icon, Is.SameAs(icon)); Assert.That(Tiring.Effects.Single().EffectType, Is.EqualTo(EffectType.BlockResourceRegeneration));
        }
        [Test]
        public void ThreeCastsReachFifteenSlowAndCapRefreshesWholeDuration()
        {
            var source = Unit(); var target = Unit(false); var session = Session(source, target);
            for (var n = 1; n <= 3; n++)
            {
                using var scope = new MageTiringSpellSystem.CastScope(session, source, "cast-" + n);
                Assert.That(Apply(source, target, "cast-" + n), Is.True);
                Assert.That(Effect(target).Stacks, Is.EqualTo(n)); Assert.That(target.Character.Stats.Speed.GetValue(), Is.EqualTo(100 - 5 * n));
            }
            new StatusEffectSystem().ProcessTurnEnd(target); Assert.That(Effect(target).RemainingDuration, Is.EqualTo(2));
            using (new MageTiringSpellSystem.CastScope(session, source, "refresh")) Assert.That(Apply(source, target, "refresh"), Is.True);
            Assert.That(Effect(target).Stacks, Is.EqualTo(3)); Assert.That(Effect(target).RemainingDuration, Is.EqualTo(3));
            Assert.That(target.Character.PassiveEffects.Count(e => e.EffectId == MageTiringSpellSystem.EffectId), Is.EqualTo(1));
            Assert.That(target.Character.Stats.Speed.GetValue(), Is.EqualTo(85)); Assert.That(source.Character.Stats.Speed.GetValue(), Is.EqualTo(100));
            Assert.That(source.MageState.TiringAttempts, Is.Empty);
        }
        [Test]
        public void FailedRollCannotRetryOnAnotherHitButEachAreaTargetHasItsOwnRoll()
        {
            var source = Unit(); var first = Unit(false); var second = Unit(false); second.Position = new GridPosition(3, 2);
            SeedFor(false); Assert.That(MageTiringSpellSystem.TryApply(source, first, Context(source, first, "area")), Is.False);
            var state = UnityEngine.Random.state;
            Assert.That(MageTiringSpellSystem.TryApply(source, first, Context(source, first, "area")), Is.False);
            Assert.That(UnityEngine.Random.state, Is.EqualTo(state));
            Assert.That(Apply(source, second, "area"), Is.True); Assert.That(Effect(first), Is.Null); Assert.That(Effect(second).Stacks, Is.EqualTo(1));
        }
        [Test]
        public void RuntimeMultiHitAddsAtMostOneStackAndReleasesAttemptCache()
        {
            var source = Unit(); var target = Unit(false); var session = Session(source, target); var skill = MultiHit();
            // The fixed damage value avoids a damage roll; bonus multiplier and magic crit roll first.
            SeedFor(true, skip: 2);
            Assert.That(new SkillExecutionSystem().TryExecute(session, source, skill, target), Is.True);
            Assert.That(Effect(target), Is.Not.Null); Assert.That(Effect(target).Stacks, Is.EqualTo(1));
            Assert.That(target.Character.CurrentHealth, Is.EqualTo(99997)); Assert.That(source.MageState.TiringAttempts, Is.Empty);
        }
        [Test]
        public void DurationTicksOnlyOnTargetAndAllStacksExpireTogether()
        {
            var source = Unit(); var target = Unit(false); Apply(source, target, "one"); Apply(source, target, "two");
            var status = new StatusEffectSystem(); status.ProcessTurnEnd(source); Assert.That(Effect(target).RemainingDuration, Is.EqualTo(3));
            status.ProcessTurnEnd(target); status.ProcessTurnEnd(target); Assert.That(target.Character.Stats.Speed.GetValue(), Is.EqualTo(90));
            status.ProcessTurnEnd(target); Assert.That(Effect(target), Is.Null); Assert.That(target.Character.Stats.Speed.GetValue(), Is.EqualTo(100));
        }
        [TestCase(0f)] [TestCase(.5f)] [TestCase(.9f)] [TestCase(1f)]
        public void ProcChanceUsesExistingWeakeningResistanceFormula(float resistance)
        {
            var source = Unit(); var target = Unit(false); target.Character.BaseResistances.WeakeningResistance = resistance;
            var chance = StatusEffectSystem.CalculateEffectChance(.7f, resistance); var successes = 0; var expected = 0;
            for (var seed = 0; seed < 80; seed++)
            {
                UnityEngine.Random.InitState(seed); if (UnityEngine.Random.value < chance) expected++;
                UnityEngine.Random.InitState(seed);
                if (MageTiringSpellSystem.TryApply(source, target, Context(source, target, "cast-" + seed))) successes++;
            }
            Assert.That(successes, Is.EqualTo(expected)); if (resistance == 1) Assert.That(Effect(target), Is.Null);
        }
        [TestCase(true, false, DamageType.Magical)] [TestCase(false, true, DamageType.Magical)]
        [TestCase(false, false, DamageType.Burn)] [TestCase(false, false, DamageType.Bleed)] [TestCase(false, false, DamageType.Poison)]
        public void PassiveCopiesReactionsAndPeriodicDamageCannotProc(bool duplicate, bool reaction, DamageType type)
        {
            var source = Unit(); var target = Unit(false); var state = UnityEngine.Random.state;
            Assert.That(MageTiringSpellSystem.TryApply(source, target, Context(source, target, "passive", duplicate, reaction, type)), Is.False);
            Assert.That(Effect(target), Is.Null); Assert.That(source.MageState.TiringAttempts, Is.Null); Assert.That(UnityEngine.Random.state, Is.EqualTo(state));
        }
        [Test]
        public void ActualPeriodicAndAdditionalDamageDoNotAddStacksAndToggleStillBlocksMana()
        {
            var source = Unit(); source.ActiveToggleSkillIds.Clear(); source.Character.ResourcePool.CurrentValue = 50;
            var target = Unit(false); var session = Session(source, target);
            Assert.That(new SkillExecutionSystem(statusEffectSystem: new StatusEffectSystem()).TryExecute(session, source, Tiring, source), Is.True);
            Assert.That(new CombatRecoverySystem().RestoreResource(source, 50), Is.Zero);
            Assert.That(source.Character.ResourcePool.CurrentValue, Is.EqualTo(50));
            var damage = new CombatDamageSystem(); damage.ApplyDamage(session, source, target, 10, DamageType.Burn, passiveContext: Context(source, target, "dot"));
            damage.ApplyDamage(session, source, target, 10, DamageType.Inevitable, passiveContext: Context(source, target, "extra"), allowWarriorProcs: false);
            Assert.That(Effect(target), Is.Null);
            Assert.That(Apply(source, target, "ordinary"), Is.True);
            Assert.That(new SkillExecutionSystem(statusEffectSystem: new StatusEffectSystem()).TryExecute(session, source, Tiring, source), Is.True);
            Assert.That(new CombatRecoverySystem().RestoreResource(source, 50), Is.EqualTo(50));
            Assert.That(MageTiringSpellSystem.TryApply(source, target, Context(source, target, "disabled")), Is.False);
            Assert.That(Effect(target).Stacks, Is.EqualTo(1), "Disabling the toggle stops new stacks but does not dispel an enemy's existing debuff.");
        }
        [Test]
        public void DelayedContactsFromOneCastShareAttemptUntilLastScheduledContact()
        {
            var source = Unit(); var target = Unit(false); var session = Session(source, target); var skill = MultiHit(1, true); var execution = new SkillExecutionSystem();
            Assert.That(execution.TryExecute(session, source, skill, target), Is.True); Assert.That(Effect(target), Is.Null);
            execution.ProcessScheduledEffects(session, source); SeedFor(true, skip: 2); execution.ProcessScheduledEffects(session, source);
            Assert.That(Effect(target).Stacks, Is.EqualTo(1)); Assert.That(source.MageState.TiringAttempts.Count, Is.EqualTo(1));
            execution.ProcessScheduledEffects(session, source);
            Assert.That(Effect(target).Stacks, Is.EqualTo(1)); Assert.That(source.MageState.TiringAttempts, Is.Empty);
        }
        [Test]
        public void LegacySlowRefreshesIntoStacksAndSaveRoundTripKeepsPayloadNameAndIcon()
        {
            var source = Unit(true, true); var target = Unit(false, true); target.Character.BaseResistances.WeakeningResistance = .5f;
            var baseline = target.Character.Stats.Speed.GetValue();
            ScoutCombatSystem.TimedStat(source, target, CharacterStatType.Speed, -3, 3, "mage.tiring");
            Assert.That(Apply(source, target, "second"), Is.True); Assert.That(Effect(target).Stacks, Is.EqualTo(2));
            Assert.That(Apply(source, target, "third"), Is.True); Assert.That(target.Character.Stats.Speed.GetValue(), Is.EqualTo(baseline - 15));
            var game = new GameSession(GameModeState.Exploration); game.TryRegisterCharacter(source.Character); game.TryRegisterCharacter(target.Character); game.TryAddCharacterToActiveParty(source.Character.CharacterId);
            var save = new GameSessionSaveSnapshotFactory().CreateSnapshot(game, "Tiring stacks");
            Assert.That(new GameSessionRestoreFactory().TryCreate(save, Load<GameContentCatalog>("World/GC_MainContentCatalog.asset"), out var restored), Is.True);
            var restoredTarget = restored.CharactersById[target.Character.CharacterId];
            var effect = restoredTarget.PassiveEffects.Single(e => e.EffectId == "mage.tiring");
            Assert.That(restoredTarget.Stats.Speed.GetValue(), Is.EqualTo(baseline - 15));
            Assert.That(effect.Stacks, Is.EqualTo(3)); Assert.That(effect.RemainingDuration, Is.EqualTo(3)); Assert.That(effect.StatModifiers.Single().Value, Is.EqualTo(-15));
            Assert.That(effect.DisplayName, Is.EqualTo("Wycieńczające Zaklęcia"));
            var presentation = new CombatStatusPresentation(effect, CombatStatusKind.Debuff, 1);
            Assert.That(presentation.StackCount, Is.EqualTo(3)); Assert.That(CombatStatusPresentationFactory.ResolveIcon(presentation, _ => null, new[] { Tiring }), Is.SameAs(Tiring.Icon));
        }
    }
}
