using System.IO;
using System.Linq;
using Godmist.Data.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEditor;

namespace Godmist.Tests.EditMode
{
    public partial class SimpleCombatAiSystemTests
    {
        [TestCase(false)] [TestCase(true)]
        public void DefenderWithZeroRageApproachesInsteadOfWaitingAndExplainsDecision(bool blocked)
        {
            var def=AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_defender.asset");
            var session=PlannerSession(6,def.StartingSkills.ToArray());
            var actor=session.ActiveUnit;actor.Character=new CharacterInstanceFactory().Create(def);
            actor.CurrentActionPoints=actor.MaxActionPoints=1000;actor.Character.ResourcePool.CurrentValue=0;
            actor.Position=new GridPosition(1,1);session.Units[1].Position=new GridPosition(6,1);session.Grid=new BattleGrid(9,4);
            if(blocked)for(var y=0;y<4;y++)session.Grid.SetBlocked(new GridPosition(2,y));
            session.Diagnostics=new CombatDiagnosticRecorder{Enabled=true};var ai=CreateProductionWraithAi();
            var acted=ai.TryExecuteNextAction(session,actor,ai.CreateTurnState(),out var action);
            var trace=session.Diagnostics.Read();Directory.CreateDirectory("../LocalHandoffs/combat-economy-ui-20261026");
            File.WriteAllText("../LocalHandoffs/combat-economy-ui-20261026/defender-"+blocked+".log",trace);
            Assert.That(trace,Does.Contain("ai-begin").And.Contain("resource=").And.Contain("afford="));
            if(!blocked) {Assert.That(acted,Is.True);Assert.That(actor.Position,Is.Not.EqualTo(new GridPosition(1,1)));}
            else Assert.That(trace,Does.Contain("reason="));
        }
    }
}
