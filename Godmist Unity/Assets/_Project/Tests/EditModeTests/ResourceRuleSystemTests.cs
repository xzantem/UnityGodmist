using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class ResourceRuleSystemTests
    {
        readonly List<Object> rageAssets = new();
        [TearDown] public void DestroyRageAssets()
        {
            foreach(var asset in rageAssets) Object.DestroyImmediate(asset);
            rageAssets.Clear();
        }
        [TestCase(0, 100)]
        [TestCase(15, 115)]
        [TestCase(30, 130)]
        public void Mana_AllRecoveryUsesOverflowLimit_WithoutChangingNominalMaximum(int overflow, int limit)
        {
            var pool = new ResourcePool(ResourceType.Mana, 95, 100, 20) { ManaOverflowPercent = overflow };
            new ManaResourceRule().ProcessTurnStart(pool);
            Assert.That(pool.CurrentValue, Is.EqualTo(System.Math.Min(115, limit)));
            pool.Restore(1000);
            Assert.That(pool.CurrentValue, Is.EqualTo(limit));
            Assert.That(pool.MaxValue, Is.EqualTo(100));
            Assert.That(pool.Restore(1), Is.Zero);
        }

        [TestCase(ResourceType.Rage)]
        [TestCase(ResourceType.Momentum)]
        public void NonMana_DoesNotUseManaOverflow(ResourceType type)
        {
            var pool = new ResourcePool(type, 95, 100, 20) { ManaOverflowPercent = 30 };
            Assert.That(pool.Restore(100), Is.EqualTo(5));
        }

        [Test]
        public void Mana_StatRefreshPreservesOverflow_AndRemovingRankRestoresLimit()
        {
            var character = new CharacterInstance
            {
                Stats = new CharacterStats(), ResourcePool = new ResourcePool(ResourceType.Mana, 125, 100, 20),
                SkillTreeProgress = new() { new SkillTreeProgress { TreeId = "tree.mage.warlock", NodeInvestments = new()
                { new SkillTreeNodeInvestment { NodeId = "node.mage.warlock.beyond_limits", Ranks = 2 } } } }
            };
            character.Stats.MaxResource = new Godmist.Core.Types.Stat(100);
            character.Stats.ResourceRegeneration = new Godmist.Core.Types.Stat(20);
            var pipeline = new Godmist.Systems.Characters.CharacterStatPipelineSystem();
            pipeline.Refresh(character);
            Assert.That(character.ResourcePool.CurrentValue, Is.EqualTo(125));
            var unit = new BattleUnit { Character = character };
            new ResourceRuleSystem().ProcessExplorationTick(unit);
            Assert.That(character.ResourcePool.CurrentValue, Is.EqualTo(125), "Waiting must preserve overflow without granting mana.");
            Assert.That(new CombatRecoverySystem().RestoreResource(unit, 20), Is.EqualTo(5));
            Assert.That(character.ResourcePool.CurrentValue, Is.EqualTo(130));
            pipeline.Refresh(character);
            Assert.That(character.ResourcePool.CurrentValue, Is.EqualTo(130));
            character.SkillTreeProgress.Clear();
            pipeline.Refresh(character);
            Assert.That(character.ResourcePool.CurrentValue, Is.EqualTo(100));
        }

        [Test]
        public void EnemyMana_RegeneratesAtTurnStart()
        {
            var unit = CreateUnit(
                ResourceType.Mana,
                current: 4,
                max: 20,
                regeneration: 5);

            var system = new ResourceRuleSystem();
            unit.IsPlayerSide = false;
            system.ProcessTurnStart(unit);

            Assert.That(
                unit.Character.ResourcePool.CurrentValue,
                Is.EqualTo(9));
        }

        [Test]
        public void Momentum_GainsEscalatingStepsIndependentlyOfActionPointCost()
        {
            var unit = CreateUnit(
                ResourceType.Momentum,
                current: 0,
                max: 200);

            var system = new ResourceRuleSystem();
            unit.MovementStepsThisTurn = 3;

            system.ProcessUnitMoved(
                unit,
                spentActionPoints: 300, completedSteps: 3);

            Assert.That(
                unit.Character.ResourcePool.CurrentValue,
                Is.EqualTo(12));
        }

        [Test]
        public void Momentum_AboveOneHundred_GainsAtHalfEfficiency()
        {
            var unit = CreateUnit(
                ResourceType.Momentum,
                current: 100,
                max: 200);

            var system = new ResourceRuleSystem();
            unit.MovementStepsThisTurn = 3;

            system.ProcessUnitMoved(
                unit,
                spentActionPoints: 300, completedSteps: 3);

            Assert.That(
                unit.Character.ResourcePool.CurrentValue,
                Is.EqualTo(106));
        }

        [Test]
        public void Rage_GainsTenForOffensiveSkillUse()
        {
            var attacker = CreateUnit(
                ResourceType.Rage,
                current: 0,
                max: 50);

            var resourceSystem = new ResourceRuleSystem();
            resourceSystem.ProcessOffensiveSkillUsed(attacker);

            Assert.That(
                attacker.Character.ResourcePool.CurrentValue,
                Is.EqualTo(10));
        }

        [Test]
        public void Rage_FirstIdleTurnIsFreeThenEveryFurtherIdleTurnCostsTen()
        {
            var unit = CreateUnit(
                ResourceType.Rage,
                current: 20,
                max: 50);

            var system = new ResourceRuleSystem();

            system.ProcessTurnStart(unit);
            system.ProcessTurnEnd(unit);
            Assert.That(unit.Character.ResourcePool.CurrentValue,Is.EqualTo(20));
            system.ProcessTurnStart(unit);
            system.ProcessTurnEnd(unit);

            Assert.That(
                unit.Character.ResourcePool.CurrentValue,
                Is.EqualTo(10));
            system.ProcessTurnStart(unit);system.ProcessTurnEnd(unit);
            Assert.That(unit.Character.ResourcePool.CurrentValue,Is.Zero);
        }

        [Test]
        public void Rage_OffensiveSkillUsePreventsNextTurnDecay()
        {
            var unit = CreateUnit(
                ResourceType.Rage,
                current: 20,
                max: 50);

            var system = new ResourceRuleSystem();

            system.ProcessTurnStart(unit);
            system.ProcessOffensiveSkillUsed(unit);
            system.ProcessTurnEnd(unit);
            system.ProcessTurnStart(unit);

            Assert.That(
                unit.Character.ResourcePool.CurrentValue,
                Is.EqualTo(30));
        }

        [Test]
        public void Rage_AtMaximumStillPreventsNextTurnDecay()
        {
            var unit = CreateUnit(
                ResourceType.Rage,
                current: 50,
                max: 50);

            var system = new ResourceRuleSystem();

            system.ProcessTurnStart(unit);
            system.ProcessOffensiveSkillUsed(unit);
            system.ProcessTurnEnd(unit);
            system.ProcessTurnStart(unit);

            Assert.That(
                unit.Character.ResourcePool.CurrentValue,
                Is.EqualTo(50));
        }

        [Test]
        public void ResourceBlock_PreventsManaMomentumAndRageGains()
        {
            var manaUnit = CreateUnit(ResourceType.Mana, 4, 20, regeneration: 5);
            var momentumUnit = CreateUnit(ResourceType.Momentum, 0, 200);
            momentumUnit.MovementStepsThisTurn = 3;
            var rageUnit = CreateUnit(ResourceType.Rage, 0, 50);
            foreach (var unit in new[] { manaUnit, momentumUnit, rageUnit })
            {
                unit.Character.PassiveEffects.Add(new PassiveEffectRuntime
                {
                    BlocksResourceRegeneration = true
                });
            }

            var system = new ResourceRuleSystem();
            manaUnit.MaxActionPoints = manaUnit.CurrentActionPoints = 1000;
            system.ProcessTurnEnd(manaUnit);
            system.ProcessUnitMoved(momentumUnit, spentActionPoints: 300, completedSteps: 3);
            system.ProcessOffensiveSkillUsed(rageUnit);

            Assert.That(manaUnit.Character.ResourcePool.CurrentValue, Is.EqualTo(4));
            Assert.That(momentumUnit.Character.ResourcePool.CurrentValue, Is.EqualTo(0));
            Assert.That(rageUnit.Character.ResourcePool.CurrentValue, Is.EqualTo(0));
        }

        [TestCase(true,1,1)] [TestCase(false,1,1)]
        [TestCase(true,3,1)] [TestCase(false,3,1)]
        [TestCase(true,1,5)] [TestCase(false,1,5)]
        [TestCase(true,3,5)] [TestCase(false,3,5)]
        public void OffensiveCastGrantsTenOnceForAnySideTargetCountOrHitCount(bool player,int targets,int hits)
        {
            var (session,caster,skill,selected)=RageCast(player,targets,hits);
            var events=new List<CombatFeedbackEvent>();var bus=new CombatFeedbackBus();bus.Published+=events.Add;
            var system=new SkillExecutionSystem(feedbackBus:bus);
            Assert.That(system.TryExecute(session,caster,skill,selected),Is.True);
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.EqualTo(10));
            Assert.That(events.Count(e=>e.Type==CombatFeedbackType.ResourceGain && e.Target==caster),Is.EqualTo(1));
            Assert.That(selected[0].All(t=>t.Character.CurrentHealth<1000),Is.True,"All selected targets must actually be hit.");
            Assert.That(system.TryExecute(session,caster,skill,selected),Is.True);
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.EqualTo(20),"A distinct second cast earns its own ten.");
        }

        [TestCase(true)] [TestCase(false)]
        public void DodgedOffensiveCastStillGrantsTenAndPreventsDecay(bool player)
        {
            var (session,caster,skill,selected)=RageCast(player,3,1);
            RageField(skill.Effects[0],"damageType",DamageType.Physical);
            foreach(var target in selected[0]) target.Character.PassiveEffects.Add(new PassiveEffectRuntime {UniqueBuffType=UniqueBuffType.GuaranteedDodge});
            var rules=new ResourceRuleSystem();rules.ProcessTurnStart(caster);
            Assert.That(new SkillExecutionSystem(resourceRuleSystem:rules).TryExecute(session,caster,skill,selected),Is.True);
            Assert.That(selected[0].All(t=>t.Character.CurrentHealth==1000),Is.True,"Every target dodges.");
            rules.ProcessTurnEnd(caster,session);rules.ProcessTurnStart(caster);
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.EqualTo(10));
        }

        [TestCase(StatusEffectIds.Stun)] [TestCase(StatusEffectIds.Freeze)]
        [TestCase(StatusEffectIds.Sleep)] [TestCase(StatusEffectIds.Paralysis)]
        public void Rage_HardCcPausesButDoesNotResetExistingIdleTurn(string status)
        {
            var unit=CreateUnit(ResourceType.Rage,30,50);var rules=new ResourceRuleSystem();
            rules.ProcessTurnStart(unit);rules.ProcessTurnEnd(unit);
            Assert.That(unit.WarriorState.RageIdleTurns,Is.EqualTo(1));
            Assert.That(new StatusEffectSystem().ApplyStatus(null,unit,status,1,3,1,0,0,"rage.test",sharedRoll:0),Is.True);
            for(int i=0;i<2;i++){rules.ProcessTurnStart(unit);rules.ProcessTurnEnd(unit);}
            Assert.That(unit.Character.ResourcePool.CurrentValue,Is.EqualTo(30));
            Assert.That(unit.WarriorState.RageIdleTurns,Is.EqualTo(1));
            new StatusEffectSystem().RemoveStatus(unit,status);
            rules.ProcessTurnStart(unit);rules.ProcessTurnEnd(unit);
            Assert.That(unit.Character.ResourcePool.CurrentValue,Is.EqualTo(20));
        }

        [TestCase(false,false,false)] [TestCase(true,false,false)]
        [TestCase(true,true,false)] [TestCase(true,false,true)]
        public void Rage_RootProtectsOnlyWhenNoLegalOffenseExists(bool rooted,bool near,bool ranged)
        {
            var (session,caster,skill,selected)=RageCast(true,1,1);
            caster.Character.ResourcePool.CurrentValue=30;caster.Character.Skills.Add(skill);
            var target=selected[0][0];target.Position=near?new GridPosition(2,1):new GridPosition(5,5);
            RageField(skill.TargetModes[0],"targetType",TargetType.Single);
            RageField(skill.TargetModes[0],"absoluteRange",new SkillRangeData(RangeShape.Square,0,ranged?6:1));
            if(rooted)Assert.That(new StatusEffectSystem().ApplyStatus(null,caster,StatusEffectIds.Immobilize,1,3,1,0,0,"rage.root",sharedRoll:0),Is.True);
            var rules=new ResourceRuleSystem();
            for(int i=0;i<2;i++)
            { rules.ProcessTurnStart(caster);rules.RecordRageOffensiveOpportunity(session,caster);rules.ProcessTurnEnd(caster,session); }
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.EqualTo(rooted&&!near&&!ranged?30:20));
        }

        [TestCase(StatusEffectIds.Stun)] [TestCase(StatusEffectIds.Immobilize)]
        public void Rage_TurnSystemSettlesControlBeforeLastStatusTurnExpires(string status)
        {
            var (session,caster,_,selected)=RageCast(true,1,1);
            caster.Character.ResourcePool.CurrentValue=30;caster.WarriorState.RageIdleTurns=1;
            var statuses=new StatusEffectSystem();
            Assert.That(statuses.ApplyStatus(null,caster,status,1,1,1,0,0,"rage.last-cc-turn",sharedRoll:0),Is.True);
            session.TurnParticipants=new List<CombatTurnParticipant>{new(caster),new(selected[0][0])};
            session.ActiveParticipant=session.TurnParticipants[0];
            var rules=new ResourceRuleSystem();rules.ProcessTurnStart(caster);
            new CombatTurnSystem(onUnitTurnEnded:statuses.ProcessTurnEnd,resourceRuleSystem:rules).EndActiveTurn(session);
            Assert.That(StatusEffectSystem.HasStatus(caster,status),Is.False);
            Assert.That(caster.WarriorState.RageIdleTurns,Is.EqualTo(1));
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.EqualTo(30));
        }

        [Test]
        public void Rage_KillingTargetAllowsOneFullApproachTurnBeforeDecay()
        {
            var (session,caster,skill,selected)=RageCast(true,1,1);
            caster.Character.ResourcePool.CurrentValue=30;
            selected[0][0].Character.CurrentHealth=1;
            var rules=new ResourceRuleSystem();rules.ProcessTurnStart(caster);
            Assert.That(new SkillExecutionSystem(resourceRuleSystem:rules).TryExecute(session,caster,skill,selected),Is.True);
            Assert.That(selected[0][0].Character.LifeState,Is.Not.EqualTo(CharacterLifeState.Alive));
            rules.ProcessTurnEnd(caster,session);
            rules.ProcessTurnStart(caster);rules.ProcessUnitMoved(caster,20);rules.ProcessTurnEnd(caster,session);
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.EqualTo(40),"First approach turn is free.");
            rules.ProcessTurnStart(caster);rules.ProcessTurnEnd(caster,session);
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.EqualTo(30));
        }

        [TestCase("cost")] [TestCase("sight")] [TestCase("invisible")] [TestCase("mobility")]
        public void Rage_RootedUnavailableAttackProtectsAndAvailabilityCheckDoesNotSpend(string blocker)
        {
            var (session,caster,skill,selected)=RageCast(true,1,1);
            caster.Character.ResourcePool.CurrentValue=30;caster.Character.Skills.Add(skill);
            RageField(skill.TargetModes[0],"targetType",TargetType.Single);
            RageField(skill.TargetModes[0],"absoluteRange",new SkillRangeData(RangeShape.Square,0,6));
            var statuses=new StatusEffectSystem();
            Assert.That(statuses.ApplyStatus(null,caster,StatusEffectIds.Immobilize,1,3,1,0,0,"rage.root",sharedRoll:0),Is.True);
            if(blocker=="cost")RageAuto(skill,"ResourceCost",31);
            if(blocker=="sight")
            {
                selected[0][0].Position=new GridPosition(4,1);
                RageAuto(skill,"RequiresLineOfSight",true);
                session.MapObjects.Add(new ObstacleCombatMapObject("tree",new GridPosition(3,1),true,true));
            }
            if(blocker=="invisible")
                Assert.That(statuses.ApplyStatus(null,selected[0][0],StatusEffectIds.Invisibility,1,3,1,0,0,"rage.hidden",sharedRoll:0),Is.True);
            if(blocker=="mobility")RageField(skill.Effects[0],"effectType",EffectType.Charge);
            var beforeAp=caster.CurrentActionPoints;var beforeHealth=selected[0][0].Character.CurrentHealth;
            var rules=new ResourceRuleSystem();
            for(int i=0;i<2;i++)
            {rules.ProcessTurnStart(caster);rules.RecordRageOffensiveOpportunity(session,caster);rules.ProcessTurnEnd(caster,session);}
            Assert.That(caster.WarriorState.RageHadOffensiveOpportunity,Is.False);
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.EqualTo(30));
            Assert.That(caster.CurrentActionPoints,Is.EqualTo(beforeAp));
            Assert.That(selected[0][0].Character.CurrentHealth,Is.EqualTo(beforeHealth));
            Assert.That(caster.WarriorState.RageOffensiveActionUsed,Is.False);
        }

        [Test]
        public void Rage_NonOffensiveGainDoesNotResetIdleCounter()
        {
            var unit=CreateUnit(ResourceType.Rage,20,50);var rules=new ResourceRuleSystem();
            rules.ProcessTurnStart(unit);rules.ProcessTurnEnd(unit);
            rules.ProcessTurnStart(unit);
            unit.Character.ResourcePool.Restore(10);
            unit.Character.ResourcePool.HasGainedResourceSinceLastTurn=true;
            rules.ProcessTurnEnd(unit);
            Assert.That(unit.Character.ResourcePool.CurrentValue,Is.EqualTo(20));
            Assert.That(unit.WarriorState.RageIdleTurns,Is.EqualTo(2));
        }

        [Test]
        public void Rage_SpendingApCannotRetroactivelyEraseRootedAttackOpportunity()
        {
            var (session,caster,skill,_)=RageCast(true,1,1);caster.Character.ResourcePool.CurrentValue=30;
            caster.Character.Skills.Add(skill);var rules=new ResourceRuleSystem();
            rules.ProcessTurnStart(caster);rules.ProcessTurnEnd(caster,session);
            Assert.That(new StatusEffectSystem().ApplyStatus(null,caster,StatusEffectIds.Immobilize,1,3,1,0,0,"rage.root",sharedRoll:0),Is.True);
            rules.ProcessTurnStart(caster);rules.RecordRageOffensiveOpportunity(session,caster);
            Assert.That(caster.WarriorState.RageHadOffensiveOpportunity,Is.True);
            caster.CurrentActionPoints=0;rules.ProcessTurnEnd(caster,session);
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.EqualTo(20));
        }

        [Test]
        public void Rage_OffenseResetsGraceEvenWithBlockedGenerationAndAtMaximum()
        {
            var unit=CreateUnit(ResourceType.Rage,50,50);var rules=new ResourceRuleSystem();
            rules.ProcessTurnStart(unit);rules.ProcessTurnEnd(unit);
            unit.Character.PassiveEffects.Add(new PassiveEffectRuntime{BlocksResourceRegeneration=true});
            rules.ProcessTurnStart(unit);rules.ProcessOffensiveSkillUsed(unit);rules.ProcessTurnEnd(unit);
            Assert.That(unit.WarriorState.RageIdleTurns,Is.Zero);
            rules.ProcessTurnStart(unit);rules.ProcessTurnEnd(unit);
            Assert.That(unit.Character.ResourcePool.CurrentValue,Is.EqualTo(50));
            rules.ProcessTurnStart(unit);rules.ProcessTurnEnd(unit);
            Assert.That(unit.Character.ResourcePool.CurrentValue,Is.EqualTo(40));
        }

        [TestCase(true)] [TestCase(false)]
        public void MissedOffensiveCastStillGrantsTen(bool player)
        {
            var (session,caster,skill,selected)=RageCast(player,1,1);
            RageField(skill.Effects[0],"damageType",DamageType.Physical);
            RageAuto(skill,"SkillAccuracy",0f);caster.Character.WeaponAccuracy=0;
            var previous=Random.state;
            try
            {
                Random.InitState(917);
                Assert.That(new SkillExecutionSystem().TryExecute(session,caster,skill,selected),Is.True);
            }
            finally {Random.state=previous;}
            Assert.That(selected[0][0].Character.CurrentHealth,Is.EqualTo(1000),"The accuracy roll must actually miss.");
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.EqualTo(10));
        }

        [TestCase(0,false,10,10)] [TestCase(45,false,50,5)]
        [TestCase(50,false,50,0)] [TestCase(20,true,20,0)]
        public void OffensiveCastRespectsCapBlockAndReportsActualGain(int start,bool blocked,int expected,int feedbackGain)
        {
            var (session,caster,skill,selected)=RageCast(true,3,5);
            caster.Character.ResourcePool.CurrentValue=start;
            if(blocked)caster.Character.PassiveEffects.Add(new PassiveEffectRuntime {BlocksResourceRegeneration=true});
            var gains=new List<CombatFeedbackEvent>();var bus=new CombatFeedbackBus();bus.Published+=e=>{if(e.Type==CombatFeedbackType.ResourceGain)gains.Add(e);};
            Assert.That(new SkillExecutionSystem(feedbackBus:bus).TryExecute(session,caster,skill,selected),Is.True);
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.EqualTo(expected));
            Assert.That(caster.Character.ResourcePool.HasGainedResourceSinceLastTurn,Is.EqualTo(!blocked));
            Assert.That(gains.Sum(e=>e.Amount),Is.EqualTo(feedbackGain));
            Assert.That(gains.Count,Is.EqualTo(feedbackGain>0?1:0));
        }

        [TestCase(ResourceType.Mana)] [TestCase(ResourceType.Momentum)]
        public void OffensiveCastDoesNotGenerateOtherResources(ResourceType resource)
        {
            var (session,caster,skill,selected)=RageCast(true,2,3);
            caster.Character.ResourcePool=new ResourcePool(resource,0,100,0);
            RageAuto(skill,"ResourceType",resource);
            Assert.That(new SkillExecutionSystem().TryExecute(session,caster,skill,selected),Is.True);
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.Zero);
        }

        [Test]
        public void DelayedOffensiveCastGrantsTenAtUseAndNothingAtItsMultipleImpacts()
        {
            var (session,caster,skill,selected)=RageCast(false,3,5);
            RageField(skill.TargetModes[0],"targetType",TargetType.AreaAroundTile);
            // Relative Square uses a side length, so [0,2] is a centered 3x3 area.
            RageField(skill.TargetModes[0],"relativeRange",new SkillRangeData(RangeShape.Square,0,2));
            foreach(var effect in skill.Effects)RageField(effect,"delayCasterTurns",1);
            var system=new SkillExecutionSystem();
            Assert.That(system.TryExecute(session,caster,skill,selected,null,null,new Dictionary<int,GridPosition?>{{0,new GridPosition(2,2)}}),Is.True);
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.EqualTo(10));
            Assert.That(selected[0].All(t=>t.Character.CurrentHealth==1000),Is.True);
            Assert.That(session.ScheduledEffects,Is.Not.Empty);
            system.ProcessScheduledEffects(session,caster);
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.EqualTo(10));
            Assert.That(selected[0].All(t=>t.Character.CurrentHealth==1000),Is.True,"Scheduling includes the casting turn before the next own turn.");
            system.ProcessScheduledEffects(session,caster);
            Assert.That(session.ScheduledEffects,Is.Empty);
            Assert.That(selected[0].All(t=>t.Character.CurrentHealth<1000),Is.True);
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.EqualTo(10));
        }

        [TestCase(true)] [TestCase(false)]
        public void SingleEffectMultiHitAreaGrantsTen(bool player)
        {
            var (session,caster,skill,selected)=RageCast(player,3,1);
            RageField(skill.Effects[0],"damageHitCount",5);
            Assert.That(new SkillExecutionSystem().TryExecute(session,caster,skill,selected),Is.True);
            Assert.That(selected[0].All(t=>t.Character.CurrentHealth==950),Is.True,"Five actual hits against every target.");
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.EqualTo(10));
        }

        [Test]
        public void PreviewInvalidCastUtilityAndPassiveDuplicateDoNotGenerateRage()
        {
            var (session,caster,skill,selected)=RageCast(true,2,2);
            var system=new SkillExecutionSystem();
            Assert.That(system.CanPreview(session,caster,skill,selected,null,null,null),Is.True);
            caster.CurrentActionPoints=0;
            Assert.That(system.TryExecute(session,caster,skill,selected),Is.False);
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.Zero);
            caster.CurrentActionPoints=10000;
            Assert.That(system.TryExecute(session,caster,skill,selected,null,null,null,skipCosts:true,isPassiveDuplicate:true),Is.True);
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.Zero,"Automatic copies are part of the original skill use.");
            RageField(skill.TargetModes[0],"targetType",TargetType.Caster);
            RageField(skill.TargetModes[0],"targetingType",TargetingType.Allies);
            RageField(skill.TargetModes[0],"includesCaster",true);
            foreach(var effect in skill.Effects)RageField(effect,"effectType",EffectType.ModifyActionValue);
            selected[0]=new[]{caster};
            Assert.That(system.TryExecute(session,caster,skill,selected),Is.True);
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.Zero,"Self support/acceleration cannot refund Rage.");
        }

        [Test]
        public void OffensiveSkillCannotUseItsFutureRageGainToPayItsCost()
        {
            var (session,caster,skill,selected)=RageCast(true,1,1);
            RageAuto(skill,"ResourceCost",10);
            Assert.That(new SkillExecutionSystem().TryExecute(session,caster,skill,selected),Is.False);
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.Zero);
            caster.Character.ResourcePool.CurrentValue=10;
            Assert.That(new SkillExecutionSystem().TryExecute(session,caster,skill,selected),Is.True);
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.EqualTo(10),"Cost is paid before generation.");
        }

        [TestCase(true)] [TestCase(false)]
        public void PureControlPreventsDecayWithoutGeneratingRageForEitherSide(bool player)
        {
            var (session,caster,skill,selected)=RageCast(player,1,1);
            caster.Character.ResourcePool.CurrentValue=30;
            var effect=skill.Effects[0];RageField(effect,"effectType",EffectType.ApplyStatus);
            RageField(effect,"statusId",StatusEffectIds.Stun);RageField(effect,"durationTurns",1);
            RageField(effect,"baseChance",1f);RageField(effect,"alwaysHits",true);
            var rules=new ResourceRuleSystem();rules.ProcessTurnStart(caster);rules.ProcessTurnEnd(caster);
            Assert.That(caster.WarriorState.RageIdleTurns,Is.EqualTo(1));
            rules.ProcessTurnStart(caster);
            var feedback=new List<CombatFeedbackEvent>();var bus=new CombatFeedbackBus();bus.Published+=feedback.Add;
            Assert.That(new SkillExecutionSystem(resourceRuleSystem:rules,feedbackBus:bus,statusEffectSystem:new StatusEffectSystem()).TryExecute(session,caster,skill,selected),Is.True);
            rules.ProcessTurnEnd(caster,session);
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.EqualTo(30));
            Assert.That(caster.WarriorState.RageOffensiveActionUsed,Is.True);
            Assert.That(caster.WarriorState.RageIdleTurns,Is.Zero);
            Assert.That(caster.Character.ResourcePool.HasGainedResourceSinceLastTurn,Is.False);
            Assert.That(feedback.Any(e=>e.Type==CombatFeedbackType.ResourceGain && e.Target==caster),Is.False);
            rules.ProcessTurnStart(caster);rules.ProcessTurnEnd(caster);
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.EqualTo(30));
            rules.ProcessTurnStart(caster);rules.ProcessTurnEnd(caster);
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.EqualTo(20));
        }

        [TestCase(StatusEffectIds.Bleed,true)] [TestCase(StatusEffectIds.Bleed,false)]
        [TestCase(StatusEffectIds.Poison,true)] [TestCase(StatusEffectIds.Poison,false)]
        [TestCase(StatusEffectIds.Burn,true)] [TestCase(StatusEffectIds.Burn,false)]
        public void DotOnlyDamageGeneratesOnceAtCastAndNotAgainOnTicks(string statusId,bool player)
        {
            var (session,caster,skill,selected)=RageCast(player,1,1);var effect=skill.Effects[0];
            RageField(effect,"effectType",EffectType.ApplyStatus);RageField(effect,"statusId",statusId);
            RageField(effect,"statusDamageMultiplier",.4f);RageField(effect,"durationTurns",2);
            RageField(effect,"baseChance",1f);RageField(effect,"alwaysHits",true);
            var statuses=new StatusEffectSystem();
            Assert.That(new SkillExecutionSystem(statusEffectSystem:statuses).TryExecute(session,caster,skill,selected),Is.True);
            var target=selected[0][0];Assert.That(target.Character.CurrentHealth,Is.EqualTo(1000));
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.EqualTo(10));
            for(var turn=0;turn<2;turn++){statuses.ProcessTurnStart(session,target);statuses.ProcessTurnEnd(target);}
            Assert.That(target.Character.CurrentHealth,Is.LessThan(1000));
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.EqualTo(10));
        }

        [Test]
        public void FriendlyDamageCannotTurnAnEnemyDebuffIntoARageGenerator()
        {
            var (_,_,skill,_)=RageCast(true,1,1);var effect=skill.Effects[0];
            RageField(effect,"effectType",EffectType.ModifyStat);
            var mode=new SkillTargetModeData();RageField(mode,"targetType",TargetType.Caster);
            RageField(mode,"targetingType",TargetingType.Allies);
            RageField(skill,"targetModes",new List<SkillTargetModeData>{skill.TargetModes[0],mode});
            var friendlyDamage=new SkillEffectData();RageField(friendlyDamage,"effectType",EffectType.DealDamage);
            RageField(friendlyDamage,"targetModeIndex",1);RageField(skill,"effects",new List<SkillEffectData>{effect,friendlyDamage});
            Assert.That(PassiveEffectCombatExecutionSystem.IsOffensiveSkill(skill),Is.True);
            Assert.That(PassiveEffectCombatExecutionSystem.IsDamagingOffensiveSkill(skill),Is.False);
            RageField(friendlyDamage,"targetModeIndex",0);
            Assert.That(PassiveEffectCombatExecutionSystem.IsDamagingOffensiveSkill(skill),Is.True);
        }

        [Test]
        public void ProductionKickThenCutSpendsTenNetAndCollisionStunCanBeRepeatedWithoutImmunity()
        {
            var (session,caster,_,selected)=RageCast(true,1,1);caster.Character.ResourcePool.CurrentValue=40;
            var target=selected[0][0];session.Grid.SetWallEdge(target.Position,new GridPosition(3,1),DungeonWallType.Full);
            SkillData Certain(string name)
            {
                var clone=Object.Instantiate(AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_"+name+".asset"));
                rageAssets.Add(clone);foreach(var effect in clone.Effects){RageField(effect,"alwaysHits",true);RageField(effect,"baseChance",1f);}return clone;
            }
            var kick=Certain("WarriorKick");var cut=Certain("WarriorCut");var statuses=new StatusEffectSystem();
            var rules=new ResourceRuleSystem();rules.ProcessTurnStart(caster);
            var execution=new SkillExecutionSystem(statusEffectSystem:statuses,resourceRuleSystem:rules);
            Assert.That(execution.TryExecute(session,caster,kick,selected),Is.True);
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.EqualTo(20));
            Assert.That(target.Position,Is.EqualTo(new GridPosition(2,1)));
            Assert.That(target.Character.PassiveEffects.Single(e=>e.EffectId==StatusEffectIds.Stun).RemainingDuration,Is.EqualTo(1));
            Assert.That(execution.TryExecute(session,caster,cut,selected),Is.True);
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.EqualTo(30));
            rules.ProcessTurnEnd(caster,session);Assert.That(caster.Character.ResourcePool.CurrentValue,Is.EqualTo(30));
            statuses.ProcessTurnEnd(target);Assert.That(StatusEffectSystem.HasStatus(target,StatusEffectIds.Stun),Is.False);
            caster.CurrentActionPoints=caster.MaxActionPoints;
            Assert.That(execution.TryExecute(session,caster,kick,selected),Is.True);
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.EqualTo(10));
            Assert.That(target.Character.PassiveEffects.Single(e=>e.EffectId==StatusEffectIds.Stun).RemainingDuration,Is.EqualTo(1));
        }

        [Test]
        public void ProductionBossHammerGeneratesAtCastDespiteItsOwnerResolvingDamageLater()
        {
            var (session,caster,_,selected)=RageCast(false,1,1);
            var hammer=AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Catacombs/Skills/SD_Catacombs_skeleton_slayer_hammer.asset");
            Assert.That(hammer.Effects.Any(e=>e.EffectType==EffectType.DealDamage),Is.False);
            Assert.That(PassiveEffectCombatExecutionSystem.IsDamagingOffensiveSkill(hammer),Is.True);
            Assert.That(new SkillExecutionSystem(statusEffectSystem:new StatusEffectSystem()).TryExecute(session,caster,hammer,selected),Is.True);
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.EqualTo(10));
            Assert.That(selected[0][0].Character.CurrentHealth,Is.EqualTo(1000),"The cast itself only roots; the boss owner resolves damage later.");
        }

        [Test, Explicit("Save only the existing Rage glossary description through Editor API.")]
        public void ApplyRageGlossary()
        {
            Assert.That(EditorApplication.isPlayingOrWillChangePlaymode,Is.False);
            var glossary=AssetDatabase.LoadAssetAtPath<Godmist.Views.UI.CombatTooltipGlossary>("Assets/_Project/ScriptableObjects/UI/CombatTooltipGlossary.asset");
            var description=Godmist.Views.UI.CombatTooltipGlossary.BuiltInEntries.Single(e=>e.Id=="rage").Description;
            for(var repeat=0;repeat<2;repeat++)
            {
                var data=new SerializedObject(glossary);var entries=data.FindProperty("entries");var found=false;
                for(var n=0;n<entries.arraySize;n++)
                {
                    var entry=entries.GetArrayElementAtIndex(n);if(entry.FindPropertyRelative("Id").stringValue!="rage")continue;
                    entry.FindPropertyRelative("Description").stringValue=description;found=true;
                }
                Assert.That(found,Is.True);data.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(glossary);
            }
            Assert.That(glossary.TryGet("rage",out var authored),Is.True);Assert.That(authored.Description,Is.EqualTo(description));
        }

        [Test]
        public void RageGlossarySeparatesDamageGenerationFromControlDecayProtection()
        {
            var glossary=AssetDatabase.LoadAssetAtPath<Godmist.Views.UI.CombatTooltipGlossary>("Assets/_Project/ScriptableObjects/UI/CombatTooltipGlossary.asset");
            Assert.That(glossary.TryGet("rage",out var authored),Is.True);
            Assert.That(authored.Description,Is.EqualTo(Godmist.Views.UI.CombatTooltipGlossary.BuiltInEntries.Single(e=>e.Id=="rage").Description));
            foreach(var phrase in new[]{"obrażenia bezpośrednie lub okresowe","także przy pudle","kolejne ticki nie generują","Każda akcja ofensywna","bez obrażeń","Wyładowanie nie generuje"})
                Assert.That(authored.Description,Does.Contain(phrase));
        }

        [Test, Explicit("Read-only report of production Rage skills under old/new generation rules.")]
        public void ReportRageSkillClassification()
        {
            var rows=new List<string>{"skill,apPercent,resourceCost,oldGenerates,newGenerates"};
            foreach(var skill in AssetDatabase.FindAssets("t:SkillData",new[]{"Assets/_Project/ScriptableObjects"})
                .Select(g=>AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(s=>s.ResourceType==ResourceType.Rage).OrderBy(s=>s.SkillId))
            {
                var offensive=PassiveEffectCombatExecutionSystem.IsOffensiveSkill(skill);
                var old=offensive && skill.SkillId!=WarriorDischargeRules.SkillId;
                var current=old && PassiveEffectCombatExecutionSystem.IsDamagingOffensiveSkill(skill);
                rows.Add($"{skill.SkillId},{skill.ActionCost},{skill.ResourceCost},{old},{current}");
            }
            var folder="../LocalHandoffs/rage-damage-generation-20261025";System.IO.Directory.CreateDirectory(folder);
            System.IO.File.WriteAllLines(folder+"/classification.csv",rows);
        }

        (CombatSession session,BattleUnit caster,SkillData skill,Dictionary<int,IReadOnlyList<BattleUnit>> selected) RageCast(bool player,int count,int hits)
        {
            var definition=ScriptableObject.CreateInstance<CharacterDefinition>();rageAssets.Add(definition);
            RageAuto(definition,"Id","test.rage.cast");RageAuto(definition,"DisplayName","Rage cast");
            BattleUnit Unit(bool side,int x,int y)
            {
                var unit=CreateUnit(ResourceType.Rage,0,50);unit.IsPlayerSide=side;unit.Position=new GridPosition(x,y);
                unit.Character.CharacterId=System.Guid.NewGuid().ToString();unit.Character.Definition=definition;
                unit.Character.CurrentHealth=1000;unit.Character.WeaponAccuracy=10000;
                unit.Character.Stats=new CharacterStats {MaxHealth=new Stat(1000),MinAttack=new Stat(10),MaxAttack=new Stat(10),Speed=new Stat(60),MaxResource=new Stat(50)};
                unit.MaxActionPoints=unit.CurrentActionPoints=10000;return unit;
            }
            var caster=Unit(player,1,1);var targets=Enumerable.Range(0,count).Select(i=>Unit(!player,2,1+i)).ToArray();
            var session=new CombatSession {Grid=new BattleGrid(6,6),ActiveUnit=caster,Units=new(){caster}};session.Units.AddRange(targets);
            var skill=ScriptableObject.CreateInstance<SkillData>();rageAssets.Add(skill);
            RageAuto(skill,"SkillId","test.rage.offensive");RageAuto(skill,"DisplayName","Offensive");
            RageAuto(skill,"ResourceType",ResourceType.Rage);RageAuto(skill,"ActionCost",20);
            var mode=new SkillTargetModeData();RageField(mode,"targetType",TargetType.All);RageField(mode,"absoluteRange",new SkillRangeData(RangeShape.Square,0,4));
            RageField(skill,"targetModes",new List<SkillTargetModeData>{mode});
            var effects=new List<SkillEffectData>();
            for(var i=0;i<hits;i++)
            {
                var effect=new SkillEffectData();RageField(effect,"effectType",EffectType.DealDamage);RageField(effect,"damageType",DamageType.Direct);
                RageField(effect,"damageValueSource",DamageValueSource.AverageAttack);RageField(effect,"damageMultiplier",1f);effects.Add(effect);
            }
            RageField(skill,"effects",effects);
            return(session,caster,skill,new(){{0,targets}});
        }
        static void RageField(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
        static void RageAuto(object target,string name,object value)=>RageField(target,"<"+name+">k__BackingField",value);

        private static BattleUnit CreateUnit(
            ResourceType resourceType,
            int current,
            int max,
            int regeneration = 0)
        {
            return new BattleUnit
            {
                Character = new CharacterInstance
                {
                    CurrentHealth = 100,
                    LifeState = CharacterLifeState.Alive,
                    ResourcePool = new ResourcePool(
                        resourceType,
                        current,
                        max,
                        regeneration)
                },
                IsPlayerSide = true,
                Position = new GridPosition(0, 0)
            };
        }

        private static CombatSession CreateSession(
            BattleUnit first,
            BattleUnit second)
        {
            return new CombatSession
            {
                CombatId = "test.resource.rules",
                Outcome = CombatOutcomeState.Ongoing,
                Grid = new BattleGrid(4, 4),
                Units = new List<BattleUnit>
                {
                    first,
                    second
                }
            };
        }
    }
}
