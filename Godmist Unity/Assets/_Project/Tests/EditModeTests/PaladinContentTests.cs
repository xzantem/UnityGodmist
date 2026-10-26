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
    public class PaladinContentTests
    {
        UnityEngine.Random.State random;
        [SetUp] public void SetUp() { random=UnityEngine.Random.state; UnityEngine.Random.InitState(7); }
        [TearDown] public void TearDown() { UnityEngine.Random.state=random; }
        const string Root="Assets/_Project/ScriptableObjects/";
        static T Load<T>(string p) where T:UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(Root+p);
        static SkillData Skill(string p)=>Load<SkillData>("Skills/SD_Paladin"+p+".asset");
        static BattleUnit Unit(bool player=true,int x=1,int y=2)
        {
            var c=new CharacterInstanceFactory().Create(Load<CharacterDefinition>("Characters/CD_Paladin.asset"),Load<CharacterClassData>("Classes/CCD_Paladin.asset"));
            c.CharacterId=Guid.NewGuid().ToString();c.Stats.MaxHealth=new Stat(1000);c.CurrentHealth=500;
            c.Stats.MinAttack=new Stat(100);c.Stats.MaxAttack=new Stat(100);c.Stats.MaxResource=new Stat(100);
            c.Stats.ResourceRegeneration=new Stat(20);c.Stats.PhysicalDefense=new Stat(0);c.Stats.MagicalDefense=new Stat(0);
            c.Resistances=new ResistanceSet();c.BaseResistances=new ResistanceSet();c.ResourcePool.MaxValue=100;c.ResourcePool.CurrentValue=100;
            return new BattleUnit{Character=c,Position=new GridPosition(x,y),IsPlayerSide=player,CurrentActionPoints=10000,MaxActionPoints=10000};
        }
        static void Rank(BattleUnit u,bool crusader,string node,int ranks)
        {
            var tree=crusader?PaladinCombatSystem.Crusader:PaladinCombatSystem.Priest;
            var progress=u.Character.SkillTreeProgress.Find(p=>p.TreeId==tree);if(progress==null){progress=new SkillTreeProgress{TreeId=tree};u.Character.SkillTreeProgress.Add(progress);}
            progress.NodeInvestments.Add(new SkillTreeNodeInvestment{NodeId="node.paladin."+(crusader?"crusader.":"priest.")+node,Ranks=ranks});
        }
        static CombatSession Session(params BattleUnit[] units)
        {var s=new CombatSession{Grid=new BattleGrid(14,12),Units=units.ToList(),ActiveUnit=units[0]};foreach(var u in units)u.WarriorState.Session=s;return s;}
        static SkillExecutionSystem Execution()=>new(statusEffectSystem:new StatusEffectSystem());
        [TestCase(0, 100)]
        [TestCase(1, 75)]
        [TestCase(2, 50)]
        [TestCase(3, 0)]
        public void HealPreviewPreservesDiscountForEveryPassiveRank(int rank, int expected)
        {
            var caster = Unit(); var ally = Unit(true, 2); var session = Session(caster, ally);
            Rank(caster, false, "persistent_healer", rank);
            ally.PaladinState.HealingDiscount = true;
            Assert.That(PaladinCombatSystem.PreviewHealCost(session, caster, Skill("Heal"), ally, 100), Is.EqualTo(expected));
            Assert.That(PaladinCombatSystem.PreviewHealCost(session, caster, Skill("Heal"), null, 100), Is.EqualTo(expected));
        }
        [Test] public void ProductionContentHasFiveStartersAndFortyNodes()
        {
            var u=Unit();Assert.That(u.Character.Skills.Count,Is.EqualTo(5));Assert.That(u.Character.GetBasicAttackSkill(),Is.EqualTo(Skill("Verdict")));
            Assert.That(u.Character.ClassData.SkillTrees.SelectMany(t=>t.Nodes).Count(),Is.EqualTo(40));
            foreach(var skill in u.Character.Skills.Concat(u.Character.ClassData.SkillTrees.SelectMany(t=>t.Nodes).Select(n=>n.GrantedSkill)).Where(s=>s!=null))Assert.That(CombatDataValidator.ValidateSkill(skill).Errors,Is.Empty,skill.SkillId);
        }
        [Test] public void HealUsesAverageAttackAndConsumesTargetDiscount()
        {
            var u=Unit();var ally=Unit(true,2);Session(u,ally);Rank(u,false,"persistent_healer",3);PaladinCombatSystem.RecordDamage(ally,201);u.CurrentActionPoints=0;
            Assert.That(Execution().TryExecute(u.WarriorState.Session,u,Skill("Heal"),ally),Is.True);
            Assert.That(ally.Character.CurrentHealth,Is.EqualTo(624));Assert.That(ally.PaladinState.HealingDiscount,Is.False);Assert.That(u.CurrentActionPoints,Is.Zero);
            Assert.That(Execution().TryExecute(u.WarriorState.Session,u,Skill("Heal"),ally),Is.False);
        }
        [Test] public void HastePreservesTotalAndLastingEffectAddsOnlyItsTail()
        {
            var u=Unit();var ally=Unit(true,2);var s=Session(u,ally);Rank(u,false,"haste",3);Rank(u,false,"lasting_effect",2);
            Assert.That(Execution().TryExecute(s,u,Skill("HolyRegeneration"),ally),Is.True);Assert.That(ally.Character.CurrentHealth,Is.EqualTo(660));
            var statuses=new StatusEffectSystem();for(var i=0;i<4;i++)statuses.ProcessTurnEnd(ally);Assert.That(ally.Character.CurrentHealth,Is.EqualTo(900));
            for(var i=0;i<2;i++)statuses.ProcessTurnEnd(ally);Assert.That(ally.Character.CurrentHealth,Is.EqualTo(1000));Assert.That(ally.Character.PassiveEffects.Any(e=>e.EffectId=="paladin.holy_regeneration"),Is.False);
        }
        [Test] public void BlessingCapsGraceAndReturnsResourceOnlyAtExpiry()
        {
            var u=Unit();var ally=Unit(true,2);var s=Session(u,ally);Rank(u,false,"grace_collection",2);Rank(u,false,"salvation",1);
            Assert.That(Execution().TryExecute(s,u,Skill("Blessing"),ally),Is.True);u.Character.ResourcePool.CurrentValue=0;ally.Character.ResourcePool.CurrentValue=0;
            for(var i=0;i<10;i++)PaladinCombatSystem.RecordDamage(ally,1);
            var statuses=new StatusEffectSystem();for(var i=0;i<3;i++)statuses.ProcessTurnEnd(ally);Assert.That(ally.Character.ResourcePool.CurrentValue,Is.Zero);
            statuses.ProcessTurnEnd(ally);Assert.That(ally.Character.ResourcePool.CurrentValue,Is.EqualTo(48));Assert.That(u.Character.ResourcePool.CurrentValue,Is.EqualTo(24));
        }
        [Test] public void HealingChainReachesAdjacentAlliesButNotEnemies()
        {
            var u=Unit();var near=Unit(true,2);var far=Unit(true,3);var enemy=Unit(false,2,3);var s=Session(u,near,far,enemy);Rank(u,false,"healing_aura",2);Rank(u,false,"healing_chain",1);
            PaladinCombatSystem.TurnStart(s,u,new CombatRecoverySystem());Assert.That(near.Character.CurrentHealth,Is.EqualTo(515));Assert.That(far.Character.CurrentHealth,Is.EqualTo(515));Assert.That(enemy.Character.CurrentHealth,Is.EqualTo(500));
        }
        [Test] public void TormentCapsAndUsesFlatDodgeReductionWithoutRecursiveDamage()
        {
            var u=Unit();var enemy=Unit(false,2);enemy.Character.Stats.PhysicalDefense=new Stat(100);enemy.Character.Stats.MagicalDefense=new Stat(100);enemy.Character.Stats.Dodge=new Stat(20);var s=Session(u,enemy);Rank(u,true,"torment",1);
            var ctx=new PassiveEffectCombatContext(source:u,target:enemy,damageType:DamageType.Physical,skillId:"skill.paladin.verdict");
            for(var i=0;i<10;i++)PaladinCombatSystem.AfterAttack(s,u,enemy,ctx,100,0,10,new CombatDamageSystem());
            var e=enemy.Character.PassiveEffects.Single(e=>e.EffectId=="paladin.torment");Assert.That(e.Stacks,Is.EqualTo(4));Assert.That(enemy.Character.Stats.PhysicalDefense.GetValue(),Is.EqualTo(92));Assert.That(enemy.Character.Stats.Dodge.GetValue(),Is.EqualTo(12));
        }
        [Test] public void JudgementFieldReplacesPreviousFieldAcrossCreatorsAndCleansSlow()
        {
            var u=Unit();var other=Unit(true,4);var enemy=Unit(false,2);var s=Session(u,other,enemy);var system=new SummonedObjectSystem();var skill=Skill("JudgementField");
            Assert.That(system.TryCreate(s,u,skill,skill.Effects[0],0,new GridPosition(2,2),out var first),Is.True);
            Assert.That(enemy.Character.PassiveEffects.Any(e=>e.EffectId==first.ObjectId+".paladin.slow"),Is.True);
            Assert.That(system.TryCreate(s,other,skill,skill.Effects[0],0,new GridPosition(10,9),out var second),Is.True);
            Assert.That(s.SummonedObjects.Count,Is.EqualTo(1));Assert.That(s.SummonedObjects[0],Is.SameAs(second));Assert.That(enemy.Character.PassiveEffects.Any(e=>e.EffectId==first.ObjectId+".paladin.slow"),Is.False);Assert.That(second.CanReceiveHit(),Is.False);
        }
        [Test] public void CriticalFocusMultipliesHealingChanceNotCriticalStrength()
        {
            var u=Unit();u.Character.Stats.CritChance=new Stat(.2f);Rank(u,false,"unexpected_grace",3);Rank(u,false,"critical_focus",2);
            Assert.That(PaladinCombatSystem.HealingCritChance(u,false),Is.EqualTo(.3f).Within(.0001));Assert.That(PaladinCombatSystem.HealingCritChance(u,true),Is.EqualTo(.6f).Within(.0001));
        }
        [Test] public void GroundingWavePullsTowardSelectedCenterInsteadOfCaster()
        {
            var u=Unit(true,1,2);var enemy=Unit(false,4,2);var s=Session(u,enemy);var skill=Skill("GroundingWave");
            Assert.That(Execution().TryExecute(s,u,skill,new System.Collections.Generic.Dictionary<int,System.Collections.Generic.IReadOnlyList<BattleUnit>>{{0,new[]{enemy}}},null,null,new System.Collections.Generic.Dictionary<int,GridPosition?>{{0,new GridPosition(5,2)}}),Is.True);
            Assert.That(enemy.Position,Is.EqualTo(new GridPosition(5,2)));
        }
        [Test] public void RegenerationRecastRestartsPoolAndDoesNotKeepOldTail()
        {
            var u=Unit();var ally=Unit(true,2);var s=Session(u,ally);Rank(u,false,"lasting_effect",2);var statuses=new StatusEffectSystem();
            Assert.That(Execution().TryExecute(s,u,Skill("HolyRegeneration"),ally),Is.True);for(var i=0;i<4;i++)statuses.ProcessTurnEnd(ally);
            u.CurrentActionPoints=u.MaxActionPoints;u.Character.ResourcePool.CurrentValue=100;ally.Character.CurrentHealth=100;
            Assert.That(Execution().TryExecute(s,u,Skill("HolyRegeneration"),ally),Is.True);for(var i=0;i<4;i++)statuses.ProcessTurnEnd(ally);Assert.That(ally.Character.CurrentHealth,Is.EqualTo(500));
        }
        [Test] public void FieldStunsOnPenultimateTickAndBurstsOnLast()
        {
            var u=Unit();var enemy=Unit(false,2);var s=Session(u,enemy);Rank(u,true,"exhaustion",2);Rank(u,true,"judgement_release",3);var system=new SummonedObjectSystem();var skill=Skill("JudgementField");
            system.TryCreate(s,u,skill,skill.Effects[0],0,new GridPosition(2,2),out var field);field.RemainingDurationTurns=2;system.ExecuteTurnEffects(s,field);
            Assert.That(StatusEffectSystem.HasStatus(enemy,StatusEffectIds.Stun),Is.True);Assert.That(enemy.Character.CurrentHealth,Is.EqualTo(450));
            field.RemainingDurationTurns=1;system.ExecuteTurnEffects(s,field);Assert.That(enemy.Character.CurrentHealth,Is.EqualTo(250));
        }
        [Test] public void FieldSlowFollowsMovementWithoutWaitingForItsTurn()
        {
            var u=Unit();var enemy=Unit(false,2);var s=Session(u,enemy);var skill=Skill("JudgementField");
            new SummonedObjectSystem().TryCreate(s,u,skill,skill.Effects[0],0,new GridPosition(2,2),out var field);
            var id=field.ObjectId+".paladin.slow";
            Assert.That(enemy.Character.PassiveEffects.Any(e=>e.EffectId==id),Is.True);
            enemy.Position=new GridPosition(9,9);CombatAuraSystem.Refresh(s,new StatusEffectSystem());
            Assert.That(enemy.Character.PassiveEffects.Any(e=>e.EffectId==id),Is.False);
            enemy.Position=new GridPosition(2,2);CombatAuraSystem.Refresh(s,new StatusEffectSystem());
            Assert.That(enemy.Character.PassiveEffects.Any(e=>e.EffectId==id),Is.True);
            Assert.That(enemy.Character.CurrentHealth,Is.EqualTo(500));
        }
        [Test] public void AdvantageCapsAtFiftyPercent()
        {
            var u=Unit();var enemy=Unit(false,2);Rank(u,true,"advantage",1);enemy.Character.Stats.MaxHealth=new Stat(100);
            Assert.That(PaladinCombatSystem.DamageMultiplier(new PassiveEffectCombatContext(source:u,target:enemy)),Is.EqualTo(1.5f));
        }
    }
}
