using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Items;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class ScoutContentTests
    {
        [Test] public void LowBlowRequiresCriticalAndUsesNormalStatusApplication()
        {
            var state=Random.state;
            try
            {
                Random.InitState(207);var u=Unit(true,1);var target=Unit(false,2);Rank(u,"assassin","t5_4",1);
                var context=new PassiveEffectCombatContext(source:u,target:target,damageType:DamageType.Physical);
                ScoutCombatSystem.LowBlow(context,false);Assert.That(StatusEffectSystem.HasStatus(target,StatusEffectIds.Stun),Is.False);
                for(int i=0;i<30&&!StatusEffectSystem.HasStatus(target,StatusEffectIds.Stun);i++)ScoutCombatSystem.LowBlow(context,true);
                Assert.That(StatusEffectSystem.HasStatus(target,StatusEffectIds.Stun),Is.True);
            }finally{Random.state=state;}
        }
        [Test] public void RollCriticalPreviewAgainstAvoidanceNeverPromisesThreeGuaranteedCrits()
        {
            var u=Unit(true,1);var target=Unit(false,2);var skill=Skill("ScoutQuickStrikes");
            ScoutCombatSystem.GrantRollCritical(u);
            var withoutAvoidance=CombatSkillPreviewCalculator.CalculateTargetPreview(u,target,skill).Damage.Maximum;
            target.Character.Equipment.Armor=new EquipmentItemInstance{UpgradeProgress=1,SocketedGaldurytes=new(){new GalduryteItemInstance{IsIdentified=true,Effects=new(){new GalduryteEffectInstance{EffectType=GalduryteEffectType.CriticalAvoidance,Value=.5f}}}}};
            var preview=CombatSkillPreviewCalculator.CalculateTargetPreview(u,target,skill);
            Assert.That(preview.Damage.Maximum,Is.EqualTo(withoutAvoidance));
            Assert.That(ScoutCombatSystem.HasRollCritical(u),Is.True);
        }
        [Test] public void ActualMultiHitConsumesExactlyOneRollCritical()
        {
            var state=Random.state;
            try
            {
                Random.InitState(207);var u=Unit(true,1);var target=Unit(false,2);var s=Session(u,target);var skill=Skill("ScoutQuickStrikes");
                ScoutCombatSystem.GrantRollCritical(u);var damage=new CombatDamageSystem();
                var context=new PassiveEffectCombatContext(source:u,target:target,skill:skill,skillId:skill.SkillId,attackType:AttackType.Melee,damageType:DamageType.Physical);
                using(new ScoutCombatSystem.AttackScope(u,skill,false))
                {
                    Assert.That(damage.ResolveAndApplyDamage(s,u,target,100,DamageType.Physical,100,passiveContext:context).IsCritical,Is.True);
                    Assert.That(damage.ResolveAndApplyDamage(s,u,target,100,DamageType.Physical,100,passiveContext:context).IsCritical,Is.False);
                    Assert.That(damage.ResolveAndApplyDamage(s,u,target,100,DamageType.Physical,100,passiveContext:context).IsCritical,Is.False);
                }
            }finally{Random.state=state;}
        }
        [Test] public void RemovingVenomAuraStopsRemainingPulses()
        {
            var u=Unit(true,1);var target=Unit(false,2);var s=Session(u,target);var statuses=new StatusEffectSystem();
            ScoutCombatSystem.StartVenom(s,u,statuses);statuses.RemoveStatus(u,ScoutCombatSystem.BottledVenom);
            target.Character.PassiveEffects.Clear();ScoutCombatSystem.Tier5TurnStart(s,u,statuses);
            Assert.That(u.ScoutState.VenomPulses,Is.Zero);Assert.That(StatusEffectSystem.HasStatus(target,StatusEffectIds.Poison),Is.False);
        }
        [Test] public void PredictionCannotExceedDisorientationFinalHitCap()
        {
            var u=Unit(true,1);Rank(u,"assassin","t5_3",2);
            WarriorCombatSystem.ArmoredBuff(u,"warrior.man_at_arms.disorientation","test",1);u.WarriorState.DisorientationCap=.2f;
            Assert.That(ScoutCombatSystem.HitChanceAfterRescue(u,.2f),Is.EqualTo(.2f).Within(.001f));
        }
        [TestCase("a2",80)][TestCase("b1",50)][TestCase("b4",55)]
        public void ScoutTier5RealCastUsesSkillAndPaysOnce(string slot,int cost)
        {
            var u=Unit(true,1);var foe=Unit(false,3);var session=Session(u,foe);
            var skill=Skill("ScoutT5_"+slot);Assert.That(skill,Is.Not.Null);u.Character.Skills.Add(skill);
            var removable=u.Character.GetActiveSkills().First(s=>s.SkillId!=u.Character.GetBasicAttackSkill().SkillId);
            u.Character.ActiveSkillLoadout.TryRemoveSkill(removable.SkillId);
            Assert.That(u.Character.ActiveSkillLoadout.TryAddSkill(skill.SkillId,u.Character.GetKnownSkills().Select(s=>s.SkillId)),Is.True);
            new CharacterStatPipelineSystem().Refresh(u.Character);CombatTurnSystem.RefreshMaxActionPoints(session);
            var start=u.CurrentActionPoints;var price=CombatActionCostCalculator.CalculateSkillCost(u,skill);
            var targets=new Dictionary<int,IReadOnlyList<BattleUnit>>{[0]=new[]{slot=="b4"?foe:u}};
            Assert.That(new SkillExecutionSystem().TryExecute(session,u,skill,targets,slot=="a2"?new GridPosition(2,2):null),Is.True);
            Assert.That(u.LastSkillResourceSpent,Is.EqualTo(cost));Assert.That(u.CurrentActionPoints,Is.EqualTo(start-price));
            if(slot=="a2")
            {Assert.That(u.Position,Is.EqualTo(new GridPosition(2,2)));Assert.That(ScoutCombatSystem.HasRollCritical(u),Is.True);Assert.That(CombatDamageSystem.HasUniqueBuff(u,UniqueBuffType.GuaranteedDodge),Is.True);}
            if(slot=="b1")Assert.That(u.ScoutState.VenomPulses,Is.EqualTo(2));
            if(slot=="b4")Assert.That(u.AvailableKnives,Is.EqualTo(4));
        }
        [Test] public void ConditioningChangesCanonicalActionCostOnlyForRoll()
        {
            var u=Unit(true,1);var skill=Skill("ScoutT5_a2");var before=CombatActionCostCalculator.CalculateSkillCost(u,skill);
            Rank(u,"assassin","t5_1",1);Assert.That(CombatActionCostCalculator.CalculateSkillCost(u,skill),Is.EqualTo(before/2));
            Assert.That(skill.ActionCost,Is.EqualTo(80));
        }
        [TestCase("a2")][TestCase("b1")][TestCase("b4")]
        public void NewScoutSkillsValidateAndHaveResolvedPoses(string slot)
        {
            var skill=Skill("ScoutT5_"+slot);Assert.That(CombatDataValidator.ValidateSkill(skill).Errors,Is.Empty);
            foreach(var type in new[]{AttackType.Melee,AttackType.Ranged})
            foreach(var facing in new[]{Vector2.up,Vector2.down,Vector2.left,Vector2.right,new Vector2(1,1),new Vector2(-1,-1)})
            {Assert.That(Godmist.Views.Characters.CombatPoseResolver.TryResolveSkill("jerzy",skill.SkillId,facing,type,false,out var pose),Is.True);Assert.That(pose.Actor,Is.Not.Null);}
        }
        [Test] public void RollCriticalDamagePreviewCountsOneCriticalInThreeHitAttack()
        {
            var u=Unit(true,1);var target=Unit(false,2);var session=Session(u,target);var skill=Skill("ScoutQuickStrikes");
            var original=CombatSkillPreviewCalculator.CalculateTargetPreview(u,target,skill).Damage.Minimum;
            ScoutCombatSystem.GrantRollCritical(u);
            var boosted=CombatSkillPreviewCalculator.CalculateTargetPreview(u,target,skill).Damage.Minimum;
            Assert.That(boosted-original,Is.EqualTo(original/3).Within(2));
            var expected=CombatSkillPreviewCalculator.CalculateExpectedTargetDamage(session,u,target,skill,0);
            Assert.That(expected,Is.LessThan(original*2*1.35f));Assert.That(ScoutCombatSystem.HasRollCritical(u),Is.True);
        }
        [Test] public void CurseDealsBonusAndAddsTickOnlyAfterHit()
        {
            var u=Unit(true,1);var target=Unit(false,2);var skill=Skill("ScoutT5_b4");
            var context=new PassiveEffectCombatContext(source:u,target:target,skill:skill,skillId:skill.SkillId,damageType:DamageType.Physical);
            var ordinary=ScoutCombatSystem.DamageMultiplier(context);
            var dot=new PassiveEffectRuntime(StatusEffectIds.Poison,new(),1,3,0,PassiveEffectLifetime.Timed,PassiveEffectTickPhase.TurnEnd);
            dot.SetPayload(new DamageOverTimePayload{RemainingTotalDamage=120});target.Character.PassiveEffects.Add(dot);
            Assert.That(ScoutCombatSystem.DamageMultiplier(context)-ordinary,Is.EqualTo(.4f).Within(.001f));
            ScoutCombatSystem.AfterSkillHit(Session(u,target),u,target,skill,false,new StatusEffectSystem());
            Assert.That(dot.RemainingDuration,Is.EqualTo(4));Assert.That(dot.GetPayload<DamageOverTimePayload>().RemainingTotalDamage,Is.EqualTo(160));
        }
        [Test] public void AdditionalKnifeBagIsIdempotentAndAddsToExistingCapacity()
        {
            var u=Unit(true,1);Rank(u,"blade_dancer","spare_daggers",2);Rank(u,"blade_dancer","t5_5",1);
            var s=Session(u);ScoutKnifeSystem.ResetForCombat(s);
            Assert.That(u.MaxKnives,Is.EqualTo(9));ScoutKnifeSystem.ResetForCombat(s);Assert.That(u.MaxKnives,Is.EqualTo(9));
        }
        [Test] public void PoisonAuraIncludesPoisonPassivesAndLeavesAppliedPoisonAfterExit()
        {
            var old=Random.state;
            try
            {
                var u=Unit(true,1);var target=Unit(false,2);var s=Session(u,target);Rank(u,"blade_dancer","poison_master",2);
                var expected=StatusEffectSystem.CalculateStatusDamagePerTurn(u,StatusEffectIds.Poison,.4f)*3;
                var statuses=new StatusEffectSystem();
                for(int i=0;i<30&&!StatusEffectSystem.HasStatus(target,StatusEffectIds.Poison);i++)ScoutCombatSystem.PulseVenom(s,u,statuses);
                var poison=target.Character.PassiveEffects.Single(e=>e.EffectId==StatusEffectIds.Poison);
                Assert.That(poison.GetPayload<DamageOverTimePayload>().RemainingTotalDamage,Is.EqualTo(expected));
                target.Position=new GridPosition(10,2);CombatAuraSystem.Refresh(s,statuses);
                Assert.That(StatusEffectSystem.HasStatus(target,StatusEffectIds.Poison),Is.True);
            }finally{Random.state=old;}
        }
    }
}
