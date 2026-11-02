using System;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class ScoutContentTests
    {
        [TestCase(1,.24f)][TestCase(2,.48f)][TestCase(3,.75f)]
        public void RecoveredStrengthCountsEveryKnifeAndCapsAtThree(int rank,float bonus)
        {
            var u=Unit(true,1);Rank(u,"blade_dancer","t5_2",rank);
            ScoutCombatSystem.RecoveredKnives(u,2);Assert.That(u.ScoutState.RecoveredStacks,Is.EqualTo(2));
            ScoutCombatSystem.RecoveredKnives(u,9);Assert.That(u.ScoutState.RecoveredStacks,Is.EqualTo(3));
            Assert.That(ScoutCombatSystem.RecoveredBonus(u),Is.EqualTo(bonus).Within(.001f));
        }
        [Test] public void GroundRecoveryDoesNotDoubleCountAndKeepsFlatMomentumReward()
        {
            var u=Unit(true,1);Rank(u,"blade_dancer","t5_2",1);u.AvailableKnives=3;u.Character.ResourcePool.CurrentValue=0;
            var s=Session(u);s.MapObjects.Add(new KnifeCombatMapObject("test",u.Position,u.Character.CharacterId,2));
            Assert.That(ScoutKnifeSystem.RecoverAtPosition(s,u),Is.EqualTo(2));
            Assert.That(u.ScoutState.RecoveredStacks,Is.EqualTo(2));Assert.That(u.Character.ResourcePool.CurrentValue,Is.EqualTo(8));
            Assert.That(ScoutKnifeSystem.RecoverAtPosition(s,u),Is.Zero);
        }
        [Test] public void RecoveredBankIsSpentForWholeCastEvenWhenNoHitOccurs()
        {
            var u=Unit(true,1);Rank(u,"blade_dancer","t5_2",3);ScoutCombatSystem.RecoveredKnives(u,3);
            using(new ScoutCombatSystem.AttackScope(u,Skill("ScoutQuickStrikes"),false))
            {Assert.That(u.ScoutState.RecoveredStacks,Is.Zero);Assert.That(ScoutCombatSystem.RecoveredBonus(u),Is.EqualTo(.75f));}
            Assert.That(ScoutCombatSystem.RecoveredBonus(u),Is.Zero);
        }
        [Test] public void SupportAndDuplicateDoNotConsumeRecoveredBank()
        {
            var u=Unit(true,1);Rank(u,"blade_dancer","t5_2",3);ScoutCombatSystem.RecoveredKnives(u,3);
            using(new ScoutCombatSystem.AttackScope(u,Skill("ScoutTonic"),false))Assert.That(ScoutCombatSystem.RecoveredBonus(u),Is.Zero);
            using(new ScoutCombatSystem.AttackScope(u,Skill("ScoutQuickStrikes"),true))Assert.That(ScoutCombatSystem.RecoveredBonus(u),Is.Zero);
            Assert.That(u.ScoutState.RecoveredStacks,Is.EqualTo(3));
        }
        [Test] public void RollGuaranteesOnlyOneCriticalAndExpiresAfterMissedCast()
        {
            var u=Unit(true,1);ScoutCombatSystem.GrantRollCritical(u);
            using(new ScoutCombatSystem.AttackScope(u,Skill("ScoutQuickStrikes"),false))
            {Assert.That(ScoutCombatSystem.ConsumeRollCritical(u),Is.True);Assert.That(ScoutCombatSystem.ConsumeRollCritical(u),Is.False);}
            Assert.That(ScoutCombatSystem.HasRollCritical(u),Is.False);
            ScoutCombatSystem.GrantRollCritical(u);
            using(new ScoutCombatSystem.AttackScope(u,Skill("ScoutQuickStrikes"),false)){}
            Assert.That(ScoutCombatSystem.HasRollCritical(u),Is.False);
        }
        [Test] public void RollDodgeIsCombatScopedSingleChargeAndRefreshDoesNotStack()
        {
            var u=Unit(true,1);ScoutCombatSystem.GrantRollDodge(u);ScoutCombatSystem.GrantRollDodge(u);
            Assert.That(u.Character.PassiveEffects.Count(e=>e.GetPayload<UniqueCombatBuffPayload>()?.BuffType==UniqueBuffType.GuaranteedDodge),Is.EqualTo(1));
            new StatusEffectSystem().ProcessTurnEnd(u);
            Assert.That(CombatDamageSystem.TryConsumeUniqueBuff(u,UniqueBuffType.GuaranteedDodge),Is.True);
            Assert.That(CombatDamageSystem.TryConsumeUniqueBuff(u,UniqueBuffType.GuaranteedDodge),Is.False);
        }
        [TestCase(0,.01f,false)][TestCase(1,.19f,true)][TestCase(1,.21f,false)][TestCase(2,.39f,true)][TestCase(2,.41f,false)]
        public void PredictionHasOneExplicitRescueChance(int rank,float roll,bool result)
        {var u=Unit(true,1);Rank(u,"assassin","t5_3",rank);Assert.That(ScoutCombatSystem.ConvertMiss(u,roll),Is.EqualTo(result));}
        [Test] public void DishonorableCutDoublesExistingBossProcOnlyWhenAlreadyStunned()
        {
            var u=Unit(true,1);var t=Unit(false,2);Rank(u,"assassin","throat_cut",3);Rank(u,"assassin","t5_5",1);
            Assert.That(ScoutCombatSystem.ThroatChance(u,t),Is.EqualTo(.1f).Within(.001f));
            WarriorCombatSystem.ArmoredBuff(t,StatusEffectIds.Stun,"test",2);
            Assert.That(ScoutCombatSystem.ThroatChance(u,t),Is.EqualTo(.2f).Within(.001f));
            t.IsBoss=true;var ctx=new PassiveEffectCombatContext(source:u,target:t,attackType:AttackType.Melee);
            Assert.That(ScoutCombatSystem.ResolveThroatCut(Session(u,t),ctx,100,()=>.15f),Is.EqualTo(200));
        }
        [Test] public void VenomHasExactlyImmediatePlusTwoFuturePulsesAndNoAllyPoison()
        {
            var state=UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(12);var u=Unit(true,1);var t=Unit(false,2);var ally=Unit(true,2);var s=Session(u,t,ally);var statuses=new StatusEffectSystem();
                ScoutCombatSystem.StartVenom(s,u,statuses);Assert.That(u.ScoutState.VenomPulses,Is.EqualTo(2));
                Assert.That(StatusEffectSystem.HasStatus(ally,StatusEffectIds.Poison),Is.False);
                ScoutCombatSystem.Tier5TurnStart(s,u,statuses);ScoutCombatSystem.Tier5TurnStart(s,u,statuses);
                Assert.That(u.ScoutState.VenomPulses,Is.Zero);
                t.Character.PassiveEffects.Clear();ScoutCombatSystem.Tier5TurnStart(s,u,statuses);
                Assert.That(StatusEffectSystem.HasStatus(t,StatusEffectIds.Poison),Is.False);
            }finally{UnityEngine.Random.state=state;}
        }
        [Test] public void VenomCannotHitOutsideRadiusOrAfterSourceDeath()
        {
            var u=Unit(true,1);var t=Unit(false,4);var s=Session(u,t);var statuses=new StatusEffectSystem();
            ScoutCombatSystem.StartVenom(s,u,statuses);Assert.That(StatusEffectSystem.HasStatus(t,StatusEffectIds.Poison),Is.False);
            u.Character.LifeState=CharacterLifeState.Dead;t.Position=new GridPosition(2,2);
            ScoutCombatSystem.Tier5TurnStart(s,u,statuses);Assert.That(u.ScoutState.VenomPulses,Is.Zero);
            Assert.That(StatusEffectSystem.HasStatus(t,StatusEffectIds.Poison),Is.False);
        }
        [Test] public void CurseExtensionAddsOneTickWithoutDilutingExistingPoison()
        {
            var target=Unit(false,2);var dot=new PassiveEffectRuntime(StatusEffectIds.Poison,new(),1,3,0,PassiveEffectLifetime.Timed,PassiveEffectTickPhase.TurnEnd);
            dot.SetPayload(new DamageOverTimePayload{RemainingTotalDamage=120});target.Character.PassiveEffects.Add(dot);
            Assert.That(ScoutCombatSystem.ExtendPoison(target),Is.True);Assert.That(dot.RemainingDuration,Is.EqualTo(4));
            Assert.That(dot.GetPayload<DamageOverTimePayload>().RemainingTotalDamage,Is.EqualTo(160));
        }
        [Test] public void HawkEyeExtendsOnlyRangedAbsoluteRangeWithoutChangingDefinition()
        {
            var u=Unit(true,1);Rank(u,"blade_dancer","t5_3",2);var skill=Skill("ScoutPoisonedDagger");var original=skill.TargetModes[0].AbsoluteRange.MaxRange;
            Assert.That(PassiveEffectCombatExecutionSystem.ResolveMode(u,skill,skill.TargetModes[0]).AbsoluteRange.MaxRange,Is.EqualTo(original+4));
            Assert.That(skill.TargetModes[0].AbsoluteRange.MaxRange,Is.EqualTo(original));
            Assert.That(ScoutCombatSystem.RangeBonus(u,Skill("ScoutShadow")),Is.Zero);
        }
    }
}
