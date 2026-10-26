using System.IO;
using Godmist.Core.Enums;
using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        [TestCase("catacombs",0)][TestCase("catacombs",4)][TestCase("catacombs",8)]
        [TestCase("forest",0)][TestCase("forest",4)][TestCase("forest",8)]
        [Explicit("Same 24 L48 EQ50 routes and party-v3 decisions; only downed status clocks changed.")]
        public void AuditDownedStatusExpiryExpeditions(string biome, int firstSeed)
            => RunRealisticExpeditions(48, .5f, biome, DungeonLength.Normal, 0, firstSeed+4,
                $"expiry-L48-U50-{biome}-S{firstSeed}", firstSeed:firstSeed,
                outputFolder:Path.GetFullPath("../LocalHandoffs/downed-status-expiry-20261019"),
                fullOnly:true, traceKnockdowns:true, previousSupportPolicy:true);
    }
}
