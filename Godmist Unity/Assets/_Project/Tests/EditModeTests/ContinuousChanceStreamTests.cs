using System;
using Godmist.Core.Utilities;
using Godmist.Systems.Dungeons;
using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public class ContinuousChanceStreamTests
    {
        [Test]
        public void KeysAreIndependentAndFixedOutcomesDoNotAdvanceHistory()
        {
            var first = new DungeonContentDistribution(184);
            var second = new DungeonContentDistribution(184);
            for (var i = 0; i < 200; i++)
            {
                first.Roll("unrelated", .8);
                Assert.That(first.Roll(null, 0), Is.False);
                Assert.That(first.Roll(null, 1), Is.True);
                Assert.That(first.Roll("chests", i % 2 == 0 ? .1 : .6),
                    Is.EqualTo(second.Roll("chests", i % 2 == 0 ? .1 : .6)));
            }
        }

        [TestCase(.1)]
        [TestCase(.33)]
        [TestCase(.5)]
        [TestCase(.8)]
        [TestCase(-1)]
        public void ManyDungeonSeedsPreserveMeanAndReduceLongDroughts(double probability)
        {
            const int trials = 20000, length = 40;
            double expected = 0, total = 0;
            var controlledDroughts = 0;
            var independentDroughts = 0;
            for (var seed = 0; seed < trials; seed++)
            {
                var stream = new DungeonContentDistribution(seed);
                var independent = new Random(seed);
                var run = 0;
                var independentRun = 0;
                var max = 0;
                var independentMax = 0;
                for (var i = 0; i < length; i++)
                {
                    var p = probability < 0 ? new[] { .1, .33, .6, .8 }[i % 4] : probability;
                    expected += p;
                    var success = stream.Roll("content", p);
                    if (success) total++;
                    run = success ? 0 : run + 1;
                    independentRun = independent.NextDouble() < p ? 0 : independentRun + 1;
                    max = Math.Max(max, run);
                    independentMax = Math.Max(independentMax, independentRun);
                }
                if (max >= 6) controlledDroughts++;
                if (independentMax >= 6) independentDroughts++;
            }
            Assert.That(total / (trials * length), Is.EqualTo(expected / (trials * length)).Within(.003));
            if (probability == .33) Assert.That(controlledDroughts, Is.LessThan(independentDroughts * .7));
        }

        [Test]
        public void InvalidProbabilitiesAndMissingKeysFailExplicitly()
        {
            var stream = new ContinuousChanceStream(0);
            foreach (var p in new[] { double.NaN, double.PositiveInfinity, -.1, 1.1 })
                Assert.Throws<ArgumentOutOfRangeException>(() => stream.Roll(p));
            Assert.Throws<ArgumentException>(() => new DungeonContentDistribution(0).Roll(" ", .33));
        }

        [Test]
        public void IndependentChildKeyPreservesConditionalChance()
        {
            var eligible = 0;
            var children = 0;
            for (var seed = 0; seed < 20000; seed++)
            {
                var distribution = new DungeonContentDistribution(seed);
                for (var i = 0; i < 20; i++)
                {
                    if (!distribution.Roll("root", .33)) continue;
                    eligible++;
                    if (distribution.Roll("child", .33)) children++;
                }
            }
            Assert.That((double)children / eligible, Is.EqualTo(.33).Within(.006));
        }
    }
}
