using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        static string RangedDuelFolder=>Path.GetFullPath("../LocalHandoffs/ranged-npc-duels-20261019");
        [Serializable] sealed class NpcDuelUnit
        {
            public string id,resource;public float maxHp,atk,physicalDefense,magicalDefense,speed,accuracy;
            public int initialResource,maxResource,basicRange,hp,turns,aiCalls,moves,damage,healing,resourceGained,ccTurns;
            public int startX,startY,basicShots,basicDistanceSum,maxBasicDistance;
            public List<SkillCount> skills=new();
        }
        [Serializable] sealed class NpcDuelRow
        {
            public string catacombs,forest,winner,stop;public bool forestLeft,range7;public int seed,distance,turns;
            public NpcDuelUnit cat,wood;
        }
        [Serializable] sealed class NpcDuelReport
        {
            public string method="L48 actual NPCs, full HP, standard resources, all real skills, same production enemy AI on BOTH sides; no hero policy.32 paired seeds, each at distances3/7/10, with mirrored assignments.32x12 open arena. First non-alive participant loses; no revive. Range7 is temporary shooter-only cap from previous experiment.";
            public List<NpcDuelRow> duels=new();
        }
        sealed partial class Engine
        {
            internal Action<CombatSession> AuditNpcDuelOutcome;
            internal Action<BattleUnit> AuditNpcDuelAiCall;
            // Production AI assumes its actor is enemy-side. For this opt-in 1v1
            // only, relabel BOTH participants together before each AI turn. Relative
            // hostility is unchanged, and both actors receive the same resource AI.
            internal void RunNpcDuel(BattleUnit left,BattleUnit right)
            {
                void Finish(CombatSession session)
                {
                    var l=left.Character.LifeState==CharacterLifeState.Alive;
                    var r=right.Character.LifeState==CharacterLifeState.Alive;
                    if(!l||!r)session.Outcome=l?CombatOutcomeState.Victory:CombatOutcomeState.Defeat;
                }
                AuditNpcDuelOutcome=Finish;
                var clock=System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    while(Session.Outcome==CombatOutcomeState.Ongoing&&Result.turns<360&&Result.actions<1200&&clock.Elapsed.TotalSeconds<10)
                    {
                        Result.turns++;
                        var actor=Session.ActiveUnit;
                        Assert.That(actor,Is.Not.Null);
                        left.IsPlayerSide=left!=actor;right.IsPlayerSide=right!=actor;
                        Assert.That(actor.IsPlayerSide,Is.False);
                        AuditUnitTurn?.Invoke(actor);
                        if(!StatusEffectSystem.IsHardCrowdControlled(actor))
                        {
                            var state=ai.CreateTurnState();
                            for(var action=0;action<24&&Session.Outcome==CombatOutcomeState.Ongoing&&Session.ActiveUnit==actor;action++)
                            {
                                AuditNpcDuelAiCall?.Invoke(actor);
                                using var job=ai.CreateNextActionJob(Session,actor,state,timeoutSeconds:1);
                                var steps=0;
                                while(!job.IsComplete&&steps++<2000)job.AdvanceSlice(2,2000);
                                if(job.HasTimedOut||!job.IsComplete){Result.stop="AI budget";break;}
                                if(!job.Executed)break;
                                Result.actions++;Observe();
                            }
                        }
                        Evaluate(Session);
                        if(Result.stop!=null||Session.Outcome!=CombatOutcomeState.Ongoing)break;
                        if(Session.ActiveParticipant==null)turns.AdvanceTurn(Session);else turns.EndActiveTurn(Session);
                    }
                    if(Session.Outcome==CombatOutcomeState.Ongoing&&Result.stop==null)Result.stop="duel budget";
                }
                finally
                {
                    left.IsPlayerSide=true;right.IsPlayerSide=false;
                    AuditNpcDuelOutcome=null;
                }
            }
        }
        NpcDuelRow RangedNpcDuel(string catId,string forestId,bool range7,int distance,int seed,bool forestLeft)
        {
            BattleUnit Create(string id,string instance,bool left)
            {
                var definition=PointEnemyDefinitions.Single(d=>d.Id==id);
                var character=new CharacterInstanceFactory().Create(definition,definition.ClassData,48);character.CharacterId=instance;
                return new BattleUnit{Character=character,IsPlayerSide=left,Position=new GridPosition((31-distance)/2+(left?0:distance),5)};
            }
            var cat=Create(catId,"duel-cat",!forestLeft);var forest=Create(forestId,"duel-forest",forestLeft);
            var left=forestLeft?forest:cat;var right=forestLeft?cat:forest;
            var session=new CombatSession{Grid=new BattleGrid(32,12),Units=new(){left,right}};
            var result=new Result();
            using var engine=new Engine(session,result);
            if(range7)
            {
                var definition=ShooterRangeCandidate(forest.Character.Definition,new ShooterRangeProofs());
                forest.Character.Skills=forest.Character.Skills.Select(s=>definition.StartingSkills.FirstOrDefault(c=>c!=null&&c.SkillId==s.SkillId)??s).ToList();
                engine.AuditShooterDefinition(forest,definition);
            }
            NpcDuelUnit Snapshot(BattleUnit u)=>new(){id=u.Character.Definition.Id,resource=u.Character.ResourcePool.ResourceType.ToString(),
                maxHp=u.Character.Stats.MaxHealth.GetValue(),atk=(u.Character.Stats.MinAttack.GetValue()+u.Character.Stats.MaxAttack.GetValue())*.5f,
                physicalDefense=u.Character.Stats.PhysicalDefense.GetValue(),magicalDefense=u.Character.Stats.MagicalDefense.GetValue(),speed=u.Character.Stats.Speed.GetValue(),accuracy=u.Character.WeaponAccuracy,
                initialResource=u.Character.ResourcePool.CurrentValue,maxResource=u.Character.ResourcePool.MaxValue,startX=u.Position.X,startY=u.Position.Y,
                basicRange=u.Character.GetBasicAttackSkill().TargetModes.Where(m=>m.TargetingType==TargetingType.Enemies).Max(m=>m.AbsoluteRange.MaxRange)};
            var row=new NpcDuelRow{catacombs=catId,forest=forestId,range7=range7,seed=seed,distance=distance,forestLeft=forestLeft,cat=Snapshot(cat),wood=Snapshot(forest)};
            Assert.That(Math.Abs(cat.Position.X-forest.Position.X),Is.EqualTo(distance));
            Assert.That(cat.Position.Y,Is.EqualTo(forest.Position.Y));
            var map=new Dictionary<BattleUnit,NpcDuelUnit>{{cat,row.cat},{forest,row.wood}};
            session.UnitEnteredCell+=u=>map[u].moves++;
            session.DamageApplied+=(source,target,amount,type,id)=>{if(source!=null)map[source].damage+=amount;};
            foreach(var u in map.Keys){var unit=u;u.Character.HealingReceived+=(amount,origin,bus)=>map[unit].healing+=amount;}
            session.SkillResolved+=(u,skill,id)=>
            {
                var c=map[u].skills.FirstOrDefault(c=>c.id==skill.SkillId);
                if(c==null){c=new SkillCount{id=skill.SkillId};map[u].skills.Add(c);}c.count++;
                if(skill.SkillId==u.Character.GetBasicAttackSkill().SkillId)
                {
                    var target=u==cat?forest:cat;
                    var mode=skill.TargetModes.First(m=>m.TargetingType==TargetingType.Enemies);
                    mode=PassiveEffectCombatExecutionSystem.ResolveMode(u,skill,mode);
                    Assert.That(CombatTargetingService.IsPositionWithinRange(u.Position,target.Position,mode.AbsoluteRange),Is.True);
                    var gap=Math.Max(Math.Abs(u.Position.X-target.Position.X),Math.Abs(u.Position.Y-target.Position.Y));
                    map[u].basicShots++;map[u].basicDistanceSum+=gap;map[u].maxBasicDistance=Math.Max(map[u].maxBasicDistance,gap);
                }
            };
            engine.AuditUnitTurn=u=>{map[u].turns++;if(StatusEffectSystem.IsHardCrowdControlled(u))map[u].ccTurns++;};
            engine.AuditNpcDuelAiCall=u=>map[u].aiCalls++;
            engine.AuditFeedback=e=>{if(e.Type==CombatFeedbackType.ResourceGain&&e.Target!=null)map[e.Target].resourceGained+=e.Amount;};
            engine.RunNpcDuel(left,right);
            row.turns=result.turns;row.stop=result.stop;row.cat.hp=cat.Character.CurrentHealth;row.wood.hp=forest.Character.CurrentHealth;
            row.winner=cat.Character.LifeState==CharacterLifeState.Alive?"catacombs":forest.Character.LifeState==CharacterLifeState.Alive?"forest":"draw";
            if(result.stop!=null)row.winner="budget";
            return row;
        }
        [TestCase("enemy.catacombs.archer","enemy.forest.bandit_shooter",false)]
        [TestCase("enemy.catacombs.archer","enemy.forest.bandit_shooter",true)]
        [TestCase("enemy.catacombs.archer","enemy.forest.elf_archer",false)]
        [TestCase("enemy.catacombs.occultist","enemy.forest.bandit_shooter",false)]
        [TestCase("enemy.catacombs.occultist","enemy.forest.bandit_shooter",true)]
        [TestCase("enemy.catacombs.occultist","enemy.forest.elf_archer",false)]
        [Explicit("Symmetric NPC-AI vs NPC-AI duels; only temporary range7 candidate, no production changes.")]
        public void AuditRangedNpcDuels(string catId,string forestId,bool range7)
        {
            var report=new NpcDuelReport();var random=UnityEngine.Random.state;
            try
            {
                foreach(var distance in new[]{3,7,10})for(var seed=0;seed<32;seed++)foreach(var mirror in new[]{false,true})
                {
                    UnityEngine.Random.InitState(919026+seed);
                    var row=RangedNpcDuel(catId,forestId,range7,distance,seed,mirror);report.duels.Add(row);
                    Assert.That(row.stop,Is.Null.Or.Empty,"A budget is not a victory.");
                }
                Directory.CreateDirectory(RangedDuelFolder);
                File.WriteAllText(Path.Combine(RangedDuelFolder,$"duels-{catId.Split('.').Last()}-{forestId.Split('.').Last()}-{(range7?7:9)}.json"),JsonUtility.ToJson(report,true));
            }
            finally{UnityEngine.Random.state=random;}
        }
        [Test]
        public void RangedNpcDuelBothSidesUseResourceAiAndStopWithoutRevivingLoser()
        {
            var random=UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(919026);
                var row=RangedNpcDuel("enemy.catacombs.archer","enemy.forest.bandit_shooter",false,10,0,false);
                Assert.That(row.stop,Is.Null.Or.Empty);
                Assert.That(row.cat.skills.Sum(s=>s.count),Is.GreaterThan(0));Assert.That(row.wood.skills.Sum(s=>s.count),Is.GreaterThan(0));
                Assert.That(row.cat.resourceGained,Is.GreaterThan(0));Assert.That(row.wood.resourceGained,Is.GreaterThan(0));
                Assert.That(Math.Min(row.cat.hp,row.wood.hp),Is.Zero);
            }
            finally{UnityEngine.Random.state=random;}
        }
        [TestCase("forest",0)][TestCase("catacombs",6)]
        [Explicit("Default player-vs-NPC path must remain exactly identical after adding opt-in duel outcome callback.")]
        public void AuditRangedNpcDuelDefaultControl(string biome,int seed)
            =>RunRealisticExpeditions(48,0,biome,DungeonLength.Normal,0,seed+1,$"control-{biome}-S{seed}",firstSeed:seed,
                outputFolder:Path.Combine(RangedDuelFolder,"controls"),fullOnly:true,traceKnockdowns:true);
    }
}
