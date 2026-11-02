using System;
using System.IO;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Characters;
using Godmist.Data.Skills;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;
namespace Godmist.Tests.EditMode
{
    public partial class SimpleCombatAiSystemTests
    {
        private static void ApplyKillChanceBuff(BattleUnit unit, UniqueBuffType type)
        {
            var effect=new PassiveEffectRuntime { EffectId="test.kill-chance."+type, IsUniqueCombatBuff=true, UniqueBuffType=type, Stacks=1 };
            Assert.That(new PassiveEffectLifecycleSystem().TryApplyCombatScopedEffect(unit.Character,effect,PassiveEffectStackingMode.ReplaceDurationAndValues,out _),Is.True);
            Assert.That(CombatDamageSystem.HasUniqueBuff(unit,type),Is.True);
        }

        private CombatSession IndependentHitFixture(int count, int health, float hitChance, out SkillData skill)
        {
            skill = PlannerAttack("independent-hits", 1, 1f);
            var session = PlannerSession(1, skill); var caster = session.ActiveUnit; var target = session.Units[1];
            caster.Character.WeaponAccuracy = hitChance * 100; SetAutoProperty(skill, "SkillAccuracy", hitChance * 100);
            caster.Character.Stats.MinAttack = new Stat(8); caster.Character.Stats.MaxAttack = new Stat(8);
            caster.Character.Stats.CritChance = new Stat(0); caster.Character.Stats.CritDamage = new Stat(2);
            target.Character.Stats.Dodge = new Stat(0); target.Character.Stats.PhysicalDefense = new Stat(0); target.Character.Stats.MagicalDefense = new Stat(0);
            target.Character.CurrentHealth = health; target.Character.ShieldPool = new ShieldPool();
            SetField(skill.Effects[0], "alwaysHits", false); SetField(skill.Effects[0], "damageHitCount", count);
            Assert.That(CombatDamageSystem.CalculateSkillHitChance(caster, target, skill), Is.EqualTo(hitChance).Within(.00001f));
            return session;
        }

        [TestCase(2,16,.5f,.25f)] [TestCase(2,8,.5f,.75f)]
        [TestCase(1,8,.5f,.5f)] [TestCase(3,24,.5f,.125f)]
        [TestCase(2,16,0f,0f)] [TestCase(2,16,1f,1f)]
        public void IndependentHitKillChanceMatchesExactProbability(int count,int health,float hitChance,float expected)
        {
            var session=IndependentHitFixture(count,health,hitChance,out var skill);
            Assert.That(CombatSkillPreviewCalculator.EstimateKillChance(session,session.ActiveUnit,session.Units[1],skill,skill.Effects[0]),Is.EqualTo(expected).Within(.00001f));
        }

        [TestCase(8,.71875f)] [TestCase(16,.25f)] [TestCase(24,.03125f)]
        public void OptionalHitsRollOccurrenceAndAccuracyIndependently(int health,float expected)
        {
            var session=IndependentHitFixture(1,health,.5f,out var skill);
            SetField(skill.Effects[0],"additionalHitTrials",2);SetField(skill.Effects[0],"additionalHitChance",.5f);
            Assert.That(CombatSkillPreviewCalculator.EstimateKillChance(session,session.ActiveUnit,session.Units[1],skill,skill.Effects[0]),Is.EqualTo(expected).Within(.00001f));
        }

        [TestCase("buff",.75f)] [TestCase("skill",.75f)] [TestCase("independent",.0625f)]
        public void CriticalStateIsPreservedByMisses(string kind,float expected)
        {
            var session=IndependentHitFixture(2,kind=="independent"?31:15,.5f,out var skill);var caster=session.ActiveUnit;
            if(kind=="buff")ApplyKillChanceBuff(caster,UniqueBuffType.GuaranteedCriticalHit);
            else if(kind=="skill")SetAutoProperty(skill,"GuaranteedCritical",true);
            else caster.Character.Stats.CritChance=new Stat(.5f);
            var before=JsonUtility.ToJson(caster.Character);var targetBefore=JsonUtility.ToJson(session.Units[1].Character);var rng=JsonUtility.ToJson(UnityEngine.Random.state);
            Assert.That(CombatSkillPreviewCalculator.EstimateKillChance(session,caster,session.Units[1],skill,skill.Effects[0]),Is.EqualTo(expected).Within(.00001f));
            Assert.That(JsonUtility.ToJson(caster.Character),Is.EqualTo(before));Assert.That(JsonUtility.ToJson(session.Units[1].Character),Is.EqualTo(targetBefore));
            Assert.That(JsonUtility.ToJson(UnityEngine.Random.state),Is.EqualTo(rng));
            if(kind=="buff")Assert.That(CombatDamageSystem.HasUniqueBuff(caster,UniqueBuffType.GuaranteedCriticalHit),Is.True);
        }

        [TestCase("always")] [TestCase("hawkeye")] [TestCase("magic")]
        public void GuaranteedHitSourcesBypassAccuracyForEveryStrike(string kind)
        {
            var session=IndependentHitFixture(2,16,0f,out var skill);
            if(kind=="always")SetField(skill.Effects[0],"alwaysHits",true);
            if(kind=="hawkeye")ApplyKillChanceBuff(session.ActiveUnit,UniqueBuffType.Hawkeye);
            if(kind=="magic")SetField(skill.Effects[0],"damageType",DamageType.Magical);
            Assert.That(CombatSkillPreviewCalculator.EstimateKillChance(session,session.ActiveUnit,session.Units[1],skill,skill.Effects[0]),Is.EqualTo(1));
        }

        [Test]
        public void IndependentHitDistributionMatchesExhaustiveEnumerationAndKeepsShieldBudget()
        {
            var rows=new List<string>{"hits\taccuracy\tneededHits\testimated\texact"};
            foreach(var p in new[]{0f,.25f,.5f,1f})for(var count=1;count<=8;count++)for(var needed=1;needed<=count;needed++)
            {
                var session=IndependentHitFixture(count,needed*8-1,p,out var skill);
                session.Units[1].Character.ShieldPool.AddLayer(1);
                double exact=0;
                for(var mask=0;mask<(1<<count);mask++)
                {
                    var hits=0;double chance=1;
                    for(var i=0;i<count;i++){var hit=(mask&(1<<i))!=0;if(hit)hits++;chance*=hit?p:1-p;}
                    if(hits>=needed)exact+=chance;
                }
                var estimated=CombatSkillPreviewCalculator.EstimateKillChance(session,session.ActiveUnit,session.Units[1],skill,skill.Effects[0]);
                Assert.That(estimated,Is.EqualTo(exact).Within(.00001),$"n={count},p={p},needed={needed}");
                Assert.That(session.Units[1].Character.ShieldPool.TotalShield,Is.EqualTo(1));
                rows.Add($"{count}\t{p}\t{needed}\t{estimated}\t{exact}");
            }
            var folder=Path.GetFullPath("../LocalHandoffs/ai-multihit-kill-chance-20261030");Directory.CreateDirectory(folder);File.WriteAllLines(Path.Combine(folder,"enumeration.tsv"),rows);
        }

        [Test]
        public void AiChoosesTheMoreReliableFinisherAtEqualExpectedDamageAndCost()
        {
            var session=IndependentHitFixture(2,16,.5f,out var twoHits);var caster=session.ActiveUnit;var target=session.Units[1];
            var oneHit=PlannerAttack("single-finisher",1,2f);SetField(oneHit.Effects[0],"alwaysHits",false);SetAutoProperty(oneHit,"SkillAccuracy",50f);
            caster.Character.Skills.Add(oneHit);
            Assert.That(CombatSkillPreviewCalculator.CalculateExpectedTargetDamage(session,caster,target,oneHit,0),
                Is.EqualTo(CombatSkillPreviewCalculator.CalculateExpectedTargetDamage(session,caster,target,twoHits,0)));
            Assert.That(TacticalScore(session,caster,oneHit,target),Is.GreaterThan(TacticalScore(session,caster,twoHits,target)));
            caster.CurrentActionPoints=caster.MaxActionPoints=ActionPointUnits.FromPoints(1);
            var rng=UnityEngine.Random.state;
            try
            {
                var ai=CreateProductionWraithAi();
                Assert.That(ai.TryExecuteNextAction(session,caster,ai.CreateTurnState(),out var action),Is.True);
                Assert.That(action.Skill,Is.SameAs(oneHit));
            }
            finally{UnityEngine.Random.state=rng;}
        }

        [TestCase(false)] [TestCase(true)]
        public void IndependentHitForecastAgreesWithActualSkillExecution(bool guaranteedCritical)
        {
            var session=IndependentHitFixture(2,guaranteedCritical?15:16,.5f,out var skill);var caster=session.ActiveUnit;var target=session.Units[1];
            if(guaranteedCritical)ApplyKillChanceBuff(caster,UniqueBuffType.GuaranteedCriticalHit);
            var expected=CombatSkillPreviewCalculator.EstimateKillChance(session,caster,target,skill,skill.Effects[0]);
            var rng=UnityEngine.Random.state;var kills=0;const int trials=4096;
            try
            {
                UnityEngine.Random.InitState(20261030);
                var execution=new SkillExecutionSystem();
                for(var i=0;i<trials;i++)
                {
                    caster.CurrentActionPoints=10000; target.Character.CurrentHealth=guaranteedCritical?15:16;
                    target.Character.LifeState=CharacterLifeState.Alive;target.DownedTurnsRemaining=0;
                    if(guaranteedCritical){caster.Character.PassiveEffects.RemoveAll(e=>e.IsUniqueCombatBuff && e.UniqueBuffType==UniqueBuffType.GuaranteedCriticalHit);ApplyKillChanceBuff(caster,UniqueBuffType.GuaranteedCriticalHit);}
                    Assert.That(execution.TryExecute(session,caster,skill,target),Is.True);
                    if(target.Character.CurrentHealth==0)kills++;
                }
                TestContext.WriteLine($"{kills}/{trials} = {kills/(float)trials}; forecast={expected}");
                Assert.That(kills/(float)trials,Is.EqualTo(expected).Within(.025f));
            }
            finally{UnityEngine.Random.state=rng;}
        }
    }
}
