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
        [Serializable] sealed class MageRegressionAudit
        {
            public List<SeedDecisionFrame> frames=new();
            public List<ForestDecisionDetail> details=new();
            public List<MageRegressionChoice> chosen=new();
            public List<MageRegressionContext> context=new();
        }
        [Serializable] sealed class MageRegressionContext
        {
            public int battle,turn;
            public string actor,focus,previousFocus;
            public bool needsProtection,rescueBlocksRetreat;
            public float cohesion;
            public List<ForestAuditPlan> currentPlans=new(),previousPlans=new();
            public List<MageRegressionCell> safest=new();
        }
        [Serializable] sealed class MageRegressionCell
        {
            public int x,y,cost;
            public float risk;
            public bool nearAnyAlly,nearHealer,keepsSupport;
        }
        sealed partial class Engine
        {
            internal MageRegressionContext CaptureMageRegressionContext(BattleUnit actor,int battle)
            {
                var rng=UnityEngine.Random.state;var fingerprint=DecisionStateFingerprint();
                var oldFlag=UsePreviousTeamPolicy;var oldFocus=playerFocus;
                var c=new MageRegressionContext{battle=battle,turn=Result.turns,actor=actor.Character.CharacterId,
                    needsProtection=PlayerNeedsProtection(actor),rescueBlocksRetreat=Session.Units.Any(u=>u.IsPlayerSide==actor.IsPlayerSide
                        &&u.Character.LifeState==CharacterLifeState.Downed&&!DeferPlayerRescue(actor,u))};
                List<ForestAuditPlan> Plans()=>BuildPlayerPlans(actor).OrderByDescending(p=>p.score).Take(6)
                    .Select(p=>new ForestAuditPlan{skill=p.skill.SkillId,score=p.score,targets=string.Join("|",p.targets.Values.SelectMany(t=>t).Distinct().Select(t=>t.Character.CharacterId))}).ToList();
                try
                {
                    c.focus=UpdatePlayerFocus(actor)?.Character.CharacterId;c.currentPlans=Plans();
                    UsePreviousTeamPolicy=true;playerFocus=oldFocus;
                    c.previousFocus=UpdatePlayerFocus(actor)?.Character.CharacterId;c.previousPlans=Plans();
                }
                finally{UsePreviousTeamPolicy=oldFlag;playerFocus=oldFocus;}
                var allies=Session.Units.Where(u=>u!=actor&&u.IsPlayerSide==actor.IsPlayerSide&&u.Character.LifeState==CharacterLifeState.Alive).ToArray();
                var healers=allies.Where(TeamHealer).ToArray();
                var anchors=healers.Length>0?healers:allies;
                c.cohesion=anchors.Length==0?999:Math.Max(3,anchors.Min(u=>PlayerDistance(actor.Position,u.Position)));
                var oldCohesion=allies.Length==0?999:Math.Max(3,allies.Min(u=>PlayerDistance(actor.Position,u.Position)));
                var danger=DangerousCells();
                c.safest=movement.CalculateReachablePaths(Session,actor).Where(p=>p.Value.Count>0&&!p.Value.Any(danger.Contains))
                    .Select(p=>new MageRegressionCell{x=p.Key.X,y=p.Key.Y,cost=PlayerPathCost(actor,p.Value),risk=PlayerIncoming(actor,p.Key),
                        nearAnyAlly=allies.Length==0||allies.Any(u=>PlayerDistance(p.Key,u.Position)<=oldCohesion),
                        nearHealer=anchors.Length==0||anchors.Any(u=>PlayerDistance(p.Key,u.Position)<=c.cohesion),keepsSupport=KeepsTeamSupportReach(actor,p.Key)})
                    .Where(p=>p.cost<=actor.CurrentActionPoints).OrderBy(p=>p.risk).ThenBy(p=>p.cost).Take(12).ToList();
                Assert.That(DecisionStateFingerprint(),Is.EqualTo(fingerprint));Assert.That(UnityEngine.Random.state,Is.EqualTo(rng));
                return c;
            }
        }

        [TestCase(1)][TestCase(3)][TestCase(6)][TestCase(11)]
        [Explicit("Neutral current-policy replay with read-only candidate and historical-plan projections.")]
        public void AuditMageV5Projection(int seed)=>AuditMageV5Regression(seed,"projection");
        [Serializable] sealed class MageRegressionChoice
        {
            public int battle,turn;
            public string actor,skill,targets;
            public float score;
        }

        [TestCase(1,"v4")][TestCase(1,"v5")][TestCase(1,"local-v4")]
        [TestCase(3,"v4")][TestCase(3,"v5")][TestCase(3,"local-v4")]
        [TestCase(6,"v4")][TestCase(6,"v5")][TestCase(6,"local-v4")]
        [TestCase(11,"v4")][TestCase(11,"v5")][TestCase(11,"local-v4")]
        [Explicit("Read-only Mage regression audit; local control changes policy only in the first selected fight, preserving its v5 entry state.")]
        public void AuditMageV5Regression(int seed,string policy)
        {
            var folder=Path.GetFullPath($"../LocalHandoffs/mage-v5-regression-audit-20261019/{policy}");
            Directory.CreateDirectory(folder);var report=new MageRegressionAudit();
            var selected=seed switch{1=>new[]{18},3=>new[]{15,18},6=>new[]{3,11},_=>new[]{13,17}};
            RunRealisticExpeditions(48,0,"forest",DungeonLength.Normal,0,seed+1,$"audit-forest-S{seed}",firstSeed:seed,
                outputFolder:folder,fullOnly:true,traceKnockdowns:true,previousTeamPolicy:policy=="v4",previousMagePolicy:true,auditEngine:(engine,battle)=>
                {
                    if(!selected.Contains(battle))return;
                    if(policy=="local-v4"&&battle==selected[0])engine.UsePreviousTeamPolicy=true;
                    engine.AuditPlayerDecision=(actor,after,played)=>
                    {
                        report.frames.Add(engine.CaptureSeedDecision(actor,battle,after?"after":"before",played:played,options:!after));
                        if(!after&&actor.Character.ClassData.Id is "class.mage" or "class.paladin")report.details.Add(engine.CaptureForestDetail(actor,battle));
                        if(!after&&policy=="projection"&&actor.Character.ClassData.Id is "class.mage" or "class.paladin")report.context.Add(engine.CaptureMageRegressionContext(actor,battle));
                    };
                    engine.AuditChosenPlan=(actor,skill,targets,score)=>report.chosen.Add(new MageRegressionChoice
                    {battle=battle,turn=engine.Result.turns,actor=actor.Character.CharacterId,skill=skill.SkillId,score=score,
                        targets=string.Join("|",targets.Select(t=>t.Character.CharacterId))});
                    engine.Session.SkillResolved+=(actor,skill,id)=>
                    {if(actor.IsPlayerSide)report.frames.Add(engine.CaptureSeedDecision(actor,battle,"skill",skill.SkillId,options:false));};
                });
            File.WriteAllText(Path.Combine(folder,$"decisions-S{seed}.json"),JsonUtility.ToJson(report,true));
        }
    }
}
