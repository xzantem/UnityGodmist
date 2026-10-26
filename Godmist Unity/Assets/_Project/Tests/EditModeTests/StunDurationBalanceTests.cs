using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Editor;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class StunDurationBalanceTests
    {
        readonly List<SkillData> clones = new(); UnityEngine.Random.State random;
        [SetUp] public void Before() => random = UnityEngine.Random.state;
        [TearDown] public void After() { UnityEngine.Random.state = random; foreach (var clone in clones) Object.DestroyImmediate(clone); clones.Clear(); }
        static SkillData Skill(string name) => AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_" + name + ".asset");
        static BattleUnit Unit(bool player = true, string definition = "Wladimir_Test")
        {
            var character = new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_" + definition + ".asset"));
            character.CharacterId = Guid.NewGuid().ToString(); character.SkillTreeProgress.Clear();
            character.CurrentHealth = 100000; character.Stats.MaxHealth = new Stat(100000);
            character.Stats.MinAttack = character.Stats.MaxAttack = new Stat(1); character.Stats.CritChance = new Stat(0);
            character.Stats.PhysicalDefense = character.Stats.MagicalDefense = character.Stats.Dodge = new Stat(0);
            character.BaseResistances = new ResistanceSet(); character.Resistances = new ResistanceSet();
            character.ResourcePool.MaxValue = 1000; character.ResourcePool.CurrentValue = 1000;
            return new BattleUnit { Character = character, IsPlayerSide = player, Position = new GridPosition(player ? 0 : 1, 1), CurrentActionPoints = 10000, MaxActionPoints = 10000 };
        }
        static CombatSession Session(BattleUnit source, BattleUnit target) => new() { Grid = new BattleGrid(12, 3), Units = new() { source, target }, ActiveUnit = source };
        static PassiveEffectRuntime Stun(BattleUnit unit) => unit.Character.PassiveEffects.SingleOrDefault(e => e.EffectId == StatusEffectIds.Stun);
        SkillData Certain(SkillData original)
        {
            var clone = Object.Instantiate(original); clones.Add(clone); var data = new SerializedObject(clone); var effects = data.FindProperty("effects");
            for (var n = 0; n < effects.arraySize; n++)
            {
                var effect = effects.GetArrayElementAtIndex(n); effect.FindPropertyRelative("alwaysHits").boolValue = true;
                effect.FindPropertyRelative("baseChance").floatValue = 1;
            }
            data.ApplyModifiedPropertiesWithoutUndo(); return clone;
        }
        [Test, Explicit("Save only the approved three skill assets through Editor API.")]
        public void ApplyContent()
        {
            StunDurationSetup.Apply(); StunDurationSetup.Apply();
            foreach (var name in new[] { "WarriorRightHook", "PaladinCrushingBlow" })
            {
                Assert.That(Skill(name).Effects.Single(e => e.EffectType == EffectType.ApplyStatus && e.StatusId == StatusEffectIds.Stun).DurationTurns, Is.EqualTo(2));
                Assert.That(Skill(name).TooltipDescription, Does.Contain("ogłuszenie na 2 tury"));
            }
            var debuff = Skill("PaladinCrushingBlow").Effects.Single(e => e.EffectType == EffectType.ModifyResistance);
            Assert.That(debuff.DurationTurns, Is.EqualTo(3)); Assert.That(debuff.ModifierValue, Is.EqualTo(-.3f)); Assert.That(debuff.BaseChance, Is.EqualTo(.9f));
            Assert.That(Skill("WarriorShout").Effects.Single().DurationTurns, Is.EqualTo(1));
            Assert.That(Skill("WarriorShout").TooltipDescription, Does.Contain("50 PW: 2 tury"));
        }
        [TestCase("WarriorRightHook", "Wladimir_Test")] [TestCase("PaladinCrushingBlow", "Paladin")]
        public void RealSkillStunsForTwoTargetTurnsAndCrushingDebuffKeepsItsThirdTurn(string name, string definition)
        {
            var source = Unit(definition: definition); var target = Unit(false, "Mage"); var session = Session(source, target);
            Assert.That(new SkillExecutionSystem(statusEffectSystem: new StatusEffectSystem()).TryExecute(session, source, Certain(Skill(name)), target), Is.True);
            Assert.That(Stun(target).RemainingDuration, Is.EqualTo(2)); var status = new StatusEffectSystem();
            status.ProcessTurnEnd(source); Assert.That(Stun(target).RemainingDuration, Is.EqualTo(2));
            status.ProcessTurnEnd(target); Assert.That(Stun(target).RemainingDuration, Is.EqualTo(1));
            status.ProcessTurnEnd(target); Assert.That(Stun(target), Is.Null);
            if (name == "PaladinCrushingBlow")
                Assert.That(target.Character.PassiveEffects.Single(e => e.EffectId.Contains("modify_resistance")).RemainingDuration, Is.EqualTo(1));
        }
        [TestCase(49, 1)] [TestCase(50, 2)] [TestCase(100, 2)]
        public void ShoutUsesActualRagePaidAndNotPostCastPool(int rage, int turns)
        {
            var source = Unit(); var target = Unit(false, "Mage"); var session = Session(source, target); source.Character.ResourcePool.CurrentValue = rage;
            Assert.That(new SkillExecutionSystem(statusEffectSystem: new StatusEffectSystem()).TryExecute(session, source, Certain(Skill("WarriorShout")), target), Is.True);
            Assert.That(source.LastSkillResourceSpent, Is.EqualTo(rage)); Assert.That(source.Character.ResourcePool.CurrentValue, Is.Zero, "Pure control prevents decay but does not generate base Rage.");
            Assert.That(Stun(target).RemainingDuration, Is.EqualTo(turns));
        }
        SkillData CollisionSkill(EffectType type, int distance)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>(); clones.Add(skill); var data = new SerializedObject(skill);
            data.FindProperty("<SkillId>k__BackingField").stringValue = "test.stun.collision";
            data.FindProperty("<DisplayName>k__BackingField").stringValue = "Collision";
            var modes = data.FindProperty("targetModes"); modes.arraySize = 1;
            var range = modes.GetArrayElementAtIndex(0).FindPropertyRelative("absoluteRange");
            range.FindPropertyRelative("shape").intValue = (int)RangeShape.Square; range.FindPropertyRelative("maxRange").intValue = 10;
            var effects = data.FindProperty("effects"); effects.arraySize = 1; var effect = effects.GetArrayElementAtIndex(0);
            effect.FindPropertyRelative("effectType").intValue = (int)type; effect.FindPropertyRelative("displacementDistance").intValue = distance;
            effect.FindPropertyRelative("alwaysHits").boolValue = true; effect.FindPropertyRelative("baseChance").floatValue = 1;
            data.ApplyModifiedPropertiesWithoutUndo(); return skill;
        }
        [TestCase(EffectType.Push, true, 2)] [TestCase(EffectType.Push, true, 6)]
        [TestCase(EffectType.Push, false, 2)] [TestCase(EffectType.Push, false, 6)]
        [TestCase(EffectType.Charge, true, 2)] [TestCase(EffectType.Charge, true, 6)]
        [TestCase(EffectType.Charge, false, 2)] [TestCase(EffectType.Charge, false, 6)]
        public void WallAndObstacleCollisionLastsOneTurnRegardlessOfPushOrChargeDistance(EffectType type, bool wall, int distance)
        {
            var source = Unit(); var target = Unit(false, "Mage"); var session = Session(source, target);
            var stop = new GridPosition(2, 1);
            if (wall) session.Grid.SetWallEdge(target.Position, stop, DungeonWallType.Full);
            else { session.MapObjects.Add(new ObstacleCombatMapObject("barrier", stop, true, true)); session.Grid.DynamicBlocked = p => session.MapObjects.Any(o => o.Position == p && o.BlocksMovement); }
            var skill = CollisionSkill(type, distance);
            Assert.That(new SkillExecutionSystem(statusEffectSystem: new StatusEffectSystem()).TryExecute(session, source, skill, target), Is.True);
            Assert.That(target.Position, Is.EqualTo(new GridPosition(1, 1))); Assert.That(Stun(target).RemainingDuration, Is.EqualTo(1));
            var status = new StatusEffectSystem(); status.ProcessTurnEnd(source); Assert.That(Stun(target).RemainingDuration, Is.EqualTo(1));
            status.ProcessTurnEnd(target); Assert.That(Stun(target), Is.Null);
        }
        [Test]
        public void ChargeChainStunsOnlyFrontEnemyForOneTurnAtWall()
        {
            var source = Unit(); var target = Unit(false, "Mage"); var front = Unit(false, "Mage"); front.Position = new GridPosition(2, 1);
            var session = Session(source, target); session.Units.Add(front); session.Grid.SetWallEdge(front.Position, new GridPosition(3, 1), DungeonWallType.Full);
            var skill = CollisionSkill(EffectType.Charge, 6);
            Assert.That(new SkillExecutionSystem(statusEffectSystem: new StatusEffectSystem()).TryExecute(session, source, skill, target), Is.True);
            Assert.That(Stun(front).RemainingDuration, Is.EqualTo(1)); Assert.That(Stun(target), Is.Null); Assert.That(Stun(source), Is.Null);
        }
        [Test]
        public void DirectOtherStunsRetainTheirAuthoredDurationWithoutGlobalClamp()
        {
            var source = Unit(); var target = Unit(false, "Mage");
            Assert.That(new StatusEffectSystem().ApplyGuaranteedStun(source, target, null, -1, 3), Is.True);
            Assert.That(Stun(target).RemainingDuration, Is.EqualTo(3));
        }
    }
}
