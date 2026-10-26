using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        [Serializable] sealed class ForestDecisionAudit
        {
            public List<SeedDecisionFrame> frames=new();
            public List<ForestDecisionDetail> details=new();
        }
        [Serializable] sealed class ForestDecisionDetail
        {
            public int battle,turn,manaReserve;
            public string actor,focus;
            public bool needsProtection;
            public float incoming;
            public List<ForestAuditPlan> plans=new();
        }
        [Serializable] sealed class ForestAuditPlan
        {
            public string skill,targets;
            public float score;
        }
        sealed partial class Engine
        {
            internal ForestDecisionDetail CaptureForestDetail(BattleUnit actor,int battle)
            {
                var rng=UnityEngine.Random.state;var fingerprint=DecisionStateFingerprint();
                var detail=new ForestDecisionDetail{battle=battle,turn=Result.turns,actor=actor.Character.CharacterId,
                    focus=playerFocus?.Character.CharacterId,manaReserve=SupportManaReserve(actor),
                    incoming=PlayerIncoming(actor,actor.Position),needsProtection=PlayerNeedsProtection(actor)};
                detail.plans=BuildPlayerPlans(actor).OrderByDescending(p=>p.score).Take(6)
                    .Select(p=>new ForestAuditPlan{skill=p.skill.SkillId,score=p.score,
                        targets=string.Join("|",p.targets.Values.SelectMany(t=>t).Distinct().Select(t=>t.Character.CharacterId))}).ToList();
                Assert.That(DecisionStateFingerprint(),Is.EqualTo(fingerprint));
                Assert.That(UnityEngine.Random.state,Is.EqualTo(rng));
                return detail;
            }
        }
        [TestCase(4)][TestCase(5)][TestCase(6)][TestCase(10)]
        [Explicit("Neutral v4 replay: observe worst forest fights; no stat, mechanic or decision changes.")]
        public void AuditWorstForestDowningDecisions(int seed)
        {
            var folder=Path.GetFullPath("../LocalHandoffs/forest-downings-audit-20261019/replay");Directory.CreateDirectory(folder);
            var report=new ForestDecisionAudit();
            var selected=seed switch{4=>new[]{11},5=>new[]{17},6=>new[]{18},_=>new[]{5,8}};
            RunRealisticExpeditions(48,.5f,"forest",DungeonLength.Normal,0,seed+1,$"audit-forest-S{seed}",firstSeed:seed,
                outputFolder:folder,fullOnly:true,traceKnockdowns:true,previousTeamPolicy:true,auditEngine:(engine,battle)=>
                {
                    if(!selected.Contains(battle))return;
                    engine.AuditPlayerDecision=(actor,after,played)=>
                    {
                        report.frames.Add(engine.CaptureSeedDecision(actor,battle,after?"after":"before",played:played,options:!after));
                        if(!after&&actor.Character.ClassData.Id is "class.mage" or "class.paladin")report.details.Add(engine.CaptureForestDetail(actor,battle));
                    };
                    engine.Session.SkillResolved+=(actor,skill,id)=>
                    {if(actor.IsPlayerSide)report.frames.Add(engine.CaptureSeedDecision(actor,battle,"skill",skill.SkillId,options:false));};
                });
            File.WriteAllText(Path.Combine(folder,$"decisions-S{seed}.json"),JsonUtility.ToJson(report,true));
        }
    }
}
