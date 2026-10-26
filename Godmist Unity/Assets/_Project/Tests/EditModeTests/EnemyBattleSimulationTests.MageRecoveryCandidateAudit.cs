using System.IO;
using Godmist.Core.Enums;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        [TestCase("forest",0,9)][TestCase("forest",5,9)][TestCase("catacombs",8,15)]
        [Explicit("Read-only stress replay at the first permanent-loss battles found in the rejected initial candidate.")]
        public void AuditMageRecoveryStressDecisions(string biome,int seed,int selectedBattle)
        {
            var folder=Path.Combine(MageRecoveryFolder,"stress-diagnostics");Directory.CreateDirectory(folder);var report=new ForestDecisionAudit();
            RunRealisticExpeditions(48,0,biome,DungeonLength.Normal,0,seed+1,$"diagnostic-{biome}-S{seed}",firstSeed:seed,
                outputFolder:folder,fullOnly:true,traceKnockdowns:true,auditEngine:(engine,battle)=>
                {
                    if(battle!=selectedBattle)return;
                    engine.AuditPlayerDecision=(actor,after,played)=>
                    {
                        report.frames.Add(engine.CaptureSeedDecision(actor,battle,after?"after":"before",played:played,options:!after));
                        if(!after&&actor.Character.ClassData.Id is "class.mage" or "class.paladin")report.details.Add(engine.CaptureForestDetail(actor,battle));
                    };
                    engine.Session.SkillResolved+=(actor,skill,id)=>{if(actor.IsPlayerSide)report.frames.Add(engine.CaptureSeedDecision(actor,battle,"skill",skill.SkillId,options:false));};
                });
            File.WriteAllText(Path.Combine(folder,$"decisions-{biome}-S{seed}.json"),JsonUtility.ToJson(report,true));
        }
    }
}
