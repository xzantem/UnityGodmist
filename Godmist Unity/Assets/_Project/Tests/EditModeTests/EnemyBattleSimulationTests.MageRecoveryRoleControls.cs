using System.IO;
using Godmist.Core.Enums;
using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        [TestCase("forest",1,"mage")][TestCase("forest",1,"healer")]
        [TestCase("forest",6,"mage")][TestCase("forest",6,"healer")]
        [TestCase("catacombs",7,"mage")][TestCase("catacombs",7,"healer")]
        [Explicit("Role-level counterfactual: historical v5 decisions for only Mage or Paladin actions; no resources/statistics changed.")]
        public void AuditMageRecoveryRoleControl(string biome,int seed,string previousRole)
            =>RunRealisticExpeditions(48,0,biome,DungeonLength.Normal,0,seed+1,$"role-{previousRole}-{biome}-S{seed}",firstSeed:seed,
                outputFolder:Path.Combine(MageRecoveryFolder,"role-controls"),fullOnly:true,traceKnockdowns:true,
                auditEngine:(engine,battle)=>engine.AuditPlayerDecision=(actor,after,played)=>
                    engine.UsePreviousMagePolicy=!after&&actor.Character.ClassData.Id==(previousRole=="mage"?"class.mage":"class.paladin"));
    }
}
