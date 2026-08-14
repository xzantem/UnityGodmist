using System;
using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class SummonSystemTests
    {
        private readonly List<UnityEngine.Object> createdAssets = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in createdAssets)
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void TrySummon_AddsPlayerSideUnitWithConfiguredHealth()
        {
            var session = CreateSession();
            var caster = CreateUnit("caster", new GridPosition(1, 1), true);
            session.Units.Add(caster);
            var effect = CreateSummonEffect(duration: 2, percentHealth: 0.5f);

            var applied = new SummonSystem().TrySummon(session, caster,
                CreateSkill(), effect, 0, new GridPosition(2, 1), out var summon);

            Assert.That(applied, Is.True);
            Assert.That(session.Units, Contains.Item(summon));
            Assert.That(session.Summons, Has.Count.EqualTo(1));
            Assert.That(summon.IsPlayerSide, Is.True);
            Assert.That(summon.Character.CurrentHealth, Is.EqualTo(50));
            Assert.That(session.TurnParticipants,
                Has.Some.Matches<CombatTurnParticipant>(item => item.Unit == summon));
        }

        [Test]
        public void TrySummon_RejectsOccupiedPosition()
        {
            var session = CreateSession();
            var caster = CreateUnit("caster", new GridPosition(1, 1), true);
            session.Units.Add(caster);
            session.Units.Add(CreateUnit("occupant", new GridPosition(2, 1), false));

            var applied = new SummonSystem().TrySummon(session, caster,
                CreateSkill(), CreateSummonEffect(1, 1f), 0,
                new GridPosition(2, 1), out _);

            Assert.That(applied, Is.False);
            Assert.That(session.Summons, Is.Empty);
        }

        [Test]
        public void AdvanceDurationAtTurnEnd_RemovesExpiredSummon()
        {
            var session = CreateSession();
            var caster = CreateUnit("caster", new GridPosition(1, 1), true);
            session.Units.Add(caster);
            var system = new SummonSystem();
            Assert.That(system.TrySummon(session, caster, CreateSkill(),
                CreateSummonEffect(1, 1f), 0, new GridPosition(2, 1),
                out var summon), Is.True);

            Assert.That(system.AdvanceDurationAtTurnEnd(session, summon), Is.True);
            Assert.That(session.Summons, Is.Empty);
            Assert.That(session.Units, Has.No.Member(summon));
        }

        [Test]
        public void TrySummon_AtLimitRemovesOldestMatchingSummon()
        {
            var session = CreateSession();
            var caster = CreateUnit("caster", new GridPosition(1, 1), true);
            session.Units.Add(caster);
            var system = new SummonSystem();
            var skill = CreateSkill();
            var effect = CreateSummonEffect(3, 1f, maxActiveSummons: 1);

            Assert.That(system.TrySummon(session, caster, skill, effect, 0,
                new GridPosition(2, 1), out var first), Is.True);
            Assert.That(system.TrySummon(session, caster, skill, effect, 0,
                new GridPosition(3, 1), out var second), Is.True);

            Assert.That(session.Summons, Has.Count.EqualTo(1));
            Assert.That(session.Summons[0].Unit, Is.EqualTo(second));
            Assert.That(session.Units, Has.No.Member(first));
        }

        [Test]
        public void RemoveSummonsCreatedBy_RemovesOnlySpecifiedCasterSummons()
        {
            var session = CreateSession();
            var firstCaster = CreateUnit("first", new GridPosition(1, 1), true);
            var secondCaster = CreateUnit("second", new GridPosition(1, 3), false);
            session.Units.Add(firstCaster);
            session.Units.Add(secondCaster);
            var system = new SummonSystem();
            var skill = CreateSkill();
            var effect = CreateSummonEffect(3, 1f);

            Assert.That(system.TrySummon(session, firstCaster, skill, effect, 0,
                new GridPosition(2, 1), out var firstSummon), Is.True);
            Assert.That(system.TrySummon(session, secondCaster, skill, effect, 0,
                new GridPosition(2, 3), out var secondSummon), Is.True);

            Assert.That(system.RemoveSummonsCreatedBy(session, "first"), Is.EqualTo(1));
            Assert.That(session.Units, Has.No.Member(firstSummon));
            Assert.That(session.Units, Has.Member(secondSummon));
        }

        [Test]
        public void TrySummon_DoesNotChangeCombatRewardsOrDefeatedEnemies()
        {
            var session = CreateSession();
            session.Rewards.ExperiencePerActiveCharacter = 10;
            session.Rewards.Gold = 25;
            session.DefeatedEnemyInstanceIds.Add("enemy.dead");
            var caster = CreateUnit("caster", new GridPosition(1, 1), true);
            session.Units.Add(caster);

            Assert.That(new SummonSystem().TrySummon(session, caster,
                CreateSkill(), CreateSummonEffect(1, 1f), 0,
                new GridPosition(2, 1), out _), Is.True);

            Assert.That(session.Rewards.ExperiencePerActiveCharacter, Is.EqualTo(10));
            Assert.That(session.Rewards.Gold, Is.EqualTo(25));
            Assert.That(session.DefeatedEnemyInstanceIds,
                Has.Member("enemy.dead"));
        }

        private CombatSession CreateSession()
        {
            return new CombatSession
            {
                Grid = new BattleGrid(6, 6),
                ActionPointsPerSpeed = 0.25f
            };
        }

        private BattleUnit CreateUnit(string id, GridPosition position, bool playerSide)
        {
            var definition = CreateDefinition(id);
            return new BattleUnit
            {
                Character = new CharacterInstance
                {
                    CharacterId = id,
                    Definition = definition,
                    LifeState = CharacterLifeState.Alive,
                    Stats = new CharacterStats { MaxHealth = new Godmist.Core.Types.Stat(100f), Speed = new Godmist.Core.Types.Stat(60f) }
                },
                Position = position,
                IsPlayerSide = playerSide
            };
        }

        private SkillEffectData CreateSummonEffect(
            int duration,
            float percentHealth,
            int maxActiveSummons = 1)
        {
            var effect = new SkillEffectData();
            SetField(effect, "summonedCharacterDefinition", CreateDefinition("skeleton"));
            SetField(effect, "summonedClassData", CreateClass());
            SetField(effect, "summonPlacementRange", new SkillRangeData(RangeShape.Square, 1, 3));
            SetField(effect, "summonDurationTurns", duration);
            SetField(effect, "maxActiveSummons", maxActiveSummons);
            SetField(effect, "summonPercentMaxHealth", percentHealth);
            return effect;
        }

        private SkillData CreateSkill()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            createdAssets.Add(skill);
            SetAutoProperty(skill, "SkillId", "skill.summon");
            return skill;
        }

        private CharacterDefinition CreateDefinition(string id)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            createdAssets.Add(definition);
            SetAutoProperty(definition, "Id", id);
            SetAutoProperty(definition, "DisplayName", id);
            SetAutoProperty(definition, "BaseStats", new CharacterBaseStatsData());
            return definition;
        }

        private CharacterClassData CreateClass()
        {
            var classData = ScriptableObject.CreateInstance<CharacterClassData>();
            createdAssets.Add(classData);
            SetAutoProperty(classData, "Id", "class.summon");
            return classData;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private static void SetAutoProperty(object target, string name, object value)
        {
            target.GetType().GetField($"<{name}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }
    }
}
