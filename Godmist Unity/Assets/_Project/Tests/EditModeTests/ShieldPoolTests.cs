using Godmist.Runtime.Characters;
using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public class ShieldPoolTests
    {
        [Test]
        public void AbsorbDamage_ConsumesNewestLayerFirst()
        {
            var shieldPool = new ShieldPool();

            shieldPool.AddLayer(10, "first");
            shieldPool.AddLayer(6, "second");

            var remainingDamage = shieldPool.AbsorbDamage(8);

            Assert.That(remainingDamage, Is.EqualTo(0));
            Assert.That(shieldPool.TotalShield, Is.EqualTo(8));
            Assert.That(shieldPool.Layers.Count, Is.EqualTo(1));
            Assert.That(shieldPool.Layers[0].SourceId, Is.EqualTo("first"));
            Assert.That(shieldPool.Layers[0].CurrentValue, Is.EqualTo(8));
        }

        [Test]
        public void AbsorbDamage_ConsumesMultipleLayersAndReturnsOverflow()
        {
            var shieldPool = new ShieldPool();

            shieldPool.AddLayer(10, "first");
            shieldPool.AddLayer(6, "second");

            var remainingDamage = shieldPool.AbsorbDamage(20);

            Assert.That(remainingDamage, Is.EqualTo(4));
            Assert.That(shieldPool.TotalShield, Is.EqualTo(0));
            Assert.That(shieldPool.Layers, Is.Empty);
        }

        [Test]
        public void AddLayer_IgnoresNonPositiveValue()
        {
            var shieldPool = new ShieldPool();

            shieldPool.AddLayer(0, "zero");
            shieldPool.AddLayer(-10, "negative");

            Assert.That(shieldPool.TotalShield, Is.EqualTo(0));
            Assert.That(shieldPool.Layers, Is.Empty);
        }
    }
}