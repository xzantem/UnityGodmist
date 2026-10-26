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
        [Serializable] sealed class SeedDecisionUnit
        {
            public string id,life,statuses;
            public int x,y,hp,ap,resource,shield,downTurns;
            public float maxHp,av,dot,possibleIncoming;
        }
        [Serializable] sealed class SeedSupportOption
        {
            public string skill,ally;
            public bool legal,contains,inRange,los,critical,regenActive;
            public int apCost,resourceCost;
            public float immediate,priority;
        }
        [Serializable] sealed class SeedDecisionFrame
        {
            public string kind,actor,skill;
            public int battle,turn;
            public bool played,endsTurn;
            public List<SeedDecisionUnit> units=new();
            public List<SeedSupportOption> support=new();
        }
        [Serializable] sealed class SeedDecisionReport { public List<SeedDecisionFrame> frames=new(); }
        sealed partial class Engine
        {
            string DecisionStateFingerprint()=>string.Join("|",Session.Units.Select(u=>
                $"{u.Character.CharacterId}:{u.Character.CurrentHealth}:{u.Character.LifeState}:{u.Position.X},{u.Position.Y}:{u.CurrentActionPoints}:{u.CurrentActionValue}:{u.Character.ResourcePool.CurrentValue}:{u.Character.ShieldPool?.TotalShield}:{u.DownedTurnsRemaining}:"
                +string.Join(",",u.Character.PassiveEffects.Select(e=>$"{e.EffectId}:{e.RemainingDuration}:{e.TotalDamage}"))));
            internal SeedDecisionFrame CaptureSeedDecision(BattleUnit actor,int battle,string kind,string skill=null,bool played=false,bool options=true)
            {
                var rng=UnityEngine.Random.state;var fingerprint=DecisionStateFingerprint();
                var frame=new SeedDecisionFrame{actor=actor.Character.CharacterId,battle=battle,turn=Result.turns,kind=kind,skill=skill,played=played,endsTurn=endPlayerTurn};
                foreach(var u in Session.Units)
                    frame.units.Add(new SeedDecisionUnit{id=u.Character.CharacterId,life=u.Character.LifeState.ToString(),x=u.Position.X,y=u.Position.Y,
                        hp=u.Character.CurrentHealth,maxHp=u.Character.Stats.MaxHealth.GetValue(),ap=u.CurrentActionPoints,resource=u.Character.ResourcePool.CurrentValue,
                        av=u.CurrentActionValue,shield=u.Character.ShieldPool?.TotalShield??0,downTurns=u.DownedTurnsRemaining,
                        dot=u.IsPlayerSide?NextPlayerDot(u):0,possibleIncoming=u.IsPlayerSide?PlayerIncoming(u,u.Position):0,
                        statuses=string.Join("|",u.Character.PassiveEffects.Select(e=>$"{e.EffectId}:{e.RemainingDuration}:{e.TotalDamage}"))});
                if(options)
                    foreach(var s in actor.Character.GetActiveSkills().Where(IsSupport))
                        foreach(var ally in Session.Units.Where(u=>u.IsPlayerSide==actor.IsPlayerSide&&u.Character.LifeState!=CharacterLifeState.Dead))
                        {
                            var legal=SupportPlan(actor,ally,s,out var targets);
                            var contains=targets.Values.Any(v=>v.Contains(ally));
                            frame.support.Add(new SeedSupportOption{skill=s.SkillId,ally=ally.Character.CharacterId,legal=legal,contains=contains,
                                inRange=s.TargetModes.Any(m=>CombatTargetingService.IsPositionWithinRange(actor.Position,ally.Position,m.AbsoluteRange)),
                                los=!s.RequiresLineOfSight||sight.HasLineOfSight(Session,actor.Position,ally.Position),critical=CriticalPatient(ally),
                                regenActive=ally.Character.PassiveEffects.Any(e=>e.EffectId=="paladin.holy_regeneration"&&PassiveEffectLifecycleSystem.IsActive(e)),
                                apCost=PaladinCombatSystem.HealCost(actor,s,targets.Values.SelectMany(v=>v).FirstOrDefault()??ally,CombatActionCostCalculator.CalculateSkillCost(actor,s)),
                                resourceCost=PassiveEffectCombatExecutionSystem.ResolveResourceCost(actor,s),
                                immediate=targets.Where(p=>p.Value.Contains(ally)).Sum(p=>ImmediateRecovery(actor,ally,s,p.Key)),
                                priority=legal?SupportPriority(actor,ally,s,targets):0});
                        }
                Assert.That(DecisionStateFingerprint(),Is.EqualTo(fingerprint),"Decision observations must not mutate live combat state.");
                Assert.That(UnityEngine.Random.state,Is.EqualTo(rng),"Decision observations must not consume RNG.");
                return frame;
            }
        }
        [Test,Explicit("Read-only replay of seed6 with exact support legality/resources/positions; no policy or balance changes.")]
        public void AuditSeed6SupportDecisions()
        {
            var folder=Path.GetFullPath("../LocalHandoffs/seed6-decisions-20261019");Directory.CreateDirectory(folder);
            var observations=new SeedDecisionReport();
            RunRealisticExpeditions(48,.5f,"forest",DungeonLength.Normal,0,7,"decision-forest-S6",firstSeed:6,
                outputFolder:folder,fullOnly:true,traceKnockdowns:true,previousSupportPolicy:true,auditEngine:(engine,battle)=>
                {
                    if(battle is not (2 or 3 or 19 or 21))return;
                    engine.AuditPlayerDecision=(actor,after,played)=>observations.frames.Add(engine.CaptureSeedDecision(actor,battle,after?"after":"before",played:played,options:!after));
                    engine.Session.SkillResolved+=(actor,skill,id)=>
                    {if(actor.IsPlayerSide)observations.frames.Add(engine.CaptureSeedDecision(actor,battle,"skill",skill.SkillId,options:false));};
                });
            File.WriteAllText(Path.Combine(folder,"decisions.json"),JsonUtility.ToJson(observations,true));
        }

        [Test,Explicit("Counterfactual only: omit the fourth fight's opening Blessing; default policy remains unchanged.")]
        public void AuditSeed6OpeningBlessingCounterfactual()
        {
            var folder=Path.GetFullPath("../LocalHandoffs/seed6-decisions-20261019/counterfactual");Directory.CreateDirectory(folder);
            var observations=new SeedDecisionReport();
            RunRealisticExpeditions(48,.5f,"forest",DungeonLength.Normal,0,7,"no-opening-blessing-forest-S6",firstSeed:6,
                outputFolder:folder,fullOnly:true,traceKnockdowns:true,previousSupportPolicy:true,auditEngine:(engine,battle)=>
                {
                    if(battle!=3)return;
                    var original=engine.AdditionalAuditScore;
                    engine.AdditionalAuditScore=(actor,skill,mode,targets)=>(original?.Invoke(actor,skill,mode,targets)??0)
                        -(actor.Character.CharacterId=="real-exp-hero-3"&&engine.Result.turns==8&&skill.SkillId=="skill.paladin.blessing"?1000000:0);
                    engine.AuditPlayerDecision=(actor,after,played)=>observations.frames.Add(engine.CaptureSeedDecision(actor,battle,after?"after":"before",played:played,options:!after));
                    engine.Session.SkillResolved+=(actor,skill,id)=>
                    {if(actor.IsPlayerSide)observations.frames.Add(engine.CaptureSeedDecision(actor,battle,"skill",skill.SkillId,options:false));};
                });
            Assert.That(observations.frames.Any(f=>f.kind=="skill"&&f.turn==8&&f.actor=="real-exp-hero-3"&&f.skill=="skill.paladin.blessing"),Is.False);
            File.WriteAllText(Path.Combine(folder,"decisions.json"),JsonUtility.ToJson(observations,true));
        }
    }
}
