using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Constants;
using Godmist.Core.Types;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class SimpleCombatAiSystemTests
    {
        private static float TacticalScore(CombatSession session,BattleUnit caster,SkillData skill,params BattleUnit[] targets)
            => (float)typeof(SimpleCombatAiSystem).GetMethod("CalculateSkillScore",BindingFlags.Static|BindingFlags.NonPublic)
                .Invoke(null,new object[]{session,caster,skill,new Dictionary<int,IReadOnlyList<BattleUnit>>{[0]=targets}});

        [Test]
        public void TacticalDamageDoesNotInventALethalHitThroughArmorOrShield()
        {
            var skill=PlannerAttack("armored",50,10f);var session=PlannerSession(1,skill);
            var target=session.Units[1];target.Character.CurrentHealth=50;
            var exposed=TacticalScore(session,session.ActiveUnit,skill,target);
            target.Character.Stats.PhysicalDefense=new Stat(10000);
            var armored=TacticalScore(session,session.ActiveUnit,skill,target);
            target.Character.Stats.PhysicalDefense=new Stat(0);target.Character.ShieldPool ??= new ShieldPool();target.Character.ShieldPool.AddLayer(200);
            var shielded=TacticalScore(session,session.ActiveUnit,skill,target);
            Assert.That(exposed,Is.GreaterThan(1000));Assert.That(armored,Is.LessThan(1000));Assert.That(shielded,Is.LessThan(1000));
        }

        [Test]
        public void TacticalReliableFinisherBeatsLargeNearlyAlwaysMissingAttack()
        {
            var accurate=PlannerAttack("accurate",50,6f);var unreliable=PlannerAttack("unreliable",50,50f);
            SetField(unreliable.Effects[0],"alwaysHits",false);SetAutoProperty(unreliable,"SkillAccuracy",0f);
            var session=PlannerSession(1,accurate,unreliable);session.ActiveUnit.Character.WeaponAccuracy=1;
            session.Units[1].Character.CurrentHealth=50;
            Assert.That(TacticalScore(session,session.ActiveUnit,accurate,session.Units[1]),
                Is.GreaterThan(TacticalScore(session,session.ActiveUnit,unreliable,session.Units[1])));
        }

        [Test]
        public void TacticalHealValuesActualRestorationInsteadOfAllMissingHealth()
        {
            var effect=CreateEffect(EffectType.Heal,1);SetField(effect,"baseValue",5);
            var small=CreateSkill(effect);var other=CreateEffect(EffectType.Heal,1);SetField(other,"baseValue",30);var large=CreateSkill(other);
            var session=PlannerSession(1);var ally=session.ActiveUnit;ally.Character.CurrentHealth=10;
            Assert.That(TacticalScore(session,ally,large,ally),Is.GreaterThan(TacticalScore(session,ally,small,ally)));
            ally.Character.CurrentHealth=100;
            Assert.That(TacticalScore(session,ally,large,ally),Is.LessThanOrEqualTo(0));
        }

        [Test]
        public void TacticalAllyLifeStealAccountsForWoundedAlliesAndShieldAbsorption()
        {
            var attack=PlannerAttack("drain",50,4f);SetField(attack.Effects[0],"allyLifeStealPercent",1f);
            var session=PlannerSession(1,attack);var caster=session.ActiveUnit;
            var ally=CreateAiCombatUnit("hurt",false,new GridPosition(0,1));ally.Character.CurrentHealth=10;session.Units.Add(ally);
            var wounded=TacticalScore(session,caster,attack,session.Units[1]);
            ally.Character.CurrentHealth=100;var full=TacticalScore(session,caster,attack,session.Units[1]);
            ally.Character.CurrentHealth=10;session.Units[1].Character.ShieldPool ??= new ShieldPool();session.Units[1].Character.ShieldPool.AddLayer(1000);
            var absorbed=TacticalScore(session,caster,attack,session.Units[1]);
            Assert.That(wounded,Is.GreaterThan(full));Assert.That(absorbed,Is.LessThan(wounded));
        }

        [Test]
        public void TacticalDamagePenalizesFriendlyFire()
        {
            var attack=PlannerAttack("friendly-fire",50,3f);var session=PlannerSession(1,attack);
            var foe=TacticalScore(session,session.ActiveUnit,attack,session.Units[1]);
            var self=TacticalScore(session,session.ActiveUnit,attack,session.ActiveUnit);
            Assert.That(foe,Is.GreaterThan(0));Assert.That(self,Is.LessThan(0));
        }

        [Test]
        public void TacticalShieldIsUsefulOnlyAgainstObservedThreat()
        {
            var incoming=PlannerAttack("incoming",50,5f);ConfigureTargetMode(incoming,TargetingType.Enemies,5);
            var session=PlannerSession(1);var caster=session.ActiveUnit;session.Units[1].Character.Skills.Add(incoming);
            var effect=CreateEffect(EffectType.Shield,1);SetField(effect,"baseValue",40);var shield=CreateSkill(effect);
            var threatened=TacticalScore(session,caster,shield,caster);
            session.Units[1].Character.Skills.Clear();var safe=TacticalScore(session,caster,shield,caster);
            Assert.That(threatened,Is.GreaterThan(safe));Assert.That(safe,Is.LessThanOrEqualTo(0));
        }

        [Test]
        public void TacticalCleanseDoesNotDispelBeneficialStatusForFreePoints()
        {
            var session=PlannerSession(1);var caster=session.ActiveUnit;
            var effect=CreateEffect(EffectType.RemoveStatus,1);SetField(effect,"statusId",StatusEffectIds.Invisibility);var cleanse=CreateSkill(effect);
            caster.Character.PassiveEffects.Add(new PassiveEffectRuntime{EffectId=StatusEffectIds.Invisibility,Lifetime=PassiveEffectLifetime.Timed,RemainingDuration=2});
            Assert.That(TacticalScore(session,caster,cleanse,caster),Is.LessThanOrEqualTo(0));
            SetField(effect,"statusId",StatusEffectIds.Poison);
            caster.Character.PassiveEffects.Add(new PassiveEffectRuntime(StatusEffectIds.Poison,new List<PassiveEffectSourceRuntime>(),1,2,80,PassiveEffectLifetime.Timed,PassiveEffectTickPhase.TurnStart));
            Assert.That(TacticalScore(session,caster,cleanse,caster),Is.GreaterThan(0));
        }

        [Test]
        public void TacticalSharedFocusRewardsTargetsReachableByAllies()
        {
            var attack=PlannerAttack("focus",50,2f);ConfigureTargetMode(attack,TargetingType.Enemies,10);
            var session=PlannerSession(2,attack);session.Grid=new BattleGrid(12,5);
            var supported=session.Units[1];supported.Position=new GridPosition(2,1);
            var isolated=CreateAiCombatUnit("isolated",true,new GridPosition(9,1));isolated.Character.CurrentHealth=1000;session.Units.Add(isolated);
            var ally=CreateAiCombatUnit("ally",false,new GridPosition(2,2));
            var melee=PlannerAttack("ally-melee",50,1);ally.Character.Skills.Add(melee);SetAutoProperty(ally.Character.Definition,"BasicAttackSkill",melee);session.Units.Add(ally);
            Assert.That(TacticalScore(session,session.ActiveUnit,attack,supported),Is.GreaterThan(TacticalScore(session,session.ActiveUnit,attack,isolated)));
        }

        [Test]
        public void TacticalSelectionAndForecastDoNotConsumeRandomOrMutateState()
        {
            var attack=PlannerAttack("no-mutation",50,2);var session=PlannerSession(1,attack);
            var rng=JsonUtility.ToJson(UnityEngine.Random.state);var hp=session.Units[1].Character.CurrentHealth;
            var mana=session.ActiveUnit.Character.ResourcePool.CurrentValue;
            TacticalScore(session,session.ActiveUnit,attack,session.Units[1]);
            CombatSkillPreviewCalculator.EstimateKillChance(session,session.ActiveUnit,session.Units[1],attack,attack.Effects[0]);
            Assert.That(JsonUtility.ToJson(UnityEngine.Random.state),Is.EqualTo(rng));
            Assert.That(session.Units[1].Character.CurrentHealth,Is.EqualTo(hp));Assert.That(session.ActiveUnit.Character.ResourcePool.CurrentValue,Is.EqualTo(mana));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TacticalGlobalAllyTargetingHonoursIncludesCasterAndFriendlyInvisibility(bool includes)
        {
            var session=PlannerSession(1);var caster=session.ActiveUnit;
            var skill=CreateSkill(CreateEffect(EffectType.Heal,1));var mode=skill.TargetModes[0];
            SetField(mode,"targetType",TargetType.All);SetField(mode,"targetingType",TargetingType.Allies);SetField(mode,"includesCaster",includes);
            caster.Character.PassiveEffects.Add(new PassiveEffectRuntime{EffectId=StatusEffectIds.Invisibility,Lifetime=PassiveEffectLifetime.Timed,RemainingDuration=2});
            var targets=new CombatTargetingService().GetTargets(session,caster,mode,null);
            Assert.That(targets.Contains(caster),Is.EqualTo(includes));
        }

        [Test]
        public void TacticalNavigationTriesAnotherObservedOpponentWhenClosestIsUnreachable()
        {
            var session=CreateNavigationSession(7,5,new GridPosition(1,2),new GridPosition(3,2));
            foreach(var cell in new[]{new GridPosition(2,2),new GridPosition(4,2),new GridPosition(3,1),new GridPosition(3,3)})session.Grid.SetBlocked(cell);
            var reachable=CreateAiCombatUnit("reachable",true,new GridPosition(1,4));session.Units.Add(reachable);
            var ai=new SimpleCombatAiSystem();
            Assert.That(ai.TryExecuteNextAction(session,session.ActiveUnit,ai.CreateTurnState(),out var action),Is.True);
            Assert.That(action.Type,Is.EqualTo(SimpleCombatAiActionType.Movement));Assert.That(session.ActiveUnit.Position,Is.EqualTo(new GridPosition(1,3)));
        }
        [TestCase(false)]
        [TestCase(true)]
        public void TacticalTrapPlacementRequiresARealApproachOpportunity(bool ranged)
        {
            var session=PlannerSession(3);session.Grid=new BattleGrid(7,5);
            var caster=session.ActiveUnit;var enemy=session.Units[1];
            if(ranged){var shot=PlannerAttack("stationary-shot",50,2f);ConfigureTargetMode(shot,TargetingType.Enemies,6);enemy.Character.Skills.Add(shot);}
            var definition=ScriptableObject.CreateInstance<Godmist.Data.Combat.SummonedObjectDefinition>();createdAssets.Add(definition);
            SetField(definition,"triggersOnEntry",true);
            var damage=new SummonedObjectEffectData();SetField(damage,"effectType",SummonedObjectEffectType.DealDamage);SetField(damage,"baseValue",40);
            SetField(definition,"effects",new List<SummonedObjectEffectData>{damage});
            var effect=CreateEffect(EffectType.CreateObject,1);SetField(effect,"summonedObjectDefinition",definition);var trap=CreateSkill(effect);
            var ai=new SimpleCombatAiSystem();var method=typeof(SimpleCombatAiSystem).GetMethod("TrapPlacementValue",BindingFlags.NonPublic|BindingFlags.Instance);
            var near=(float)method.Invoke(ai,new object[]{session,caster,trap,new GridPosition(2,0)});
            var isolated=(float)method.Invoke(ai,new object[]{session,caster,trap,new GridPosition(0,4)});
            Assert.That(isolated,Is.EqualTo(0));
            if(ranged)Assert.That(near,Is.EqualTo(0));else Assert.That(near,Is.GreaterThan(0));
        }

        [Test]
        public void TacticalControlDoesNotAssumeAnUnarmedTargetHasAnAttackToDeny()
        {
            var session=PlannerSession(1);var effect=CreateEffect(EffectType.ApplyStatus,1);SetField(effect,"statusId",StatusEffectIds.Stun);
            var control=CreateSkill(effect);var foe=session.Units[1];
            var harmless=TacticalScore(session,session.ActiveUnit,control,foe);
            foe.Character.Skills.Add(PlannerAttack("dangerous-foe",50,5f));
            Assert.That(TacticalScore(session,session.ActiveUnit,control,foe),Is.GreaterThan(harmless));
        }

        [Test]
        public void TacticalRareProcsAndExtraHitsUseTheirProbabilities()
        {
            var attack=PlannerAttack("rare-proc",50,1f);var session=PlannerSession(1,attack);
            var caster=session.ActiveUnit;var target=session.Units[1];var effect=attack.Effects[0];
            caster.Character.Stats.CritChance=new Stat(0);caster.Character.Stats.MinAttack=new Stat(100);caster.Character.Stats.MaxAttack=new Stat(100);
            SetField(effect,"bonusDamageMultiplier",10f);SetField(effect,"bonusDamageMultiplierChance",.01f);
            SetField(effect,"additionalHitTrials",3);SetField(effect,"additionalHitChance",.1f);
            var value=CombatSkillPreviewCalculator.CalculateExpectedTargetDamage(session,caster,target,attack,0,effect);
            Assert.That(value,Is.EqualTo(141.7f).Within(1f));
            target.Character.CurrentHealth=500;
            Assert.That(CombatSkillPreviewCalculator.EstimateKillChance(session,caster,target,attack,effect),Is.LessThanOrEqualTo(.02f));
        }

        [Test]
        public void TacticalHealerCanSelectAndActuallyReviveADownedAlly()
        {
            var effect=CreateEffect(EffectType.Heal,1);SetField(effect,"baseValue",30);var heal=CreateSkill(effect);SetAutoProperty(heal,"ActionCost",50);
            ConfigureTargetMode(heal,TargetingType.Allies,4);
            var session=PlannerSession(2,heal);var caster=session.ActiveUnit;
            var ally=CreateAiCombatUnit("downed-ally",false,new GridPosition(1,0));ally.Character.CurrentHealth=0;ally.Character.LifeState=CharacterLifeState.Downed;ally.DownedTurnsRemaining=1;session.Units.Add(ally);
            var ai=CreateProductionWraithAi();
            Assert.That(ai.TryExecuteNextAction(session,caster,ai.CreateTurnState(),out var action),Is.True);
            Assert.That(action.Skill,Is.SameAs(heal));Assert.That(ally.Character.LifeState,Is.EqualTo(CharacterLifeState.Alive));Assert.That(ally.Character.CurrentHealth,Is.EqualTo(30));
        }

        [TestCase(false,.25f)]
        [TestCase(true,0f)]
        public void TacticalMultiHitFinisherDoesNotTreatAllCritsAsOneRoll(bool oneShotCrit,float expected)
        {
            var attack=PlannerAttack("two-crits",50,1f);var session=PlannerSession(1,attack);var caster=session.ActiveUnit;var target=session.Units[1];
            caster.Character.Stats.MinAttack=new Stat(40);caster.Character.Stats.MaxAttack=new Stat(40);caster.Character.Stats.CritDamage=new Stat(2);
            caster.Character.Stats.CritChance=new Stat(oneShotCrit?0f:.5f);SetField(attack.Effects[0],"damageHitCount",2);
            if(oneShotCrit)ApplyKillChanceBuff(caster,UniqueBuffType.GuaranteedCriticalHit);
            target.Character.CurrentHealth=150;
            Assert.That(CombatSkillPreviewCalculator.EstimateKillChance(session,caster,target,attack,attack.Effects[0]),Is.EqualTo(expected).Within(.001f));
            if(oneShotCrit)Assert.That(CombatDamageSystem.HasUniqueBuff(caster,UniqueBuffType.GuaranteedCriticalHit),Is.True);
        }

        [Test]
        public void TacticalFreezeAndWeakeningPreviewMatchesRealTwoHitDamageWithoutConsumingStatus()
        {
            var attack=PlannerAttack("freeze-opening",50,1f);SetField(attack.Effects[0],"damageHitCount",2);
            var session=PlannerSession(1,attack);var caster=session.ActiveUnit;var target=session.Units[1];
            caster.Character.Stats.MinAttack=new Stat(100);caster.Character.Stats.MaxAttack=new Stat(100);
            caster.Character.Stats.CritChance=new Stat(0);
            caster.Character.PassiveEffects.Add(new PassiveEffectRuntime{EffectId=StatusEffectIds.Weakening,Lifetime=PassiveEffectLifetime.Timed,RemainingDuration=2});
            target.Character.PassiveEffects.Add(new PassiveEffectRuntime{EffectId=StatusEffectIds.Freeze,Lifetime=PassiveEffectLifetime.Timed,RemainingDuration=2});
            var expected=CombatSkillPreviewCalculator.CalculateExpectedTargetDamage(session,caster,target,attack,0);
            Assert.That(expected,Is.EqualTo(240));Assert.That(StatusEffectSystem.IsFrozen(target),Is.True);
            var hp=target.Character.CurrentHealth;
            Assert.That(new SkillExecutionSystem().TryExecute(session,caster,attack,target),Is.True);
            Assert.That(hp-target.Character.CurrentHealth,Is.EqualTo(expected));Assert.That(StatusEffectSystem.IsFrozen(target),Is.False);
        }

        [Test]
        public void TacticalDefensiveBuffUsesActualPreventedDamageAndDoesNotMutateRecipient()
        {
            var incoming=PlannerAttack("defense-threat",50,10f);var session=PlannerSession(1);var caster=session.ActiveUnit;
            session.Units[1].Character.Skills.Add(incoming);
            var effect=CreateEffect(EffectType.ModifyStat,1);SetField(effect,"modifierStatType",CharacterStatType.PhysicalDefense);SetField(effect,"modifierValue",100f);
            var buff=CreateSkill(effect);var before=caster.Character.Stats.PhysicalDefense.GetValue();
            var threatened=TacticalScore(session,caster,buff,caster);
            session.Units[1].Character.Skills.Clear();var safe=TacticalScore(session,caster,buff,caster);
            Assert.That(threatened,Is.GreaterThan(safe));Assert.That(safe,Is.LessThanOrEqualTo(0));
            Assert.That(caster.Character.Stats.PhysicalDefense.GetValue(),Is.EqualTo(before));Assert.That(caster.Character.PassiveEffects,Is.Empty);
        }

        [Test]
        public void TacticalControlCanDenyHealingEvenWithoutAnOffensiveAttack()
        {
            var session=PlannerSession(1);var caster=session.ActiveUnit;var foe=session.Units[1];foe.Character.CurrentHealth=20;
            var status=CreateEffect(EffectType.ApplyStatus,1);SetField(status,"statusId",StatusEffectIds.Stun);var stun=CreateSkill(status);
            var before=TacticalScore(session,caster,stun,foe);
            var healEffect=CreateEffect(EffectType.Heal,1);SetField(healEffect,"baseValue",50);var heal=CreateSkill(healEffect);
            SetField(heal.TargetModes[0],"targetType",TargetType.Caster);SetField(heal.TargetModes[0],"targetingType",TargetingType.Allies);foe.Character.Skills.Add(heal);
            Assert.That(TacticalScore(session,caster,stun,foe),Is.GreaterThan(before));
        }

        [Test]
        public void TacticalThreatValuationDoesNotReadAnUnobservedOpponent()
        {
            var attack=PlannerAttack("hidden-threat",50,10f);var session=PlannerSession(1);session.Units[1].Character.Skills.Add(attack);
            session.Contact=new DungeonContactState((_,_)=>false);session.Contact.Refresh(session);
            var effect=CreateEffect(EffectType.Shield,1);SetField(effect,"baseValue",100);var shield=CreateSkill(effect);
            Assert.That(TacticalScore(session,session.ActiveUnit,shield,session.ActiveUnit),Is.LessThanOrEqualTo(0));
        }

        [Test]
        public void TacticalComplexDecisionCompletesInCooperativeSlices()
        {
            var attack=PlannerAttack("sliced-attack",25,2f);ConfigureTargetMode(attack,TargetingType.Enemies,5);
            var buff=PlannerBuff("sliced-buff",CharacterStatType.CritChance,.2f,25);
            var session=PlannerSession(3,attack,buff);session.Grid=new BattleGrid(12,8);
            session.ActiveUnit.Character.Stats.CritChance=new Stat(.2f);
            for(var i=0;i<3;i++)
            {
                var foe=CreateAiCombatUnit("slice-foe-"+i,true,new GridPosition(4+i,2+i));foe.Character.Skills.Add(attack);session.Units.Add(foe);
                var ally=CreateAiCombatUnit("slice-ally-"+i,false,new GridPosition(i,3));ally.Character.Skills.Add(attack);session.Units.Add(ally);
            }
            var ai=CreateProductionWraithAi();var timer=System.Diagnostics.Stopwatch.StartNew();double longest=0;var slices=0;
            var previousRandom=UnityEngine.Random.state;
            try
            {
                using(var job=ai.CreateNextActionJob(session,session.ActiveUnit,ai.CreateTurnState()))
                {
                    while(!job.IsComplete)
                    {
                        var before=timer.Elapsed.TotalMilliseconds;job.AdvanceSlice(milliseconds:2,maxSteps:32);
                        longest=Math.Max(longest,timer.Elapsed.TotalMilliseconds-before);
                        Assert.That(++slices,Is.LessThan(2000));
                    }
                    Assert.That(job.IsCanceled,Is.False);Assert.That(job.HasTimedOut,Is.False);Assert.That(job.Executed,Is.True);
                }
                TestContext.WriteLine($"Tactical decision: {slices} slices, total {timer.Elapsed.TotalMilliseconds:0.00} ms, longest {longest:0.00} ms. Editor harness, not gameplay FPS.");
            }
            finally{UnityEngine.Random.state=previousRandom;}
        }

        [Test]
        public void TacticalPublicPreviewRemainsSafeBeforeASelectionExists()
        {
            var attack=PlannerAttack("unselected",50,1);var session=PlannerSession(1,attack);var caster=session.ActiveUnit;var target=session.Units[1];
            Assert.That(CombatSkillPreviewCalculator.CalculateExpectedTargetDamage(session,caster,null,attack,0),Is.Zero);
            Assert.That(CombatSkillPreviewCalculator.CalculateExpectedTargetDamage(session,null,target,attack,0),Is.Zero);
            Assert.That(CombatSkillPreviewCalculator.CalculateExpectedTargetDamage(session,caster,target,null,0),Is.Zero);
            Assert.That(CombatSkillPreviewCalculator.EstimateKillChance(session,caster,null,attack,attack.Effects[0]),Is.Zero);
            Assert.That(CombatSkillPreviewCalculator.EstimateKillChance(session,caster,target,null,attack.Effects[0]),Is.Zero);
            Assert.That(CombatSkillPreviewCalculator.EstimateKillChance(session,caster,target,attack,null),Is.Zero);
        }

    }
}
