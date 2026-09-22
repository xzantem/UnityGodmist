using System;
using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace Godmist.Tests.EditMode
{
    public class WarriorContentTests
    {
        const string Root="Assets/_Project/ScriptableObjects/";
        static T Load<T>(string path) where T:UnityEngine.Object=>AssetDatabase.LoadAssetAtPath<T>(Root+path);
        static SkillData Skill(string name)=>Load<SkillData>("Skills/SD_Warrior"+name+".asset");
        static BattleUnit Unit(bool player=true,int x=1)
        {
            var c=new CharacterInstanceFactory().Create(Load<CharacterDefinition>("Characters/CD_Wladimir_Test.asset"),Load<CharacterClassData>("Classes/CCD_Warrior.asset"),1);
            c.CharacterId=Guid.NewGuid().ToString();c.Stats.MaxHealth=new Stat(1000);c.CurrentHealth=500;
            c.Stats.MinAttack=new Stat(100);c.Stats.MaxAttack=new Stat(100);c.Stats.MaxResource=new Stat(50);
            c.Stats.PhysicalDefense=new Stat(0);c.Stats.MagicalDefense=new Stat(0);c.Stats.Dodge=new Stat(0);c.Stats.CritChance=new Stat(0);c.Stats.CritDamage=new Stat(2);
            c.Resistances=new ResistanceSet();c.BaseResistances=new ResistanceSet();c.WeaponAccuracy=10000;c.ResourcePool.MaxValue=50;c.ResourcePool.CurrentValue=50;
            return new BattleUnit{Character=c,Position=new GridPosition(x,2),IsPlayerSide=player,CurrentActionPoints=10000,MaxActionPoints=10000};
        }
        static void Rank(BattleUnit u,bool commander,string id,int rank)
        {
            var tree=commander?WarriorCombatSystem.Commander:WarriorCombatSystem.Brawler;
            var p=u.Character.SkillTreeProgress.Find(x=>x.TreeId==tree);if(p==null){p=new SkillTreeProgress{TreeId=tree};u.Character.SkillTreeProgress.Add(p);}
            p.NodeInvestments.Add(new SkillTreeNodeInvestment{NodeId="node.warrior."+(commander?"commander.":"brawler.")+id,Ranks=rank});
        }
        static CombatSession Session(params BattleUnit[] units){var s=new CombatSession{Grid=new BattleGrid(10,8),Units=units.ToList(),ActiveUnit=units[0]};foreach(var u in units)u.WarriorState.Session=s;return s;}
        [Test] public void ProductionContentHasFiveStartersFortyNodesAndResolvableRequirements()
        {
            var u=Unit();Assert.That(u.Character.Skills.Count,Is.EqualTo(5));Assert.That(u.Character.GetBasicAttackSkill(),Is.EqualTo(Skill("Cut")));
            Assert.That(u.Character.ClassData.SkillTrees.Count,Is.EqualTo(2));
            foreach(var t in u.Character.ClassData.SkillTrees){Assert.That(t.Nodes.Count,Is.EqualTo(20));foreach(var n in t.Nodes){Assert.That(n.Tier,Is.InRange(1,4));foreach(var r in n.Requirements)Assert.That(t.Nodes.Any(v=>v.Id==r.NodeId),Is.True,n.Id);}}
            foreach(var skill in u.Character.Skills)Assert.That(CombatDataValidator.ValidateSkill(skill).Errors,Is.Empty,skill.name);
        }
        [Test] public void OffensiveCostReductionLeavesSupportCostUnchanged()
        {
            var u=Unit();Rank(u,false,"furious_strikes",3);
            Assert.That(PassiveEffectCombatExecutionSystem.ResolveResourceCost(u,Skill("BleedingDestruction")),Is.EqualTo(28));
            Assert.That(PassiveEffectCombatExecutionSystem.ResolveResourceCost(u,Skill("Bandage")),Is.EqualTo(40));
        }
        [Test] public void OverspendRequiresFullRageAndCostStrictlyAboveMaximum()
        {
            var u=Unit();Assert.That(WarriorCombatSystem.CanOverspend(u,Skill("RightHook"),60),Is.True);
            Assert.That(WarriorCombatSystem.CanOverspend(u,Skill("RightHook"),50),Is.False);u.Character.ResourcePool.CurrentValue=49;
            Assert.That(WarriorCombatSystem.CanOverspend(u,Skill("RightHook"),60),Is.False);
        }
        [TestCase(false,.8f)][TestCase(true,.9f)] public void ShortagePenaltyUsesMissingPointsAndExpires(bool reduced,float expected)
        {
            var u=Unit();if(reduced)Rank(u,false,"rage",1);WarriorCombatSystem.Paid(u,Skill("RightHook"),60,50,new CombatRecoverySystem());
            Assert.That(new CombatVariableSystem().GetMultiplier(u.Character,GameplayVariableType.DamageDealtPercent),Is.EqualTo(expected).Within(.0001));
            var statuses=new StatusEffectSystem();for(var i=0;i<3;i++)statuses.ProcessTurnEnd(u);
            Assert.That(new CombatVariableSystem().GetMultiplier(u.Character,GameplayVariableType.DamageDealtPercent),Is.EqualTo(1));
        }
        [Test] public void ThirstForRevengeUsesActualLossAndAccumulatesFractions()
        {
            var u=Unit();Rank(u,false,"thirst_revenge",3);u.Character.ResourcePool.CurrentValue=0;u.Character.CurrentHealth=40;var foe=Unit(false,2);var s=Session(u,foe);
            new CombatDamageSystem().ApplyDamage(s,foe,u,1000,DamageType.Physical);
            Assert.That(u.Character.ResourcePool.CurrentValue,Is.EqualTo(2));
        }
        [Test] public void DeathWishPreservesOneHpUntilDeactivated()
        {
            var u=Unit();u.ActiveToggleSkillIds.Add(WarriorCombatSystem.DeathWish);var foe=Unit(false,2);var s=Session(u,foe);var damage=new CombatDamageSystem();
            damage.ApplyDamage(s,foe,u,2000,DamageType.Physical);Assert.That(u.Character.CurrentHealth,Is.EqualTo(1));Assert.That(u.Character.LifeState,Is.EqualTo(CharacterLifeState.Alive));
            u.ActiveToggleSkillIds.Clear();damage.ApplyDamage(s,foe,u,1,DamageType.Physical);Assert.That(u.Character.LifeState,Is.EqualTo(CharacterLifeState.Downed));
        }
        [Test] public void ProtectionAndRiposteTrackNeighboursAndCap()
        {
            var u=Unit();var a=Unit(true,2);var b=Unit(true,1);b.Position=new GridPosition(1,1);var c=Unit(true,1);c.Position=new GridPosition(1,3);var s=Session(u,a,b,c);
            u.WarriorState.ProtectionTurns=3;u.WarriorState.Riposte=true;
            Assert.That(WarriorCombatSystem.Protection(s,u),Is.EqualTo(.4f).Within(.0001));Assert.That(WarriorCombatSystem.TargetPriority(s,u),Is.EqualTo(2.25f));
            a.Position=new GridPosition(8,7);Assert.That(WarriorCombatSystem.Protection(s,u),Is.EqualTo(.5f).Within(.0001));
        }
        [Test] public void StrategyFreeStepsHaveMatchingPathAndExecutionPrices()
        {
            var u=Unit();u.WarriorState.FreeMovementTurns=3;
            Assert.That(CombatActionCostCalculator.CalculateMovementCost(u),Is.Zero);Assert.That(CombatMovementSystem.CalculatePathCost(u,3),Is.EqualTo(3*ActionPointUnits.PerPoint));
            WarriorCombatSystem.Moved(u);u.MovementStepsThisTurn++;WarriorCombatSystem.Moved(u);u.MovementStepsThisTurn++;
            Assert.That(CombatActionCostCalculator.CalculateMovementCost(u),Is.EqualTo(3*ActionPointUnits.PerPoint));
            WarriorCombatSystem.TurnStart(u);Assert.That(WarriorCombatSystem.FreeSteps(u),Is.EqualTo(2));
        }
        [TestCase(49,1)][TestCase(50,3)] public void ShoutUsesActualSpentRage(int spent,int turns)
        {
            var u=Unit();u.LastSkillResourceSpent=spent;u.Character.ResourcePool.CurrentValue=0;var skill=Skill("Shout");
            Assert.That(PassiveEffectCombatExecutionSystem.ResolveEffect(u,skill,skill.Effects[0]).DurationTurns,Is.EqualTo(turns));
        }
        [Test] public void ThirdCutBonusResetsAfterAnotherSkillOrTarget()
        {
            var u=Unit();Rank(u,false,"third_time",2);var foe=Unit(false,2);var ctx=new Godmist.Runtime.Combat.PassiveEffectCombatContext(source:u,target:foe,skillId:WarriorCombatSystem.Cut);
            WarriorCombatSystem.BeginSkill(u,Skill("Cut"),new[]{foe});WarriorCombatSystem.BeginSkill(u,Skill("Cut"),new[]{foe});
            Assert.That(WarriorCombatSystem.DamageMultiplier(ctx,true),Is.EqualTo(1.25f));WarriorCombatSystem.BeginSkill(u,Skill("Kick"),new[]{foe});
            Assert.That(WarriorCombatSystem.DamageMultiplier(ctx,true),Is.EqualTo(1));
        }

        [TestCase("Cut")][TestCase("Kick")][TestCase("RightHook")][TestCase("Immobilize")][TestCase("BleedingDestruction")][TestCase("Shout")]
        public void OffensiveAbilitiesExecuteAgainstAdjacentEnemy(string file)
        {
            var u=Unit();var enemy=Unit(false,2);var session=Session(u,enemy);var execution=new SkillExecutionSystem(statusEffectSystem:new StatusEffectSystem());
            Assert.That(execution.TryExecute(session,u,Skill(file),enemy),Is.True,file);
        }
        [Test] public void RegroupZerosEvenActionValueAboveItsNormalMaximum()
        {
            var u=Unit();var ally=Unit(true,2);ally.CurrentActionValue=99999;var s=Session(u,ally);
            Assert.That(new SkillExecutionSystem().TryExecute(s,u,Skill("Regroup"),ally),Is.True);Assert.That(ally.CurrentActionValue,Is.Zero);
        }
        [Test] public void BandageHealsMissingHpAndRemovesBleed()
        {
            var u=Unit();var ally=Unit(true,2);var s=Session(u,ally);var status=new StatusEffectSystem();status.ApplyStatus(u,ally,StatusEffectIds.Bleed,1,3,1,1,0,"test");
            Assert.That(new SkillExecutionSystem(statusEffectSystem:status).TryExecute(s,u,Skill("Bandage"),ally),Is.True);
            Assert.That(ally.Character.CurrentHealth,Is.EqualTo(540));Assert.That(StatusEffectSystem.HasStatus(ally,StatusEffectIds.Bleed),Is.False);
        }
        [Test] public void DeathWishUpkeepAndDeactivationRemoveBothDamageModifiers()
        {
            var u=Unit();var s=Session(u);Rank(u,false,"death_wish",1);var skill=Skill("DeathWish");u.Character.Skills.Add(skill);u.Character.ActiveSkillLoadout.TryRemoveSkill(u.Character.Skills[0].SkillId);u.Character.ActiveSkillLoadout.TryAddSkill(skill.SkillId,u.Character.GetKnownSkills().Select(x=>x.SkillId));
            var execution=new SkillExecutionSystem();Assert.That(execution.TryExecute(s,u,skill,u),Is.True);Assert.That(WarriorCombatSystem.DeathProtected(u),Is.True);
            Assert.That(new CombatVariableSystem().GetMultiplier(u.Character,GameplayVariableType.DamageDealtPercent),Is.EqualTo(1.5f));
            u.Character.ResourcePool.CurrentValue=0;execution.ProcessToggleUpkeep(s,u);
            Assert.That(WarriorCombatSystem.DeathProtected(u),Is.False);Assert.That(new CombatVariableSystem().GetMultiplier(u.Character,GameplayVariableType.DamageDealtPercent),Is.EqualTo(1));
        }
        [Test] public void ActualExecutionAllowsFullRageOverspendAndAppliesPenalty()
        {
            var u=Unit();u.Character.Stats.MaxResource=new Stat(20);u.Character.ResourcePool.MaxValue=20;u.Character.ResourcePool.CurrentValue=20;var enemy=Unit(false,2);var s=Session(u,enemy);
            Assert.That(new SkillExecutionSystem(statusEffectSystem:new StatusEffectSystem()).TryExecute(s,u,Skill("RightHook"),enemy),Is.True);
            Assert.That(new CombatVariableSystem().GetMultiplier(u.Character,GameplayVariableType.DamageDealtPercent),Is.EqualTo(.7f).Within(.0001));
            Assert.That(u.LastSkillResourceSpent,Is.EqualTo(20));
        }
        [Test] public void DeathWishCannotBeActivatedWithoutUpkeepResource()
        {
            var u=Unit();u.Character.ResourcePool.CurrentValue=0;var s=Session(u);
            Assert.That(new SkillExecutionSystem().TryExecute(s,u,Skill("DeathWish"),u),Is.False);Assert.That(WarriorCombatSystem.DeathProtected(u),Is.False);
        }
        [Test] public void AdrenalineUpdatesAfterExternalResourceDrain()
        {
            var u=Unit();Rank(u,false,"adrenaline",1);var s=Session(u);CombatTurnSystem.RefreshMaxActionPoints(s);var speed=u.Character.Stats.Speed.GetValue();
            u.Character.ResourcePool.Drain(30);CombatTurnSystem.RefreshMaxActionPoints(s);Assert.That(u.Character.Stats.Speed.GetValue(),Is.EqualTo(speed-8));
        }
        [Test] public void AmokRefreshesWithoutStackingAndExpires()
        {
            var u=Unit();Rank(u,false,"amok",3);var enemy=Unit(false,2);var s=Session(u,enemy);var ctx=new PassiveEffectCombatContext(source:u,target:enemy,skillId:WarriorCombatSystem.Cut);
            WarriorCombatSystem.AfterHit(s,ctx,true,100,100,new CombatDamageSystem());var max=u.Character.ResourcePool.MaxValue;
            WarriorCombatSystem.AfterHit(s,ctx,true,100,100,new CombatDamageSystem());Assert.That(u.Character.ResourcePool.MaxValue,Is.EqualTo(max));
            var statuses=new StatusEffectSystem();for(var i=0;i<5;i++)statuses.ProcessTurnEnd(u);Assert.That(u.Character.ResourcePool.MaxValue,Is.LessThan(max));
        }
        [Test] public void FractionalCommanderDamageDoesNotRoundEveryHitUpToOne()
        {
            var u=Unit();Rank(u,true,"weapon_mastery",1);var enemy=Unit(false,2);var s=Session(u,enemy);u.Character.Equipment.Weapon ??= new Godmist.Runtime.Items.EquipmentItemInstance();u.Character.Equipment.Weapon.UpgradeProgress=1;
            var ctx=new PassiveEffectCombatContext(source:u,target:enemy,skillId:WarriorCombatSystem.Cut);var hp=enemy.Character.CurrentHealth;
            WarriorCombatSystem.AfterHit(s,ctx,false,100,100,new CombatDamageSystem());Assert.That(enemy.Character.CurrentHealth,Is.EqualTo(hp));Assert.That(u.WarriorState.TrueDamageRemainder,Is.EqualTo(.2f).Within(.0001));
        }
        [Test] public void TreeGrantedSkillIsDerivedAndRemovedWithInvestment()
        {
            var u=Unit();Rank(u,true,"destruction",1);Assert.That(u.Character.GetKnownSkills(),Has.Member(Skill("Destruction")));u.Character.SkillTreeProgress.Clear();Assert.That(u.Character.GetKnownSkills(),Has.No.Member(Skill("Destruction")));
        }
    }
}
