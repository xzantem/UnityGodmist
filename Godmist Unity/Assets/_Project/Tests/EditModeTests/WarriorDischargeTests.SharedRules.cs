using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public sealed partial class WarriorDischargeTests
    {
        [TestCase(0, 0)][TestCase(1, 8)][TestCase(2, 16)]
        public void SharedRefundRulesRoundOddCostsOnceAndRefundOnlyTheFirstKill(int rank, int expected)
        {
            var caster = Unit(33); Rank(caster, "wounding", rank); var cast = WarriorDischargeRules.Capture(caster, Skill);
            caster.Character.ResourcePool.CurrentValue = 0;
            Assert.That(WarriorDischargeRules.RequestedRefund(33, rank), Is.EqualTo(expected));
            Assert.That(WarriorDischargeRules.RefundKill(caster, cast, new CombatRecoverySystem()), Is.EqualTo(expected));
            Assert.That(WarriorDischargeRules.RefundKill(caster, cast, new CombatRecoverySystem()), Is.Zero);
        }

        [TestCase(false, 4)][TestCase(true, 0)]
        public void SharedRefundExecutionRespectsTheRecoveryLimitAndBlocking(bool blocked, int expected)
        {
            var caster = Unit(33); Rank(caster, "wounding", 2); var cast = WarriorDischargeRules.Capture(caster, Skill);
            caster.Character.ResourcePool.CurrentValue = 196;
            if (blocked)
            {
                var effect = new PassiveEffectRuntime { EffectId = "test.refund.block" };
                effect.SetPayload(new ResourceRegenerationBlockPayload()); caster.Character.PassiveEffects.Add(effect);
            }
            Assert.That(WarriorDischargeRules.RefundKill(caster, cast, new CombatRecoverySystem()), Is.EqualTo(expected));
        }
    }
}
