using System.IO;
using Godmist.Core.Enums;
using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        [TestCase("catacombs", 0)]
        [TestCase("forest", 6)]
        [Explicit("E06 control: authored L48 party, fixed support policy, real combat/AI; no authoring writes.")]
        public void PaladinRecoveryExpeditionRegression(string biome, int seed)
            => RunRealisticExpeditions(48, .5f, biome, DungeonLength.Normal, 0, seed + 1,
                $"E06-L48-U50-{biome}-S{seed}", firstSeed: seed,
                outputFolder: Path.GetFullPath("../LocalHandoffs/paladin-ai-recovery-20261030/expeditions"),
                fullOnly: true, traceKnockdowns: true, previousTeamPolicy: true);
    }
}
