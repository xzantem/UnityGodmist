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
        static string AlternativeAuditFolder=>Path.GetFullPath("../LocalHandoffs/alternative-party-diagnosis-20261019");
        [Serializable] sealed class AlternativePlan
        {
            public string skill,targets,aim,placement;
            public float score;
        }
        [Serializable] sealed class AlternativeOption
        {
            public string skill;public int ap,rp,range;
            public bool support,knives,preservesMana,zeroUsed;
        }
        [Serializable] sealed class AlternativeDecision
        {
            public int battle,turn;public string actor,focus,synergy;
            public List<AlternativePlan> plans=new();
            public List<AlternativeOption> options=new();
        }
        [Serializable] sealed class AlternativeFeedback
        {
            public int battle,turn,amount;public string type,source,target,action,effect,tickingEffect,active;
        }
        [Serializable] sealed class AlternativeInput
        {
            public int battle;public string actor,definition,skills,known,investments;
            public float hp,attack,physicalDefense,magicalDefense;
        }
        [Serializable] sealed class AlternativeAuditReport
        {
            public int level,profile,seed;public string biome;public int[] selected;
            public List<MatchupAuditEvent> events=new();
            public List<AlternativeFeedback> feedback=new();
            public List<AlternativeInput> units=new();
            public List<AlternativeDecision> decisions=new();
        }
        sealed partial class Engine
        {
            internal AlternativeDecision CaptureAlternativeDecision(BattleUnit actor,int battle)
            {
                var rng=UnityEngine.Random.state;var fingerprint=DecisionStateFingerprint();
                var detail=new AlternativeDecision{battle=battle,turn=Result.turns,actor=actor.Character.CharacterId,
                    focus=playerFocus?.Character.CharacterId,
                    synergy=string.Join("|",Session.Units.Select(u=>$"{u.Character.CharacterId}:order={u.WarriorState.OrderTurns}:protect={u.WarriorState.ProtectionTurns}:weak={u.ScoutState.WeakPointTurns}:knives={u.AvailableKnives}/{u.MaxKnives}"))
                        +";fields="+string.Join("|",Session.SummonedObjects.Select(o=>$"{o.SourceSkillId}@{o.Position.X},{o.Position.Y}:{o.RemainingDurationTurns}"))};
                detail.plans=BuildPlayerPlans(actor).OrderByDescending(p=>p.score).Take(12).Select(p=>new AlternativePlan{
                    skill=p.skill.SkillId,score=p.score,targets=string.Join("|",p.targets.Values.SelectMany(t=>t).Distinct().Select(t=>t.Character.CharacterId)),
                    aim=string.Join("|",p.positions.Select(v=>$"{v.Key}:{v.Value?.X},{v.Value?.Y}")),
                    placement=p.objectPosition.HasValue?$"{p.objectPosition.Value.X},{p.objectPosition.Value.Y}":null}).ToList();
                detail.options=actor.Character.GetActiveSkills().Select(s=>new AlternativeOption{skill=s.SkillId,
                    ap=CombatActionCostCalculator.CalculateSkillCost(actor,s),rp=PassiveEffectCombatExecutionSystem.ResolveResourceCost(actor,s),
                    range=s.TargetModes.Select(m=>PassiveEffectCombatExecutionSystem.ResolveMode(actor,s,m).AbsoluteRange.MaxRange).DefaultIfEmpty(0).Max(),
                    support=IsSupport(s),knives=ScoutKnifeSystem.CanPay(actor,s),preservesMana=PreservesSupportMana(actor,s),zeroUsed=zeroCostUsed.Contains(s)}).ToList();
                Assert.That(DecisionStateFingerprint(),Is.EqualTo(fingerprint));
                Assert.That(UnityEngine.Random.state,Is.EqualTo(rng));
                return detail;
            }
            internal string AlternativeTickingEffect=>knockdownTrace?.tickingEffect?.EffectId;
        }

        [TestCase(48,1,"forest",3,"0,1")]
        [TestCase(48,1,"forest",0,"5,6,7")]
        [TestCase(10,1,"forest",1,"8,10,11,12,15")]
        [TestCase(48,1,"catacombs",3,"9,11,12")]
        [TestCase(10,1,"catacombs",2,"0,11,16")]
        [TestCase(48,0,"forest",3,"0,1")]
        [TestCase(48,4,"forest",3,"0,1")]
        [TestCase(48,7,"forest",3,"0,1")]
        [TestCase(10,0,"forest",1,"8,11,12,15")]
        [Explicit("Neutral current-v8 route replay; inspect alternative-party synergies and decisions, never alter policy or stats.")]
        public void AuditAlternativePartyDecisions(int level,int profile,string biome,int seed,string battleList)
        {
            var selected=battleList.Split(',').Select(int.Parse).ToArray();
            var report=new AlternativeAuditReport{level=level,profile=profile,biome=biome,seed=seed,selected=selected};
            var folder=Path.Combine(AlternativeAuditFolder,$"L{level}-P{profile}-{biome}-S{seed}");Directory.CreateDirectory(folder);
            RunRealisticExpeditions(level,0,biome,DungeonLength.Normal,profile,seed+1,$"alternative-audit-L{level}-P{profile}-{biome}-S{seed}",firstSeed:seed,
                outputFolder:folder,fullOnly:true,traceKnockdowns:true,auditEngine:(engine,battle)=>
                {
                    if(!selected.Contains(battle))return;
                    var session=engine.Session;BattleUnit active=null;
                    foreach(var u in session.Units)
                        report.units.Add(new AlternativeInput{battle=battle,actor=u.Character.CharacterId,definition=u.Character.Definition.Id,
                            skills=string.Join("|",u.Character.GetActiveSkills().Select(s=>s.SkillId)),known=string.Join("|",u.Character.GetKnownSkills().Select(s=>s.SkillId)),
                            investments=u.IsPlayerSide?string.Join("|",u.Character.SkillTreeProgress.SelectMany(t=>t.NodeInvestments.Select(n=>$"{t.TreeId}/{n.NodeId}:{n.Ranks}"))):null,
                            hp=u.Character.Stats.MaxHealth.GetValue(),attack=(u.Character.Stats.MinAttack.GetValue()+u.Character.Stats.MaxAttack.GetValue())*.5f,
                            physicalDefense=u.Character.Stats.PhysicalDefense.GetValue(),magicalDefense=u.Character.Stats.MagicalDefense.GetValue()});
                    MatchupAuditEvent Add(string kind,BattleUnit actor=null,BattleUnit target=null)
                    {
                        var value=new MatchupAuditEvent{sequence=report.events.Count,battle=battle,turn=engine.Result.turns,
                            kind=kind,actor=actor?.Character.CharacterId,target=target?.Character.CharacterId,focus=engine.MatchupCurrentFocus};
                        report.events.Add(value);return value;
                    }
                    Add("initial").frame=engine.CaptureSeedDecision(session.Units.First(u=>u.IsPlayerSide),battle,"initial",options:false);
                    var turn=engine.AuditUnitTurn;
                    engine.AuditUnitTurn=actor=>{turn?.Invoke(actor);active=actor;Add("ready",actor).frame=engine.CaptureSeedDecision(actor,battle,"ready",options:false);};
                    engine.AuditPlayerDecision=(actor,after,played)=>
                    {
                        var e=Add(after?"after-decision":"before-decision",actor);
                        e.frame=engine.CaptureSeedDecision(actor,battle,e.kind,played:played,options:!after);
                        if(!after)report.decisions.Add(engine.CaptureAlternativeDecision(actor,battle));
                    };
                    engine.AuditChosenPlan=(actor,skill,targets,score)=>
                    {var e=Add("chosen-plan",actor);e.skill=skill.SkillId;e.target=string.Join("|",targets.Select(t=>t.Character.CharacterId));e.score=score;};
                    var positions=session.Units.ToDictionary(u=>u,u=>u.Position);
                    session.UnitEnteredCell+=actor=>
                    {
                        if(!positions.TryGetValue(actor,out var previous)){positions[actor]=actor.Position;return;}
                        var e=Add("step",actor);e.previousX=previous.X;e.previousY=previous.Y;e.x=actor.Position.X;e.y=actor.Position.Y;positions[actor]=actor.Position;
                    };
                    session.DamageApplied+=(source,target,amount,type,id)=>
                    {var e=Add("applied",source,target);e.amount=amount;e.type=type.ToString();e.action=id;};
                    session.SkillResolved+=(actor,skill,id)=>
                    {var e=Add("skill",actor);e.skill=skill.SkillId;e.action=id;e.frame=engine.CaptureSeedDecision(actor,battle,"skill",skill.SkillId,options:false);};
                    var feedback=engine.AuditFeedback;
                    engine.AuditFeedback=value=>
                    {
                        feedback?.Invoke(value);
                        report.feedback.Add(new AlternativeFeedback{battle=battle,turn=engine.Result.turns,type=value.Type.ToString(),amount=value.Amount,
                            source=value.Source?.Character.CharacterId,target=value.Target?.Character.CharacterId,action=value.ActionId,effect=value.EffectName,
                            tickingEffect=engine.AlternativeTickingEffect,active=active?.Character.CharacterId});
                    };
                });
            Assert.That(report.events.Where(e=>e.kind=="initial").Select(e=>e.battle),Is.EquivalentTo(selected));
            Assert.That(report.decisions,Is.Not.Empty);
            File.WriteAllText(Path.Combine(folder,"decisions.json"),JsonUtility.ToJson(report,true));
        }
    }
}
