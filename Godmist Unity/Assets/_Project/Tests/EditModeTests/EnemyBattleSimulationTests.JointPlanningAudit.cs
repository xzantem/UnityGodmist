using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        static string JointFolder=>Path.GetFullPath("../LocalHandoffs/simulator-joint-planning-20261019");
        [Serializable] sealed class JointInput
        {public int level,profile,hero,variant,points;public string[] skills;public string ranks;}
        [Serializable] sealed class JointInputs {public List<JointInput> rows=new();}
        [Test]
        public void JointManifestRecordsLegalVariantsWithoutChangingRanks()
        {
            var report=new JointInputs();
            foreach(var level in new[]{10,48})foreach(var profile in BuildAuditProfiles)foreach(var index in Enumerable.Range(0,4))
            {
                var own=RealisticProfile(index,profile);var hero=BuildHero(index,level,own).Character;
                string Ranks()=>string.Join("|",hero.SkillTreeProgress.SelectMany(t=>t.NodeInvestments.Select(n=>$"{t.TreeId}/{n.NodeId}:{n.Ranks}")));
                var ranks=Ranks();
                foreach(var variant in new[]{0,1,2,3})
                {
                    SetJointLoadout(hero,own,variant);Assert.That(Ranks(),Is.EqualTo(ranks));
                    report.rows.Add(new JointInput{level=level,profile=profile,hero=index,variant=variant,ranks=ranks,
                        points=hero.SkillTreeProgress.Sum(t=>t.NodeInvestments.Sum(n=>n.Ranks)),skills=hero.GetActiveSkills().Select(s=>s.SkillId).ToArray()});
                }
            }
            Directory.CreateDirectory(JointFolder);File.WriteAllText(Path.Combine(JointFolder,"loadout-inputs.json"),JsonUtility.ToJson(report,true));
        }
        [TestCase(10,"catacombs")][TestCase(10,"forest")][TestCase(48,"catacombs")][TestCase(48,"forest")]
        [Explicit("Three fixed legal loadouts, identical v9 decisions, same four full P1 routes; no investment or stat changes.")]
        [Timeout(600000)]
        public void AuditJointLoadoutCandidates(int level,string biome)
        {
            foreach(var variant in new[]{0,1,2})
                RunRealisticExpeditions(level,0,biome,DungeonLength.Normal,1,4,$"L{level}-P1-{biome}",
                    outputFolder:Path.Combine(JointFolder,"candidate-"+variant,$"L{level}-P1-{biome}"),fullOnly:true,traceKnockdowns:true,planningLoadout:variant);
        }
        static IEnumerable<TestCaseData> JointFullCases()
        {
            foreach(var level in new[]{10,48})foreach(var profile in BuildAuditProfiles)foreach(var biome in new[]{"catacombs","forest"})
                yield return new TestCaseData(level,profile,biome);
        }
        [TestCaseSource(nameof(JointFullCases))]
        [Explicit("Final adaptive v9 defaults: all seven profiles and the same four full routes, including a fresh P1 replay.")]
        [Timeout(600000)]
        public void AuditJointFinalExpeditions(int level,int profile,string biome)
            =>RunRealisticExpeditions(level,0,biome,DungeonLength.Normal,profile,4,$"L{level}-P{profile}-{biome}",
                outputFolder:Path.Combine(JointFolder,"after",$"L{level}-P{profile}-{biome}"),fullOnly:true,traceKnockdowns:true);
        [TestCase(10,0,"catacombs",0)][TestCase(10,1,"forest",1)]
        [TestCase(48,0,"forest",3)][TestCase(48,1,"forest",3)][TestCase(48,1,"catacombs",3)][TestCase(48,4,"forest",3)]
        [Explicit("Exact historical v8 controls with old loadouts and planning; prior evidence is never overwritten.")]
        public void AuditJointHistoricalV8Control(int level,int profile,string biome,int seed)
            =>RunRealisticExpeditions(level,0,biome,DungeonLength.Normal,profile,seed+1,$"L{level}-P{profile}-{biome}-S{seed}",firstSeed:seed,
                outputFolder:Path.Combine(JointFolder,"control",$"L{level}-P{profile}-{biome}-S{seed}"),fullOnly:true,traceKnockdowns:true,previousPlanningPolicy:true);
    }
}
