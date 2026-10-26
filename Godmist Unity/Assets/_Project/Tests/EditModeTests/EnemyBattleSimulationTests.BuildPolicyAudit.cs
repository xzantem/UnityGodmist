using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        static readonly int[] BuildAuditProfiles={0,5,6,4,7,1,2};
        [Serializable] sealed class BuildInput {public int level,profile,hero,points;public string[] before,after;}
        [Serializable] sealed class BuildInputs {public List<BuildInput> rows=new();}
        [Test]
        public void BuildInputManifestRecordsUnchangedInvestmentAndLegalRoleLoadouts()
        {
            var report=new BuildInputs();
            foreach(var level in new[]{10,48})foreach(var profile in BuildAuditProfiles)foreach(var index in Enumerable.Range(0,4))
            {
                var own=RealisticProfile(index,profile);var hero=BuildHero(index,level,own).Character;
                var row=new BuildInput{level=level,profile=profile,hero=index,points=hero.SkillTreeProgress.Sum(t=>t.NodeInvestments.Sum(n=>n.Ranks)),
                    after=hero.GetActiveSkills().Select(s=>s.SkillId).ToArray()};
                SetExpeditionLoadout(hero,own);row.before=hero.GetActiveSkills().Select(s=>s.SkillId).ToArray();
                Assert.That(row.points,Is.EqualTo(PointBudget(level)));report.rows.Add(row);
            }
            Directory.CreateDirectory(BuildPolicyFolder);File.WriteAllText(Path.Combine(BuildPolicyFolder,"build-inputs.json"),JsonUtility.ToJson(report,true));
        }
        [Serializable] sealed class BuildSkillEvent
        {public int turn,hp,mana,ap,x,y;public string actor,skill;}
        [Serializable] sealed class BuildFight
        {public int level,profile,seed;public string biome,encounter,stage,trace;public Result result;public List<BuildSkillEvent> skills=new();}
        [Serializable] sealed class BuildFights {public List<BuildFight> fights=new();}
        [TestCase(10,"catacombs")][TestCase(10,"forest")][TestCase(48,"catacombs")][TestCase(48,"forest")]
        [Explicit("Paired fresh difficult encounters: v7, curated loadout only, v8. No production mutations.")]
        public void AuditBuildEncounterDecisions(int level,string biome)
        {
            var folder=Path.Combine(BuildPolicyFolder,"focused",$"L{level}-{biome}");Directory.CreateDirectory(folder);
            var reference=JsonUtility.FromJson<RealisticReport>(File.ReadAllText(Path.Combine(EarlyBuildFolder,"runs",$"expeditions-L{level}-P1-{biome}.json")));
            var names=biome=="forest"?new[]{"encounter.forest.elf_archers.balanced","encounter.forest.bandit_patrol.balanced"}
                :new[]{"encounter.catacombs.ambush","encounter.catacombs.undead_pack"};
            var report=new BuildFights();var random=UnityEngine.Random.state;
            try
            {
                for(var roster=0;roster<names.Length;roster++)
                {
                    var ids=reference.expeditions.SelectMany(r=>r.battles).First(r=>r.encounter==names[roster]).enemyIds;
                    foreach(var profile in BuildAuditProfiles)foreach(var seed in new[]{0,1})foreach(var stage in new[]{"v7","loadout-only","v8"})
                    {
                        var session=new CombatSession{Grid=new BattleGrid(12,10)};var enemies=PointEnemies(level);var history=RealisticDropHistory(level,seed).ToArray();
                        foreach(var index in Enumerable.Range(0,4))
                        {
                            var own=RealisticProfile(index,profile);var unit=BuildHero(index,level,own,index==0||index==3?3:2,3+index);
                            EquipHistoricalDrops(unit.Character,index,history,0,enemies);PointRefresh(unit.Character);
                            if(stage=="v7")SetExpeditionLoadout(unit.Character,own);else SetBuildLoadout(unit.Character,own);
                            unit.Character.CharacterId="build-hero-"+index;session.Units.Add(unit);
                        }
                        for(var index=0;index<ids.Length;index++)
                        {
                            var definition=PointCatalog.CharacterDefinitions.Single(d=>d.Id==ids[index]);var enemy=new CharacterInstanceFactory().Create(definition,definition.ClassData,level);
                            enemy.CharacterId="build-enemy-"+index;session.Units.Add(new BattleUnit{Character=enemy,Position=new GridPosition(7+index/6,2+index%6)});
                        }
                        var row=new BuildFight{level=level,profile=profile,seed=seed,biome=biome,encounter=names[roster],stage=stage,
                            trace=$"trace-R{roster}-P{profile}-S{seed}-{stage}.json",result=new Result{level=level,seed=seed,encounter=names[roster]}};
                        var trace=new KnockdownTrace();var used=new HashSet<string>();
                        session.SkillResolved+=(actor,skill,id)=>
                        {
                            if(!actor.IsPlayerSide)return;used.Add(actor.Character.CharacterId+":"+skill.SkillId);
                            row.skills.Add(new BuildSkillEvent{turn=row.result.turns,actor=actor.Character.CharacterId,skill=skill.SkillId,hp=actor.Character.CurrentHealth,
                                mana=actor.Character.ResourcePool.CurrentValue,ap=actor.CurrentActionPoints,x=actor.Position.X,y=actor.Position.Y});
                        };
                        UnityEngine.Random.InitState(81016+seed);
                        using(var engine=new Engine(session,row.result,trace))
                        {
                            engine.UsePreviousBuildPolicy=stage!="v8";engine.AuditTurnLimit=600;
                            engine.AuditLateKnifeExtraction=true;
                            engine.AuditUnitTurn=actor=>used.RemoveWhere(id=>id.StartsWith(actor.Character.CharacterId+":",StringComparison.Ordinal));
                            engine.AdditionalAuditScore=(a,s,m,t)=>engine.BuildUtility(a,s,m,t,used,row.result.turns>=200);
                            engine.Run();trace.Verify(row.result);
                        }
                        File.WriteAllText(Path.Combine(folder,row.trace),JsonUtility.ToJson(trace,true));report.fights.Add(row);
                    }
                    File.WriteAllText(Path.Combine(folder,"fights.json"),JsonUtility.ToJson(report,true));
                }
            }
            finally{UnityEngine.Random.state=random;}
            Assert.That(report.fights.All(f=>f.result.stop==null),Is.True,"A budget is unresolved, not a defeat.");
        }
        static IEnumerable<TestCaseData> BuildExpeditionCases()
        {
            foreach(var stage in new[]{"before","after"})foreach(var level in new[]{10,48})
            foreach(var profile in stage=="before"?new[]{5,6,7}:BuildAuditProfiles)foreach(var biome in new[]{"catacombs","forest"})
                yield return new TestCaseData(stage,level,profile,biome);
        }
        [TestCaseSource(nameof(BuildExpeditionCases))]
        [Explicit("Four fixed paired full routes per level/profile/biome, EQ0, unchanged stats and investment paths.")]
        public void AuditBuildFullExpeditions(string stage,int level,int profile,string biome)
            =>RunRealisticExpeditions(level,0,biome,DungeonLength.Normal,profile,4,$"L{level}-P{profile}-{biome}",
                outputFolder:Path.Combine(BuildPolicyFolder,stage,$"L{level}-P{profile}-{biome}"),fullOnly:true,traceKnockdowns:true,previousBuildPolicy:stage=="before");
        static IEnumerable<TestCaseData> BuildAfterExpeditionCases()
        {
            foreach(var level in new[]{10,48})foreach(var profile in BuildAuditProfiles)foreach(var biome in new[]{"catacombs","forest"})
                yield return new TestCaseData(level,profile,biome);
        }
        [TestCaseSource(nameof(BuildAfterExpeditionCases))]
        [Explicit("Final replay of after only; completed paired v7 baseline is retained and verified separately.")]
        public void AuditBuildAfterExpeditions(int level,int profile,string biome)
            =>AuditBuildFullExpeditions("after",level,profile,biome);
        [TestCase(10,0,"catacombs",0)][TestCase(10,0,"forest",0)][TestCase(48,0,"catacombs",0)][TestCase(48,0,"forest",0)]
        [TestCase(10,4,"forest",3)][TestCase(48,1,"catacombs",0)]
        [Explicit("Exact historical v7 controls after introducing v8; no changed stats or execution rules.")]
        public void AuditBuildHistoricalV7Control(int level,int profile,string biome,int seed)
            =>RunRealisticExpeditions(level,0,biome,DungeonLength.Normal,profile,seed+1,$"L{level}-P{profile}-{biome}-S{seed}",firstSeed:seed,
                outputFolder:Path.Combine(BuildPolicyFolder,"control",$"L{level}-P{profile}-{biome}-S{seed}"),fullOnly:true,traceKnockdowns:true,previousBuildPolicy:true);
    }
}
