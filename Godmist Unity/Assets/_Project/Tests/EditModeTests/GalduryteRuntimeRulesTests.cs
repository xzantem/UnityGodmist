using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Items;
using Godmist.Systems.Items;
using NUnit.Framework;
using Godmist.Views.UI.Workspace;

namespace Godmist.Tests.EditMode
{
    public sealed class GalduryteRuntimeRulesTests
    {
        [TestCase(11, 2, 3)]
        [TestCase(20, 2, 3)]
        [TestCase(21, 3, 4)]
        [TestCase(41, 5, 6)]
        public void GetEffectCountRange_UsesConfiguredLevelBands(
            int level,
            int minimum,
            int maximum)
        {
            Assert.That(
                GalduryteRuntimeRules.TryGetEffectCountRange(
                    level,
                    out var actualMinimum,
                    out var actualMaximum),
                Is.True);
            Assert.That(actualMinimum, Is.EqualTo(minimum));
            Assert.That(actualMaximum, Is.EqualTo(maximum));
        }

        [Test]
        public void TryRollEffects_TierOneGuaranteesPowderColorAndDoesNotRepeatColor()
        {
            var success = GalduryteRuntimeRules.TryRollEffects(
                GalduryteTargetType.Weapon,
                GaldurytePowderColor.Red,
                RarityTier.Rare,
                11,
                new System.Random(4),
                out var effects,
                out var failureReason);

            Assert.That(success, Is.True, failureReason);
            Assert.That(effects, Has.Count.InRange(2, 3));
            Assert.That(
                effects.Count(effect => effect.PowderColor == GaldurytePowderColor.Red),
                Is.EqualTo(1));
            Assert.That(
                effects.Select(effect => effect.PowderColor).Distinct().Count(),
                Is.EqualTo(effects.Count));
            Assert.That(
                effects.Select(effect => effect.EffectType).Distinct().Count(),
                Is.EqualTo(effects.Count));
        }

        [Test]
        public void TryRollEffects_TierTwoFailsInsteadOfDuplicatingEffects()
        {
            var success = GalduryteRuntimeRules.TryRollEffects(
                GalduryteTargetType.Armor,
                GaldurytePowderColor.Blue,
                RarityTier.Common,
                21,
                new System.Random(7),
                out List<GalduryteEffectInstance> effects,
                out var failureReason);

            Assert.That(success, Is.False);
            Assert.That(effects, Is.Empty);
            Assert.That(failureReason, Does.Contain("Poziom galdurytu"));
        }

        [Test]
        public void GetLevel_UsesGalduryteStoneTierLevels()
        {
            Assert.That(GalduryteRuntimeRules.GetLevel(GalduryteTier.Limestone), Is.EqualTo(11));
            Assert.That(GalduryteRuntimeRules.GetLevel(GalduryteTier.Granite), Is.EqualTo(21));
            Assert.That(GalduryteRuntimeRules.GetLevel(GalduryteTier.Marble), Is.EqualTo(41));
        }
        [Test]
        public void CraftingSummary_ShowsWeaponPowderRangesWithoutRollingPreview()
        {
            var preview = new GalduryteItemInstance { TargetType = GalduryteTargetType.Weapon,
                GuaranteedPowderColor = GaldurytePowderColor.Red, ItemLevel = 11 };
            var summary = GalduryteCraftingWindowView.BuildPossibleEffects(preview);
            Assert.That(summary, Does.Contain("+2%–5%").And.Contain("+3%–6%"));
            Assert.That(summary, Does.Contain("Jeden z").And.Contain("losowe z puli broni"));
            Assert.That(summary, Does.Not.Contain("Zdrowie"));
            Assert.That(preview.Effects, Is.Empty);
            Assert.That(preview.IsIdentified, Is.False);
        }

        [Test]
        public void CraftingSummary_SwitchesGuaranteedPoolForArmor()
        {
            var preview = new GalduryteItemInstance { TargetType = GalduryteTargetType.Armor,
                GuaranteedPowderColor = GaldurytePowderColor.Blue, ItemLevel = 11 };
            var summary = GalduryteCraftingWindowView.BuildPossibleEffects(preview);
            Assert.That(summary, Does.Contain("Obrona fizyczna").And.Contain("Obrona magiczna"));
            Assert.That(summary, Does.Contain("losowe z puli pancerza"));
            Assert.That(summary, Does.Not.Contain("Szansa na trafienie krytyczne"));
        }
    }
}
