using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        static string HeroBiomeMatchupFolder=>Path.GetFullPath("../LocalHandoffs/hero-biome-matchup-20261019");
        [Serializable] sealed class MatchupAuditEvent
        {
            public int sequence,battle,turn,amount,x,y,previousX,previousY;
            public string kind,actor,target,skill,action,type,focus;
            public float score,beforeRisk,afterRisk;
            public SeedDecisionFrame frame;
        }
        [Serializable] sealed class MatchupAuditRoster
        {
            public int battle;public string instance,id,role;public float hp,attack,physicalDefense,magicalDefense;
        }
        [Serializable] sealed class MatchupAuditReport
        {
            public string biome;public int seed;public int[] selected;
            public List<MatchupAuditEvent> events=new();
            public List<MatchupAuditRoster> units=new();
            public List<ForestDecisionDetail> plans=new();
        }
        sealed partial class Engine
        {
            internal string MatchupCurrentFocus=>playerFocus?.Character.CharacterId;
            internal float MatchupIncoming(BattleUnit actor,GridPosition cell)=>actor.IsPlayerSide?PlayerIncoming(actor,cell):0;
        }

        [TestCase("forest",6)][TestCase("forest",9)]
        [TestCase("catacombs",7)][TestCase("catacombs",11)][TestCase("catacombs",10)]
        [Explicit("Neutral replay of worst hero-party battles: focus, movement, healing and carried resources; no decision or balance changes.")]
        public void AuditHeroBiomeMatchupDecisions(string biome,int seed)
        {
            Directory.CreateDirectory(HeroBiomeMatchupFolder);
            var selected=biome=="forest"?(seed==6?new[]{11,17,18,19,21}:new[]{9})
                :seed==7?new[]{11,12}:seed==11?new[]{0,8}:new[]{14};
            var report=new MatchupAuditReport{biome=biome,seed=seed,selected=selected};
            var folder=Path.Combine(HeroBiomeMatchupFolder,"replay");Directory.CreateDirectory(folder);
            RunRealisticExpeditions(48,0,biome,DungeonLength.Normal,0,seed+1,$"matchup-{biome}-S{seed}",firstSeed:seed,
                outputFolder:folder,fullOnly:true,traceKnockdowns:true,auditEngine:(engine,battle)=>
                {
                    if(!selected.Contains(battle))return;
                    var session=engine.Session;
                    foreach(var u in session.Units)
                        report.units.Add(new MatchupAuditRoster{battle=battle,instance=u.Character.CharacterId,id=u.Character.Definition.Id,
                            role=u.Character.ClassData?.Id,hp=u.Character.Stats.MaxHealth.GetValue(),
                            attack=(u.Character.Stats.MinAttack.GetValue()+u.Character.Stats.MaxAttack.GetValue())*.5f,
                            physicalDefense=u.Character.Stats.PhysicalDefense.GetValue(),magicalDefense=u.Character.Stats.MagicalDefense.GetValue()});
                    MatchupAuditEvent Add(string kind,BattleUnit actor=null,BattleUnit target=null)
                    {
                        var value=new MatchupAuditEvent{sequence=report.events.Count,battle=battle,turn=engine.Result.turns,
                            kind=kind,actor=actor?.Character.CharacterId,target=target?.Character.CharacterId,focus=engine.MatchupCurrentFocus};
                        report.events.Add(value);return value;
                    }
                    void Frame(string kind,BattleUnit actor,bool options=false)
                    {Add(kind,actor).frame=engine.CaptureSeedDecision(actor,battle,kind,options:options);}
                    Frame("initial",session.Units.First(u=>u.IsPlayerSide));
                    var turn=engine.AuditUnitTurn;
                    engine.AuditUnitTurn=actor=>{turn?.Invoke(actor);Frame("ready",actor);};
                    engine.AuditPlayerDecision=(actor,after,played)=>
                    {
                        var e=Add(after?"after-decision":"before-decision",actor);
                        e.frame=engine.CaptureSeedDecision(actor,battle,e.kind,played:played,options:!after);
                        if(!after)report.plans.Add(engine.CaptureForestDetail(actor,battle));
                    };
                    engine.AuditChosenPlan=(actor,skill,targets,score)=>
                    {var e=Add("chosen-plan",actor);e.skill=skill.SkillId;e.target=string.Join("|",targets.Select(t=>t.Character.CharacterId));e.score=score;};
                    var positions=session.Units.ToDictionary(u=>u,u=>u.Position);
                    session.UnitEnteredCell+=actor=>
                    {
                        if(!positions.TryGetValue(actor,out var previous)){positions[actor]=actor.Position;return;}
                        var rng=UnityEngine.Random.state;
                        var e=Add("step",actor);e.previousX=previous.X;e.previousY=previous.Y;e.x=actor.Position.X;e.y=actor.Position.Y;
                        e.beforeRisk=engine.MatchupIncoming(actor,previous);e.afterRisk=engine.MatchupIncoming(actor,actor.Position);
                        positions[actor]=actor.Position;
                        Assert.That(UnityEngine.Random.state,Is.EqualTo(rng));
                    };
                    session.DamageApplied+=(source,target,amount,type,id)=>
                    {var e=Add("applied",source,target);e.amount=amount;e.type=type.ToString();e.action=id;};
                    session.SkillResolved+=(actor,skill,id)=>
                    {var e=Add("skill",actor);e.skill=skill.SkillId;e.action=id;
                        e.frame=engine.CaptureSeedDecision(actor,battle,"skill",skill.SkillId,options:false);};
                    var feedback=engine.AuditFeedback;
                    engine.AuditFeedback=value=>
                    {
                        feedback?.Invoke(value);
                        if(value.Type is CombatFeedbackType.Damage or CombatFeedbackType.ShieldLoss or CombatFeedbackType.ResourceSpend or CombatFeedbackType.ShieldGain)
                        {var e=Add(value.Type.ToString(),value.Source,value.Target);e.amount=value.Amount;e.type=value.DamageType.ToString();}
                    };
                });
            Assert.That(report.events.Count,Is.GreaterThan(0));
            Assert.That(report.events.Where(e=>e.kind=="initial").Select(e=>e.battle),Is.EquivalentTo(selected));
            Assert.That(report.events.Any(e=>e.kind=="skill"&&e.actor.StartsWith("real-exp-enemy-")),Is.True);
            Assert.That(report.events.Any(e=>e.kind=="before-decision"),Is.True);
            File.WriteAllText(Path.Combine(HeroBiomeMatchupFolder,$"decisions-{biome}-S{seed}.json"),JsonUtility.ToJson(report,true));
        }
    }
}
