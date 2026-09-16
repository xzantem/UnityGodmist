using System;
using Godmist.Core.Utilities;
using Godmist.Systems.Dungeons;
using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public class ContinuousChanceStreamTests
    {
        [TestCase(.25, 8, 2)]
        [TestCase(.5, 4, 2)]
        [TestCase(.8, 10, 8)]
        [TestCase(.33, 7, -1)]
        [TestCase(.2573, 8, -1)]
        public void CompleteBagsHaveExpectedCountsWithoutRoundingChance(double p, int size, int exact)
        {
            for (var seed = 0; seed < 100; seed++)
            {
                var stream = new ContinuousChanceStream(seed);
                for (var bag = 0; bag < 20; bag++)
                {
                    var hits = 0;
                    for (var i = 0; i < size; i++) if (stream.Roll(p)) hits++;
                    Assert.That(stream.PoolSize, Is.EqualTo(size));
                    Assert.That(stream.RemainingCount, Is.Zero);
                    if (exact >= 0) Assert.That(hits, Is.EqualTo(exact));
                    else Assert.That(hits, Is.InRange((int)Math.Floor(size * p), (int)Math.Ceiling(size * p)));
                }
            }
        }

        [Test]
        public void BuffChangesThresholdImmediatelyButResizesOnlyOnRefill()
        {
            var stream = new ContinuousChanceStream(91);
            stream.Roll(.25);
            Assert.That(stream.PoolSize, Is.EqualTo(8));
            Assert.That(stream.RemainingCount, Is.EqualTo(7));
            for (var i = 0; i < 7; i++) stream.Roll(.8);
            Assert.That(stream.PoolSize, Is.EqualTo(8));
            Assert.That(stream.RemainingCount, Is.Zero);
            stream.Roll(.8);
            Assert.That(stream.PoolSize, Is.EqualTo(10));
            Assert.That(stream.RemainingCount, Is.EqualTo(9));
        }

        [Test]
        public void GuaranteedAndInvalidRollsLeavePartialBagUnchanged()
        {
            var a = new ContinuousChanceStream(7);
            var b = new ContinuousChanceStream(7);
            for (var i = 0; i < 80; i++)
            {
                a.Roll(0); a.Roll(1);
                Assert.Throws<ArgumentOutOfRangeException>(() => a.Roll(double.NaN));
                var p = new[] { .25, .5, .6, .35, .25 }[i % 5];
                Assert.That(a.Roll(p), Is.EqualTo(b.Roll(p)));
            }
        }

        [Test]
        public void ExtremeProbabilitiesUseSparseBagWithoutOverflow()
        {
            var stream = new ContinuousChanceStream(4);
            Assert.DoesNotThrow(() => stream.Roll(double.Epsilon));
            Assert.That(stream.PoolSize, Is.EqualTo(int.MaxValue));
            Assert.That(stream.RemainingCount, Is.EqualTo(int.MaxValue - 1));
            Assert.DoesNotThrow(() => stream.Roll(1 - 1e-15));
        }

        [TestCase(.8f, 10)]
        [TestCase(.1f, 20)]
        [TestCase(.33f, 7)]
        public void UnityFloatChancesDoNotInflateIntegralBagSizes(float chance, int size)
        {
            var stream = new ContinuousChanceStream(5);
            stream.Roll(chance);
            Assert.That(stream.PoolSize, Is.EqualTo(size));
        }

        [Test]
        public void ChangedThresholdAffectsCurrentBagBeforeRefill()
        {
            var different = 0;
            for (var seed = 0; seed < 200; seed++)
            {
                var low = new ContinuousChanceStream(seed);
                var high = new ContinuousChanceStream(seed);
                low.Roll(.25); high.Roll(.25);
                for (var i = 0; i < 7; i++)
                {
                    var a = low.Roll(.25);
                    var b = high.Roll(.75);
                    Assert.That(!a || b, Is.True);
                    if (a != b) different++;
                }
            }
            Assert.That(different, Is.GreaterThan(0));
        }

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
