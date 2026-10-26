using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class ScoutKnifeArrivalTests
    {
        BattleUnit scout, enemy;
        CombatSession session;
        UnityEngine.Random.State random;

        [SetUp] public void SetUp()
        {
            random = UnityEngine.Random.state;
            UnityEngine.Random.InitState(7412);
            BattleUnit Unit(bool player, int x)
            {
                var character = new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                    "Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset"));
                character.CharacterId = Guid.NewGuid().ToString();
                character.ResourcePool.CurrentValue = 100;
                character.WeaponAccuracy = 10000;
                return new BattleUnit { Character = character, IsPlayerSide = player, Position = new GridPosition(x, 3),
                    MaxKnives = 5, AvailableKnives = 3, CurrentActionPoints = 10000, MaxActionPoints = 10000 };
            }
            scout = Unit(true, 1); enemy = Unit(false, 2);
            session = new CombatSession { Grid = new BattleGrid(12, 8), ActiveUnit = scout,
                Units = new List<BattleUnit> { scout, enemy } };
        }
        [TearDown] public void TearDown() => UnityEngine.Random.state = random;
        KnifeCombatMapObject Knife(GridPosition cell, string owner = null, int count = 1)
        {
            var knife = new KnifeCombatMapObject(Guid.NewGuid().ToString(), cell, owner ?? scout.Character.CharacterId, count);
            session.MapObjects.Add(knife); return knife;
        }

        [Test] public void RealSwapRecoversOnlyOwnedKnivesAndRewardsMomentumOnce()
        {
            var own = Knife(enemy.Position); var foreign = Knife(enemy.Position, "foreign");
            var skill = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<SkillData>(
                "Assets/_Project/ScriptableObjects/Skills/SD_ScoutSwap.asset"));
            try
            {
                var serialized = new SerializedObject(skill);
                serialized.FindProperty("effects").GetArrayElementAtIndex(0).FindPropertyRelative("baseChance").floatValue = 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var before = scout.Character.ResourcePool.CurrentValue;
                Assert.That(new SkillExecutionSystem(statusEffectSystem: new StatusEffectSystem()).TryExecute(session, scout, skill, enemy), Is.True);
                Assert.That(scout.Position, Is.EqualTo(new GridPosition(2, 3)));
                Assert.That(scout.AvailableKnives, Is.EqualTo(4));
                Assert.That(session.MapObjects, Has.No.Member(own));
                Assert.That(session.MapObjects, Has.Member(foreign));
                Assert.That(scout.Character.ResourcePool.CurrentValue, Is.EqualTo(before - skill.ResourceCost + 8));
            }
            finally { UnityEngine.Object.DestroyImmediate(skill); }
        }

        [Test] public void RealChargeRecoversAlongItsPathIncludingTheLandingCellWithoutExceedingCapacity()
        {
            enemy.Position = new GridPosition(4, 3);
            Knife(new GridPosition(2, 3)); var landing = Knife(new GridPosition(6, 3), count: 3);
            var foreign = Knife(new GridPosition(3, 3), "foreign");
            var skill = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_Charge.asset");
            Assert.That(new SkillExecutionSystem(statusEffectSystem: new StatusEffectSystem()).TryExecute(session, scout, skill, enemy), Is.True);
            Assert.That(scout.Position, Is.EqualTo(new GridPosition(6, 3)));
            Assert.That(scout.AvailableKnives, Is.EqualTo(5));
            Assert.That(landing.KnifeCount, Is.EqualTo(2));
            Assert.That(session.MapObjects, Has.Member(foreign));
        }

        [Test] public void TeleportArrivalAndRepeatedNotificationDoNotDuplicatePickup()
        {
            var cell = new GridPosition(4, 3); Knife(cell);
            var before = scout.Character.ResourcePool.CurrentValue;
            Assert.That(new CombatTeleportSystem().TryTeleport(session, scout, scout, cell, false), Is.True);
            CombatCellEntrySystem.Notify(session, scout);
            Assert.That(scout.AvailableKnives, Is.EqualTo(4));
            Assert.That(scout.Character.ResourcePool.CurrentValue, Is.EqualTo(before + 8));
        }

        [Test] public void LethalArrivalTrapPreventsPickup()
        {
            var cell = new GridPosition(4, 3); var knife = Knife(cell);
            session.UnitEnteredCell = unit => unit.Character.LifeState = CharacterLifeState.Dead;
            Assert.That(new CombatTeleportSystem().TryTeleport(session, scout, scout, cell, false), Is.True);
            Assert.That(scout.AvailableKnives, Is.EqualTo(3));
            Assert.That(session.MapObjects, Has.Member(knife));
        }
    }
}
