using System;
using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class SimpleCombatAiSystemTests
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
        public void CalculateSkillScore_PrefersGuaranteedKillOverNonLethalAreaDamage()
        {
            var caster = CreateUnit("caster", 100, 10, 10);
            var lowHealthEnemy = CreateUnit("low", 10, 0, 0);
            var firstAreaEnemy = CreateUnit("area-first", 20, 0, 0);
            var secondAreaEnemy = CreateUnit("area-second", 20, 0, 0);
            var killSkill = CreateSkill(CreateEffect(EffectType.DealDamage, 1f));
            var areaSkill = CreateSkill(CreateEffect(EffectType.DealDamage, 0.9f));

            var killScore = CalculateScore(caster, killSkill, lowHealthEnemy);
            var areaScore = CalculateScore(caster, areaSkill,
                firstAreaEnemy, secondAreaEnemy);

            Assert.That(killScore, Is.GreaterThan(areaScore));
        }

        [Test]
        public void CalculateSkillScore_PrefersHardControlOverOrdinaryStatus()
        {
            var caster = CreateUnit("caster", 100, 10, 10);
            var enemy = CreateUnit("enemy", 100, 0, 0);
            var stun = CreateEffect(EffectType.ApplyStatus, 1f);
            SetField(stun, "statusId", "stun");
            var poison = CreateEffect(EffectType.ApplyStatus, 1f);
            SetField(poison, "statusId", "poison");

            var stunScore = CalculateScore(caster, CreateSkill(stun), enemy);
            var poisonScore = CalculateScore(caster, CreateSkill(poison), enemy);

            Assert.That(stunScore, Is.GreaterThan(poisonScore));
        }

        [Test]
        public void CalculateSkillScore_PrefersReviveOverShield()
        {
            var caster = CreateUnit("caster", 100, 10, 10);
            var downedAlly = CreateUnit("downed", 0, 0, 0);
            downedAlly.Character.LifeState = CharacterLifeState.Downed;
            var heal = CreateEffect(EffectType.Heal, 1f);
            SetField(heal, "baseValue", 20);
            var shield = CreateEffect(EffectType.Shield, 1f);
            SetField(shield, "baseValue", 100);

            var reviveScore = CalculateScore(caster, CreateSkill(heal), downedAlly);
            var shieldScore = CalculateScore(caster, CreateSkill(shield), downedAlly);

            Assert.That(reviveScore, Is.GreaterThan(shieldScore));
        }

        [Test]
        public void GetPreferredCombatRange_IgnoresLongRangeHealForMeleeDps()
        {
            var meleeAttack = CreateSkill(CreateEffect(EffectType.DealDamage, 1f));
            ConfigureTargetMode(meleeAttack, TargetingType.Enemies, 1);
            var longRangeHeal = CreateSkill(CreateEffect(EffectType.Heal, 1f));
            ConfigureTargetMode(longRangeHeal, TargetingType.Allies, 4);
            var unit = CreateUnit("dps", 100, 10, 10);
            unit.Character.Skills.Add(meleeAttack);
            unit.Character.Skills.Add(longRangeHeal);
            var method = typeof(SimpleCombatAiSystem).GetMethod(
                "GetPreferredCombatRange",
                BindingFlags.Static | BindingFlags.NonPublic);

            var range = (int)method.Invoke(null, new object[] { unit });

            Assert.That(range, Is.EqualTo(1));
        }

        private float CalculateScore(
            BattleUnit caster,
            SkillData skill,
            params BattleUnit[] targets)
        {
            var method = typeof(SimpleCombatAiSystem).GetMethod(
                "CalculateSkillScore",
                BindingFlags.Static | BindingFlags.NonPublic);
            var targetsByMode = new Dictionary<int, IReadOnlyList<BattleUnit>>
            {
                [0] = targets
            };

            return (float)method.Invoke(null, new object[]
            {
                caster,
                skill,
                targetsByMode
            });
        }

        private SkillData CreateSkill(SkillEffectData effect)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            createdAssets.Add(skill);
            SetField(skill, "targetModes", new List<SkillTargetModeData>
            {
                new()
            });
            SetField(skill, "effects", new List<SkillEffectData> { effect });
            return skill;
        }

        private static void ConfigureTargetMode(
            SkillData skill,
            TargetingType targetingType,
            int maxRange)
        {
            var mode = skill.TargetModes[0];
            SetField(mode, "targetType", TargetType.Single);
            SetField(mode, "targetingType", targetingType);
            SetField(mode, "absoluteRange",
                new SkillRangeData(RangeShape.Square, 1, maxRange));
        }

        private static SkillEffectData CreateEffect(EffectType type, float multiplier)
        {
            var effect = new SkillEffectData();
            SetField(effect, "effectType", type);
            SetField(effect, "targetModeIndex", 0);
            SetField(effect, "damageMultiplier", multiplier);
            SetField(effect, "damageValueSource", DamageValueSource.AverageAttack);
            return effect;
        }

        private static BattleUnit CreateUnit(
            string id,
            int health,
            float minAttack,
            float maxAttack)
        {
            return new BattleUnit
            {
                Character = new CharacterInstance
                {
                    CharacterId = id,
                    CurrentHealth = health,
                    LifeState = CharacterLifeState.Alive,
                    Stats = new CharacterStats
                    {
                        MaxHealth = new Stat(100f),
                        MinAttack = new Stat(minAttack),
                        MaxAttack = new Stat(maxAttack)
                    }
                }
            };
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }
    }
}
