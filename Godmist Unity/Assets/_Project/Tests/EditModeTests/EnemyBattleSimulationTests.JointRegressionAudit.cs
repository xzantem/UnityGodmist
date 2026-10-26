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
        static string JointRegressionFolder=>Path.GetFullPath("../LocalHandoffs/joint-regression-diagnosis-20261019");
        [Serializable] sealed class JointRegressionRow
        {
            public int sequence,battle,turn;
            public string kind,actor,skill,targets,focus;
            public bool played,repositioned,joint,committed;
            public SeedDecisionFrame frame;
            public AlternativeDecision decision;
            public List<JointRegressionMove> moves=new();
        }
        [Serializable] sealed class JointRegressionMove
        {
            public int x,y,cost;public float risk;public bool safe,cohesive;
            public string skill;public float score;
        }
        [Serializable] sealed class JointRegressionReport
        {
            public int level,profile,seed,mode;public string biome;public int[] selected;
            public List<JointRegressionRow> events=new();
            public List<AlternativeInput> inputs=new();
        }
        sealed partial class Engine
        {
            internal void CaptureJointRegressionState(JointRegressionRow row,BattleUnit actor,bool ended)
            {
                var rng=UnityEngine.Random.state;var fingerprint=DecisionStateFingerprint();
                row.repositioned=playerRepositioned;row.joint=JointPolicy;row.focus=playerFocus?.Character.CharacterId;
                row.committed=committedTeamActor==actor&&committedTeamAttack!=null;
                row.frame=CaptureSeedDecision(actor,row.battle,row.kind,row.skill,row.played,
                    options:row.kind=="before"&&actor.Character.ClassData.Id=="class.paladin");
                if(row.kind=="before")row.decision=CaptureAlternativeDecision(actor,row.battle);
                if(ended&&actor.CurrentActionPoints>=20)
                {
                    var focus=playerFocus;var danger=DangerousCells();
                    foreach(var p in movement.CalculateReachablePaths(Session,actor)
                        .Where(p=>p.Value.Count>0&&!p.Value.Any(danger.Contains))
                        .Select(p=>(cell:p.Key,path:p.Value,cost:PlayerPathCost(actor,p.Value)))
                        .Where(p=>p.cost<=actor.CurrentActionPoints)
                        .OrderBy(p=>focus==null?0:PlayerDistance(p.cell,focus.Position)).ThenBy(p=>p.cost).Take(6))
                    {
                        var plan=ProjectPlayerPlan(actor,p.cell,actor.CurrentActionPoints-p.cost);
                        row.moves.Add(new JointRegressionMove{x=p.cell.X,y=p.cell.Y,cost=p.cost,
                            risk=PlayerIncoming(actor,p.cell),safe=SafeCoordinatedSetup(actor,p.cell),cohesive=KeepsTeamSupportReach(actor,p.cell),
                            skill=plan?.skill.SkillId,score=plan?.score??0});
                    }
                }
                Assert.That(DecisionStateFingerprint(),Is.EqualTo(fingerprint),"Read-only regression observations changed combat state.");
                Assert.That(UnityEngine.Random.state,Is.EqualTo(rng),"Read-only regression observations consumed RNG.");
            }
        }
        static IEnumerable<TestCaseData> JointRegressionCases()
        {
            var cases=new[]{
                (10,7,"catacombs",3,"1,14,20"),(48,7,"forest",0,"0,5,6"),(48,7,"forest",2,"0,15"),
                (10,2,"catacombs",3,"2,11,12"),(10,2,"forest",2,"1,7,16"),(10,2,"forest",3,"0,13,15"),
                (48,2,"forest",0,"0,9,13,20")};
            foreach(var c in cases)
                foreach(var mode in c.Item2==7?new[]{0,1,2,3}:new[]{0,1})
                    yield return new TestCaseData(c.Item1,c.Item2,c.Item3,c.Item4,c.Item5,mode);
        }
        // mode0=v8 decisions/bars, 1=v9 decisions/bars,
        // mode2=v8 decisions/v9 bars, 3=v9 decisions/v8 bars. Controls are in-memory only.
        [TestCaseSource(nameof(JointRegressionCases))]
        [Explicit("Neutral worst-route replays and crossed legal bar/decision controls. No policy, stats or investment changes.")]
        [Timeout(600000)]
        public void AuditJointRegressionDecisions(int level,int profile,string biome,int seed,string selection,int mode)
        {
            var report=new JointRegressionReport{level=level,profile=profile,biome=biome,seed=seed,mode=mode,
                selected=selection.Split(',').Select(int.Parse).ToArray()};
            var folder=Path.Combine(JointRegressionFolder,$"L{level}-P{profile}-{biome}-S{seed}-M{mode}");
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(JointRegressionFolder,"progress.log"),DateTime.UtcNow.ToString("O")+" "+Path.GetFileName(folder)+Environment.NewLine);
            RunRealisticExpeditions(level,0,biome,DungeonLength.Normal,profile,seed+1,$"regression-L{level}-P{profile}-{biome}-S{seed}-M{mode}",firstSeed:seed,
                outputFolder:folder,fullOnly:true,traceKnockdowns:true,previousPlanningPolicy:mode==0||mode==2,
                planningLoadout:mode==3?0:JointDefaultLoadout,auditEngine:(engine,battle)=>
                {
                    // The crossed control starts every fight with the legal v9 bar.
                    // PreviousPlanningPolicy otherwise intentionally restores v8 bars.
                    if(mode==2)
                        foreach(var index in Enumerable.Range(0,4))
                        {
                            var unit=engine.Session.Units.SingleOrDefault(u=>u.Character.CharacterId=="real-exp-hero-"+index);
                            if(unit!=null)SetJointLoadout(unit.Character,RealisticProfile(index,profile),JointDefaultLoadout);
                        }
                    if(!report.selected.Contains(battle))return;
                    var session=engine.Session;
                    foreach(var u in session.Units)
                        report.inputs.Add(new AlternativeInput{battle=battle,actor=u.Character.CharacterId,definition=u.Character.Definition.Id,
                            skills=string.Join("|",u.Character.GetActiveSkills().Select(s=>s.SkillId)),known=string.Join("|",u.Character.GetKnownSkills().Select(s=>s.SkillId)),
                            investments=u.IsPlayerSide?string.Join("|",u.Character.SkillTreeProgress.SelectMany(t=>t.NodeInvestments.Select(n=>$"{t.TreeId}/{n.NodeId}:{n.Ranks}"))):null,
                            hp=u.Character.Stats.MaxHealth.GetValue(),attack=(u.Character.Stats.MinAttack.GetValue()+u.Character.Stats.MaxAttack.GetValue())*.5f,
                            physicalDefense=u.Character.Stats.PhysicalDefense.GetValue(),magicalDefense=u.Character.Stats.MagicalDefense.GetValue()});
                    JointRegressionRow Add(string kind,BattleUnit actor,string skill=null)
                    {
                        var row=new JointRegressionRow{sequence=report.events.Count,battle=battle,turn=engine.Result.turns,kind=kind,
                            actor=actor.Character.CharacterId,skill=skill};report.events.Add(row);return row;
                    }
                    var first=session.Units.First(u=>u.IsPlayerSide);
                    engine.CaptureJointRegressionState(Add("initial",first),first,false);
                    engine.AuditPlayerDecision=(actor,after,played)=>
                    {
                        var row=Add(after?"after":"before",actor);row.played=played;
                        engine.CaptureJointRegressionState(row,actor,after&&!played);
                    };
                    engine.AuditChosenPlan=(actor,skill,targets,score)=>
                    {var row=Add("chosen",actor,skill.SkillId);row.targets=string.Join("|",targets.Select(u=>u.Character.CharacterId));};
                    session.SkillResolved+=(actor,skill,id)=>
                    {var row=Add("skill",actor,skill.SkillId);engine.CaptureJointRegressionState(row,actor,false);};
                });
            // A divergent control may die before a later selected fight; record only reached fights.
            Assert.That(report.events.Any(e=>e.kind=="initial"),Is.True);
            File.WriteAllText(Path.Combine(folder,"decisions.json"),JsonUtility.ToJson(report,true));
        }
    }
}
