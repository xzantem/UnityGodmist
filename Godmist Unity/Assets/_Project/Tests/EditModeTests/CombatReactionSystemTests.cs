using System.Collections.Generic;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class CombatReactionSystemTests
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
        public void TryConsumeCounterattack_InRangeConsumesSingleReaction()
        {
            var skill = CreateSkill();
            var defender = CreateUnit("defender", new GridPosition(2, 2), false);
            var attacker = CreateUnit("attacker", new GridPosition(3, 2), true);
            defender.Character.PassiveEffects.Add(CreateCounterattack(skill, 1));
            var system = new CombatReactionSystem();

            var firstConsumed = system.TryConsumeCounterattack(
                defender, attacker, out var firstSkill, out var firstEffects);
            var secondConsumed = system.TryConsumeCounterattack(
                defender, attacker, out _, out _);

            Assert.That(firstConsumed, Is.True);
            Assert.That(firstSkill, Is.EqualTo(skill));
            Assert.That(firstEffects, Has.Count.EqualTo(1));
            Assert.That(defender.HasReacted, Is.True);
            Assert.That(secondConsumed, Is.False);
        }

        [Test]
        public void TryConsumeCounterattack_OutsideConfiguredRangeDoesNotConsume()
        {
            var defender = CreateUnit("defender", new GridPosition(1, 1), false);
            var attacker = CreateUnit("attacker", new GridPosition(4, 1), true);
            defender.Character.PassiveEffects.Add(CreateCounterattack(CreateSkill(), 1));

            var consumed = new CombatReactionSystem().TryConsumeCounterattack(
                defender, attacker, out _, out _);

            Assert.That(consumed, Is.False);
            Assert.That(defender.HasReacted, Is.False);
        }

        [Test]
        public void TryConsumeCounterattack_DoesNotReactToAlly()
        {
            var defender = CreateUnit("defender", new GridPosition(1, 1), false);
            var ally = CreateUnit("ally", new GridPosition(2, 1), false);
            defender.Character.PassiveEffects.Add(CreateCounterattack(CreateSkill(), 1));

            var consumed = new CombatReactionSystem().TryConsumeCounterattack(
                defender, ally, out _, out _);

            Assert.That(consumed, Is.False);
            Assert.That(defender.HasReacted, Is.False);
        }

        private SkillData CreateSkill()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            createdAssets.Add(skill);
            return skill;
        }

        private static PassiveEffectRuntime CreateCounterattack(
            SkillData skill,
            int range)
        {
            return new PassiveEffectRuntime
            {
                EffectId = StatusEffectIds.Counterattack,
                Lifetime = PassiveEffectLifetime.Timed,
                RemainingDuration = 1,
                CounterattackSkill = skill,
                CounterattackEffects = new List<Godmist.Data.Effects.SkillEffectData>
                {
                    new()
                },
                CounterattackRange = new SkillRangeData(
                    RangeShape.Square,
                    1,
                    range)
            };
        }

        private static BattleUnit CreateUnit(
            string id,
            GridPosition position,
            bool playerSide)
        {
            return new BattleUnit
            {
                Position = position,
                IsPlayerSide = playerSide,
                Character = new CharacterInstance
                {
                    CharacterId = id,
                    LifeState = CharacterLifeState.Alive
                }
            };
        }
    }
}
