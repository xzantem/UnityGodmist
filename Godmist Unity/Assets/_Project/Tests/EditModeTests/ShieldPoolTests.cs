using Godmist.Runtime.Characters;
using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public class ShieldPoolTests
    {
        [Test]
        public void TimedLayersExpireIndependentlyWithoutChangingUntimedLayers()
        {
            var pool = new ShieldPool();
            pool.AddLayer(20, "untimed"); pool.AddLayer(30, "short", 1); pool.AddLayer(40, "long", 3);
            Assert.That(pool.AdvanceTurn(), Is.EqualTo(30)); Assert.That(pool.TotalShield, Is.EqualTo(60));
            Assert.That(pool.AdvanceTurn(), Is.Zero); Assert.That(pool.TotalShield, Is.EqualTo(60));
            Assert.That(pool.AdvanceTurn(), Is.EqualTo(40)); Assert.That(pool.TotalShield, Is.EqualTo(20));
            Assert.That(pool.AdvanceTurn(), Is.Zero); Assert.That(pool.Layers[0].RemainingTurns, Is.Zero);
        }

        [Test]
        public void DamageUsesExistingNewestFirstOrderAndExpiryRemovesOnlyRemainingValue()
        {
            var pool = new ShieldPool(); pool.AddLayer(50, "untimed"); pool.AddLayer(100, "timed", 1);
            Assert.That(pool.AbsorbDamage(70), Is.Zero); Assert.That(pool.TotalShield, Is.EqualTo(80));
            Assert.That(pool.AdvanceTurn(), Is.EqualTo(30)); Assert.That(pool.TotalShield, Is.EqualTo(50));
        }

        [Test]
        public void RejectedGrantDoesNotRemoveExistingShieldDuringReplacement()
        {
            var character = new Godmist.Runtime.Characters.CharacterInstance { LifeState = Godmist.Core.Enums.CharacterLifeState.Alive, ShieldPool = new ShieldPool() };
            var unit = new Godmist.Runtime.Combat.BattleUnit { Character = character };
            character.ShieldPool.AddLayer(50, "same", 2);
            Assert.That(new Godmist.Systems.Combat.CombatShieldSystem().GrantShield(unit, 0, "same", 3, true), Is.Zero);
            Assert.That(character.ShieldPool.TotalShield, Is.EqualTo(50)); Assert.That(character.ShieldPool.Layers[0].RemainingTurns, Is.EqualTo(2));
        }

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
