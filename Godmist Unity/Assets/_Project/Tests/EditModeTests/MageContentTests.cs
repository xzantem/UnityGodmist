using System;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Core.Constants;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace Godmist.Tests.EditMode
{
    public class MageContentTests
    {
        UnityEngine.Random.State random;
        [SetUp] public void SetUp() { random=UnityEngine.Random.state; UnityEngine.Random.InitState(7); }
        [TearDown] public void TearDown() { UnityEngine.Random.state=random; }
        const string Root="Assets/_Project/ScriptableObjects/";
        static T Load<T>(string p) where T:UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(Root+p);
        static SkillData Skill(string p)=>Load<SkillData>("Skills/SD_Mage"+p+".asset");
        static BattleUnit Unit(bool player=true,int x=1,int y=2)
        {
            var c=new CharacterInstanceFactory().Create(Load<CharacterDefinition>("Characters/CD_Mage.asset"),Load<CharacterClassData>("Classes/CCD_Mage.asset"));
            c.CharacterId=Guid.NewGuid().ToString();c.Stats.MaxHealth=new Stat(1000);c.CurrentHealth=500;
            c.Stats.MinAttack=new Stat(100);c.Stats.MaxAttack=new Stat(100);c.Stats.MaxResource=new Stat(100);
            c.Stats.ResourceRegeneration=new Stat(20);c.Stats.PhysicalDefense=new Stat(0);c.Stats.MagicalDefense=new Stat(0);
            c.Resistances=new ResistanceSet();c.BaseResistances=new ResistanceSet();c.ResourcePool.MaxValue=100;c.ResourcePool.CurrentValue=100;
            return new BattleUnit{Character=c,Position=new GridPosition(x,y),IsPlayerSide=player,CurrentActionPoints=10000,MaxActionPoints=10000};
        }
        static void Rank(BattleUnit u,bool fire,string id,int rank)
        {
            var key=fire?MageCombatSystem.Fire:MageCombatSystem.Warlock;
            var p=u.Character.SkillTreeProgress.Find(v=>v.TreeId==key);
            if(p==null){p=new SkillTreeProgress{TreeId=key};u.Character.SkillTreeProgress.Add(p);}
            p.NodeInvestments.Add(new SkillTreeNodeInvestment{NodeId="node.mage."+(fire?"fire.":"warlock.")+id,Ranks=rank});
        }
        static CombatSession Session(params BattleUnit[] units)
        { var s=new CombatSession{Grid=new BattleGrid(14,12),Units=units.ToList(),ActiveUnit=units[0]};foreach(var u in units)u.WarriorState.Session=s;return s; }
        [Test] public void ProductionContentHasFiveStartersFortyNodesAndValidSkills()
        {
            var u=Unit();Assert.That(u.Character.Skills.Count,Is.EqualTo(5));Assert.That(u.Character.GetBasicAttackSkill(),Is.EqualTo(Skill("EnergyOrb")));
            foreach(var tree in u.Character.ClassData.SkillTrees)
            {
                Assert.That(tree.Nodes.Count,Is.EqualTo(20));
                foreach(var n in tree.Nodes){Assert.That(n.Tier,Is.InRange(1,4));foreach(var r in n.Requirements)Assert.That(tree.Nodes.Any(v=>v.Id==r.NodeId),Is.True,n.Id);}
            }
            foreach(var skill in u.Character.Skills.Concat(u.Character.ClassData.SkillTrees.SelectMany(t=>t.Nodes).Select(n=>n.GrantedSkill)).Where(s=>s!=null))
                Assert.That(CombatDataValidator.ValidateSkill(skill).Errors,Is.Empty,skill.SkillId);
        }
        [Test] public void RitualRestoresThirtyPercentInTotalOverFiveTurns()
        {
            var mage=Unit();var ally=Unit(true,2);var session=Session(mage,ally);var statuses=new StatusEffectSystem();
            var execution=new SkillExecutionSystem(statusEffectSystem:statuses);
            Assert.That(execution.TryExecute(session,mage,Skill("Ritual"),new[]{mage,ally}),Is.True);
            Assert.That(ally.Character.CurrentHealth,Is.EqualTo(400));
            for(var i=0;i<5;i++)statuses.ProcessTurnEnd(ally);
            Assert.That(ally.Character.CurrentHealth,Is.EqualTo(700));
            statuses.ProcessTurnEnd(ally);Assert.That(ally.Character.CurrentHealth,Is.EqualTo(700));
        }
        [Test] public void OfferingToggleRemovesItsBonusWhenDisabled()
        {
            var mage=Unit();var session=Session(mage);var execution=new SkillExecutionSystem(statusEffectSystem:new StatusEffectSystem());
            Assert.That(execution.TryExecute(session,mage,Skill("Offering"),mage),Is.True);
            Assert.That(CombatDamageSystem.HasUniqueBuff(mage,UniqueBuffType.MageOffering),Is.True);
            Assert.That(execution.TryExecute(session,mage,Skill("Offering"),mage),Is.True);
            Assert.That(CombatDamageSystem.HasUniqueBuff(mage,UniqueBuffType.MageOffering),Is.False);
        }
        [Test] public void MagicFocusPersistsAcrossAttacksUntilItsDurationExpires()
        {
            var caster=Unit();var enemy=Unit(false,2);enemy.Character.CurrentHealth=1000;var session=Session(caster,enemy);
            var execution=new SkillExecutionSystem(statusEffectSystem:new StatusEffectSystem());Assert.That(execution.TryExecute(session,caster,Skill("MagicFocus"),caster),Is.True);
            caster.CurrentActionPoints=caster.MaxActionPoints;Assert.That(execution.TryExecute(session,caster,Skill("EnergyOrb"),enemy),Is.True);
            caster.CurrentActionPoints=caster.MaxActionPoints;Assert.That(execution.TryExecute(session,caster,Skill("EnergyOrb"),enemy),Is.True);
            Assert.That(CombatDamageSystem.HasUniqueBuff(caster,UniqueBuffType.MageMaximumDamage),Is.True);
            var statuses=new StatusEffectSystem();for(var i=0;i<3;i++)statuses.ProcessTurnEnd(caster);
            Assert.That(CombatDamageSystem.HasUniqueBuff(caster,UniqueBuffType.MageMaximumDamage),Is.False);
        }
        [Test] public void DispellingBurnPaysFeedingOnceAndRemovesAllCopies()
        {
            var mage=Unit();var enemy=Unit(false,2);Rank(mage,true,"fire_feeding",2);var session=Session(mage,enemy);
            var statuses=new StatusEffectSystem();statuses.ApplyStatus(mage,enemy,StatusEffectIds.Burn,1,3,1,1,0,"burn-a");
            statuses.ApplyStatus(mage,enemy,StatusEffectIds.Burn,1,3,1,1,0,"burn-b");
            var burns=enemy.Character.PassiveEffects.ToArray();foreach(var burn in burns)MageCombatSystem.AfterBurnTick(session,burn,50,false);
            Assert.That(statuses.RemoveStatus(enemy,StatusEffectIds.Burn),Is.True);Assert.That(mage.Character.CurrentHealth,Is.EqualTo(520));
            foreach(var burn in burns)MageCombatSystem.AfterBurnTick(session,burn,0,true);
            Assert.That(mage.Character.CurrentHealth,Is.EqualTo(520));Assert.That(enemy.Character.PassiveEffects,Is.Empty);
        }
        [Test] public void SacrificeUsesChosenAlliesAndRecastReplacesPreviousDonors()
        {
            var caster=Unit();var first=Unit(true,2);var second=Unit(true,3);var other=Unit(true,4);
            Rank(caster,false,"mass_sacrifice",1);var session=Session(caster,first,second,other);
            var execution=new SkillExecutionSystem(statusEffectSystem:new StatusEffectSystem());var skill=Skill("Sacrifice");
            Assert.That(PassiveEffectCombatExecutionSystem.ResolveMode(caster,skill,skill.TargetModes[0]).ChainTargets,Is.EqualTo(2));
            Assert.That(execution.TryExecute(session,caster,skill,new[]{first,second}),Is.True);
            Assert.That(caster.Character.PassiveEffects.Count(e=>e.EffectId.StartsWith("mage.sacrifice.gain.")),Is.EqualTo(2));
            Assert.That(other.Character.PassiveEffects.Any(e=>e.EffectId.StartsWith("mage.sacrifice.loss.")),Is.False);
            caster.Character.ResourcePool.CurrentValue=100;
            Assert.That(execution.TryExecute(session,caster,skill,new[]{other}),Is.True);
            Assert.That(caster.Character.PassiveEffects.Count(e=>e.EffectId.StartsWith("mage.sacrifice.gain.")),Is.EqualTo(1));
            Assert.That(first.Character.PassiveEffects.Any(e=>e.EffectId.StartsWith("mage.sacrifice.loss.")),Is.False);
        }
        [Test] public void SacrificeRejectsDuplicateExcessEnemyAndDistantTargetsWithoutSpending()
        {
            var caster=Unit();var ally=Unit(true,2);var extra=Unit(true,3);var enemy=Unit(false,4);var far=Unit(true,13);
            var session=Session(caster,ally,extra,enemy,far);var execution=new SkillExecutionSystem(statusEffectSystem:new StatusEffectSystem());var skill=Skill("Sacrifice");
            foreach(var chosen in new[]{new[]{ally,ally},new[]{ally,extra},new[]{enemy},new[]{far},new[]{caster}})
                Assert.That(execution.TryExecute(session,caster,skill,chosen),Is.False);
            Assert.That(caster.Character.ResourcePool.CurrentValue,Is.EqualTo(100));Assert.That(caster.CurrentActionPoints,Is.EqualTo(10000));
        }
        [Test] public void MasteryMakesBasicAttackFree_AndLowHealthSavingStacks()
        {
            var u=Unit();Rank(u,false,"mastery",1);Rank(u,false,"magic_saving",1);u.Character.CurrentHealth=100;
            Assert.That(PassiveEffectCombatExecutionSystem.ResolveResourceCost(u,Skill("EnergyOrb")),Is.Zero);
            Assert.That(PassiveEffectCombatExecutionSystem.ResolveResourceCost(u,Skill("Fireball")),Is.EqualTo(Mathf.CeilToInt(35*.99f*.8f)));
        }
        [Test] public void StatsAndMemoryRefreshWithoutStacking()
        {
            var u=Unit();Rank(u,false,"defensive_power",3);Rank(u,false,"memory",2);Rank(u,false,"energy_saving",2);u.Character.ResourcePool.CurrentValue=50;
            var pipeline=new CharacterStatPipelineSystem();pipeline.Refresh(u.Character);pipeline.Refresh(u.Character);
            Assert.That(u.Character.Stats.MaxHealth.GetValue(),Is.EqualTo(1050));Assert.That(u.Character.ResourcePool.Regeneration,Is.EqualTo(25));Assert.That(u.Character.ActiveSkillLoadout.Capacity,Is.EqualTo(7));
        }
        [Test] public void ExplorationRegenerationRecalculatesMissingManaEveryTick()
        {
            var unit=Unit();Rank(unit,false,"energy_saving",2);unit.Character.ResourcePool.CurrentValue=0;
            var rules=new ResourceRuleSystem();rules.ProcessExplorationTick(unit);
            Assert.That(unit.Character.ResourcePool.CurrentValue,Is.EqualTo(30));
            rules.ProcessExplorationTick(unit);
            Assert.That(unit.Character.ResourcePool.CurrentValue,Is.EqualTo(57));
        }
        [Test] public void ShieldUsesAverageAttack_AndFocusPaysActualHealth()
        {
            var u=Unit();var ally=Unit(true,2);var s=Session(u,ally);var exec=new SkillExecutionSystem(statusEffectSystem:new StatusEffectSystem());
            Assert.That(exec.TryExecute(s,u,Skill("Shield"),ally),Is.True);
            Assert.That(ally.Character.ShieldPool.TotalShield,Is.EqualTo(250));
            u.Character.ResourcePool.CurrentValue=0;u.Character.CurrentHealth=51;
            Assert.That(exec.TryExecute(s,u,Skill("Focus"),u),Is.True);
            Assert.That(u.Character.CurrentHealth,Is.EqualTo(1));Assert.That(u.Character.ResourcePool.CurrentValue,Is.EqualTo(100));
        }
        [Test] public void BurnCopiesRetainHalfStrengthAndNeverExceedThirdGeneration()
        {
            var u=Unit();Rank(u,true,"aggressive_fire",1);var target=Unit(false,4);var neighbor=Unit(false,5);var s=Session(u,target,neighbor);
            var statuses=new StatusEffectSystem();statuses.ApplyStatus(u,target,StatusEffectIds.Burn,1,3,1,1,0,"test");
            var burn=target.Character.PassiveEffects.Single(e=>e.EffectId==StatusEffectIds.Burn);
            for(var seed=0;seed<50 && !neighbor.Character.PassiveEffects.Any();seed++)
            { UnityEngine.Random.InitState(seed);burn.RemoveStateValue("mage.burn.ticked");MageCombatSystem.BeforeBurnTick(s,target,burn,100); }
            var child=neighbor.Character.PassiveEffects.Single(e=>e.EffectId==StatusEffectIds.Burn);
            Assert.That(child.GetStateValue("mage.burn.generation").IntegerValue,Is.EqualTo(1));
            Assert.That(child.GetPayload<DamageOverTimePayload>().RemainingTotalDamage,Is.EqualTo(Mathf.FloorToInt(burn.GetPayload<DamageOverTimePayload>().RemainingTotalDamage*.5f)));
            var count=neighbor.Character.PassiveEffects.Count;MageCombatSystem.BeforeBurnTick(s,target,burn,100);Assert.That(neighbor.Character.PassiveEffects.Count,Is.EqualTo(count));
            child.SetStateValue(PassiveEffectRuntimeStateValue.Integer("mage.burn.generation",3));
            for(var i=0;i<20;i++){child.RemoveStateValue("mage.burn.ticked");MageCombatSystem.BeforeBurnTick(s,neighbor,child,50);}
            Assert.That(target.Character.PassiveEffects.Count,Is.EqualTo(1));
        }
        [Test] public void FreshBurnDoesNotResetOlderCopyGeneration()
        {
            var u=Unit();var enemy=Unit(false,2);var statuses=new StatusEffectSystem();
            statuses.ApplyStatus(u,enemy,StatusEffectIds.Burn,1,3,1,1,0,"one");
            var original=enemy.Character.PassiveEffects.Single();original.SetStateValue(PassiveEffectRuntimeStateValue.Integer("mage.burn.generation",3));
            statuses.ApplyStatus(u,enemy,StatusEffectIds.Burn,1,3,1,1,0,"two");
            Assert.That(enemy.Character.PassiveEffects.Count,Is.EqualTo(2));Assert.That(original.GetStateValue("mage.burn.generation").IntegerValue,Is.EqualTo(3));
        }
        [Test] public void FireFeedingHealsOnlyAfterFinalTickAndUsesActualDamage()
        {
            var u=Unit();Rank(u,true,"fire_feeding",2);var enemy=Unit(false,2);var s=Session(u,enemy);
            var burn=new PassiveEffectRuntime(StatusEffectIds.Burn,new(),1,3,300,PassiveEffectLifetime.Timed,PassiveEffectTickPhase.TurnStart);
            MageCombatSystem.AfterBurnTick(s,burn,40,false);Assert.That(u.Character.CurrentHealth,Is.EqualTo(500));
            MageCombatSystem.AfterBurnTick(s,burn,60,true);Assert.That(u.Character.CurrentHealth,Is.EqualTo(520));
        }
        [Test] public void OverflowDischargeConsumesExcessOnceAndAllowsMagicCrit()
        {
            var u=Unit();Rank(u,false,"energy_discharge",3);u.Character.ResourcePool.CurrentValue=130;
            MageCombatSystem.BeginSkill(u,Skill("EnergyOrb"),"action");
            Assert.That(u.Character.ResourcePool.CurrentValue,Is.EqualTo(100));
            Assert.That(MageCombatSystem.MagicCritChance(new PassiveEffectCombatContext(source:u,actionId:"action")),Is.EqualTo(.3f).Within(.0001));
            Assert.That(MageCombatSystem.MagicCritChance(new PassiveEffectCombatContext(source:u,actionId:"other")),Is.Zero);
        }
    }
}
